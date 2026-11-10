using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeNotificationRepository : INotificationRepository
    {
        private readonly List<Notification> _notifications = new();

        public List<Notification> Notifications => _notifications;

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            _notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Notification>> GetAllScheduledAndFailedByAsync(DateTimeOffset time, CancellationToken stoppingToken)
        {
            var results = _notifications
                .Where(n => !n.IsDeleted && (n.ScheduledAt == null || n.ScheduledAt <= time))
                .ToList();
            return Task.FromResult<IEnumerable<Notification>>(results);
        }

        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == id && !n.IsDeleted);
            return Task.FromResult(notification);
        }

        public Task<PagedResult<Notification>> GetNotificationsForPerson(Guid? personId, DateTime createdBefore, int limit)
        {
            var query = _notifications
                .Where(n => !n.IsDeleted && (!personId.HasValue || n.PersonId == personId.Value) && n.CreatedAt < createdBefore)
                .Take(limit)
                .ToList();

            return Task.FromResult(new PagedResult<Notification>(query, query.Count));
        }
    }
}
