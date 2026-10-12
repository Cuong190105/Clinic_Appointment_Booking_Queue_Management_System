using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class MedicalReportTests
    {
        [Fact]
        public void CreateMedicalReport_ShouldInitializeWithDraftStatus()
        {
            // Arrange & Act
            var report = new MedicalReport
            {
                QueueTicketId = Guid.NewGuid(),
                Symptoms = "Fever and sore throat"
            };

            // Assert
            Assert.Equal(MedicalReportStatus.Draft, report.Status);
            Assert.Null(report.Diagnosis);
            Assert.Null(report.Prescription);
            Assert.Null(report.ExamEndTime);
        }

        [Fact]
        public void UpdateMedicalReport_WhenDraft_ShouldUpdateSymptomsDiagnosisPrescription()
        {
            // Arrange
            var report = new MedicalReport
            {
                QueueTicketId = Guid.NewGuid(),
                Status = MedicalReportStatus.Draft
            };

            // Act
            report.Symptoms = "Persistent cough";
            report.Diagnosis = "Acute bronchitis";
            report.Prescription = "Amoxicillin 500mg, 3 times/day";
            report.Notes = "Drink plenty of water and rest";
            report.ExamStartTime = DateTime.UtcNow.AddMinutes(-20);

            // Assert
            Assert.Equal("Persistent cough", report.Symptoms);
            Assert.Equal("Acute bronchitis", report.Diagnosis);
            Assert.Equal("Amoxicillin 500mg, 3 times/day", report.Prescription);
            Assert.Equal("Drink plenty of water and rest", report.Notes);
            Assert.NotNull(report.ExamStartTime);
        }

        [Fact]
        public void FinalizeMedicalReport_ShouldSetStatusFinalizedAndEndTime()
        {
            // Arrange
            var report = new MedicalReport
            {
                QueueTicketId = Guid.NewGuid(),
                Status = MedicalReportStatus.Draft,
                ExamStartTime = DateTime.UtcNow.AddMinutes(-30)
            };

            // Act
            report.Status = MedicalReportStatus.Finalized;
            report.ExamEndTime = DateTime.UtcNow;

            // Assert
            Assert.Equal(MedicalReportStatus.Finalized, report.Status);
            Assert.NotNull(report.ExamEndTime);
        }
    }
}
