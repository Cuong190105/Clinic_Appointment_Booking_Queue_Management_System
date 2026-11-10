using Clinic.Application.Common.Models;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Dispatchers;
using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.Notifications
{
    public class NotificationRetryAndProtectionTests
    {
        private class TestEmailSender : IEmailSender
        {
            public bool ShouldThrow { get; set; }
            public List<(string To, string Subject, string Body)> Sent { get; } = new();

            public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("SMTP connection timeout");
                }
                Sent.Add((to, subject, body));
                return Task.CompletedTask;
            }
        }

        private class TestSmsSender : ISmsSender
        {
            public bool ShouldThrow { get; set; }
            public List<(string Phone, string Message)> Sent { get; } = new();

            public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("SMS Gateway unavailable");
                }
                Sent.Add((phoneNumber, message));
                return Task.CompletedTask;
            }
        }

        private class TestInAppSender : IInAppSender
        {
            public bool ShouldThrow { get; set; }
            public List<(Guid UserId, string Title, string Message)> Sent { get; } = new();

            public Task SendAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("SignalR hub disconnected");
                }
                Sent.Add((userId, title, message));
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task ProcessAsync_EmailSuccess_MarksNotificationAsSent()
        {
            // Arrange
            var repo = new FakeNotificationRepository();
            var emailSender = new TestEmailSender();
            var smsSender = new TestSmsSender();
            var inAppSender = new TestInAppSender();
            var handler = new DueNotificationHandler(repo, emailSender, smsSender, inAppSender);

            var person = TestDataFactory.CreatePerson(email: "patient@example.com");
            var notification = new Notification(person.Id, NotificationType.AppointmentConfirmation, "Title", "Message", NotificationChannel.Email);
            notification.Person = person;
            await repo.AddAsync(notification);

            // Act
            await handler.ProcessAsync(CancellationToken.None);

            // Assert
            Assert.Equal(NotificationStatus.Sent, notification.Status);
            Assert.NotNull(notification.SendTime);
            Assert.Single(emailSender.Sent);
            Assert.Equal("patient@example.com", emailSender.Sent[0].To);
        }

        [Fact]
        public async Task ProcessAsync_SenderThrows_IncrementsDeliveryAttempts()
        {
            // Arrange
            var repo = new FakeNotificationRepository();
            var emailSender = new TestEmailSender { ShouldThrow = true };
            var smsSender = new TestSmsSender();
            var inAppSender = new TestInAppSender();
            var handler = new DueNotificationHandler(repo, emailSender, smsSender, inAppSender);

            var person = TestDataFactory.CreatePerson(email: "patient@example.com");
            var notification = new Notification(person.Id, NotificationType.AppointmentConfirmation, "Title", "Message", NotificationChannel.Email);
            notification.Person = person;
            await repo.AddAsync(notification);

            // Act
            await handler.ProcessAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, notification.DeliveryAttempts);
            Assert.Equal(NotificationStatus.Pending, notification.Status);
            Assert.Contains("SMTP connection timeout", notification.FailureReason);
        }

        [Fact]
        public async Task ProcessAsync_AfterThreeFailures_TransitionsToFailedStatus()
        {
            // Arrange
            var repo = new FakeNotificationRepository();
            var emailSender = new TestEmailSender { ShouldThrow = true };
            var smsSender = new TestSmsSender();
            var inAppSender = new TestInAppSender();
            var handler = new DueNotificationHandler(repo, emailSender, smsSender, inAppSender);

            var person = TestDataFactory.CreatePerson(email: "patient@example.com");
            var notification = new Notification(person.Id, NotificationType.AppointmentConfirmation, "Title", "Message", NotificationChannel.Email);
            notification.Person = person;
            // Pre-fail twice
            notification.IncrementDeliveryAttempts("Attempt 1 failed");
            notification.IncrementDeliveryAttempts("Attempt 2 failed");
            await repo.AddAsync(notification);

            // Act - 3rd attempt
            await handler.ProcessAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, notification.DeliveryAttempts);
            Assert.Equal(NotificationStatus.Failed, notification.Status);
        }

        [Fact]
        public async Task ProcessAsync_AlreadySentNotification_IsDeduplicatedAndSkipped()
        {
            // Arrange
            var repo = new FakeNotificationRepository();
            var emailSender = new TestEmailSender();
            var smsSender = new TestSmsSender();
            var inAppSender = new TestInAppSender();
            var handler = new DueNotificationHandler(repo, emailSender, smsSender, inAppSender);

            var person = TestDataFactory.CreatePerson(email: "patient@example.com");
            var notification = new Notification(person.Id, NotificationType.AppointmentConfirmation, "Title", "Message", NotificationChannel.Email);
            notification.Person = person;
            notification.MarkAsSent(); // Already Sent
            await repo.AddAsync(notification);

            // Act
            await handler.ProcessAsync(CancellationToken.None);

            // Assert
            Assert.Empty(emailSender.Sent);
            Assert.Equal(0, notification.DeliveryAttempts);
        }

        [Fact]
        public async Task ProcessAsync_MissingRecipientEmail_IncrementsDeliveryAttemptsAndRecordsReason()
        {
            // Arrange
            var repo = new FakeNotificationRepository();
            var emailSender = new TestEmailSender();
            var smsSender = new TestSmsSender();
            var inAppSender = new TestInAppSender();
            var handler = new DueNotificationHandler(repo, emailSender, smsSender, inAppSender);

            var person = TestDataFactory.CreatePerson(email: ""); // Empty email!
            var notification = new Notification(person.Id, NotificationType.AppointmentConfirmation, "Title", "Message", NotificationChannel.Email);
            notification.Person = person;
            await repo.AddAsync(notification);

            // Act
            await handler.ProcessAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, notification.DeliveryAttempts);
            Assert.Contains("Missing recipient email", notification.FailureReason);
            Assert.Empty(emailSender.Sent);
        }

        [Fact]
        public async Task ForgotPasswordHandler_UserNotFound_ReturnsMinusOneAndDoesNotEnqueueNotification()
        {
            // Arrange
            var personRepository = new FakePersonRepository();
            var userRepository = new FakeUserRepository();
            var passwordHasher = new FakePasswordHasher();
            var userService = new UserService(userRepository, personRepository, passwordHasher);
            var unitOfWork = new FakeUnitOfWork();
            var notificationQueue = new FakeNotificationQueue();

            var handler = new ForgotPasswordCommandHandler(personRepository, userService, unitOfWork, notificationQueue);

            // Act
            var result = await handler.Handle(new ForgotPasswordCommand("nonexistent@example.com"), CancellationToken.None);

            // Assert
            Assert.Equal(-1, result);
            Assert.Empty(notificationQueue.ReadAll());
        }

        [Fact]
        public async Task ForgotPasswordHandler_ValidUser_UpdatesPasswordAndEnqueuesJobSafely()
        {
            // Arrange
            var personRepository = new FakePersonRepository();
            var userRepository = new FakeUserRepository();
            var passwordHasher = new FakePasswordHasher();
            var userService = new UserService(userRepository, personRepository, passwordHasher);
            var unitOfWork = new FakeUnitOfWork();
            var notificationQueue = new FakeNotificationQueue();

            var person = TestDataFactory.CreatePerson(email: "valid@example.com");
            var user = TestDataFactory.CreateUser("Patient", "validuser", "oldhashedpwd", person);
            person.User = user;
            await personRepository.AddAsync(person);
            await userRepository.AddAsync(user);

            var handler = new ForgotPasswordCommandHandler(personRepository, userService, unitOfWork, notificationQueue);

            // Act
            var result = await handler.Handle(new ForgotPasswordCommand("valid@example.com"), CancellationToken.None);

            // Assert
            Assert.Equal(0, result);
            Assert.Single(notificationQueue.ReadAll());
            var queuedJob = notificationQueue.ReadAll()[0];
            Assert.Equal(NotificationType.ResetPassword, queuedJob.NotificationType);
            Assert.True(queuedJob.SendEmail);
            Assert.NotEqual("oldhashedpwd", user.PasswordHash);
        }
    }
}
