using Clinic.Contracts.Events;
using Clinic.Notification.Service.Entities;

namespace Clinic.Notification.Service.Services;

public sealed record NotificationIntent(
    Guid EventId,
    Guid RecipientId,
    RecipientType RecipientType,
    NotificationChannel Channel,
    NotificationType NotificationType,
    string IdempotencyKey,
    string Title,
    string Body,
    RecipientContactSnapshot? ContactSnapshot,
    DateTimeOffset? ScheduledFor = null,
    Guid? AppointmentId = null,
    string? SlotKey = null
);

public interface INotificationPolicyEngine
{
    IReadOnlyList<NotificationIntent> EvaluateAppointmentConfirmed(Guid eventId, AppointmentConfirmed payload);
    IReadOnlyList<NotificationIntent> EvaluateAppointmentCancelled(Guid eventId, AppointmentCancelled payload);
    IReadOnlyList<NotificationIntent> EvaluatePatientCalled(Guid eventId, PatientCalled payload);
    IReadOnlyList<NotificationIntent> EvaluateAppointmentReminder(Guid eventId, AppointmentConfirmed payload, DateTimeOffset now);
    bool ShouldSuppressCancellation(AppointmentCancelled payload);
    bool ShouldCancelPendingReminder(AppointmentCancelled payload);
    string GenerateIdempotencyKey(Guid eventId, Guid recipientId, NotificationChannel channel, NotificationType type);
}

public sealed class NotificationPolicyEngine : INotificationPolicyEngine
{
    /// <summary>Reminders are sent this long before the booked slot.</summary>
    public static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(24);

    /// <summary>Clinic-local UTC offset used to interpret ScheduleDate/TimeSlot (Vietnam, UTC+7, no DST).</summary>
    public static readonly TimeSpan ClinicUtcOffset = TimeSpan.FromHours(7);

    /// <summary>Marker used by cancellations that do not identify a specific slot: suppress every slot.</summary>
    public const string AnySlot = "*";

    public static string BuildSlotKey(Guid workScheduleId, TimeOnly timeSlot) =>
        workScheduleId == Guid.Empty ? AnySlot : $"{workScheduleId:D}:{timeSlot:HH\\:mm}";

    public string GenerateIdempotencyKey(Guid eventId, Guid recipientId, NotificationChannel channel, NotificationType type) =>
        $"{eventId:D}:{recipientId:D}:{channel}:{type}";

    public IReadOnlyList<NotificationIntent> EvaluateAppointmentReminder(Guid eventId, AppointmentConfirmed payload, DateTimeOffset now)
    {
        var slotStart = new DateTimeOffset(payload.ScheduleDate.ToDateTime(payload.TimeSlot), ClinicUtcOffset);
        var dueAt = slotStart - ReminderLeadTime;

        // Appointment confirmed inside the lead window (or already past): the confirmation itself suffices.
        if (dueAt <= now)
        {
            return Array.Empty<NotificationIntent>();
        }

        var intents = new List<NotificationIntent>();
        var isAccount = payload.IdentityUserId.HasValue && payload.IdentityUserId.Value != Guid.Empty;
        var recipientId = isAccount ? payload.IdentityUserId!.Value : payload.PatientId;
        var recipientType = isAccount ? RecipientType.Account : RecipientType.Guest;
        var contact = new RecipientContactSnapshot(payload.PatientName, payload.PatientEmail, payload.PatientPhone);
        var slotKey = BuildSlotKey(payload.WorkScheduleId, payload.TimeSlot);

        var title = "Nhắc lịch khám";
        var body = $"Bạn có lịch khám ngày {payload.ScheduleDate:dd/MM/yyyy} lúc {payload.TimeSlot:HH\\:mm}. Vui lòng đến đúng giờ.";

        NotificationIntent Make(NotificationChannel channel) => new(
            EventId: eventId,
            RecipientId: recipientId,
            RecipientType: recipientType,
            Channel: channel,
            NotificationType: NotificationType.AppointmentReminder,
            IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, channel, NotificationType.AppointmentReminder),
            Title: title,
            Body: body,
            ContactSnapshot: contact,
            ScheduledFor: dueAt,
            AppointmentId: payload.AppointmentId,
            SlotKey: slotKey);

        if (isAccount) intents.Add(Make(NotificationChannel.InApp));
        if (!string.IsNullOrWhiteSpace(payload.PatientEmail)) intents.Add(Make(NotificationChannel.Email));
        else if (!string.IsNullOrWhiteSpace(payload.PatientPhone)) intents.Add(Make(NotificationChannel.Sms));

