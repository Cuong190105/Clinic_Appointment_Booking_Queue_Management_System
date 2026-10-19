using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Domain.Common;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Clinic.IntegrationTests.Appointments;

public sealed class BaselineBusinessWorkflowTests : IClassFixture<ApiContractFactory>, IAsyncLifetime
{
    private readonly ApiContractFactory _factory;
    private HttpClient _patientClient = null!;
    private HttpClient _doctorClient = null!;

    public BaselineBusinessWorkflowTests(ApiContractFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        _patientClient = _factory.CreateAuthenticatedClient(ApiContractFactory.PatientUserId);
        _doctorClient = _factory.CreateAuthenticatedClient(ApiContractFactory.DoctorUserId);
    }

    public Task DisposeAsync()
    {
        _patientClient.Dispose();
        _doctorClient.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Booking_persists_pending_appointment_and_rejects_duplicate_slot()
    {
        var scheduleId = await CreateFutureScheduleAsync(capacity: 1);
        var request = new
        {
            workScheduleId = scheduleId,
            timeSlot = "09:00:00",
            reason = "baseline booking"
        };

        using var createResponse = await PostAppointmentAsync(request);
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created,
            await createResponse.Content.ReadAsStringAsync());
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var createdResult = GetResult(created);
        var appointmentId = createdResult.GetProperty("id").GetGuid();
        Assert.Equal("Pending", createdResult.GetProperty("status").GetString());

        using var duplicateResponse = await PostAppointmentAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        await using var dbContext = _factory.CreateDbContext();
        var appointment = await dbContext.Appointments.SingleAsync(a => a.Id == appointmentId);
        var schedule = await dbContext.WorkSchedules.SingleAsync(s => s.Id == scheduleId);
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        Assert.Equal(WorkScheduleStatus.Full, schedule.Status);
    }

    [Fact]
    public async Task Check_in_call_exam_and_finalize_report_complete_the_visit()
    {
        var appointmentId = await SeedConfirmedAppointmentForTodayAsync();

        using var missingKeyRequest = new HttpRequestMessage(HttpMethod.Post, $"/appointments/{appointmentId}/check-in");
        using var missingKeyResponse = await _patientClient.SendAsync(missingKeyRequest);
        Assert.Equal(HttpStatusCode.BadRequest, missingKeyResponse.StatusCode);

        using var checkInRequest = new HttpRequestMessage(HttpMethod.Post, $"/appointments/{appointmentId}/check-in");
        checkInRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var idempotencyKey = checkInRequest.Headers.GetValues("Idempotency-Key").Single();
        await using var beforeCheckInDb = _factory.CreateDbContext();
        var outboxCountBeforeCheckIn = await beforeCheckInDb.OutboxMessages
            .CountAsync(message => message.RoutingKey == "appointment.checked-in");

        using var checkInResponse = await _patientClient.SendAsync(checkInRequest);
        Assert.Equal(HttpStatusCode.NoContent, checkInResponse.StatusCode);

        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, $"/appointments/{appointmentId}/check-in");
        retryRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        using var retryResponse = await _patientClient.SendAsync(retryRequest);
        Assert.Equal(HttpStatusCode.NoContent, retryResponse.StatusCode);

        await using var dbContext = _factory.CreateDbContext();
        var ticket = await dbContext.QueueTickets.SingleAsync(q => q.AppointmentId == appointmentId);
        Assert.Equal(QueueStatus.Waiting, ticket.Status);
        Assert.Equal(1, await dbContext.QueueTickets.CountAsync(q => q.AppointmentId == appointmentId));
        Assert.Equal(outboxCountBeforeCheckIn + 1, await dbContext.OutboxMessages
            .CountAsync(message => message.RoutingKey == "appointment.checked-in"));

        using var callNextResponse = await _doctorClient.PostAsync($"/queue/call-next/{ApiContractFactory.DoctorId}", null);
        Assert.Equal(HttpStatusCode.OK, callNextResponse.StatusCode);
        using var called = JsonDocument.Parse(await callNextResponse.Content.ReadAsStringAsync());
        Assert.Equal("Called", GetResult(called).GetProperty("status").GetString());

        using var startExamResponse = await _doctorClient.PostAsync($"/queue/{ticket.Id}/start-exam", null);
        Assert.Equal(HttpStatusCode.NoContent, startExamResponse.StatusCode);

