namespace Clinic.Queue.Service.Common.Exceptions;

public class QueueNotFoundException : Exception
{
    public QueueNotFoundException(string message) : base(message) { }
}

public class QueueConflictException : Exception
{
    public QueueConflictException(string message) : base(message) { }
}

public class QueueForbiddenException : Exception
{
    public QueueForbiddenException(string message) : base(message) { }
}

public class QueueValidationException : Exception
{
    public QueueValidationException(string message) : base(message) { }
}
