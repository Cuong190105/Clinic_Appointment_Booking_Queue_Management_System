using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Queries
{
    public class GetPatientMedicalHistoryForDoctorTests
    {
        [Fact]
        public async Task Handle_DoctorWithRelatedPatient_ShouldReturnFinalizedReportsOnly()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var appointmentRepository = new FakeAppointmentRepository();

            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var appointment = TestDataFactory.CreateAppointment(patient: patient, workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            var ticket1 = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            var reportFinalized = new MedicalReport(appointment.Id, appointment.PatientId, "Fever", "Flu", "Paracetamol", null)
            {
                Appointment = appointment,
                Patient = appointment.Patient
            };
            reportFinalized.FinalizeReport();
            await medicalReportRepository.AddAsync(reportFinalized);

            var appointment2 = TestDataFactory.CreateAppointment(patient: patient, workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment2);
            var ticket2 = TestDataFactory.CreateQueueTicket(appointment2, queueNumber: 2);
            var reportDraft = new MedicalReport(appointment2.Id, appointment2.PatientId, "Cough", "Bronchitis", "Syrup", null)
            {
                Appointment = appointment2,
                Patient = appointment2.Patient
            };
            await medicalReportRepository.AddAsync(reportDraft);

            var handler = new GetPatientMedicalHistoryForDoctorHandler(medicalReportRepository, appointmentRepository, currentUser);

            // Act
            var result = await handler.Handle(new GetPatientMedicalHistoryForDoctorQuery(patient.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Flu", result[0].Diagnosis);
            Assert.Equal(MedicalReportStatus.Finalized, result[0].Status);
        }

        [Fact]
        public async Task Handle_DoctorWithUnrelatedPatient_ShouldThrowForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var appointmentRepository = new FakeAppointmentRepository();

            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            // No appointment between this doctor and patient

            var handler = new GetPatientMedicalHistoryForDoctorHandler(medicalReportRepository, appointmentRepository, currentUser);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new GetPatientMedicalHistoryForDoctorQuery(patient.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_Admin_ShouldBeAllowedForAnyPatient()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var appointmentRepository = new FakeAppointmentRepository();

            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var currentUser = new FakeCurrentUser { UserId = Guid.NewGuid() };
            currentUser.GrantPermission("patient-history.view.any");

            var appointment = TestDataFactory.CreateAppointment(patient: patient, workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            var report = new MedicalReport(appointment.Id, appointment.PatientId, "Headache", "Migraine", "Painkiller", null)
            {
                Appointment = appointment,
                Patient = appointment.Patient
            };
            report.FinalizeReport();
            await medicalReportRepository.AddAsync(report);

            var handler = new GetPatientMedicalHistoryForDoctorHandler(medicalReportRepository, appointmentRepository, currentUser);

            // Act
            var result = await handler.Handle(new GetPatientMedicalHistoryForDoctorQuery(patient.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Migraine", result[0].Diagnosis);
        }
    }
}
