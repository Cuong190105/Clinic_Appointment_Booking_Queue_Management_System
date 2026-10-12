using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Doctors.Queries
{
    public class GetDoctorsForBookingTests
    {
        [Fact]
        public async Task TestGetDoctorsForBooking()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorsForBookingQueryHandler(doctorRepository);
            var specialty1 = TestDataFactory.CreateSpecialty("Cardiology", "Heart specialist");
            var specialty2 = TestDataFactory.CreateSpecialty("Dermatology", "Skin specialist");
            var doctor1 = TestDataFactory.CreateDoctor(specialty: specialty1);
            var doctor2 = TestDataFactory.CreateDoctor(specialty: specialty2);
            await doctorRepository.AddAsync(doctor1);
            await doctorRepository.AddAsync(doctor2);
            var query = new GetDoctorsForBookingQuery();
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.Count);

            var queryWithSpecialty = new GetDoctorsForBookingQuery { SpecialtyId = specialty1.Id };
            var resultWithSpecialty = await handler.Handle(queryWithSpecialty, CancellationToken.None);
            Assert.Single(resultWithSpecialty);

            doctor1.UpdateStatus(DoctorStatus.Inactive);
            var resultWithInactiveDoctor = await handler.Handle(queryWithSpecialty, CancellationToken.None);
            Assert.Empty(resultWithInactiveDoctor);
        }
    }
}
