using Clinic.Domain.Entities;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class UserTests
    {
        [Fact]
        public void TestUserCreationValid()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);

            Assert.Equal("username", user.Username);
            Assert.Equal("hashedpassword", user.PasswordHash);
            Assert.Equal(person.Id, user.PersonId);
            Assert.True(user.IsActive);
        }

        [Fact]
        public void TestUserCreationBlankUsername()
        {
            var person = TestDataFactory.CreatePerson();
            Assert.Throws<ArgumentException>(() => new User("", "hashedpassword", person));
            Assert.Throws<ArgumentException>(() => new User("   ", "hashedpassword", person));
            Assert.Throws<ArgumentException>(() => new User(null!, "hashedpassword", person));
        }

        [Fact]
        public void TestUserCreationBlankPassword()
        {
            var person = TestDataFactory.CreatePerson();
            Assert.Throws<ArgumentException>(() => new User("username", "", person));
            Assert.Throws<ArgumentException>(() => new User("username", "   ", person));
            Assert.Throws<ArgumentException>(() => new User("username", null!, person));
        }

        [Fact]
        public void TestUserCreationNullPerson()
        {
            Assert.Throws<ArgumentNullException>(() => new User("username", "hashedpassword", null!));
        }

        [Fact]
        public void TestUpdatePasswordValid()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            user.UpdatePassword("newhashedpassword");
            Assert.Equal("newhashedpassword", user.PasswordHash);
        }

        [Fact]
        public void TestUpdatePasswordBlank()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            Assert.Throws<ArgumentException>(() => user.UpdatePassword(""));
            Assert.Throws<ArgumentException>(() => user.UpdatePassword("   "));
            Assert.Throws<ArgumentException>(() => user.UpdatePassword(null!));
        }

        [Fact]
        public void TestChangeStatus()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            user.ChangeStatus(false);
            Assert.False(user.IsActive);
            user.ChangeStatus(true);
            Assert.True(user.IsActive);
        }

        [Fact]
        public void TestAssignRole()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            var role = TestDataFactory.RoleSet.First();
            user.AssignRole(role);
            Assert.Contains(user.UserRoles, ur => ur.RoleId == role.Id);
        }

        [Fact]
        public void TestAssignRoleNull()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            Assert.Throws<ArgumentNullException>(() => user.AssignRole(null!));
        }

        [Fact]
        public void TestAssignRoleDuplicate()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            var role = TestDataFactory.RoleSet.First();
            user.AssignRole(role);
            Assert.Throws<ArgumentException>(() => user.AssignRole(role));
        }

        [Fact]
        public void TestRemoveRole()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            var role = TestDataFactory.RoleSet.First();
            user.AssignRole(role);
            user.RemoveRole(role);
            Assert.DoesNotContain(user.UserRoles, ur => ur.RoleId == role.Id);
        }

        [Fact]
        public void TestRemoveRoleNull()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            Assert.Throws<ArgumentNullException>(() => user.RemoveRole(null!));
        }

        [Fact]
        public void TestRemoveRoleNotAssigned()
        {
            var person = TestDataFactory.CreatePerson();
            var user = new User("username", "hashedpassword", person);
            var role = TestDataFactory.RoleSet.First();
            Assert.Throws<ArgumentException>(() => user.RemoveRole(role));
        }
    }
}
