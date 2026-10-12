using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System.Reflection.Metadata.Ecma335;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeRoleRepository : IRoleRepository
    {
        private readonly List<Role> _roles = [];

        public FakeRoleRepository()
        {
            _roles.Add(new Role(id: Guid.NewGuid(), name: "Admin", description: "Administrator role", createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow, isDeleted: false));
            _roles.Add(new Role(id: Guid.NewGuid(), name: "Patient", description: "Patient role", createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow, isDeleted: false));
            _roles.Add(new Role(id: Guid.NewGuid(), name: "Doctor", description: "Doctor role", createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow, isDeleted: false));
            _roles.Add(new Role(id: Guid.NewGuid(), name: "Receptionist", description: "Receptionist role", createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow, isDeleted: false));
        }

        public async Task<Role?> GetByNameAsync(string roleName)
        {
            return _roles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        }

    }
}
