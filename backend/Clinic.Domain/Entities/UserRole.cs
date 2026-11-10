using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Báº£ng trung gian N-N giá»¯a User vÃ  Role.
    /// KhÃ³a chÃ­nh kÃ©p (UserId, RoleId)
    /// </summary>
    public class UserRole
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public UserRole(User user, Role role)
        {
            User = user;
            UserId = user.Id;
            Role = role;
            RoleId = role.Id;
        }

        public UserRole(Guid userId, Guid roleId)
        {
            UserId = userId;
            RoleId = roleId;
        }
    }
}
