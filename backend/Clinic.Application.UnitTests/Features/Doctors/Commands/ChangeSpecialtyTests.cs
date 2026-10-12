using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.UnitTests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Doctors.Commands
{
    public class ChangeSpecialtyTests
    {
        [Fact]
        public async Task ChangeSpecialty_ShouldChangeDoctorSpecialty()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new ChangeSpecialtyCommandHandler(doctorRepository, specialtyRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);
            var newSpecialty = TestDataFactory.CreateSpecialty(name: "New name123");
            await specialtyRepository.AddAsync(newSpecialty);
            var command = new ChangeSpecialtyCommand(doctor.Id, newSpecialty.Id);
            await handler.Handle(command, CancellationToken.None);
            var queriedSpecialty = doctor.WorkHistories.FirstOrDefault(wh => wh.Specialty.Id == newSpecialty.Id)?.Specialty;
            Assert.NotNull(queriedSpecialty);
            Assert.Equal(2, doctor.WorkHistories.Count);
        }

        [Fact]
        public async Task ChangeSpecialty_ShouldThrowException_WhenDoctorNotFound()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new ChangeSpecialtyCommandHandler(doctorRepository, specialtyRepository, unitOfWork);
            var newSpecialty = TestDataFactory.CreateSpecialty(name: "New name123");
            await specialtyRepository.AddAsync(newSpecialty);
            var command = new ChangeSpecialtyCommand(Guid.NewGuid(), newSpecialty.Id);
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task ChangeSpecialty_ShouldThrowException_WhenSpecialtyNotFound()
        {
            // Arrange
            var doctorRepository = new FakeDoctorRepository();
            var specialtyRepository = new FakeSpecialtyRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new ChangeSpecialtyCommandHandler(doctorRepository, specialtyRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);
            var command = new ChangeSpecialtyCommand(doctor.Id, Guid.NewGuid());
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
