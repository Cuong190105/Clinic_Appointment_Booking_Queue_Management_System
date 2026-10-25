namespace Clinic.Scheduling.Service.Enums;

public enum EmployeeStatus { Active, OnLeave, Resigned }

public enum DoctorStatus { Active, Inactive }

public enum WorkScheduleStatus { Active, Full, Cancelled }

public enum ShiftRequestStatus { Pending, Approved, Rejected, Cancelled }

public enum WorkHistoryStatus { Active, Ended }

public enum ReservationStatus { Reserved, Confirmed, Released, Expired }

public enum OutboxMessageStatus
{
    Pending = 0,
    Publishing = 1,
    Sent = 2,
    Failed = 3,
    DeadLetterPending = 4,
    DeadLettered = 5,
    DeadLetterPublishing = 6
}

