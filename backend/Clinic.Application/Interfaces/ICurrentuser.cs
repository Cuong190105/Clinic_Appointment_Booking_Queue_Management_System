using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }
        Guid? PatientId { get; }
        Guid? DoctorId { get; }
        /// <summary>Id của User hiện tại, lấy từ claim "sub". Null nếu chưa đăng nhập.</summary>
        Guid? UserId { get; }
        Guid? EmployeeId { get; }
        Guid? PersonId { get; }
        bool HasPermission(string permission);
    }
}
