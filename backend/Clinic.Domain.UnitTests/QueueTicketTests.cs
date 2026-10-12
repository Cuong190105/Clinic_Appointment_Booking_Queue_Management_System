using Clinic.Domain.Enums;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class QueueTicketTests
    {
        [Fact]
        public void Cancel_WaitingTicket_ShouldCancel()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);

            queueTicket.Cancel();

            Assert.Equal(QueueStatus.Cancelled, queueTicket.Status);
        }

        [Fact]
        public void Cancel_CalledTicket_ShouldCancel()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Call();

            queueTicket.Cancel();

            Assert.Equal(QueueStatus.Cancelled, queueTicket.Status);
        }

        [Fact]
        public void Cancel_InProgressTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Call();
            queueTicket.StartExam();

            Assert.Throws<ArgumentException>(() => queueTicket.Cancel());
        }

        [Fact]
        public void Cancel_CompletedTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Call();
            queueTicket.StartExam();
            queueTicket.Complete();

            Assert.Throws<ArgumentException>(() => queueTicket.Cancel());
        }

        [Fact]
        public void Cancel_SkippedTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Skip();

            Assert.Throws<ArgumentException>(() => queueTicket.Cancel());
        }

        [Fact]
        public void Cancel_AlreadyCancelledTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Cancel();

            Assert.Throws<ArgumentException>(() => queueTicket.Cancel());
        }
    }
}