using System;

namespace Clinic.Domain.Common
{
    /// <summary>
    /// Lớp cơ sở cho mọi entity trong Domain.
    /// Cung cấp Id, thời gian tạo/cập nhật và soft-delete dùng chung.
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>Khóa chính dùng chung cho mọi Entity.</summary>
        public Guid Id { get; protected set; }

        /// <summary>Thời điểm bản ghi được tạo (UTC).</summary>
        public DateTime CreatedAt { get; protected set; }

        /// <summary>Thời điểm bản ghi được cập nhật gần nhất (UTC). Null nếu chưa từng sửa.</summary>
        public DateTime? UpdatedAt { get; protected set; }

        /// <summary>Dùng khi tạo mới một Entity: tự sinh Id và thời điểm tạo.</summary>
        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        protected BaseEntity(Guid id, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
        {
            Id = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            IsDeleted = isDeleted;
        }

        public void MarkUpdated()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Soft-delete: API GET mặc định lọc IsDeleted = false.
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}
