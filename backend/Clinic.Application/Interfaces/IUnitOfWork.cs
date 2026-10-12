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
        /// Mở 1 transaction DB tường minh. Dùng khi 1 use case cần gộp nhiều thao tác
        /// (vd: khóa 1 dòng bằng raw SQL rồi INSERT/UPDATE bằng EF Core) vào chung 1
        /// transaction, thay vì để SaveChangesAsync tự mở/đóng transaction ngầm riêng lẻ
        /// cho từng lệnh. Bắt buộc gọi CommitTransactionAsync hoặc RollbackTransactionAsync
        /// sau đó để đóng transaction, nếu không sẽ giữ khóa/connection lâu hơn cần thiết.
        /// </summary>
        Task InitializeTransactionLockAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commit transaction đã mở bằng BeginTransactionAsync. Không làm gì nếu chưa có
        /// transaction nào đang mở.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rollback transaction đã mở bằng BeginTransactionAsync. Không làm gì nếu chưa có
        /// transaction nào đang mở. Nên gọi trong catch-all của handler đã BeginTransactionAsync,
        /// để giải phóng khóa/connection ngay khi có lỗi thay vì chờ hết request.
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}