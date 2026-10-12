using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    /// <summary>
    /// DTO chi tiết bác sĩ dùng cho endpoint public/anonymous (GET /doctors/{doctorId}).
    /// KHÔNG chứa các trường nhạy cảm/nội bộ như DateOfBirth, Address, HireDate
    /// (những trường này chỉ có trong DoctorDetailDto, dùng cho các endpoint yêu cầu
    /// đăng nhập như GET /doctors/me).
    /// </summary>
    public class DoctorPublicDetailDto : DoctorSummaryDto
    {
        public string Biography { get; set; }
    }
}