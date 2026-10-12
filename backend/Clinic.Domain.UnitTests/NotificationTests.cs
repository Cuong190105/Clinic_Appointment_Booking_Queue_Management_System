using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class NotificationTests
    {
        [Fact]
        public void CreateNotification_ShouldInitializePending()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();

            // Act
            var notification = new Notification(person.Id, NotificationType.AppointmentConfirmation, "Title", "Message", NotificationChannel.Email);

            // Assert
            Assert.Equal(NotificationStatus.Pending, notification.Status);
            Assert.Equal(NotificationType.AppointmentConfirmation, notification.Type);
            Assert.Equal(NotificationChannel.Email, notification.Channel);
            Assert.Null(notification.SendTime);
        }

        [Fact]
        public void MarkSent_ShouldUpdateStatusAndSendTime()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();
            var notification = new Notification(person.Id, NotificationType.AppointmentReminder, "Title", "Message", NotificationChannel.Sms);

            // Act
            notification.MarkAsSent();

            // Assert
            Assert.Equal(NotificationStatus.Sent, notification.Status);
            Assert.NotNull(notification.SendTime);
        }
    }
}
