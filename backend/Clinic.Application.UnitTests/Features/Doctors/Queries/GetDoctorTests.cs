using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Doctors.Queries
{
    public class GetDoctorTests
    {
        [Fact]
        public async Task TestFindExistDoctor()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorHandler(doctorRepository);
            var doctor = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);
            var query = new GetDoctorQuery(doctor.Id);
            var result = await handler.Handle(query, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(doctor.Id, result.Id);
        }

        [Fact]
        public async Task TestFindNonExistDoctor()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorHandler(doctorRepository);
            var query = new GetDoctorQuery(Guid.NewGuid());
            var exception = Assert.ThrowsAsync<Clinic.Application.Common.Exceptions.NotFoundException>(async () =>
            {
                await handler.Handle(query, CancellationToken.None);
            });
            Assert.Equal("Doctor not found", exception.Result.Message);
        }
    }
}
