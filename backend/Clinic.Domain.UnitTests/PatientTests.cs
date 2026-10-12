using Clinic.Domain.Entities;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class PatientTests
    {
        [Fact]
        public void CreatePatient_WithValidPerson_ShouldSetProperties()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson(fullName: "Alice Smith");

            // Act
            var patient = new Patient(person, "INS-987654321", "0987654321");

            // Assert
            Assert.Equal(person.Id, patient.PersonId);
            Assert.Equal(person, patient.Person);
            Assert.Equal("INS-987654321", patient.InsuranceNumber);
            Assert.Equal("0987654321", patient.EmergencyContact);
            Assert.False(patient.IsDeleted);
            Assert.Empty(patient.Appointments);
        }

        [Fact]
        public void AddAppointment_ShouldAddToAppointmentsCollection()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();
            var patient = new Patient(person, "INS-987654321", "0987654321");

            var appointment = TestDataFactory.CreateAppointment(patientId: patient.Id);

            // Act
            patient.Appointments.Add(appointment);

            // Assert
            Assert.Single(patient.Appointments);
            Assert.Contains(appointment, patient.Appointments);
        }
    }
}
