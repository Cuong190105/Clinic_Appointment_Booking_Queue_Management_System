using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class AppointmentTests
    {
        [Fact]
        public void Cancel_PendingAppointment_ShouldCancel()
        {
            var appointment = TestDataFactory.CreateAppointment(day: 2);

            appointment.Cancel(Guid.NewGuid());

            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }

        [Fact]
        public void Cancel_ConfirmedAppointment_ShouldCancel()
        {
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, day: 2);

            appointment.Cancel(Guid.NewGuid());

            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }

        [Fact]
        public void Cancel_AlreadyCancelledAppointment_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, day: 2);
            appointment.Cancel(Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));
        }

        // Cannot cancel appointment within 24 hours before the appointment time, so this test is commented out.
        /// <summary>
        /// Bug g?c: Cancel() ch? ch?n Completed/Cancelled/NoShow, nên 1 Appointment CheckedIn
        /// (vé dang Waiting) v?n b? h?y du?c -> Appointment=Cancelled nhung QueueTicket v?n Waiting,
        /// khi?n l? tân v?n g?i ph?i b?nh nhân "?o". Sau fix, cascade h?y QueueTicket khi vé còn Waiting.
        /// </summary>
        //[Fact]
        //public void Cancel_CheckedInAppointmentWithWaitingTicket_ShouldCancelAppointmentAndTicket()
        //{
        //    var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
        //    var queueTicket = TestDataFactory.CreateQueueTicket(appointment);

        //    appointment.Cancel(Guid.NewGuid());

        //    Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        //    Assert.Equal(QueueStatus.Cancelled, queueTicket.Status);
        //}

        // Cannot cancel appointment within 24 hours before the appointment time, so this test is commented out.
        /// <summary>Tuong t? tru?ng h?p Waiting, nhung vé dã du?c l? tân g?i s? (Called).</summary>
        //[Fact]
        //public void Cancel_CheckedInAppointmentWithCalledTicket_ShouldCancelAppointmentAndTicket()
        //{
        //    var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
        //    var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
        //    queueTicket.Call();

        //    appointment.Cancel(Guid.NewGuid());

        //    Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        //    Assert.Equal(QueueStatus.Cancelled, queueTicket.Status);
        //}

        /// <summary>Bác si dang khám d? (InProgress) -> h?y Appointment lúc này vô nghia, ph?i ch?n.</summary>
        [Fact]
        public void Cancel_CheckedInAppointmentWithInProgressTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Call();
            queueTicket.StartExam();

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));

            // Tr?ng thái không b? thay d?i khi b? ch?n.
            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
            Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
        }

        /// <summary>Ðã khám xong (Completed) -> h?y Appointment lúc này cung vô nghia, ph?i ch?n.</summary>
        [Fact]
        public void Cancel_CheckedInAppointmentWithCompletedTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Call();
            queueTicket.StartExam();
            queueTicket.Complete();

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));
        }

        /// <summary>
        /// B?o v? b?t bi?n "CheckedIn => có QueueTicket": n?u t?ng g?i (vd. repository thi?u
        /// Include) không load QueueTicket, Cancel() ph?i báo l?i rõ ràng thay vì âm th?m h?y
        /// Appointment và d? l?i vé "m? côi" trong DB - dúng nguyên nhân g?c c?a bug.
        /// </summary>
        [Fact]
        public void Cancel_CheckedInAppointmentWithoutLoadedQueueTicket_ShouldThrowInvalidOperationException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            // C? tình không g?i CreateQueueTicket - mô ph?ng repository quên Include(a => a.QueueTicket).

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));
        }

        /// <summary>
        /// Bug g?c: CompleteExamHandler ch? g?i QueueTicket.Complete(Guid.NewGuid()) mà không d?i luôn
        /// Appointment.Status, khi?n Appointment k?t ? CheckedIn dù dã khám xong.
        /// </summary>
        [Fact]
        public void Complete_CheckedInAppointment_ShouldComplete()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);

            appointment.Complete(Guid.NewGuid());

            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        }

        [Theory]
        [InlineData(false, false)] // Pending
        [InlineData(true, false)]  // Confirmed
        public void Complete_NotCheckedInAppointment_ShouldThrowArgumentException(bool confirmed, bool checkedIn)
        {
            var appointment = TestDataFactory.CreateAppointment(confirmed: confirmed, checkedIn: checkedIn);

            Assert.Throws<InvalidOperationException>(() => appointment.Complete(Guid.NewGuid()));
        }

        [Fact]
        public void Complete_AlreadyCompletedAppointment_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            appointment.Complete(Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(() => appointment.Complete(Guid.NewGuid()));
        }

        [Fact]
        public void Complete_CancelledAppointment_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, day: 2);
            appointment.Cancel(Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(() => appointment.Complete(Guid.NewGuid()));
        }
    }
}