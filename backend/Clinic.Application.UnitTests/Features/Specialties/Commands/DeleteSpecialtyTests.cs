using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Specialties.Commands;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Specialties.Commands
{
    public class DeleteSpecialtyTests
    {
        [Fact]
        public async Task TestDeleteValidSpecialty()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new DeleteSpecialtyCommandHandler(specialtyRepository, unitOfWork);

            var specialty = new Domain.Entities.Specialty("Cardiology", "Heart related specialty", new DateOnly(2000, 1, 1));
            await specialtyRepository.AddAsync(specialty);
            await handler.Handle(new DeleteSpecialtyCommand(specialty.Id), CancellationToken.None);
            Assert.True(specialty.IsDeleted);
            Assert.Null(await specialtyRepository.GetByIdAsync(specialty.Id));
        }

        [Fact]
        public void TestDeleteInvalidSpecialty()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new DeleteSpecialtyCommandHandler(specialtyRepository, unitOfWork);
            var invalidId = Guid.NewGuid();
            var exception = Assert.ThrowsAsync<NotFoundException>(async () =>
                await handler.Handle(new DeleteSpecialtyCommand(invalidId), CancellationToken.None));
            Assert.Equal("Specialty not found.", exception.Result.Message);
        }
    }
}
