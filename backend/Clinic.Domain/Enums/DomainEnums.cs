namespace Clinic.Domain.Enums
{
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

    public enum WorkScheduleStatus
    {
        Active,
        Full,
        Cancelled
    }

    /// <summary>
    /// Pending -> Confirmed -> CheckedIn -> Completed, hoáº·c Cancelled / NoShow.
    /// </summary>
    public enum AppointmentStatus
    {
        Pending,
        Confirmed,
        CheckedIn,
        Completed,
        Cancelled,
        NoShow
    }

    public enum QueueStatus
    {
        Waiting,
        Called,
        InProgress,
        Completed,
        Skipped,
        /// <summary>VÃ© bá»‹ há»§y vÃ¬ Appointment gá»‘c bá»‹ há»§y sau khi Ä‘Ã£ check-in (cÃ²n Waiting/Called).</summary>
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

    /// <summary>Tráº¡ng thÃ¡i 1 giai Ä‘oáº¡n trong lá»‹ch sá»­ chuyá»ƒn khoa cá»§a bÃ¡c sÄ©.</summary>
    public enum WorkHistoryStatus
    {
        Active,
        Ended
    }

    /// <summary>Tráº¡ng thÃ¡i duyá»‡t yÃªu cáº§u ca lÃ m viá»‡c cá»§a bÃ¡c sÄ©.</summary>
    public enum ShiftRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }
    public enum DoctorStatus
    {
        Active,
        Inactive
    }
    public enum MedicalReportStatus
    {
        Draft,
        Finalized
    }
}