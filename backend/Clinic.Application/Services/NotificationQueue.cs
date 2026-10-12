using Clinic.Application.Interfaces;
using System.Threading.Channels;

namespace Clinic.Application.Services
{
    public class NotificationQueue : INotificationQueue
    {
        private readonly Channel<INotificationJob> _queue;
            
        public NotificationQueue()
        {
            _queue = Channel.CreateBounded<INotificationJob>(
            new BoundedChannelOptions(500)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public ValueTask EnqueueAsync(
            INotificationJob job,
            CancellationToken cancellationToken = default)
        {
            return _queue.Writer.WriteAsync(job, cancellationToken);
        }

        public IAsyncEnumerable<INotificationJob> ReadAllAsync(
            CancellationToken cancellationToken = default)
        {
            return _queue.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
