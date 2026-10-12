using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeNotificationQueue : INotificationQueue
    {
        private readonly List<INotificationJob> jobs = new();
        public ValueTask EnqueueAsync(INotificationJob job, CancellationToken cancellationToken = default)
        {
            jobs.Add((INotificationJob)job);
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<INotificationJob> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public List<INotificationJob> ReadAll() {
            return jobs;
        }
    }
}
