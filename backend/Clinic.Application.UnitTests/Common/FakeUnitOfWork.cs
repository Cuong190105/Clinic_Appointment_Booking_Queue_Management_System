using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeUnitOfWork : IUnitOfWork
    {
        private static readonly AsyncLocal<FakeUnitOfWork?> _current = new();
        public static FakeUnitOfWork? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        private readonly HashSet<Guid> _acquiredLocks = [];

        public bool TransactionOpen { get; private set; }
        public bool WasCommitted { get; private set; }
        public bool WasRolledBack { get; private set; }

        public void RegisterAcquiredLock(Guid lockId)
        {
            _acquiredLocks.Add(lockId);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task InitializeTransactionLockAsync(CancellationToken cancellationToken = default)
        {
            TransactionOpen = true;
            Current = this;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (TransactionOpen)
            {
                WasCommitted = true;
                TransactionOpen = false;
                Current = null;
                ReleaseAcquiredLocks();
            }

            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (TransactionOpen)
            {
                WasRolledBack = true;
                TransactionOpen = false;
                Current = null;
                ReleaseAcquiredLocks();
            }

            return Task.CompletedTask;
        }

        public void ReleaseAcquiredLocks()
        {
            foreach (var lockId in _acquiredLocks)
            {
                FakeWorkScheduleRepository.ReleaseStaticLock(lockId);
            }
            _acquiredLocks.Clear();
        }
    }
}