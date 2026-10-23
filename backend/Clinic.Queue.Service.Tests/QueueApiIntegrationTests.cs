using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Queue.Service.DTOs;
using Clinic.Queue.Service.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Clinic.Queue.Service.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-NoAuth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();
        if (Request.Headers.TryGetValue("X-Test-Role", out var roleVal))
        {
            claims.Add(new Claim(ClaimTypes.Role, roleVal!));
        }
        if (Request.Headers.TryGetValue("X-Test-DoctorId", out var docVal))
        {
            claims.Add(new Claim("doctorId", docVal!));
        }
        if (Request.Headers.TryGetValue("X-Test-PatientId", out var patVal))
        {
            claims.Add(new Claim("patientId", patVal!));
        }
        if (Request.Headers.TryGetValue("X-Test-Sub", out var subVal))
        {
            claims.Add(new Claim("sub", subVal!));
        }

        if (claims.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class TestQueueAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:QueueConnection"] = "",
            ["SkipBackgroundWorkers"] = "true"
        }));

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<QueueDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<QueueDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Add Test Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

public class QueueApiIntegrationTests : IClassFixture<TestQueueAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestQueueAppFactory _factory;

    public QueueApiIntegrationTests(TestQueueAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Unauthorized_WhenNoAuthHeaderProvided()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/doctors/" + Guid.NewGuid() + "/queue");
        request.Headers.Add("X-Test-NoAuth", "1");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InternalEvents_AppointmentCheckedIn_CreatesTicket_AndIsIdempotent()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(appointmentId, patientId, doctorId, appointmentId, DateTimeOffset.UtcNow, false, "Nguyen Thi Mai", "Dr. House")
        };

        // First call
        var response1 = await _client.PostAsJsonAsync("/internal/events/appointment-checked-in", envelope);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var ticket1 = await response1.Content.ReadFromJsonAsync<QueueTicketDto>();
        Assert.NotNull(ticket1);
        Assert.Equal(appointmentId, ticket1.AppointmentId);
        Assert.Equal(1, ticket1.QueueNumber);
        Assert.Equal("Waiting", ticket1.Status);
        Assert.Equal("Nguyen Thi Mai", ticket1.PatientName);

        // Duplicate call with same EventId
        var response2 = await _client.PostAsJsonAsync("/internal/events/appointment-checked-in", envelope);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var ticket2 = await response2.Content.ReadFromJsonAsync<QueueTicketDto>();
        Assert.NotNull(ticket2);
        Assert.Equal(ticket1.Id, ticket2.Id);
        Assert.Equal(1, ticket2.QueueNumber);

        // Call with different EventId but same AppointmentId
        var envelopeDifferentEventId = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = envelope.Payload
        };
        var response3 = await _client.PostAsJsonAsync("/internal/events/appointment-checked-in", envelopeDifferentEventId);
        Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
        var ticket3 = await response3.Content.ReadFromJsonAsync<QueueTicketDto>();
        Assert.NotNull(ticket3);
        Assert.Equal(ticket1.Id, ticket3.Id);
        Assert.Equal(1, ticket3.QueueNumber);
    }

    [Fact]
    public async Task DoctorQueueWorkflow_FullLifecycle_CallNext_StartExam_CompleteExam()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        // 1. Check in patient via internal event
        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(appointmentId, patientId, doctorId, appointmentId, DateTimeOffset.UtcNow, true, "Bao Nguyen", "Dr. Strange")
        };
        var checkinRes = await _client.PostAsJsonAsync("/internal/events/appointment-checked-in", envelope);
        var ticket = await checkinRes.Content.ReadFromJsonAsync<QueueTicketDto>();
        Assert.NotNull(ticket);

        // 2. Doctor views their queue
        var getQueueReq = new HttpRequestMessage(HttpMethod.Get, $"/doctors/{doctorId}/queue");
        getQueueReq.Headers.Add("X-Test-Role", "Doctor");
        getQueueReq.Headers.Add("X-Test-DoctorId", doctorId.ToString());
        var getQueueRes = await _client.SendAsync(getQueueReq);
        Assert.Equal(HttpStatusCode.OK, getQueueRes.StatusCode);
        var queueList = await getQueueRes.Content.ReadFromJsonAsync<List<QueueTicketDto>>();
        Assert.NotNull(queueList);
        Assert.Contains(queueList, t => t.Id == ticket.Id);

        // 3. Different doctor viewing queue without any scope gets 403 Forbidden
        var forbiddenReq = new HttpRequestMessage(HttpMethod.Get, $"/doctors/{doctorId}/queue");
        forbiddenReq.Headers.Add("X-Test-Role", "Doctor");
        forbiddenReq.Headers.Add("X-Test-DoctorId", Guid.NewGuid().ToString());
        var forbiddenRes = await _client.SendAsync(forbiddenReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // 4. Doctor calls next patient
        var callNextReq = new HttpRequestMessage(HttpMethod.Post, $"/doctors/{doctorId}/queue/next");
        callNextReq.Headers.Add("X-Test-Role", "Doctor");
        callNextReq.Headers.Add("X-Test-DoctorId", doctorId.ToString());
        var callNextRes = await _client.SendAsync(callNextReq);
        Assert.Equal(HttpStatusCode.OK, callNextRes.StatusCode);
        var calledTicket = await callNextRes.Content.ReadFromJsonAsync<QueueTicketDto>();
        Assert.NotNull(calledTicket);
        Assert.Equal(ticket.Id, calledTicket.Id);
        Assert.Equal("Called", calledTicket.Status);

        // 5. Calling next again while patient is Called returns 409 Conflict
        var callNextConflictReq = new HttpRequestMessage(HttpMethod.Post, $"/doctors/{doctorId}/queue/next");
        callNextConflictReq.Headers.Add("X-Test-Role", "Doctor");
        callNextConflictReq.Headers.Add("X-Test-DoctorId", doctorId.ToString());
        var callNextConflictRes = await _client.SendAsync(callNextConflictReq);
        Assert.Equal(HttpStatusCode.Conflict, callNextConflictRes.StatusCode);

        // 6. Doctor starts exam
        var startExamReq = new HttpRequestMessage(HttpMethod.Post, $"/queue/{ticket.Id}/start-exam");
        startExamReq.Headers.Add("X-Test-Role", "Doctor");
        startExamReq.Headers.Add("X-Test-DoctorId", doctorId.ToString());
        var startExamRes = await _client.SendAsync(startExamReq);
        Assert.Equal(HttpStatusCode.NoContent, startExamRes.StatusCode);

        // 7. Doctor completes exam
        var completeExamReq = new HttpRequestMessage(HttpMethod.Post, $"/queue/{ticket.Id}/complete-exam");
        completeExamReq.Headers.Add("X-Test-Role", "Doctor");
        completeExamReq.Headers.Add("X-Test-DoctorId", doctorId.ToString());
        var completeExamRes = await _client.SendAsync(completeExamReq);
        Assert.Equal(HttpStatusCode.NoContent, completeExamRes.StatusCode);

        // 8. Verify ticket is now Completed
        var getTicketReq = new HttpRequestMessage(HttpMethod.Get, $"/queue/{ticket.Id}");
        getTicketReq.Headers.Add("X-Test-Role", "Receptionist");
        var getTicketRes = await _client.SendAsync(getTicketReq);
        Assert.Equal(HttpStatusCode.OK, getTicketRes.StatusCode);
        var finalTicket = await getTicketRes.Content.ReadFromJsonAsync<QueueTicketDto>();
        Assert.NotNull(finalTicket);
        Assert.Equal("Completed", finalTicket.Status);
    }

    [Fact]
    public async Task PatientQueueStatus_ReturnsCorrectStatus_OrNoContentWhenEmpty()
    {
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        // 1. Before check-in: patient has no queue status -> 204 NoContent
        var emptyReq = new HttpRequestMessage(HttpMethod.Get, "/me/queue-status");
        emptyReq.Headers.Add("X-Test-Role", "Patient");
        emptyReq.Headers.Add("X-Test-PatientId", patientId.ToString());
        var emptyRes = await _client.SendAsync(emptyReq);
        Assert.Equal(HttpStatusCode.NoContent, emptyRes.StatusCode);

        // 2. Check-in patient
        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(Guid.NewGuid(), patientId, doctorId, Guid.NewGuid(), DateTimeOffset.UtcNow, false, "Patient Patient", "Dr. Doctor")
        };
        await _client.PostAsJsonAsync("/internal/events/appointment-checked-in", envelope);

        // 3. Now /me/queue-status returns 200 OK
        var statusReq = new HttpRequestMessage(HttpMethod.Get, "/me/queue-status");
        statusReq.Headers.Add("X-Test-Role", "Patient");
        statusReq.Headers.Add("X-Test-PatientId", patientId.ToString());
        var statusRes = await _client.SendAsync(statusReq);
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

        var list = await statusRes.Content.ReadFromJsonAsync<List<MyQueueStatusDto>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("Waiting", list[0].Status);
    }
}
