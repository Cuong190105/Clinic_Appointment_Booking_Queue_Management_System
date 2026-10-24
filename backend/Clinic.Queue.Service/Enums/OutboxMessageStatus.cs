namespace Clinic.Queue.Service.Enums;

public enum OutboxMessageStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2,
    Publishing = 3,
    DeadLetterPending = 4,
    DeadLetterPublishing = 5,
    DeadLettered = 6
}
