using Clinic.Application.Features.Specialties.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Features.Specialties.Commands
{
    public class CreateSpecialtyTests
    {
        [Fact]
        public async Task Handle_ValidRequest_ShouldCreateSpecialty()
        {
            // Arrange
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            var command = new CreateSpecialtyCommand("Cardiology", "Heart related specialty", new DateOnly(2020, 1, 1));
            // Act
            var result = await handler.Handle(command, CancellationToken.None);
            // Assert
            Assert.NotNull(result);
            Assert.Equal(command.Name, result.Name);
            Assert.Equal(command.Description, result.Description);
            Assert.Equal(command.EstablishedDate, result.EstablishedDate);
            Assert.NotNull(await specialtyRepository.GetByIdAsync(result.Id));
        }

        [Fact]
        public async Task Handle_EmptyName_ShouldThrowArgumentException()
        {
            // Arrange
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            var command = new CreateSpecialtyCommand("", "Heart related specialty", new DateOnly(2020, 1, 1));
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DuplicateName_ShouldThrowArgumentException()
        {
            // Arrange
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            await specialtyRepository.AddAsync(new Specialty("Cardiology", "Heart related specialty", new DateOnly(2020, 1, 1)));

            var command2 = new CreateSpecialtyCommand("Cardiology", "Another description", new DateOnly(2021, 1, 1));
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command2, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InvalidEstablishedDate_ShouldThrowArgumentException()
        {
            // Arrange
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            var command = new CreateSpecialtyCommand("Cardiology", "Heart related specialty", DateOnly.FromDateTime(DateTime.Now.AddDays(1))); // Future date
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
