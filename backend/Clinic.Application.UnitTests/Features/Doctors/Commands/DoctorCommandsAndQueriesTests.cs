using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Doctors.Commands
{
    public class DoctorCommandsAndQueriesTests
    {
        [Fact]
        public async Task CreateDoctorFromUser_ValidUser_ShouldCreateDoctor()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();

            var handler = new CreateDoctorFromUserCommandHandler(
                userRepository, employeeRepository, doctorRepository, specialtyRepository, roleRepository, unitOfWork);

            var person = TestDataFactory.CreatePerson(email: "doctor@example.com", address: "123 Clinic St");
            var patient = TestDataFactory.CreatePatient(person: person);
            var user = TestDataFactory.CreateUser("Patient", "docuser", "hash", person);
            await userRepository.AddAsync(user);

            var specialty = TestDataFactory.CreateSpecialty();
            await specialtyRepository.AddAsync(specialty);

            var command = new CreateDoctorFromUserCommand(
                user.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "DOC-12345",
                "MD, PhD",
                "Experienced cardiologist",
                10,
                DoctorStatus.Active,
                specialty.Id
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("DOC-12345", result.LicenseNumber);
            Assert.Equal(person.FullName, result.FullName);
            var doctor = await doctorRepository.GetInfoByIdAsync(result.Id);
            Assert.NotNull(doctor);
            Assert.Equal(10, doctor.ExperienceYears);
        }

        [Fact]
        public async Task CreateDoctorFromUser_NonExistentUser_ShouldThrowArgumentException()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();

            var handler = new CreateDoctorFromUserCommandHandler(
                userRepository, employeeRepository, doctorRepository, specialtyRepository, roleRepository, unitOfWork);

            var command = new CreateDoctorFromUserCommand(
                Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow),
                "DOC-999",
                "MD",
                null,
                5,
                DoctorStatus.Active,
                Guid.NewGuid()
            );

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateDoctorStatus_ValidDoctor_ShouldUpdateStatus()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateDoctorStatusCommandHandler(doctorRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);

            var command = new UpdateDoctorStatusCommand(doctor.Id, DoctorStatus.Inactive);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(doctor.Id, result);
            Assert.Equal(DoctorStatus.Inactive, doctor.Status);
        }

        [Fact]
        public async Task UpdateDoctorStatus_NotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateDoctorStatusCommandHandler(doctorRepository, unitOfWork);

            var command = new UpdateDoctorStatusCommand(Guid.NewGuid(), DoctorStatus.Inactive);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
