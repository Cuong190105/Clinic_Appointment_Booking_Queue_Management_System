using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class CancelAppointmentTests2
    {
        [Fact]
        public async Task TestCancelAppointment()
        {
            var appointmentRepository = new FakeAppointmentRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient, workSchedule);
            var appointment2 = TestDataFactory.CreateAppointment(patient, workSchedule, workSchedule.ShiftStart.AddMinutes(30));
            await appointmentRepository.AddAsync(appointment);
            await appointmentRepository.AddAsync(appointment2);

            currentUser.UserId = patient.Person.User.Id;
            currentUser.PatientId = patient.Id;
            // Patient cancels the appointment themselves
            var command = new CancelAppointmentCommand(AppointmentId: appointment.Id);
            await handler.Handle(command, CancellationToken.None);
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
            Assert.Equal(patient.Person.User.Id, appointment.UpdatedByUserId);

            // Receptionist cancels the appointment on behalf of the patient
            var receptionist = TestDataFactory.CreateEmployee(role: "Receptionist");
            currentUser.UserId = receptionist.Person.User.Id;
            currentUser.GrantPermission("appointment.edit.any");
            var command2 = new CancelAppointmentCommand(AppointmentId: appointment2.Id);
            await handler.Handle(command2, CancellationToken.None);
            Assert.Equal(AppointmentStatus.Cancelled, appointment2.Status);
            Assert.Equal(receptionist.Person.User.Id, appointment2.UpdatedByUserId);
        }

        [Fact]
        public async Task TestCancelNonExistentAppointment()
        {
            var appointmentRepository = new FakeAppointmentRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var receptionist = TestDataFactory.CreateEmployee(role: "Receptionist");
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient, workSchedule);
            currentUser.UserId = patient.Person.User.Id;
            await appointmentRepository.AddAsync(appointment);
            var command = new CancelAppointmentCommand(AppointmentId: Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task TestNonExistentCancelAppointment()
        {
            var appointmentRepository = new FakeAppointmentRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            var appointment = TestDataFactory.CreateAppointment(patient, workSchedule);
            await appointmentRepository.AddAsync(appointment);
            var command = new CancelAppointmentCommand(AppointmentId: appointment.Id);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(command, CancellationToken.None));

            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            currentUser.UserId = admin.Person.User.Id;
            currentUser.GrantPermission("appointment.edit.own");
            var command2 = new CancelAppointmentCommand(AppointmentId: appointment.Id);
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(command2, CancellationToken.None));
        }
    }
}
