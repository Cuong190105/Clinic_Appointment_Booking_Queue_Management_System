using Clinic.Application.Features.Specialties.Queries;
using Clinic.Application.UnitTests.Common;
using System.Runtime.InteropServices;

namespace Clinic.Application.UnitTests.Features.Specialties.Queries
{
    public class GetSpecialties
    {
        [Fact]
        public async Task TestGetSpecialties()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var handler = new GetSpecialtiesQueryHandler(specialtyRepository);


            specialtyRepository.PrepareData();
            var query = new GetSpecialtiesQuery();
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(5, result.TotalCount);
        }

        [Fact]
        public async Task TestGetSpecialtiesWithSearch()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var handler = new GetSpecialtiesQueryHandler(specialtyRepository);
            specialtyRepository.PrepareData();
            var query = new GetSpecialtiesQuery(Search: "Cardiology");
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Single(result.Items);
            Assert.Equal("Cardiology", result.Items.First().Name);

            var query2 = new GetSpecialtiesQuery(Search: "ology");
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.Equal(4, result2.TotalCount);
        }

        [Fact]
        public async Task TestGetSpecialtiesWithSorting()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var handler = new GetSpecialtiesQueryHandler(specialtyRepository);
            specialtyRepository.PrepareData();
            var query = new GetSpecialtiesQuery(SortBy: "establisheddate", Descending: true);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(5, result.TotalCount);
            Assert.Equal("Oncology", result.Items.First().Name);
            Assert.True(result.Items.First().EstablishedDate > result.Items.Last().EstablishedDate);
        }

        [Fact]
        public async Task TestGetSpecialtiesWithPagination()
        {
            var specialtyRepository = new FakeSpecialtyRepository();
            var handler = new GetSpecialtiesQueryHandler(specialtyRepository);
            specialtyRepository.PrepareData();
            var query = new GetSpecialtiesQuery(Page: 2, PageSize: 2);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.Items.Count());
            Assert.Equal("Neurology", result.Items.First().Name);

            var query2 = new GetSpecialtiesQuery(Page: 3, PageSize: 2);
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.Single(result2.Items);
        }
    }
}
