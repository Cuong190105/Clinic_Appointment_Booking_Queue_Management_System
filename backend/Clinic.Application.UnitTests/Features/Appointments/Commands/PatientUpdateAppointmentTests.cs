using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class UpdateAppointmentTests
    {
        [Fact]
        public async Task TestUpdateAppointment()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateAppointmentCommandHandler(appointmentRepository, userRepository, workScheduleRepository, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();

            var appointment = TestDataFactory.CreateAppointment(patient: patient); // Initial version
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await userRepository.AddAsync(patient.Person.User);
            await appointmentRepository.AddAsync(appointment);

            var command = new UpdateAppointmentCommand(patient.Person.User.Id, appointment.Id, workSchedule.Id, new TimeOnly(10, 0), "Reason"); // 1st update
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(workSchedule.Appointments.FirstOrDefault(a => a.Id == result.Id));

            var receptionist = TestDataFactory.CreateUser();
            await userRepository.AddAsync(receptionist);
            receptionist.AssignRole(TestDataFactory.RoleSet.First(r => r.Name == "Receptionist"));
            var command2 = new UpdateAppointmentCommand(receptionist.Id, appointment.Id, workSchedule.Id, new TimeOnly(10, 30), "Reason"); // 2nd update by receptionist
            var result2 = await handler.Handle(command2, CancellationToken.None);
            Assert.Equal(receptionist.Id, appointment.UpdatedByUserId);
            Assert.Equal(2, appointment.Snapshots.Count); // => 2 snapshots created
        }

        [Fact]
        public async Task TestUpdateAppointmentInNonExistentWorkSchedule()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateAppointmentCommandHandler(appointmentRepository, userRepository, workScheduleRepository, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient: patient);
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await userRepository.AddAsync(patient.Person.User);
            await appointmentRepository.AddAsync(appointment);

            var command = new UpdateAppointmentCommand(patient.Person.User.Id, appointment.Id, Guid.NewGuid(), new TimeOnly(10, 0), "Reason");
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task TestNonExistentUpdateAppointment()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateAppointmentCommandHandler(appointmentRepository, userRepository, workScheduleRepository, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient: patient);
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await userRepository.AddAsync(patient.Person.User);
            await appointmentRepository.AddAsync(appointment);

            var command = new UpdateAppointmentCommand(Guid.NewGuid(), appointment.Id, workSchedule.Id, new TimeOnly(10, 0), "Reason");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task TestUpdateNonExistentAppointment()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateAppointmentCommandHandler(appointmentRepository, userRepository, workScheduleRepository, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient: patient);
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await userRepository.AddAsync(patient.Person.User);
            await appointmentRepository.AddAsync(appointment);

            var command = new UpdateAppointmentCommand(patient.Person.User.Id, Guid.NewGuid(), workSchedule.Id, new TimeOnly(10, 0), "Reason");
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task TestUnauthorizedUpdateAppointment()
        {
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateAppointmentCommandHandler(appointmentRepository, userRepository, workScheduleRepository, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var patient2 = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient: patient);
            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await userRepository.AddAsync(patient.Person.User);
            await userRepository.AddAsync(patient2.Person.User);
            await userRepository.AddAsync(admin.Person.User);
            await appointmentRepository.AddAsync(appointment);

            var command = new UpdateAppointmentCommand(Guid.NewGuid(), appointment.Id, workSchedule.Id, new TimeOnly(10, 0), "Reason");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(command, CancellationToken.None));

            var command2 = new UpdateAppointmentCommand(admin.Person.User.Id, appointment.Id, workSchedule.Id, new TimeOnly(10, 0), "Reason");
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(command2, CancellationToken.None));
        }
    }
}
