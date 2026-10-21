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
}
