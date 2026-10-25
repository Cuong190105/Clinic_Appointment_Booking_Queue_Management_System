using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Scheduling.Service.DTOs;
using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Scheduling.Service.Tests;

public class SchedulingApiIntegrationTests : IClassFixture<SchedulingServiceFactory>
{
    private readonly HttpClient _client;
    private readonly SchedulingServiceFactory _factory;

    public SchedulingApiIntegrationTests(SchedulingServiceFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSpecialties_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/specialties");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.GetProperty("isSuccess").GetBoolean());
    }

    [Fact]
    public async Task GetDoctors_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/doctors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.GetProperty("isSuccess").GetBoolean());
    }

    [Fact]
    public async Task GetDoctorShifts_ReturnsScheduleJsonWithoutPropertyCollision()
    {
        var doctorId = Guid.NewGuid();
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = Guid.NewGuid(),
                FullName = "Shift Endpoint Integration Doctor",
                HireDate = new DateOnly(2020, 1, 1),
                Status = EmployeeStatus.Active
            };
            db.Employees.Add(employee);
            db.Doctors.Add(new Doctor
            {
                Id = doctorId,
                EmployeeId = employee.Id,
                Employee = employee,
                LicenseNumber = $"LIC-{doctorId:N}",
                Qualification = "MD",
                ExperienceYears = 5,
                Status = DoctorStatus.Active
            });
            db.WorkSchedules.Add(new WorkSchedule
            {
                Id = Guid.NewGuid(),
                DoctorId = doctorId,
                Date = workDate,
                ShiftStart = new TimeOnly(9, 0),
                ShiftEnd = new TimeOnly(12, 0),
                RecommendedCapacity = 20,
                Status = WorkScheduleStatus.Active
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/doctors/{doctorId}/shifts?startDate={workDate:yyyy-MM-dd}&endDate={workDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schedule = Assert.Single(json.RootElement.GetProperty("schedules").EnumerateArray());
        Assert.Equal(20, schedule.GetProperty("recommendedCapacity").GetInt32());
    }

    [Fact]
    public async Task ReservationLifecycle_ViaHttpApi_Succeeds()
    {
        // 1. Seed a test schedule in DB
        Guid scheduleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var doctor = await db.Doctors.FirstOrDefaultAsync();
            if (doctor == null)
            {
                var employee = new Employee
                {
                    Id = Guid.NewGuid(),
                    PersonId = Guid.NewGuid(),
                    FullName = "Integration Doctor",
                    HireDate = new DateOnly(2020, 1, 1),
                    Status = EmployeeStatus.Active
                };
                doctor = new Doctor
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    Employee = employee,
                    LicenseNumber = "LIC-INTEG",
                    Qualification = "MD",
                    ExperienceYears = 8,
                    Status = DoctorStatus.Active
                };
                db.Employees.Add(employee);
                db.Doctors.Add(doctor);
                await db.SaveChangesAsync();
            }
            var doctorId = doctor.Id;

            var schedule = new WorkSchedule
            {
                Id = Guid.NewGuid(),
                DoctorId = doctorId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                ShiftStart = new TimeOnly(9, 0),
                ShiftEnd = new TimeOnly(11, 0),
                RecommendedCapacity = 8,
                Status = WorkScheduleStatus.Active
            };
            db.WorkSchedules.Add(schedule);
            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        // 2. Query availability for this schedule
        var availResponse = await _client.GetAsync($"/schedules/availability?fromDate={DateTime.UtcNow.AddDays(5):yyyy-MM-dd}&toDate={DateTime.UtcNow.AddDays(5):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, availResponse.StatusCode);

        // 3. Make a reservation
        var patientId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/schedules/{scheduleId}/reservations")
        {
            Content = JsonContent.Create(new CreateReservationRequest(new TimeOnly(9, 15), patientId))
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var reserveResponse = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, reserveResponse.StatusCode);

        var reservation = await reserveResponse.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal("Reserved", reservation.Status);
        Assert.Equal(scheduleId, reservation.ScheduleId);

        // 4. Confirm reservation
        var confirmResponse = await _client.PostAsync($"/schedules/{scheduleId}/reservations/{reservation.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        // 5. Release reservation
        var releaseResponse = await _client.PostAsync($"/schedules/{scheduleId}/reservations/{reservation.Id}/release", null);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
    }

    [Fact]
    public async Task ScheduleUpdateAndCancel_ViaHttpApi_Succeeds()
    {
        Guid scheduleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var doctor = await db.Doctors.FirstOrDefaultAsync();
            var schedule = new WorkSchedule
            {
                Id = Guid.NewGuid(),
                DoctorId = doctor!.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                ShiftStart = new TimeOnly(13, 0),
                ShiftEnd = new TimeOnly(17, 0),
                RecommendedCapacity = 16,
                Status = WorkScheduleStatus.Active
            };
            db.WorkSchedules.Add(schedule);
            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        // 1. Update schedule
        var updateReq = new UpdateScheduleRequest(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            new TimeOnly(14, 0),
            new TimeOnly(18, 0),
            15
        );
        var putResponse = await _client.PutAsJsonAsync($"/schedules/{scheduleId}", updateReq);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        // 2. Cancel schedule
        var cancelReq = new CancelScheduleRequest("Doctor conference");
        var cancelResponse = await _client.PostAsJsonAsync($"/schedules/{scheduleId}/cancel", cancelReq);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task InternalUserProfileUpdated_ViaHttpApi_SynchronizesEmployee()
    {
        var personId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var emp = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                FullName = "Initial Name",
                PhoneNumber = "0123456789",
                Email = "initial@test.com",
                Gender = "Male",
                HireDate = new DateOnly(2022, 1, 1),
                Status = EmployeeStatus.Active
            };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
        }

        var updateEvt = new Clinic.Contracts.Events.UserProfileUpdated(
            personId,
            "Updated Name via Event",
            "0999999999",
            "updated@test.com",
            "Female"
        );

        using var unauthorizedResponse = await _client.PostAsJsonAsync("/internal/events/user-profile-updated", updateEvt);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/events/user-profile-updated")
        {
            Content = JsonContent.Create(updateEvt)
        };
        request.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var emp = await db.Employees.FirstOrDefaultAsync(e => e.PersonId == personId);
            Assert.NotNull(emp);
            Assert.Equal("Updated Name via Event", emp.FullName);
            Assert.Equal("0999999999", emp.PhoneNumber);
            Assert.Equal("Female", emp.Gender);
        }
    }
}
