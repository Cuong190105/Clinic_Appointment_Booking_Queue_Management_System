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
            Console.WriteLine("Sent SMS to {0}: {1}", phoneNumber, message);
        }
    }
}
