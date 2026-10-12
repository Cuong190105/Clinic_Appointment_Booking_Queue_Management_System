using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Users.Queries
{
    public record GetNotificationsQuery : IRequest<PaginationResponse<NotificationDto>>
    {
        public DateTime CreatedBefore { get; set; } = DateTime.UtcNow;
        public int Limit { get; set; } = 10;
    }
    public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, PaginationResponse<NotificationDto>>
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly ICurrentUser _currentUser;
        public GetNotificationsQueryHandler(INotificationRepository notificationRepository, ICurrentUser currentUser)
        {
            _notificationRepository = notificationRepository;
            _currentUser = currentUser;
        }

        public async Task<PaginationResponse<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
        {
            var notifications = await _notificationRepository.GetNotificationsForPerson(_currentUser.PersonId, request.CreatedBefore, request.Limit);
            return new PaginationResponse<NotificationDto>
            {
                Items = notifications.Items.Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead ?? false,
                    CreatedAt = n.CreatedAt
                }).ToList(),
                TotalCount = notifications.TotalCount
            };
        }
    }
}
