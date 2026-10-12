using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Queries
{
    public class GetMyMedicalHistoryTests
    {
        [Fact]
        public async Task Handle_PatientWithFinalizedReports_ShouldReturnMedicalHistory()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new GetMyMedicalHistoryHandler(medicalReportRepository, currentUser);

            var patient = TestDataFactory.CreatePatient();
            currentUser.PatientId = patient.Id;

            // Finalized report for this patient
            var app1 = TestDataFactory.CreateAppointment(patient: patient, checkedIn: true);
            var ticket1 = TestDataFactory.CreateQueueTicket(app1);
            var report1 = TestDataFactory.CreateMedicalReport(queueTicket: ticket1, status: MedicalReportStatus.Finalized, symptoms: "Cough", diagnosis: "Common cold");

            // Another patient's report
            var app2 = TestDataFactory.CreateAppointment(patient: TestDataFactory.CreatePatient(), checkedIn: true);
            var ticket2 = TestDataFactory.CreateQueueTicket(app2);
            var report2 = TestDataFactory.CreateMedicalReport(queueTicket: ticket2, status: MedicalReportStatus.Finalized);

            medicalReportRepository.Add(report1);
            medicalReportRepository.Add(report2);

            // Act
            var result = await handler.Handle(new GetMyMedicalHistoryQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(report1.Id, result[0].Id);
            Assert.Equal("Cough", result[0].Symptoms);
            Assert.Equal("Common cold", result[0].Diagnosis);
        }

        [Fact]
        public async Task Handle_DraftReports_ShouldBeExcluded()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new GetMyMedicalHistoryHandler(medicalReportRepository, currentUser);

            var patient = TestDataFactory.CreatePatient();
            currentUser.PatientId = patient.Id;

            // Draft report for this patient (still being written)
            var app = TestDataFactory.CreateAppointment(patient: patient, checkedIn: true);
            var ticket = TestDataFactory.CreateQueueTicket(app);
            var draftReport = TestDataFactory.CreateMedicalReport(queueTicket: ticket, status: MedicalReportStatus.Draft);

            medicalReportRepository.Add(draftReport);

            // Act
            var result = await handler.Handle(new GetMyMedicalHistoryQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_UserWithoutPatientId_ShouldThrowForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var currentUser = new FakeCurrentUser { PatientId = null };
            var handler = new GetMyMedicalHistoryHandler(medicalReportRepository, currentUser);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new GetMyMedicalHistoryQuery(), CancellationToken.None));
        }
    }
}
