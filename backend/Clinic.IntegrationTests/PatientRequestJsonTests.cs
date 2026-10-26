using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Clinic.API.Models;

namespace Clinic.IntegrationTests;

public class PatientRequestJsonTests
{
    [Theory]
    [InlineData("0", "Male")]
    [InlineData("1", "Female")]
    [InlineData("2", "Other")]
    [InlineData("\"Female\"", "Female")]
    public void CreatePatientRequest_AcceptsNumericAndStringGender(string genderJson, string expected)
    {
        var json = $$"""{"fullName":"Test Patient","dateOfBirth":"1990-01-01","gender":{{genderJson}},"address":"Test address"}""";

        var request = JsonSerializer.Deserialize<CreatePatientRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(expected, request.Gender);
    }

    [Fact]
    public void CreatePatientRequest_RequiresTheFourMinimumProfileFields()
    {
        var request = new CreatePatientRequest();
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreatePatientRequest.FullName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreatePatientRequest.DateOfBirth)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreatePatientRequest.Gender)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreatePatientRequest.Address)));
    }
}
