using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _currentTransaction;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // RowVersion khÃ´ng khá»›p - báº£n ghi Ä‘Ã£ bá»‹ request khÃ¡c cáº­p nháº­t trÆ°á»›c.
                // Dá»‹ch sang exception riÃªng cá»§a Application Ä‘á»ƒ Infrastructure (EF Core)
                // khÃ´ng rÃ² rá»‰ lÃªn Handler, giá»¯ Ä‘Ãºng ranh giá»›i Clean Architecture.
                throw new ConcurrencyConflictException(
                    "Dá»¯ liá»‡u Ä‘Ã£ bá»‹ thay Ä‘á»•i bá»Ÿi thao tÃ¡c khÃ¡c, vui lÃ²ng thá»­ láº¡i.");
            }
        }

        public async Task InitializeTransactionLockAsync(CancellationToken cancellationToken = default)
        {
            // TrÃ¡nh má»Ÿ Ä‘Ã¨ 1 transaction khÃ¡c lÃªn transaction Ä‘ang má»Ÿ dá»Ÿ náº¿u handler lá»¡ gá»i
            // 2 láº§n - giá»¯ nguyÃªn transaction Ä‘áº§u tiÃªn, khÃ´ng rÃ² rá»‰ connection.
            if (_currentTransaction != null)
            {
                return;
            }

            _currentTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                return;
            }

            try
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            finally
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                return;
            }

            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }
}