        using var saveReportResponse = await _doctorClient.PostAsJsonAsync("/medical-reports", new
        {
            appointmentId,
            symptoms = "baseline symptom",
            diagnosis = "baseline diagnosis",
            prescription = "baseline prescription",
            notes = "baseline workflow",
            isFinalize = false
        });
        Assert.Equal(HttpStatusCode.OK, saveReportResponse.StatusCode);
        using var savedReport = JsonDocument.Parse(await saveReportResponse.Content.ReadAsStringAsync());
        var reportId = GetResult(savedReport).GetProperty("id").GetGuid();
        Assert.Equal("Draft", GetResult(savedReport).GetProperty("status").GetString());

        using var finalizeResponse = await _doctorClient.PostAsync($"/medical-reports/{reportId}/finalize", null);
        Assert.Equal(HttpStatusCode.OK, finalizeResponse.StatusCode);
        using var finalizedReport = JsonDocument.Parse(await finalizeResponse.Content.ReadAsStringAsync());
        Assert.Equal("Finalized", GetResult(finalizedReport).GetProperty("status").GetString());

        await dbContext.Entry(ticket).ReloadAsync();
        var appointment = await dbContext.Appointments.SingleAsync(a => a.Id == appointmentId);
        var report = await dbContext.MedicalReports.SingleAsync(r => r.Id == reportId);
        Assert.Equal(QueueStatus.Completed, ticket.Status);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Equal(MedicalReportStatus.Finalized, report.Status);
    }

    [Fact]
    public async Task Doctor_shift_suggestion_can_be_approved_into_a_work_schedule()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        using var createResponse = await _doctorClient.PostAsJsonAsync("/shifts/suggestions", new
        {
            date,
            startTime = "09:00:00",
            endTime = "10:00:00",
            recommendedCapacity = 7,
            reason = "baseline approved shift"
        });

        Assert.True(createResponse.StatusCode == HttpStatusCode.Created,
            await createResponse.Content.ReadAsStringAsync());
        using var suggestion = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var suggestionId = GetResult(suggestion).GetProperty("id").GetGuid();

        using var approveResponse = await _doctorClient.PostAsync($"/shifts/suggestions/{suggestionId}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        await using var dbContext = _factory.CreateDbContext();
        var request = await dbContext.ShiftRequests.SingleAsync(sr => sr.Id == suggestionId);
        var schedule = await dbContext.WorkSchedules.SingleOrDefaultAsync(ws =>
            ws.DoctorId == ApiContractFactory.DoctorId && ws.Date == date &&
            ws.ShiftStart == new TimeOnly(9, 0) && ws.ShiftEnd == new TimeOnly(10, 0));

        Assert.Equal(ShiftRequestStatus.Approved, request.Status);
        Assert.NotNull(schedule);
        Assert.Equal(7, schedule.RecommendedCapacity);
    }

    private async Task<HttpResponseMessage> PostAppointmentAsync(object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/appointments")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await _patientClient.SendAsync(message);
    }

    private async Task<Guid> CreateFutureScheduleAsync(int capacity)
    {
        await using var dbContext = _factory.CreateDbContext();
        var doctor = await dbContext.Doctors.SingleAsync(d => d.Id == ApiContractFactory.DoctorId);
        var date = new TimeConverter().Today.AddDays(2);
        var schedule = new WorkSchedule(doctor, date, new TimeOnly(9, 0), new TimeOnly(10, 0), capacity);

        dbContext.WorkSchedules.Add(schedule);
        await dbContext.SaveChangesAsync();
        return schedule.Id;
    }

    private async Task<Guid> SeedConfirmedAppointmentForTodayAsync()
    {
        await using var dbContext = _factory.CreateDbContext();
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var schedule = new WorkSchedule(
            Guid.NewGuid(), ApiContractFactory.DoctorId, todayUtc,
            new TimeOnly(8, 0), new TimeOnly(12, 0), 10,
            WorkScheduleStatus.Active, now, null, false);
        var appointment = new Appointment(
            Guid.NewGuid(), ApiContractFactory.PatientId, schedule.Id,
            new TimeOnly(9, 0), "baseline queue workflow", AppointmentStatus.Confirmed,
            false, ApiContractFactory.PatientUserId, now, null, false);

        dbContext.WorkSchedules.Add(schedule);
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();
        return appointment.Id;
    }

    private static JsonElement GetResult(JsonDocument document) =>
        document.RootElement.TryGetProperty("result", out var result) ? result : document.RootElement;
}
