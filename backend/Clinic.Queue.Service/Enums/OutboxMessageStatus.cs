namespace Clinic.Queue.Service.Enums;

public enum OutboxMessageStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2
}
