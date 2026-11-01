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

}
