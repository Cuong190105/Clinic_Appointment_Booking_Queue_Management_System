using Clinic.Application.Interfaces;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeUnitOfWork : IUnitOfWork
    {
        // Test double ch?y trong b? nh?, không có DB th?t nên không có transaction th?t -
        // ch? c?n ghi nh?n tr?ng thái d? test có th? assert Begin/Commit/Rollback du?c g?i
        // dúng c?p n?u c?n.
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