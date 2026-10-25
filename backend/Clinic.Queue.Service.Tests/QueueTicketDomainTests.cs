namespace Clinic.Queue.Service.Tests;

using Clinic.Queue.Service.Entities;
using Clinic.Queue.Service.Enums;
using Xunit;

public class QueueTicketDomainTests
{
    [Fact]
    public void Call_FromWaiting_TransitionsToCalledAndSetsCalledAt()
    {
        var ticket = new QueueTicket
        {
            Status = QueueStatus.Waiting
        };

        ticket.Call();

        Assert.Equal(QueueStatus.Called, ticket.Status);
        Assert.NotNull(ticket.CalledAt);
        Assert.NotNull(ticket.UpdatedAt);
    }

    [Fact]
    public void Call_FromCalled_RecallsAndUpdatesCalledAt()
    {
        var ticket = new QueueTicket
        {
            Status = QueueStatus.Waiting
        };
        ticket.Call();
        var firstCalledAt = ticket.CalledAt;

        ticket.Call();

        Assert.Equal(QueueStatus.Called, ticket.Status);
        Assert.True(ticket.CalledAt >= firstCalledAt);
    }

    [Fact]
    public void StartExam_FromCalled_TransitionsToInProgress()
    {
        var ticket = new QueueTicket
        {
            Status = QueueStatus.Waiting
        };
        ticket.Call();

        ticket.StartExam();

        Assert.Equal(QueueStatus.InProgress, ticket.Status);
        Assert.NotNull(ticket.ExamStartedAt);
    }

    [Fact]
    public void CompleteExam_FromInProgress_TransitionsToCompleted()
    {
        var ticket = new QueueTicket
        {
            Status = QueueStatus.Waiting
        };
        ticket.Call();
        ticket.StartExam();

        ticket.CompleteExam();

        Assert.Equal(QueueStatus.Completed, ticket.Status);
        Assert.NotNull(ticket.CompletedAt);
    }

    [Fact]
    public void Skip_FromWaitingOrCalled_TransitionsToSkipped()
    {
        var ticket1 = new QueueTicket { Status = QueueStatus.Waiting };
        ticket1.Skip();
        Assert.Equal(QueueStatus.Skipped, ticket1.Status);
        Assert.NotNull(ticket1.SkippedAt);

        var ticket2 = new QueueTicket { Status = QueueStatus.Waiting };
        ticket2.Call();
        ticket2.Skip();
        Assert.Equal(QueueStatus.Skipped, ticket2.Status);
        Assert.NotNull(ticket2.SkippedAt);
    }

    [Theory]
    [InlineData(QueueStatus.InProgress)]
    [InlineData(QueueStatus.Completed)]
    [InlineData(QueueStatus.Skipped)]
    public void Call_FromInvalidStatus_ThrowsInvalidOperationException(QueueStatus status)
    {
        var ticket = new QueueTicket { Status = status };
        Assert.Throws<InvalidOperationException>(() => ticket.Call());
    }

    [Theory]
    [InlineData(QueueStatus.Completed)]
    [InlineData(QueueStatus.Skipped)]
    public void StartExam_FromInvalidStatus_ThrowsInvalidOperationException(QueueStatus status)
    {
        var ticket = new QueueTicket { Status = status };
        Assert.Throws<InvalidOperationException>(() => ticket.StartExam());
    }

    [Theory]
    [InlineData(QueueStatus.Waiting)]
    [InlineData(QueueStatus.Called)]
    [InlineData(QueueStatus.Completed)]
    [InlineData(QueueStatus.Skipped)]
    public void CompleteExam_FromNonInProgressStatus_ThrowsInvalidOperationException(QueueStatus status)
    {
        var ticket = new QueueTicket { Status = status };
        Assert.Throws<InvalidOperationException>(() => ticket.CompleteExam());
    }

    [Theory]
    [InlineData(QueueStatus.InProgress)]
    [InlineData(QueueStatus.Completed)]
    [InlineData(QueueStatus.Skipped)]
    public void Skip_FromInvalidStatus_ThrowsInvalidOperationException(QueueStatus status)
    {
        var ticket = new QueueTicket { Status = status };
        Assert.Throws<InvalidOperationException>(() => ticket.Skip());
    }

    [Fact]
    public void SetPriority_UpdatesPriorityAndTimestamp()
    {
        var ticket = new QueueTicket { Priority = false };
        ticket.SetPriority(true);
        Assert.True(ticket.Priority);
        Assert.NotNull(ticket.UpdatedAt);
    }
}
