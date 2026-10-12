using Clinic.Application.Interfaces;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeCurrentUser : ICurrentUser
    {
        private readonly HashSet<string> _permissions = [];

        public bool IsAuthenticated { get; set; } = true;

        public Guid? PatientId { get; set; }

        public Guid? DoctorId { get; set; }

        public Guid? UserId { get; set; }

        public Guid? EmployeeId { get; set; }

        public Guid? PersonId { get; set; }

        public bool HasPermission(string permission) => _permissions.Contains(permission);

        public void GrantPermission(string permission) => _permissions.Add(permission);
    }
}