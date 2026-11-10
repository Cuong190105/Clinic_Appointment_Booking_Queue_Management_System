using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Há»“ sÆ¡ khÃ¡m bá»‡nh gáº¯n vá»›i 1 lá»‹ch háº¹n (Appointment)
    /// </summary>
    public class MedicalReport : BaseEntity
    {
        /// <summary>FK - liÃªn káº¿t duy nháº¥t cho há»“ sÆ¡ cá»§a lÆ°á»£t khÃ¡m lÃ  AppointmentId.</summary>
        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        /// <summary>Optional legacy link to QueueTicket for backward compatibility.</summary>
        public Guid? QueueTicketId { get; set; }
        public QueueTicket? QueueTicket { get; set; }

        public string? Symptoms { get; set; }

        public string? Diagnosis { get; set; }

        public string? Prescription { get; set; }

        public string? Notes { get; set; }

        /// <summary>Thá»i Ä‘iá»ƒm bÃ¡c sÄ© báº¯t Ä‘áº§u khÃ¡m.</summary>
        public DateTime? ExamStartTime { get; set; }

        /// <summary>Thá»i Ä‘iá»ƒm khÃ¡m xong.</summary>
        public DateTime? ExamEndTime { get; set; }

        /// <summary>Draft trong lÃºc bÃ¡c sÄ© Ä‘ang ghi, Finalized khi Ä‘Ã£ chá»‘t (khÃ´ng cho sá»­a trá»±c tiáº¿p, pháº£i qua amendments).</summary>
        public MedicalReportStatus Status { get; set; } = MedicalReportStatus.Draft;

        public ICollection<MedicalReportAmendment> Amendments { get; set; } = new List<MedicalReportAmendment>();

        public MedicalReport() { }

        public MedicalReport(
            Guid id,
            Guid appointmentId,
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
            AppointmentId = appointmentId;
            Symptoms = symptoms;
            Diagnosis = diagnosis;
            Prescription = prescription;
            Notes = notes;
            ExamStartTime = examStartTime;
            ExamEndTime = examEndTime;
            Status = status;
        }

        public MedicalReport(Guid appointmentId, string? symptoms, string? diagnosis, string? prescription, string? notes, DateTime? examStartTime = null)
        {
            AppointmentId = appointmentId;
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
                throw new InvalidOperationException("Cannot update a finalized medical report. Use amendments instead.");
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

        public MedicalReportAmendment AddAmendment(string reason, string content, Guid doctorId, string doctorName)
        {
            if (Status != MedicalReportStatus.Finalized)
            {
                throw new InvalidOperationException("Amendments can only be added to finalized medical reports.");
            }
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Amendment reason is required.", nameof(reason));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Amendment content is required.", nameof(content));

            var amendment = new MedicalReportAmendment(Id, reason, content, doctorId, doctorName);
            Amendments.Add(amendment);
            MarkUpdated();
            return amendment;
        }
    }
}
