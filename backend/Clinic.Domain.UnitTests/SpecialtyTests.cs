using Clinic.Domain.Entities;

namespace Clinic.Domain.UnitTests
{
    public class SpecialtyTests
    {
        [Fact]
        public void CreateSpecialty_ValidParameters()
        {
            // Arrange
            var name = "Cardiology";
            var description = "Heart specialist";
            var establishedDate = DateOnly.FromDateTime(DateTime.UtcNow);
            // Act
            var specialty = new Specialty(name, description, establishedDate);
            // Assert
            Assert.Equal(name, specialty.Name);
            Assert.Equal(description, specialty.Description);
        }

        [Fact]
        public void CreateSpecialty_InvalidName_ThrowsArgumentException()
        {
            // Arrange
            var name = "";
            var description = "Heart specialist";
            var establishedDate = DateOnly.FromDateTime(DateTime.UtcNow);
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new Specialty(name, description, establishedDate));
        }

        [Fact]
        public void UpdateSpecialty_ValidParameters()
        {
            // Arrange
            var specialty = new Specialty("Cardiology", "Heart specialist", DateOnly.FromDateTime(DateTime.UtcNow));
            var newName = "Neurology";
            var newDescription = "Brain specialist";
            var newEstablishedDate = DateOnly.FromDateTime(DateTime.UtcNow);
            // Act
            specialty.Update(newName, newDescription, newEstablishedDate);
            // Assert
            Assert.Equal(newName, specialty.Name);
            Assert.Equal(newDescription, specialty.Description);
        }

        [Fact]
        public void UpdateSpecialty_InvalidName_ThrowsArgumentException()
        {
            // Arrange
            var specialty = new Specialty("Cardiology", "Heart specialist", DateOnly.FromDateTime(DateTime.UtcNow));
            var newName = "";
            var newDescription = "Brain specialist";
            var newEstablishedDate = DateOnly.FromDateTime(DateTime.UtcNow);
            // Act & Assert
            Assert.Throws<ArgumentException>(() => specialty.Update(newName, newDescription, newEstablishedDate));
        }

        [Fact]
        public void DeleteSpecialty_SetsIsDeletedToTrue()
        {
            // Arrange
            var specialty = new Specialty("Cardiology", "Heart specialist", DateOnly.FromDateTime(DateTime.UtcNow));
            // Act
            specialty.Delete();
            // Assert
            Assert.True(specialty.IsDeleted);
        }
    }
}
