namespace Clinic.Appointment.Service.Enums;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled
}

public enum OutboxMessageStatus
{
    Pending,
    Publishing,
    Sent,
    Failed,
    DeadLettered
}

