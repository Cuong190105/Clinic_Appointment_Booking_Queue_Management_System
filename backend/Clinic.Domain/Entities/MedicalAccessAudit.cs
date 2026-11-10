using Clinic.Domain.Common;
using System;

namespace Clinic.Domain.Entities
{
    public class MedicalAccessAudit : BaseEntity
    {
        public Guid MedicalReportId { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public MedicalAccessAudit() { }

        public MedicalAccessAudit(Guid medicalReportId, Guid userId, string action, string? details = null)
        {
            MedicalReportId = medicalReportId;
            UserId = userId;
            Action = action;
            Details = details;
            Timestamp = DateTime.UtcNow;
        }
    }
}
