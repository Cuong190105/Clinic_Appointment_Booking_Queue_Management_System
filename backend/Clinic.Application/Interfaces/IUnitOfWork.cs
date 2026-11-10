using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Application.Interfaces
{
    /// <summary>
    /// Use this to commit all changes to the database in a single transaction.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Submit button. Throws DbUpdateException if the commit fails.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// M? 1 transaction DB tu?ng minh. Dùng khi 1 use case c?n g?p nhi?u thao tác
        /// (vd: khóa 1 dòng b?ng raw SQL r?i INSERT/UPDATE b?ng EF Core) vào chung 1
        /// transaction, thay vì d? SaveChangesAsync t? m?/dóng transaction ng?m riêng l?
        /// cho t?ng l?nh. B?t bu?c g?i CommitTransactionAsync ho?c RollbackTransactionAsync
        /// sau dó d? dóng transaction, n?u không s? gi? khóa/connection lâu hon c?n thi?t.
        /// </summary>
        Task InitializeTransactionLockAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commit transaction dã m? b?ng BeginTransactionAsync. Không làm gì n?u chua có
        /// transaction nào dang m?.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rollback transaction dã m? b?ng BeginTransactionAsync. Không làm gì n?u chua có
        /// transaction nào dang m?. Nên g?i trong catch-all c?a handler dã BeginTransactionAsync,
        /// d? gi?i phóng khóa/connection ngay khi có l?i thay vì ch? h?t request.
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}