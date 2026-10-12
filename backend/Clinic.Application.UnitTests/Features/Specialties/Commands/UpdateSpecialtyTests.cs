using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Specialties.Commands;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Specialties.Commands
{
    public class UpdateSpecialtyTests
    {
        [Fact]
        public async Task TestUpdateSpecialtyValid()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateSpecialtyCommandHandler(specialtyRepository, unitOfWork);

            var specialty = new Domain.Entities.Specialty("Cardiology", "Heart related specialty", new DateOnly(2000, 1, 1));
            await specialtyRepository.AddAsync(specialty);
            await handler.Handle(new UpdateSpecialtyCommand(specialty.Id, "Neurology", "Brain related specialty", new DateOnly(2005, 1, 1)), CancellationToken.None);

            Assert.Equal("Neurology", specialty.Name);
            Assert.Equal("Brain related specialty", specialty.Description);
            Assert.Equal(new DateOnly(2005, 1, 1), specialty.EstablishedDate);
        }

        [Fact]
        public async Task TestUpdateSpecialtyInvalidName()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            var specialty = new Domain.Entities.Specialty("Cardiology", "Heart related specialty", new DateOnly(2000, 1, 1));
            await specialtyRepository.AddAsync(specialty);
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await handler.Handle(new UpdateSpecialtyCommand(specialty.Id, "", "Brain related specialty", new DateOnly(2005, 1, 1)), CancellationToken.None));
        }

        [Fact]
        public async Task TestUpdateDuplicatedName()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            var specialty1 = new Domain.Entities.Specialty("Cardiology", "Heart related specialty", new DateOnly(2000, 1, 1));
            var specialty2 = new Domain.Entities.Specialty("Neurology", "Brain related specialty", new DateOnly(2005, 1, 1));
            await specialtyRepository.AddAsync(specialty1);
            await specialtyRepository.AddAsync(specialty2);
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await handler.Handle(new UpdateSpecialtyCommand(specialty1.Id, "Neurology", "Updated description", new DateOnly(2005, 1, 1)), CancellationToken.None));

            // Duplicate name with itself
            await handler.Handle(new UpdateSpecialtyCommand(specialty1.Id, "Cardiology", "Updated description", new DateOnly(2005, 1, 1)), CancellationToken.None);
            Assert.Equal("Updated description", specialty1.Description);

            // Duplicate name with deleted entity
            specialty2.Delete();
            await handler.Handle(new UpdateSpecialtyCommand(specialty1.Id, "Neurology", "Updated description", new DateOnly(2005, 1, 1)), CancellationToken.None);
            Assert.Equal("Neurology", specialty1.Name);
        }

        [Fact]
        public async Task TestUpdateSpecialtyNotFound()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await handler.Handle(new UpdateSpecialtyCommand(Guid.NewGuid(), "Neurology", "Brain related specialty", new DateOnly(2005, 1, 1)), CancellationToken.None));
        }
    }
}
