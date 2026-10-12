using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.UnitTests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Appointments.Queries
{
    public class GetAppointmentByIdTests
    {
        [Fact]
        public async Task TestGetAppointmentValid()
        {
            // Arrange
            var fakeAppointmentRepository = new FakeAppointmentRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetAppointmentByIdQueryHandler(fakeAppointmentRepository, userRepository);
            var patient1 = TestDataFactory.CreatePatient();
            var receptionist = TestDataFactory.CreateEmployee(role: "Receptionist");
            var doctor1 = TestDataFactory.CreateDoctor();
            var workSchedule1 = TestDataFactory.CreateWorkSchedule(doctor1);
            var appointment = TestDataFactory.CreateAppointment(patient1, workSchedule1);
            await fakeAppointmentRepository.AddAsync(appointment);
            await userRepository.AddAsync(receptionist.Person.User);
            await userRepository.AddAsync(doctor1.Employee.Person.User);
            await userRepository.AddAsync(patient1.Person.User);

            var result = await handler.Handle(new GetAppointmentByIdQuery(appointment.Id, receptionist.Person.User.Id), CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(appointment.Id, result.Id);

            var result2 = await handler.Handle(new GetAppointmentByIdQuery(appointment.Id, doctor1.Employee.Person.User.Id), CancellationToken.None);
            Assert.NotNull(result2);
            Assert.Equal(appointment.Id, result2.Id);

            var result3 = await handler.Handle(new GetAppointmentByIdQuery(appointment.Id, patient1.Person.User.Id), CancellationToken.None);
            Assert.NotNull(result3);
            Assert.Equal(appointment.Id, result3.Id);
        }

        [Fact]
        public async Task TestGetAppointmentInvalid()
        {
            // Arrange
            var fakeAppointmentRepository = new FakeAppointmentRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetAppointmentByIdQueryHandler(fakeAppointmentRepository, userRepository);
            var patient1 = TestDataFactory.CreatePatient();
            var patient2 = TestDataFactory.CreatePatient();
            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            var doctor1 = TestDataFactory.CreateDoctor();
            var doctor2 = TestDataFactory.CreateDoctor();
            var workSchedule1 = TestDataFactory.CreateWorkSchedule(doctor1);
            var appointment = TestDataFactory.CreateAppointment(patient1, workSchedule1);
            await fakeAppointmentRepository.AddAsync(appointment);
            await userRepository.AddAsync(admin.Person.User);
            await userRepository.AddAsync(doctor1.Employee.Person.User);
            await userRepository.AddAsync(doctor2.Employee.Person.User);
            await userRepository.AddAsync(patient1.Person.User);
            await userRepository.AddAsync(patient2.Person.User);

            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(new GetAppointmentByIdQuery(appointment.Id, doctor2.Employee.Person.User.Id), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(new GetAppointmentByIdQuery(appointment.Id, patient2.Person.User.Id), CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(new GetAppointmentByIdQuery(appointment.Id, Guid.NewGuid()), CancellationToken.None));

        }
    }
}
