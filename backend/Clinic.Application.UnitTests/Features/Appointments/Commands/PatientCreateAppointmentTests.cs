using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class CreateAppointmentTests
    {
        [Fact]
        public async Task TestCreateAppointment()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser(person: person);
            var patient = TestDataFactory.CreatePatient(person: person);
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await patientRepository.AddAsync(patient);

            var command = new CreateAppointmentCommand(user.Id, workSchedule.Id, new TimeOnly(10, 0), "Reason", false);
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(workSchedule.Appointments.FirstOrDefault(a => a.Id == result.Id));

            // Same time slot, but walk-in appointment should be allowed
            var receptionist = TestDataFactory.CreateUser();
            receptionist.AssignRole(TestDataFactory.RoleSet.First(r => r.Name == "Receptionist"));
            var command2 = new CreateAppointmentCommand(receptionist.Id, workSchedule.Id, new TimeOnly(10, 0), "Reason", true, patient.Id);
            var result2 = await handler.Handle(command2, CancellationToken.None);
            Assert.Equal(receptionist.Id, workSchedule.Appointments.FirstOrDefault(a => a.Id == result2.Id)?.UpdatedByUserId);
        }

        [Fact]
        public async Task TestCreateAppointmentInNonExistentWorkSchedule()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser(person: person);
            var patient = TestDataFactory.CreatePatient(person: person);
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await patientRepository.AddAsync(patient);

            var command = new CreateAppointmentCommand(user.Id, Guid.NewGuid(), new TimeOnly(10, 0), "Reason", false);
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task TestNonExistentCreateAppointment()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser(person: person);
            var patient = TestDataFactory.CreatePatient(person: person);
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await patientRepository.AddAsync(patient);

            var command = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, new TimeOnly(10, 0), "Reason", false);
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }
    }
}
