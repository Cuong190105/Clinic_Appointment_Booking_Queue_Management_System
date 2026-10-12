using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _context;
        public NotificationRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
        {
            await _context.Notifications.AddAsync(notification, cancellationToken);
        }

        public async Task<IEnumerable<Notification>> GetAllScheduledAndFailedByAsync(DateTimeOffset time, CancellationToken stoppingToken)
        {
            return await _context.Notifications.Include(n => n.Person).ThenInclude(p => p.User)
                .Where(n => n.Status == NotificationStatus.Failed || (n.ScheduledAt != null && n.ScheduledAt <= time && n.Status == NotificationStatus.Pending)).ToListAsync(stoppingToken);
        }

        public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return await _context.Notifications.Include(n => n.Person).ThenInclude(p => p.User).FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        }

        public async Task<PagedResult<Notification>> GetNotificationsForPerson(Guid? personId, DateTime createdBefore, int limit)
        {
            var query = _context.Notifications.Where(n => n.PersonId == personId && n.CreatedAt < createdBefore && n.IsDeleted == false);
            var item = await query.Take(limit).ToListAsync();
            var count = await query.CountAsync();
            return new PagedResult<Notification>
            (
                items: item,
                totalCount: count
            );
        }
    }
}
