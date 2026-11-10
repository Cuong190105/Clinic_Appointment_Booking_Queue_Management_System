using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    /// <summary>
    /// DTO chi ti?t bác si dùng cho endpoint public/anonymous (GET /doctors/{doctorId}).
    /// KHÔNG ch?a các tru?ng nh?y c?m/n?i b? nhu DateOfBirth, Address, HireDate
    /// (nh?ng tru?ng này ch? có trong DoctorDetailDto, dùng cho các endpoint yêu c?u
    /// dang nh?p nhu GET /doctors/me).
    /// </summary>
    public class DoctorPublicDetailDto : DoctorSummaryDto
    {
        public string Biography { get; set; }
    }
}