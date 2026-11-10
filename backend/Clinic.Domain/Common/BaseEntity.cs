using System;

namespace Clinic.Domain.Common
{
    /// <summary>
    /// Lá»›p cÆ¡ sá»Ÿ cho má»i entity trong Domain.
    /// Cung cáº¥p Id, thá»i gian táº¡o/cáº­p nháº­t vÃ  soft-delete dÃ¹ng chung.
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>KhÃ³a chÃ­nh dÃ¹ng chung cho má»i Entity.</summary>
        public Guid Id { get; protected set; }

        /// <summary>Thá»i Ä‘iá»ƒm báº£n ghi Ä‘Æ°á»£c táº¡o (UTC).</summary>
        public DateTime CreatedAt { get; protected set; }

        /// <summary>Thá»i Ä‘iá»ƒm báº£n ghi Ä‘Æ°á»£c cáº­p nháº­t gáº§n nháº¥t (UTC). Null náº¿u chÆ°a tá»«ng sá»­a.</summary>
        public DateTime? UpdatedAt { get; protected set; }

        /// <summary>DÃ¹ng khi táº¡o má»›i má»™t Entity: tá»± sinh Id vÃ  thá»i Ä‘iá»ƒm táº¡o.</summary>
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
        /// Soft-delete: API GET máº·c Ä‘á»‹nh lá»c IsDeleted = false.
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}
