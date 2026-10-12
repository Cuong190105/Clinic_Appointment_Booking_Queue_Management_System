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
            var appointment = TestDataFactory.CreateAppointment(day: 1);

            appointment.Cancel(Guid.NewGuid());

            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }

        [Fact]
        public void Cancel_ConfirmedAppointment_ShouldCancel()
        {
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, day: 1);

            appointment.Cancel(Guid.NewGuid());

            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }

        [Fact]
        public void Cancel_AlreadyCancelledAppointment_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, day: 1);
            appointment.Cancel(Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));
        }

        // Cannot cancel appointment within 24 hours before the appointment time, so this test is commented out.
        /// <summary>
        /// Bug gốc: Cancel() chỉ chặn Completed/Cancelled/NoShow, nên 1 Appointment CheckedIn
        /// (vé đang Waiting) vẫn bị hủy được -> Appointment=Cancelled nhưng QueueTicket vẫn Waiting,
        /// khiến lễ tân vẫn gọi phải bệnh nhân "ảo". Sau fix, cascade hủy QueueTicket khi vé còn Waiting.
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
        /// <summary>Tương tự trường hợp Waiting, nhưng vé đã được lễ tân gọi số (Called).</summary>
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

        /// <summary>Bác sĩ đang khám dở (InProgress) -> hủy Appointment lúc này vô nghĩa, phải chặn.</summary>
        [Fact]
        public void Cancel_CheckedInAppointmentWithInProgressTicket_ShouldThrowArgumentException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
            queueTicket.Call();
            queueTicket.StartExam();

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));

            // Trạng thái không bị thay đổi khi bị chặn.
            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
            Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
        }

        /// <summary>Đã khám xong (Completed) -> hủy Appointment lúc này cũng vô nghĩa, phải chặn.</summary>
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
        /// Bảo vệ bất biến "CheckedIn => có QueueTicket": nếu tầng gọi (vd. repository thiếu
        /// Include) không load QueueTicket, Cancel() phải báo lỗi rõ ràng thay vì âm thầm hủy
        /// Appointment và để lại vé "mồ côi" trong DB - đúng nguyên nhân gốc của bug.
        /// </summary>
        [Fact]
        public void Cancel_CheckedInAppointmentWithoutLoadedQueueTicket_ShouldThrowInvalidOperationException()
        {
            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            // Cố tình không gọi CreateQueueTicket - mô phỏng repository quên Include(a => a.QueueTicket).

            Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid()));
        }

        /// <summary>
        /// Bug gốc: CompleteExamHandler chỉ gọi QueueTicket.Complete(Guid.NewGuid()) mà không đổi luôn
        /// Appointment.Status, khiến Appointment kẹt ở CheckedIn dù đã khám xong.
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
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, day: 1);
            appointment.Cancel(Guid.NewGuid());

            Assert.Throws<InvalidOperationException>(() => appointment.Complete(Guid.NewGuid()));
        }
    }
}