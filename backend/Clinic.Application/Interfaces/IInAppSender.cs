using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    public interface IInAppSender
    {
        Task SendAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default);
    }
}
