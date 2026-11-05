namespace Clinic.StaffScheduling.Service.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.StaffScheduling.Service.DTOs;
using Clinic.StaffScheduling.Service.Entities;
using Clinic.StaffScheduling.Service.Enums;
using Clinic.StaffScheduling.Service.Persistence;
using Clinic.StaffScheduling.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class StaffOrchestrationTests : IDisposable
{
    private readonly SchedulingDbContext _db;
    private readonly TestOutboxWriter _outbox = new();

    public StaffOrchestrationTests()
    {
        var options = new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SchedulingDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateEmployee_Success_ProvisionsIdentityAndMedicalRecordsAndSavesEmployee()
    {
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("internal/patients/resolve-registration", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { personId = Guid.NewGuid(), patientId = Guid.NewGuid() })
        });
        mockHandler.When("v1/internal/users/provision", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { userId = Guid.NewGuid(), personId = Guid.NewGuid(), patientId = Guid.NewGuid(), isActive = false, created = true })
        });
        mockHandler.When("internal/patients/provision-identity", new HttpResponseMessage(HttpStatusCode.OK));
        mockHandler.When("v1/internal/users/", new HttpResponseMessage(HttpStatusCode.NoContent));

        var service = CreateService(mockHandler);
        var request = new CreateEmployeeRequest(
            FullName: "Dr. Receptionist",
            PhoneNumber: "0987654321",
            Email: "rec@clinic.test",
            Gender: "Female",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Status: EmployeeStatus.Active,
            Username: "receptionist1",
            Password: "Password123!",
            Roles: ["Receptionist"]
        );

        var result = await service.CreateEmployeeAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.PhoneNumber == "0987654321");
        Assert.NotNull(emp);
        Assert.Equal("Dr. Receptionist", emp.FullName);
        Assert.Equal(EmployeeStatus.Active, emp.Status);
    }

    [Fact]
    public async Task CreateEmployee_DuplicatePhoneNumber_ReturnsConflict()
    {
        _db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            FullName = "Existing Employee",
            PhoneNumber = "0987654321",
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = EmployeeStatus.Active
        });
        await _db.SaveChangesAsync();

        var service = CreateService(new MockHttpMessageHandler());
        var request = new CreateEmployeeRequest(
            FullName: "New Employee",
            PhoneNumber: "0987654321",
            Username: "newemp",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateEmployeeAsync(request, CancellationToken.None);

        var httpResult = result as Microsoft.AspNetCore.Http.IStatusCodeHttpResult;
        Assert.NotNull(httpResult);
        Assert.Equal(StatusCodes.Status409Conflict, httpResult.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_MedicalRecordsUnavailable_Returns503()
    {
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("internal/patients/resolve-registration", new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var service = CreateService(mockHandler);
        var request = new CreateEmployeeRequest(
            FullName: "Test Staff",
            PhoneNumber: "0911223344",
            Username: "teststaff",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateEmployeeAsync(request, CancellationToken.None);

        var httpResult = result as Microsoft.AspNetCore.Http.IStatusCodeHttpResult;
        Assert.NotNull(httpResult);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpResult.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_IdentityConflict_ReturnsConflict()
    {
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("internal/patients/resolve-registration", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { personId = Guid.NewGuid(), patientId = Guid.NewGuid() })
        });
        mockHandler.When("v1/internal/users/provision", new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { code = "username_exists", message = "Username is already registered." })
        });

        var service = CreateService(mockHandler);
        var request = new CreateEmployeeRequest(
            FullName: "Staff Test",
            PhoneNumber: "0933445566",
            Username: "existinguser",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateEmployeeAsync(request, CancellationToken.None);

        var httpResult = result as Microsoft.AspNetCore.Http.IStatusCodeHttpResult;
        Assert.NotNull(httpResult);
        Assert.Equal(StatusCodes.Status409Conflict, httpResult.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_MedicalRecordsProvisionFails_DoesNotActivateIdentityUser()
    {
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("internal/patients/resolve-registration", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { personId = Guid.NewGuid(), patientId = Guid.NewGuid() })
        });
        mockHandler.When("v1/internal/users/provision", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { userId = Guid.NewGuid(), personId = Guid.NewGuid(), patientId = Guid.NewGuid(), isActive = false, created = true })
        });
        mockHandler.When("internal/patients/provision-identity", new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var service = CreateService(mockHandler);
        var request = new CreateEmployeeRequest(
            FullName: "Staff Compensation",
            PhoneNumber: "0955667788",
            Username: "compstaff",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateEmployeeAsync(request, CancellationToken.None);

        var httpResult = result as Microsoft.AspNetCore.Http.IStatusCodeHttpResult;
        Assert.NotNull(httpResult);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpResult.StatusCode);

        Assert.DoesNotContain(mockHandler.Calls, c => c.Contains("/roles"));
    }

    [Fact]
    public async Task CreateDoctor_Success_ProvisionsIdentityMedicalRecordsAndDoctorEntity()
    {
        var specialty = new Specialty { Id = Guid.NewGuid(), Name = "Cardiology", EstablishedDate = new DateOnly(2020, 1, 1) };
        _db.Specialties.Add(specialty);
        await _db.SaveChangesAsync();

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("internal/patients/resolve-registration", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { personId = Guid.NewGuid(), patientId = Guid.NewGuid() })
        });
        mockHandler.When("v1/internal/users/provision", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { userId = Guid.NewGuid(), personId = Guid.NewGuid(), patientId = Guid.NewGuid(), isActive = false, created = true })
        });
        mockHandler.When("internal/patients/provision-identity", new HttpResponseMessage(HttpStatusCode.OK));
        mockHandler.When("v1/internal/users/", new HttpResponseMessage(HttpStatusCode.NoContent));

        var service = CreateService(mockHandler);
        var request = new CreateDoctorRequest(
            FullName: "Dr. Strange",
            PhoneNumber: "0977889900",
            Email: "strange@marvel.test",
            LicenseNumber: "LIC-STRANGE-1",
            Qualification: "MD, PhD",
            ExperienceYears: 10,
            Status: "Active",
            SpecialtyId: specialty.Id,
            Username: "drstrange",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateDoctorAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        var doc = await _db.Doctors.Include(d => d.Employee).Include(d => d.WorkHistories).FirstOrDefaultAsync(d => d.LicenseNumber == "LIC-STRANGE-1");
        Assert.NotNull(doc);
        Assert.Equal("Dr. Strange", doc.Employee.FullName);
        Assert.Single(doc.WorkHistories);
        Assert.Equal(specialty.Id, doc.WorkHistories.First().SpecialtyId);
    }

    [Fact]
    public async Task CreateDoctor_DuplicateLicense_ReturnsConflict()
    {
        var specialty = new Specialty { Id = Guid.NewGuid(), Name = "Dermatology", EstablishedDate = new DateOnly(2020, 1, 1) };
        _db.Specialties.Add(specialty);
        var emp = new Employee { Id = Guid.NewGuid(), PersonId = Guid.NewGuid(), FullName = "Existing Doc", HireDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        _db.Employees.Add(emp);
        _db.Doctors.Add(new Doctor { Id = Guid.NewGuid(), EmployeeId = emp.Id, LicenseNumber = "DUP-LIC-001", Qualification = "MD", Status = DoctorStatus.Active });
        await _db.SaveChangesAsync();

        var service = CreateService(new MockHttpMessageHandler());
        var request = new CreateDoctorRequest(
            FullName: "New Doctor",
            PhoneNumber: "0911998877",
            LicenseNumber: "DUP-LIC-001",
            Qualification: "MD",
            ExperienceYears: 5,
            Status: "Active",
            SpecialtyId: specialty.Id,
            Username: "newdoc",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateDoctorAsync(request, CancellationToken.None);

        var httpResult = result as Microsoft.AspNetCore.Http.IStatusCodeHttpResult;
        Assert.NotNull(httpResult);
        Assert.Equal(StatusCodes.Status409Conflict, httpResult.StatusCode);
    }

    [Fact]
    public async Task CreateDoctor_IdentityProvisionFails_RollsBack()
    {
        var specialty = new Specialty { Id = Guid.NewGuid(), Name = "Orthopedics", EstablishedDate = new DateOnly(2020, 1, 1) };
        _db.Specialties.Add(specialty);
        await _db.SaveChangesAsync();

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("internal/patients/resolve-registration", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { personId = Guid.NewGuid(), patientId = Guid.NewGuid() })
        });
        mockHandler.When("v1/internal/users/provision", new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { code = "username_exists", message = "Username already exists." })
        });

        var service = CreateService(mockHandler);
        var request = new CreateDoctorRequest(
            FullName: "Doc Error",
            PhoneNumber: "0922334455",
            LicenseNumber: "LIC-ERR-1",
            Qualification: "MD",
            ExperienceYears: 3,
            Status: "Active",
            SpecialtyId: specialty.Id,
            Username: "existinguser",
            Password: "Password123!",
            HireDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var result = await service.CreateDoctorAsync(request, CancellationToken.None);

        var httpResult = result as Microsoft.AspNetCore.Http.IStatusCodeHttpResult;
        Assert.NotNull(httpResult);
        Assert.Equal(StatusCodes.Status409Conflict, httpResult.StatusCode);

        Assert.False(await _db.Doctors.AnyAsync(d => d.LicenseNumber == "LIC-ERR-1"));
        Assert.False(await _db.Employees.AnyAsync(e => e.PhoneNumber == "0922334455"));
    }

    [Fact]
    public void CreateDoctorRequest_CanDeserialize_WithIntegerStatusAndGender()
    {
        var json = """
        {
            "username": "drtest",
            "password": "Password123!",
            "fullName": "Test Doctor",
            "phoneNumber": "0912345678",
            "email": "dr@test.com",
            "dateOfBirth": "1980-01-01",
            "gender": 0,
            "address": "123 Street",
            "hireDate": "2026-11-05",
            "licenseNumber": "LIC-1234",
            "qualification": "MD",
            "experienceYears": 5,
            "status": 0,
            "specialtyId": "a0000006-0000-0000-0000-000000000001"
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var req = JsonSerializer.Deserialize<CreateDoctorRequest>(json, options);
        Assert.NotNull(req);
        Assert.Equal("Active", req.Status);
    }

    private StaffOrchestrationService CreateService(MockHttpMessageHandler handler)
    {
        var clientFactory = new TestHttpClientFactory(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Authority"] = "http://identity-service:8080",
            ["MedicalRecords:Authority"] = "http://medical-records-service:8080",
            ["Identity:ServiceToken"] = "test-token"
        }).Build();

        return new StaffOrchestrationService(
            _db,
            clientFactory,
            _outbox,
            config,
            NullLogger<StaffOrchestrationService>.Instance
        );
    }

    private class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler)
        {
            BaseAddress = new Uri($"http://{name.ToLowerInvariant()}-service:8080/")
        };
    }

    private class TestOutboxWriter : ISchedulingOutboxWriter
    {
        public readonly List<object> Envelopes = [];
        public void Add<T>(EventEnvelope<T> envelope, string routingKey) => Envelopes.Add(envelope);
        public Task<int> CommitAsync(CancellationToken cancellationToken = default) => Task.FromResult(Envelopes.Count);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<(string PathPattern, HttpResponseMessage Response)> _routes = [];
        public readonly List<string> Calls = [];

        public void When(string pathPattern, HttpResponseMessage response)
        {
            _routes.Add((pathPattern, response));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            Calls.Add(uri);
            foreach (var (pattern, response) in _routes)
            {
                if (uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(response);
                }
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
