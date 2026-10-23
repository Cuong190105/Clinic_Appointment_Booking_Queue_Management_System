using Clinic.Application.Contracts;
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
    public class GetMedicalReportsQueryTests
    {
        [Fact]
        public async Task Handle_WithSearchFilter_ReturnsMatchingReports()
        {
            var repo = new FakeMedicalReportRepository();
            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            var ws = TestDataFactory.CreateWorkSchedule(doctor: doctor);
            var appointment1 = TestDataFactory.CreateAppointment(workSchedule: ws);
            var appointment2 = TestDataFactory.CreateAppointment(workSchedule: ws, timeSlot: new TimeOnly(9, 0));

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.GrantPermission("medical-report.view.any");

            var report1 = new MedicalReport(appointment1.Id, patient.Id, "Sốt cao", "Viêm họng cấp", "Paracetamol", "Nghỉ ngơi")
            {
                Appointment = appointment1,
                Patient = patient
            };
            var report2 = new MedicalReport(appointment2.Id, patient.Id, "Đau đầu", "Đau nửa đầu", "Seduxen", "Tránh stress")
            {
                Appointment = appointment2,
                Patient = patient
            };
            await repo.AddAsync(report1);
            await repo.AddAsync(report2);

            var handler = new GetMedicalReportsQueryHandler(repo, currentUser);
            var result = await handler.Handle(new GetMedicalReportsQuery(Search: "Viêm họng"), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Contains("Viêm họng cấp", result.Items[0].Diagnosis);
        }
    }
}

