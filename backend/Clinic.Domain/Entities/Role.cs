using Clinic.Domain.Common;
using System.Collections.Generic;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Vai trò: Admin, Doctor, Receptionist, Patient.
    /// </summary>
    public class Role : BaseEntity
    {
        /// <summary>UNIQUE.</summary>
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

        public Role(Guid id, string name, string? description, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            Name = name;
            Description = description;
        }
    }
}
