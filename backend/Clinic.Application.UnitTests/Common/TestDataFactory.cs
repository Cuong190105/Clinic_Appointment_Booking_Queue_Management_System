using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common;

public static class TestDataFactory
{
    public static Person CreatePerson(
        string fullName = "John Doe",
        string? phoneNumber = "1234567890",
        string? email = "",
        string? address = "",
        DateOnly? dateOfBirth = null,
        string? userRole = null)
    {
        return new Person(
            fullName,
            phoneNumber,
            email,
            dateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Gender.Male,
            address);
    }

}
