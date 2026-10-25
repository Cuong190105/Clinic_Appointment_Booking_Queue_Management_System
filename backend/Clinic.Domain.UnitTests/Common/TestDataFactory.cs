using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Domain.UnitTests.Common;

internal static class TestDataFactory
{
    internal static Person CreatePerson(string fullName = "John Doe")
    {
        return new Person(
            fullName,
            "0900000000",
            "john@example.test",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Male,
            "Test address");
    }

    internal static Patient CreatePatient(Guid? patientId = null)
    {
        var person = CreatePerson();
        return new Patient(
            patientId ?? Guid.NewGuid(),
            person.Id,
            "TEST-INSURANCE",
            "0900000000",
            DateTime.UtcNow,
            null,
            false);
    }

}
