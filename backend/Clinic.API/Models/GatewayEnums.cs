namespace Clinic.API.Models;

public enum Gender
{
    Male,
    Female,
    Other
}

public enum EmployeeStatus
{
    Active,
    OnLeave,
    Resigned
}

public enum DoctorStatus
{
    Active,
    Inactive
}

public enum WorkScheduleStatus
{
    Active,
    Full,
    Cancelled
}

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled,
    NoShow
}

public enum MedicalReportStatus
{
    Draft,
    Finalized
}

public enum WorkHistoryStatus
{
    Active,
    Ended
}

public enum ShiftRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public enum NotificationType
{
    AppointmentReminder,
    AppointmentConfirmation,
    AppointmentCancellation,
    ResetPassword,
    QueueUpdate,
    System
}

public enum NotificationChannel
{
    Email,
    Sms,
    InApp
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Cancelled
}

