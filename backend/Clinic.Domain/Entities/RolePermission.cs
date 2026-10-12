using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Bảng trung gian N-N giữa Role và Permission.
    /// Khóa chính kép (RoleId, PermissionId)
    /// </summary>
    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
