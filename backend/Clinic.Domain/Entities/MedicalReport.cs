using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Hồ sơ khám bệnh gắn với 1 lượt khám (QueueTicket)
    /// </summary>
    public class MedicalReport : BaseEntity
    {
        /// <summary>FK - đường dẫn duy nhất tới Doctor là QueueTicket.Appointment.WorkSchedule.Doctor.</summary>
        public Guid QueueTicketId { get; set; }
        public QueueTicket QueueTicket { get; set; } = null!;

        public string? Symptoms { get; set; }

        public string? Diagnosis { get; set; }

        public string? Prescription { get; set; }

        public string? Notes { get; set; }

        /// <summary>Thời điểm bác sĩ bắt đầu khám (khi bấm "bắt đầu" trên UI, khác với QueueTicket.CalledAt là lúc gọi số).</summary>
        public DateTime? ExamStartTime { get; set; }

        /// <summary>Thời điểm khám xong - dùng để tính thời gian khám thực tế cho báo cáo hiệu suất bác sĩ.</summary>
        public DateTime? ExamEndTime { get; set; }

        /// <summary>Draft trong lúc bác sĩ đang ghi, Finalized khi đã chốt (không cho sửa nữa qua PUT /medical-reports/{id}).</summary>
        public MedicalReportStatus Status { get; set; } = MedicalReportStatus.Draft;

        public MedicalReport() { }

        public MedicalReport(
            Guid id,
            Guid queueTicketId,
            string? symptoms,
            string? diagnosis,
            string? prescription,
            string? notes,
            DateTime? examStartTime,
            DateTime? examEndTime,
            MedicalReportStatus status,
            DateTime createdAt,
            DateTime? updatedAt,
            bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            QueueTicketId = queueTicketId;
            Symptoms = symptoms;
            Diagnosis = diagnosis;
            Prescription = prescription;
            Notes = notes;
            ExamStartTime = examStartTime;
            ExamEndTime = examEndTime;
            Status = status;
        }

        public MedicalReport(Guid queueTicketId, string? symptoms, string? diagnosis, string? prescription, string? notes, DateTime? examStartTime = null)
        {
            QueueTicketId = queueTicketId;
            Symptoms = symptoms;
            Diagnosis = diagnosis;
            Prescription = prescription;
            Notes = notes;
            ExamStartTime = examStartTime ?? DateTime.UtcNow;
            Status = MedicalReportStatus.Draft;
        }

        public void UpdateDetails(string? symptoms, string? diagnosis, string? prescription, string? notes)
        {
            if (Status == MedicalReportStatus.Finalized)
            {
                throw new InvalidOperationException("Cannot update a finalized medical report.");
            }

            Symptoms = symptoms;
            Diagnosis = diagnosis;
            Prescription = prescription;
            Notes = notes;
            MarkUpdated();
        }

        public void FinalizeReport(DateTime? examEndTime = null)
        {
            if (Status == MedicalReportStatus.Finalized) return;

            Status = MedicalReportStatus.Finalized;
            ExamEndTime = examEndTime ?? DateTime.UtcNow;
            MarkUpdated();
        }
    }
}
