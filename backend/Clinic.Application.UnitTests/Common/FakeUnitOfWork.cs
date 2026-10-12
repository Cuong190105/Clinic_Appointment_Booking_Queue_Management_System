using Clinic.Application.Interfaces;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeUnitOfWork : IUnitOfWork
    {
        // Test double chạy trong bộ nhớ, không có DB thật nên không có transaction thật -
        // chỉ cần ghi nhận trạng thái để test có thể assert Begin/Commit/Rollback được gọi
        // đúng cặp nếu cần.
        public bool TransactionOpen { get; private set; }
        public bool WasCommitted { get; private set; }
        public bool WasRolledBack { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task InitializeTransactionLockAsync(CancellationToken cancellationToken = default)
        {
            TransactionOpen = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (TransactionOpen)
            {
                WasCommitted = true;
                TransactionOpen = false;
            }

            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (TransactionOpen)
            {
                WasRolledBack = true;
                TransactionOpen = false;
            }

            return Task.CompletedTask;
        }
    }
}