using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.UnitTests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Doctors.Queries
{
    public class GetDoctorsTests
    {
        [Fact]
        public async Task TestGetAllDoctors()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorsHandler(doctorRepository);
            var specialty = TestDataFactory.CreateSpecialty();
            var person1 = TestDataFactory.CreatePerson(fullName: "Doctor 1");
            var person2 = TestDataFactory.CreatePerson(fullName: "Doctor 2");
            var user1 = TestDataFactory.CreateUser(person: person1, role: "Doctor");
            var user2 = TestDataFactory.CreateUser(person: person2, role: "Doctor");
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            var doctor1 = TestDataFactory.CreateDoctor(employee: employee1, specialty: specialty);
            var doctor2 = TestDataFactory.CreateDoctor(employee: employee2, specialty: specialty);
            await doctorRepository.AddAsync(doctor1);
            await doctorRepository.AddAsync(doctor2);
            var query = new GetDoctorsQuery();
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public async Task TestSearchDoctors()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorsHandler(doctorRepository);
            var specialty = TestDataFactory.CreateSpecialty();
            var person1 = TestDataFactory.CreatePerson(fullName: "P1");
            var person2 = TestDataFactory.CreatePerson(fullName: "P2");
            var user1 = TestDataFactory.CreateUser(person: person1, role: "Doctor");
            var user2 = TestDataFactory.CreateUser(person: person2, role: "Doctor");
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            var doctor1 = TestDataFactory.CreateDoctor(employee: employee1, specialty: specialty);
            var doctor2 = TestDataFactory.CreateDoctor(employee: employee2, specialty: specialty);
            await doctorRepository.AddAsync(doctor1);
            await doctorRepository.AddAsync(doctor2);
            var query = new GetDoctorsQuery(Search: "P1");
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Single(result.Items);
            Assert.Equal(doctor1.Id, result.Items[0].Id);
        }

        [Fact]
        public async Task TestSortDoctors()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorsHandler(doctorRepository);
            var person1 = TestDataFactory.CreatePerson(fullName: "A");
            var person2 = TestDataFactory.CreatePerson(fullName: "B");
            var user1 = TestDataFactory.CreateUser(person: person1, role: "Doctor");
            var user2 = TestDataFactory.CreateUser(person: person2, role: "Doctor");
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            var doctor1 = TestDataFactory.CreateDoctor(employee: employee1);
            var doctor2 = TestDataFactory.CreateDoctor(employee: employee2);
            await doctorRepository.AddAsync(doctor1);
            await doctorRepository.AddAsync(doctor2);
            var query = new GetDoctorsQuery(SortBy: "FullName", Descending: true);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(doctor2.Id, result.Items[0].Id);
        }

        [Fact]
        public async Task TestFilterDoctors()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorsHandler(doctorRepository);
            var specialty1 = TestDataFactory.CreateSpecialty(name: "S1");
            var specialty2 = TestDataFactory.CreateSpecialty(name: "S2");
            var person1 = TestDataFactory.CreatePerson(fullName: "A");
            var person2 = TestDataFactory.CreatePerson(fullName: "B");
            var user1 = TestDataFactory.CreateUser(person: person1, role: "Doctor");
            var user2 = TestDataFactory.CreateUser(person: person2, role: "Doctor");
            var doctor1 = TestDataFactory.CreateDoctor(employee: TestDataFactory.CreateEmployee(person1), qualification: "Q");
            var doctor2 = TestDataFactory.CreateDoctor(employee: TestDataFactory.CreateEmployee(person2), qualification: "Q");
            doctor1.ChangeSpecialty(specialty1);
            doctor2.ChangeSpecialty(specialty2);
            doctor2.UpdateStatus(Domain.Enums.DoctorStatus.Inactive);
            await doctorRepository.AddAsync(doctor1);
            await doctorRepository.AddAsync(doctor2);
            var query = new GetDoctorsQuery(SpecialtyId: specialty1.Id);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Single(result.Items);
            Assert.Equal(doctor1.Id, result.Items[0].Id);

            var query2 = new GetDoctorsQuery(Status: Domain.Enums.DoctorStatus.Inactive);
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.Single(result2.Items);
            Assert.Equal(doctor2.Id, result2.Items[0].Id);

            var query3 = new GetDoctorsQuery(Qualification: "Q");
            var result3 = await handler.Handle(query3, CancellationToken.None);
            Assert.Equal(2, result3.Items.Count);
        }

        [Fact]
        public async Task TestPaginationDoctors()
        {
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorsHandler(doctorRepository);
            for (int i = 0; i < 15; i++)
            {
                var person = TestDataFactory.CreatePerson();
                var user = TestDataFactory.CreateUser(person: person, role: "Doctor");
                var doctor = TestDataFactory.CreateDoctor(employee: TestDataFactory.CreateEmployee(person: person));
                await doctorRepository.AddAsync(doctor);
            }
            var query = new GetDoctorsQuery(PageNumber: 2, PageSize: 10);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(5, result.Items.Count);
            Assert.Equal(15, result.TotalCount);
        }
    }
}
