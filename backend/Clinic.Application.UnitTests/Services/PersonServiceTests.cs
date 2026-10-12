using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Services
{
    public class PersonServiceTests
    {
        [Fact]
        public async Task TestPersonCreation()
        {
            var personRepository = new FakePersonRepository();
            var personService = new PersonService(personRepository);
            var person = await personService.CreateOrGetPersonAsync("Full Name", "0123456789", "full.name@example.com", new DateOnly(2000, 1, 1), Domain.Enums.Gender.Female, "123 Abc St.");
            Assert.NotNull(person);
            Assert.Equal("Full Name", person.FullName);

            var addedPerson = await personRepository.GetByPhoneNumberAsync("0123456789");
            Assert.NotNull(addedPerson);
            Assert.Equal(person.Id, addedPerson.Id);
        }

        [Fact]
        public async Task TestPersonCreationDuplicatePhone()
        {
            var personRepository = new FakePersonRepository();
            var personService = new PersonService(personRepository);
            var person1 = TestDataFactory.CreatePerson("Full Name", "0123456789", "full.name1@example.com", "123 Abc St.");
            await personRepository.AddAsync(person1);
            var id = person1.Id;
            var person2 = await personService.CreateOrGetPersonAsync("Full Name", "0123456789", "full.name2@example.com", new DateOnly(2000, 1, 1), Domain.Enums.Gender.Female, "456 Def St.");
            Assert.NotNull(person2);
            Assert.Equal(id, person2.Id);
            Assert.Equal("full.name2@example.com", person2.Email);
        }

        [Fact]
        public async Task TestPersonCreationNoPhone()
        {
            var personRepository = new FakePersonRepository();
            var personService = new PersonService(personRepository);
            var person1 = TestDataFactory.CreatePerson("Full Name", "0123456789", "full.name1@example.com", "123 Abc St.");
            await personRepository.AddAsync(person1);

            // Creating new person with no phone number should succeed
            var person2 = await personService.CreateOrGetPersonAsync("Full Name", null, "full.name2@example.com", new DateOnly(2000, 1, 1), Domain.Enums.Gender.Female, "456 Def St.");
            Assert.NotNull(person2);
            Assert.NotEqual(person1.Id, person2.Id);
            Assert.NotNull(await personRepository.GetByIdAsync(person2.Id));

            // Creating new person with no phone number and same basic info should return the existing person
            var remakePerson2 = await personService.CreateOrGetPersonAsync("Full Name", null, "full.name3@example.com", new DateOnly(2000, 1, 1), Domain.Enums.Gender.Female, "789 Ghi St.");
            Assert.NotNull(remakePerson2);
            Assert.Equal(person2.Id, remakePerson2.Id);
        }
    }
}
