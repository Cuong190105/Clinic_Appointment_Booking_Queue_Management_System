using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Specialties.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Specialties.Queries
{
    public class SpecialtyQueriesTests
    {
        [Fact]
        public async Task GetSpecialtyById_Exists_ShouldReturnDto()
        {
            // Arrange
            var specialtyRepository = new FakeSpecialtyRepository();
            var handler = new GetSpecialtyQueryHandler(specialtyRepository);

            var specialty = TestDataFactory.CreateSpecialty("Dermatology", "Skin care");
            await specialtyRepository.AddAsync(specialty);

            // Act
            var result = await handler.Handle(new GetSpecialtyQuery(specialty.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(specialty.Id, result.Id);
            Assert.Equal("Dermatology", result.Name);
            Assert.Equal("Skin care", result.Description);
        }

        [Fact]
        public async Task GetSpecialtyById_NotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var specialtyRepository = new FakeSpecialtyRepository();
            var handler = new GetSpecialtyQueryHandler(specialtyRepository);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new GetSpecialtyQuery(Guid.NewGuid()), CancellationToken.None));
        }
    }
}
