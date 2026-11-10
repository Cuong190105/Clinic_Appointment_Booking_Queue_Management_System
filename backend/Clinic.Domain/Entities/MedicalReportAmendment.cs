using Clinic.Domain.Common;
using System;

namespace Clinic.Domain.Entities
{
    public class MedicalReportAmendment : BaseEntity
    {
        public Guid MedicalReportId { get; set; }
        public MedicalReport MedicalReport { get; set; } = null!;

        public string Reason { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public Guid CreatedByDoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        public MedicalReportAmendment() { }

        public MedicalReportAmendment(Guid medicalReportId, string reason, string content, Guid createdByDoctorId, string doctorName)
        {
            MedicalReportId = medicalReportId;
            Reason = reason;
            Content = content;
            CreatedByDoctorId = createdByDoctorId;
            DoctorName = doctorName;
        }
    }
}
