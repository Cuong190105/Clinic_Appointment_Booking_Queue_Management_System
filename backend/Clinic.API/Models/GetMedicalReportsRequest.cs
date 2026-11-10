using System;

namespace Clinic.API.Models
{
    public class GetMedicalReportsRequest
    {
        public string? Search { get; set; }
        public int? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? PageNumber { get; set; } = 1;
        public int? PageSize { get; set; } = 10;
    }
}
