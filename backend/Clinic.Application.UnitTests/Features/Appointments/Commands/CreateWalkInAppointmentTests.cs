using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class CreateWalkInAppointmentTests
    {
        [Fact]
        public async Task Handle_WalkInWithoutDoctor_ShouldAutoAssignDoctorWithFewestAppointments()
        {
            var patientRepo = new FakePatientRepository();
            var workScheduleRepo = new FakeWorkScheduleRepository();
            var appointmentRepo = new FakeAppointmentRepository();
            var queueTicketRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var specialty = TestDataFactory.CreateSpecialty("Nội tổng quát");
            var patient = TestDataFactory.CreatePatient();
            await patientRepo.AddAsync(patient);

            var today = new TimeConverter().ToLocalDate(DateTime.UtcNow);

            // Doctor 1 has 2 appointments
            var doctor1 = TestDataFactory.CreateDoctor(specialty: specialty);
            var ws1 = new WorkSchedule(Guid.NewGuid(), doctor1.Id, today, new TimeOnly(8, 0), new TimeOnly(23, 59), 15, WorkScheduleStatus.Active, DateTime.UtcNow, null, false);
            ws1.AddAppointment(TestDataFactory.CreateAppointment(workSchedule: ws1, timeSlot: new TimeOnly(9, 0)));
            ws1.AddAppointment(TestDataFactory.CreateAppointment(workSchedule: ws1, timeSlot: new TimeOnly(9, 15)));
            await workScheduleRepo.AddWorkScheduleAsync(ws1);

            // Doctor 2 has 0 appointments
            var doctor2 = TestDataFactory.CreateDoctor(specialty: specialty);
            var ws2 = new WorkSchedule(Guid.NewGuid(), doctor2.Id, today, new TimeOnly(8, 0), new TimeOnly(23, 59), 15, WorkScheduleStatus.Active, DateTime.UtcNow, null, false);
            await workScheduleRepo.AddWorkScheduleAsync(ws2);

            var handler = new CreateWalkInAppointmentHandler(patientRepo, workScheduleRepo, appointmentRepo, unitOfWork);

            var command = new CreateWalkInAppointmentCommand(
                UserId: Guid.NewGuid(),
                PatientId: patient.Id,
                SpecialtyId: specialty.Id,
                DoctorId: null, // Auto select
                Reason: "Đau bụng cấp",
                IsUrgent: true
            );

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(doctor2.Id, result.DoctorId); // Should select Doctor 2 with 0 appointments!
            Assert.True(result.IsUrgent);
        }
    }
}

