using System;
using System.Collections.Generic;

namespace Clinic.Application.Interfaces
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }
        Guid? PatientId { get; }
        Guid? DoctorId { get; }
        Guid? UserId { get; }
        Guid? EmployeeId { get; }
        Guid? PersonId { get; }
        bool HasPermission(string permission);
        bool IsInRole(string role);
        IReadOnlyCollection<string> Roles { get; }
        IReadOnlyCollection<string> Permissions { get; }
    }
}
