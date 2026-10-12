using Clinic.API.Hubs;
using Clinic.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Clinic.API.Services
{
    public class InAppSender : IInAppSender
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public InAppSender(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendAsync(
            Guid userId,
            string title,
            string content,
            CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync(
                    "NotificationReceived",
                    new
                    {
                        title,
                        content
                    },
                    cancellationToken);
        }
    }
}
