using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class PersonTests
    {
        [Fact]
        public void TestPersonCreationValid()
        {
            var person = new Person("Hi Low", "1234567890", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.Equal("Hi Low", person.FullName);
            Assert.Equal("1234567890", person.PhoneNumber);
            Assert.Equal("main@example.com", person.Email);
            Assert.Equal(new DateOnly(1990, 1, 1), person.DateOfBirth);
            Assert.Equal(Gender.Male, person.Gender);
            Assert.Equal("123 Main St", person.Address);
            Assert.False(person.IsDeleted);
        }

        [Fact]
        public void TestPersonCreattionBlankAddressValid()
        {
            var person = new Person("Hi Low", "1234567890", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "");
            Assert.Equal("Hi Low", person.FullName);
            Assert.Equal("1234567890", person.PhoneNumber);
            Assert.Equal("main@example.com", person.Email);
            Assert.Equal(new DateOnly(1990, 1, 1), person.DateOfBirth);
            Assert.Equal(Gender.Male, person.Gender);
            Assert.Equal("", person.Address);
            Assert.False(person.IsDeleted);
        }

        [Fact]
        public void TestPersonCreationBlankEmailValid()
        {
            var person = new Person("Hi Low", "1234567890", "", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.False(person.IsDeleted);

            person = new Person("Hi Low", "1234567890", "          ", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.False(person.IsDeleted);

            person = new Person("Hi Low", "1234567890", null!, new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.False(person.IsDeleted);
        }

        [Fact]
        public void TestPersonCreationBlankPhoneNumberValid()
        {
            var person = new Person("Hi Low", "", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.False(person.IsDeleted);

            person = new Person("Hi Low", "          ", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.False(person.IsDeleted);

            person = new Person("Hi Low", null!, "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St");
            Assert.False(person.IsDeleted);
        }

        [Fact]
        public void TestPersonCreationBlankFullNameThrows()
        {
            Assert.Throws<ArgumentException>(() => new Person("", "1234567890", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St"));
            Assert.Throws<ArgumentException>(() => new Person("         ", "1234567890", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St"));
            Assert.Throws<ArgumentException>(() => new Person(null!, "1234567890", "main@example.com", new DateOnly(1990, 1, 1), Gender.Male, "123 Main St"));
        }

        [Fact]
        public void TestInvalidPhoneNumberThrows()
        {
            Assert.Throws<ArgumentException>(() => new Person("Hi Low", "abc123", "", new DateOnly(1990, 1, 1), Gender.Male, null!));
        }

        [Fact]
        public void TestFutureDateOfBirthThrows()
        {
            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
            Assert.Throws<ArgumentException>(() => new Person("Hi Low", "1234567890", "main@example.com", futureDate, Gender.Male, "123 Main St"));
        }

        [Fact]
        public void TestUpdateDetailsValid()
        {
            var person = TestDataFactory.CreatePerson();
            var name = person.FullName;
            person.UpdateDetails("", Gender.Male, "123 Main St");
            Assert.Equal("", person.Email);
            Assert.Equal(Gender.Male, person.Gender);
            Assert.Equal("123 Main St", person.Address);
            Assert.Equal(name, person.FullName); 
        }

        [Fact]
        public void TestAdvancedUpdateDetailsValid()
        {
            var person = TestDataFactory.CreatePerson();
            person.UpdateAdvancedDetails("New Name", "0999999999", "Email@gmai", Gender.Female, new DateOnly(2000, 1, 1), "ABCDEF");
            Assert.Equal("New Name", person.FullName);
            Assert.Equal("0999999999", person.PhoneNumber);
        }

        [Fact]
        public void TestAdvancedUpdateDetailsInvalid()
        {
            var person = TestDataFactory.CreatePerson();
            // Null or empty name
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails(null!, "0999", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("", "0999", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("  ", "0999", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            // Null or empty phone
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full Name", null!, "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full Name", "", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full Name", "  ", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            // Null or empty email
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full name", "0999", null!, Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full name", "0999", "", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full name", "0999", "  ", Gender.Male, new DateOnly(1990, 1, 1), "ABCDEF"));
            // Null or empty address
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full Name", "0999", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), null!));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full Name", "0999", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), ""));
            Assert.Throws<ArgumentException>(() => person.UpdateAdvancedDetails("Full Name", "0999", "Email@g", Gender.Male, new DateOnly(1990, 1, 1), "       "));
        }

        [Fact]
        public void TestDeletePerson()
        {
            var person = TestDataFactory.CreatePerson();
            Assert.False(person.IsDeleted);
            person.Delete();
            Assert.True(person.IsDeleted);
        }
    }
}
