using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeCurrentUser : ICurrentUser
    {
        private readonly HashSet<string> _permissions = [];
        private readonly HashSet<string> _roles = [];

        public bool IsAuthenticated { get; set; } = true;

        public Guid? PatientId { get; set; }

        public Guid? DoctorId { get; set; }

        public Guid? UserId { get; set; }

        public Guid? EmployeeId { get; set; }

        public Guid? PersonId { get; set; }

        public bool HasPermission(string permission) => _permissions.Contains(permission);

        public void GrantPermission(string permission) => _permissions.Add(permission);

        public bool IsInRole(string role) => _roles.Contains(role);

        public void AddRole(string role) => _roles.Add(role);

        public IReadOnlyCollection<string> Roles => _roles;

        public IReadOnlyCollection<string> Permissions => _permissions;
    }
}
