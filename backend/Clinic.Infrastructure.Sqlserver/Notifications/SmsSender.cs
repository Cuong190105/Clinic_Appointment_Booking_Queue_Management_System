using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Infrastructure.Sqlserver.Notifications
{
    public class SmsSender : ISmsSender
    {
        public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            // Currently found no free SMS sending service, so this is a placeholder for future implementation.
            // Placeholder provider: never log message content because it may contain a one-time reset code.
            await Task.CompletedTask;
        }
    }
}
