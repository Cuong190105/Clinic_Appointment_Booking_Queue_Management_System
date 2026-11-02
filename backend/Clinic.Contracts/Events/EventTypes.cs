namespace Clinic.Contracts.Events;

public static class EventTypes
{
    public const string AppointmentCreated = "AppointmentCreated";

    public const string AppointmentConfirmed = "AppointmentConfirmed";

    public const string AppointmentCancelled = "AppointmentCancelled";

    public const string AppointmentCheckedIn = "AppointmentCheckedIn";

    public const string QueueTicketCreated = "QueueTicketCreated";

    public const string ExamCompleted = "ExamCompleted";

    public const string ExamStarted = "ExamStarted";

    public const string PatientCalled = "PatientCalled";

    public const string PatientSkipped = "PatientSkipped";

    public const string WorkScheduleCreated = "WorkScheduleCreated";

    public const string WorkScheduleUpdated = "WorkScheduleUpdated";

    public const string WorkScheduleCancelled = "WorkScheduleCancelled";

    public const string UserProfileUpdated = "UserProfileUpdated";

    public const string AppointmentReminderScheduled = "AppointmentReminderScheduled";

    public const string NotificationRequested = "NotificationRequested";

    public const string PasswordResetNotificationRequested = "PasswordResetNotificationRequested";
}
