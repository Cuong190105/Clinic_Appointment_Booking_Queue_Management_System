using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Báº£ng trung gian N-N giá»¯a Role vÃ  Permission.
    /// KhÃ³a chÃ­nh kÃ©p (RoleId, PermissionId)
    /// </summary>
    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
