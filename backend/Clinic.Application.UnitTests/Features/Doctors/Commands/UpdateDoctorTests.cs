using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Doctors.Commands
{
    public class UpdateDoctorTests
    {
        [Fact]
        public async Task UpdateDoctor_ShouldUpdateDoctorInfo()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var unitOfWork = new FakeUnitOfWork();
            var personRepo = new FakePersonRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new UpdateDoctorCommandHandler(doctorRepository, personRepo, currentUser, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            await doctorRepository.AddAsync(doctor);
            currentUser.UserId = admin.Person.IdentityUserId!.Value;
            currentUser.GrantRole("Admin");
            var command = new UpdateDoctorCommand(
                admin.Person.IdentityUserId!.Value,
                "Updated Name",
                "1234567890",
                "abc@gmail.com",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Female,
                "AB C123 st",
                "ABC456",
                "MD",
                10,
                "Updated biography",
                doctor.Id
            );
            await handler.Handle(command, CancellationToken.None);
            Assert.Equal("Updated biography", doctor.Biography);
            Assert.Equal("Updated Name", doctor.Employee.Person.FullName);
            
            // self update
            currentUser.UserId = doctor.Employee.Person.IdentityUserId!.Value;
            currentUser.DoctorId = doctor.Id;
            currentUser.GrantRole("Doctor");
            var command2 = new UpdateDoctorCommand(
                doctor.Employee.Person.IdentityUserId!.Value,
                "Updated Name",
                "1234567890",
                "abc@gmail.com",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Female,
                "AB C123 st",
                "ABC456",
                "MD",
                15,
                "Updated biography"
            );
            await handler.Handle(command2, CancellationToken.None);
            Assert.Equal(15, doctor.ExperienceYears);
        }

        [Fact]
        public async Task UpdateDoctor_ShouldThrowException_WhenDoctorNotFound()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var unitOfWork = new FakeUnitOfWork();
            var personRepo = new FakePersonRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new UpdateDoctorCommandHandler(doctorRepository, personRepo, currentUser, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            await doctorRepository.AddAsync(doctor);
            currentUser.UserId = admin.Person.IdentityUserId!.Value;
            currentUser.GrantRole("Admin");
            var command = new UpdateDoctorCommand(
                admin.Person.IdentityUserId!.Value,
                "Updated Name",
                "1234567890",
                "abc@gmail.com",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Female,
                "AB C123 st",
                "ABC456",
                "MD",
                10,
                "Updated biography",
                Guid.NewGuid()
            );
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));

            // self update, admin not a doctor
            var command2 = new UpdateDoctorCommand(
                admin.Person.IdentityUserId!.Value,
                "Updated Name",
                "1234567890",
                "abc@gmail.com",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Female,
                "AB C123 st",
                "ABC456",
                "MD",
                10,
                "Updated biography"
            );
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command2, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateDoctor_ShouldThrowException_WhenUserNotAuthorized()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var unitOfWork = new FakeUnitOfWork();
            var personRepo = new FakePersonRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new UpdateDoctorCommandHandler(doctorRepository, personRepo, currentUser, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            await doctorRepository.AddAsync(doctor);
            var command = new UpdateDoctorCommand(
                null,
                "Updated Name",
                "1234567890",
                "abc@gmail.com",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Female,
                "AB C123 st",
                "ABC456",
                "MD",
                10,
                "Updated biography"
            );
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(command, CancellationToken.None));
        }
    }
}