        return intents;
    }

    public bool ShouldSuppressCancellation(AppointmentCancelled payload) =>
        payload.IsRescheduleRelease;

    public bool ShouldCancelPendingReminder(AppointmentCancelled payload) =>
        true;

    public IReadOnlyList<NotificationIntent> EvaluateAppointmentConfirmed(Guid eventId, AppointmentConfirmed payload)
    {
        var intents = new List<NotificationIntent>();
        var isAccount = payload.IdentityUserId.HasValue && payload.IdentityUserId.Value != Guid.Empty;
        var recipientId = isAccount ? payload.IdentityUserId!.Value : payload.PatientId;
        var recipientType = isAccount ? RecipientType.Account : RecipientType.Guest;

        var contact = new RecipientContactSnapshot(
            payload.PatientName,
            payload.PatientEmail,
            payload.PatientPhone
        );

        var title = "Lịch khám đã được xác nhận";
        var body = $"Lịch khám ngày {payload.ScheduleDate:dd/MM/yyyy} vào lúc {payload.TimeSlot:hh\\:mm} đã được xác nhận thành công.";

        // Account recipients get InApp notification
        if (isAccount)
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.InApp,
                NotificationType: NotificationType.AppointmentConfirmed,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.InApp, NotificationType.AppointmentConfirmed),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        // Email delivery if email is available
        if (!string.IsNullOrWhiteSpace(payload.PatientEmail))
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.Email,
                NotificationType: NotificationType.AppointmentConfirmed,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.Email, NotificationType.AppointmentConfirmed),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        // SMS delivery if phone is available and no email
        if (string.IsNullOrWhiteSpace(payload.PatientEmail) && !string.IsNullOrWhiteSpace(payload.PatientPhone))
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.Sms,
                NotificationType: NotificationType.AppointmentConfirmed,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.Sms, NotificationType.AppointmentConfirmed),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        return intents;
    }

    public IReadOnlyList<NotificationIntent> EvaluateAppointmentCancelled(Guid eventId, AppointmentCancelled payload)
    {
        // Reschedule-release cancellations must never notify as a cancellation
        if (ShouldSuppressCancellation(payload))
        {
            return Array.Empty<NotificationIntent>();
        }

        var intents = new List<NotificationIntent>();
        var isAccount = payload.IdentityUserId.HasValue && payload.IdentityUserId.Value != Guid.Empty;
        var recipientId = isAccount ? payload.IdentityUserId!.Value : payload.PatientId;
        var recipientType = isAccount ? RecipientType.Account : RecipientType.Guest;

        var contact = new RecipientContactSnapshot(
            payload.PatientName,
            payload.PatientEmail,
            payload.PatientPhone
        );

        var title = "Lịch khám đã bị hủy";
        var body = $"Lịch hẹn khám của bạn đã bị hủy.";

        if (isAccount)
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.InApp,
                NotificationType: NotificationType.AppointmentCancelled,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.InApp, NotificationType.AppointmentCancelled),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        if (!string.IsNullOrWhiteSpace(payload.PatientEmail))
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.Email,
                NotificationType: NotificationType.AppointmentCancelled,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.Email, NotificationType.AppointmentCancelled),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }
        else if (!string.IsNullOrWhiteSpace(payload.PatientPhone))
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.Sms,
                NotificationType: NotificationType.AppointmentCancelled,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.Sms, NotificationType.AppointmentCancelled),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        return intents;
    }

    public IReadOnlyList<NotificationIntent> EvaluatePatientCalled(Guid eventId, PatientCalled payload)
    {
        var intents = new List<NotificationIntent>();
        var isAccount = payload.IdentityUserId.HasValue && payload.IdentityUserId.Value != Guid.Empty;
        var recipientId = isAccount ? payload.IdentityUserId!.Value : payload.PatientId;
        var recipientType = isAccount ? RecipientType.Account : RecipientType.Guest;

        var contact = new RecipientContactSnapshot(
            payload.PatientName,
            null,
            payload.PatientPhone
        );

        var title = "Mời vào phòng khám";
        var body = $"Số thứ tự {payload.QueueNumber} được mời vào phòng khám.";

        if (isAccount)
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.InApp,
                NotificationType: NotificationType.PatientCalled,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.InApp, NotificationType.PatientCalled),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        if (!string.IsNullOrWhiteSpace(payload.PatientPhone))
        {
            intents.Add(new NotificationIntent(
                EventId: eventId,
                RecipientId: recipientId,
                RecipientType: recipientType,
                Channel: NotificationChannel.Sms,
                NotificationType: NotificationType.PatientCalled,
                IdempotencyKey: GenerateIdempotencyKey(eventId, recipientId, NotificationChannel.Sms, NotificationType.PatientCalled),
                Title: title,
                Body: body,
                ContactSnapshot: contact,
                AppointmentId: payload.AppointmentId
            ));
        }

        return intents;
    }
}

