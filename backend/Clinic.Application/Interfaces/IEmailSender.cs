using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
    }
}
