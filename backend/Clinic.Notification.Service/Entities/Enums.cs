namespace Clinic.Notification.Service.Entities;

public enum RecipientType
{
    Account = 1,
    Guest = 2
}

public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Sms = 3
}

public enum NotificationType
{
    AppointmentConfirmed = 1,
    AppointmentCancelled = 2,
    AppointmentReminder = 3,
    PatientCalled = 4,
    PasswordReset = 5
}

public enum DeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
    DeadLettered = 3
}

