using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Auth.Commands
{
    public class RegisterPatientTests
    {
        [Fact]
        public async Task TestRegisterPatient()
        {
            var userRepository = new FakeUserRepository();
            var passwordHasher = new FakePasswordHasher();
            var personRepository = new FakePersonRepository();
            var roleRepository = new FakeRoleRepository();
            var patientRepository = new FakePatientRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new RegisterCommandHandler(new UserService(userRepository, personRepository, passwordHasher), new PersonService(personRepository), roleRepository, patientRepository, unitOfWork);
            var command = new RegisterCommand(
                Username: "newpatient",
                Password: "password123",
                FullName: "John Doe",
                PhoneNumber: "1234567890",
                Email: "john.doe@example.com",
                DateOfBirth: new DateOnly(1990, 1, 1),
                Address: "123 Main St",
                Gender: Gender.Male
            );
            var result = await handler.Handle(command, CancellationToken.None);
            var createdUser = await userRepository.GetByUsernameAsync("newpatient");
            var createdPerson = await personRepository.GetByEmailAsync("john.doe@example.com");
            Assert.NotNull(createdPerson);
            Assert.NotNull(createdUser);
            Assert.Equal(createdPerson.Id, createdUser.PersonId);
            await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(command, CancellationToken.None));
        }
    }
}