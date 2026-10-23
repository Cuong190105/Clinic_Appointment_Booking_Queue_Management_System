using Clinic.Domain.Enums;
using System;

namespace Clinic.API.Models
{
    public class GetMedicalReportsRequest
    {
        public string? Search { get; set; }
        public MedicalReportStatus? Status { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public int? PageNumber { get; set; } = 1;
        public int? PageSize { get; set; } = 10;
    }

    public class UpdateMedicalReportRequest
    {
        public string? Symptoms { get; set; }
        public string? Diagnosis { get; set; }
        public string? Prescription { get; set; }
        public string? Notes { get; set; }
        public bool IsFinalize { get; set; } = false;
    }
}

