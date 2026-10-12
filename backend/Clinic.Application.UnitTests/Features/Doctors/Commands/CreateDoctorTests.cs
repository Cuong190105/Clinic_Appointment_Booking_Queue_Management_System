using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Doctors.Commands
{
    public class CreateDoctorTests
    {
        [Fact]
        public async Task TestCreateDoctorCommand()
        {
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var roleRepository = new FakeRoleRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var unitOfWork = new FakeUnitOfWork();
            var passwordHasher = new FakePasswordHasher();
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var userService = new UserService(userRepository, personRepository, passwordHasher);
            var personService = new PersonService(personRepository);
            var handler = new CreateDoctorCommandHandler(userService, personService, doctorRepository, specialtyRepository, roleRepository, employeeRepository, unitOfWork);

            var specialty = TestDataFactory.CreateSpecialty();
            await specialtyRepository.AddAsync(specialty);
            var command = new CreateDoctorCommand
            (
                Username: "johndoe",
                Password: "SecurePassword123!",
                FullName: "Dr. John Doe",
                PhoneNumber: "1234567890",
                Email: "john.doe@example.com",
                DateOfBirth: new DateOnly(1980, 1, 1),
                Gender: Gender.Male,
                Address: "123 Main St",
                HireDate: new DateOnly(2020, 1, 1),
                LicenseNumber: "LIC123456",
                Qualification: "MD",
                Biography: "Experienced doctor in internal medicine.",
                Status: DoctorStatus.Active,
                ExperienceYears: 10,
                SpecialtyId: specialty.Id
            );
            var summary = await handler.Handle(command, CancellationToken.None);
            var createdDoctor = await doctorRepository.GetInfoByIdAsync(summary.Id);
            Assert.NotNull(createdDoctor);
        }

        [Fact]
        public async Task TestCreateDoctorCommandWithInvalidSpecialty()
        {
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var roleRepository = new FakeRoleRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var unitOfWork = new FakeUnitOfWork();
            var passwordHasher = new FakePasswordHasher();
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var userService = new UserService(userRepository, personRepository, passwordHasher);
            var personService = new PersonService(personRepository);
            var handler = new CreateDoctorCommandHandler(userService, personService, doctorRepository, specialtyRepository, roleRepository, employeeRepository, unitOfWork);
            var command = new CreateDoctorCommand
            (
                Username: "johndoe",
                Password: "SecurePassword123!",
                FullName: "Dr. John Doe",
                PhoneNumber: "1234567890",
                Email: "john.doe@example.com",
                DateOfBirth: new DateOnly(1980, 1, 1),
                Gender: Gender.Male,
                Address: "123 Main St",
                HireDate: new DateOnly(2020, 1, 1),
                LicenseNumber: "LIC123456",
                Status: DoctorStatus.Active,
                Qualification: "MD",
                Biography: "Experienced doctor in internal medicine.",
                ExperienceYears: 10,
                SpecialtyId: Guid.NewGuid()
            );
            await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(command, CancellationToken.None));
        }
    }
}
