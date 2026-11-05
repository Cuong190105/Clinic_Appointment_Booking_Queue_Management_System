using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clinic.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinic.IntegrationTests;

/// <summary>
/// Phase 7: Automated API Contract Drift, Two-Way Inventory Reconciliation, Visibility Policy,
/// Direct/Proxy OpenAPI Access Control, Wire-Schema Alignment, and Real OpenAPI Specification Tests.
/// </summary>
public sealed class ApiContractDriftTests : IClassFixture<ApiContractFactory>
{
    private readonly ApiContractFactory _factory;
    private readonly HttpClient _anonymousClient;
    private readonly HttpClient _adminClient;

    public ApiContractDriftTests(ApiContractFactory factory)
    {
        _factory = factory;
        _anonymousClient = factory.CreateStrictlyAnonymousClient();
        _adminClient = factory.CreateRoleClient("Admin");
    }

    private static string GetContractsDirectory()
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tasks", "contracts"));
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    private static string GetInventoryFilePath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tasks", "API_INVENTORY_MEMBER_A.md"));

    private static bool ShouldUpdateSnapshots() =>
        string.Equals(Environment.GetEnvironmentVariable("UPDATE_CONTRACT_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase);

    private static void WriteSnapshotIfRequested(string fileName, string jsonContent)
    {
        if (!ShouldUpdateSnapshots())
        {
            return;
        }

        using var parsed = JsonDocument.Parse(jsonContent);
        var formatted = JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(GetContractsDirectory(), fileName), formatted + Environment.NewLine);
    }

    private static JsonDocument ReadReviewedBaseline(string fileName)
    {
        var path = Path.Combine(GetContractsDirectory(), fileName);
        Assert.True(File.Exists(path), $"Reviewed baseline snapshot '{fileName}' does not exist at '{path}'. Run with UPDATE_CONTRACT_SNAPSHOTS=true to generate snapshots intentionally.");
        var content = File.ReadAllText(path);
        Assert.False(string.IsNullOrWhiteSpace(content), $"Reviewed baseline snapshot '{fileName}' is empty.");
        return JsonDocument.Parse(content);
    }

    [Fact]
    public async Task ApiCatalog_ReturnsAllSixMicroservices_WithStructuredAuthAndVisibilityMetadata_AndMatchesBaseline()
    {
        var response = await _anonymousClient.GetAsync("/api/catalog");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        WriteSnapshotIfRequested("api-catalog-baseline.json", json);

        using var runtimeDoc = JsonDocument.Parse(json);
        var root = runtimeDoc.RootElement;

        // Verify wire envelope is ApiResponse<ApiCatalogResponse>
        Assert.Equal("OK", root.GetProperty("statusCode").GetString());
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(0, root.GetProperty("errorMessages").GetArrayLength());

        var catalogRoot = root.GetProperty("result");
        Assert.Equal("1.0.0", catalogRoot.GetProperty("gatewayVersion").GetString());
        Assert.Equal("/openapi/v1.json", catalogRoot.GetProperty("openApiSpecUrl").GetString());
        Assert.Equal("Public", catalogRoot.GetProperty("openApiSpecVisibility").GetString());

        var services = catalogRoot.GetProperty("services").EnumerateArray().ToList();
        Assert.Equal(6, services.Count);

        var byName = services.ToDictionary(s => s.GetProperty("name").GetString()!, s => s);
        Assert.Contains("Clinic.Identity.Service", byName.Keys);
        Assert.Contains("Clinic.StaffScheduling.Service", byName.Keys);
        Assert.Contains("Clinic.Appointment.Service", byName.Keys);
        Assert.Contains("Clinic.Queue.Service", byName.Keys);
        Assert.Contains("Clinic.MedicalRecords.Service", byName.Keys);
        Assert.Contains("Clinic.Notification.Service", byName.Keys);

        // Verify Identity service distinguishes anonymous vs authenticated routes and does not expose misleading AuthRequired=false
        var identityEntry = byName["Clinic.Identity.Service"];
        Assert.False(identityEntry.TryGetProperty("authRequired", out _));
        Assert.Equal("Mixed", identityEntry.GetProperty("authPolicy").GetString());
        var identityAnon = identityEntry.GetProperty("anonymousRoutes").EnumerateArray().Select(x => x.GetString()).ToList();
        var identityAuth = identityEntry.GetProperty("authenticatedRoutes").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("POST /auth/login", identityAnon);
        Assert.Contains("POST /forgot-password", identityAnon);
        Assert.Contains("POST /reset-password", identityAnon);
        Assert.Contains("GET /auth/me", identityAuth);
        Assert.Contains("GET /auth/permissions", identityAuth);
        Assert.Contains("POST /change-password", identityAuth);
        Assert.Contains("GET /admin/users", identityAuth);

        // Verify authority values match active IConfiguration and declare InternalClusterOnly scope (not hardcoded localhost fallbacks)
        var gatewayConfig = _factory.Services.GetRequiredService<IConfiguration>();
        var expectedAuthorityConfigKeys = new Dictionary<string, string>
        {
            ["Clinic.Identity.Service"] = "Identity:Authority",
            ["Clinic.StaffScheduling.Service"] = "Scheduling:Authority",
            ["Clinic.Appointment.Service"] = "Appointment:Authority",
            ["Clinic.Queue.Service"] = "Queue:Authority",
            ["Clinic.MedicalRecords.Service"] = "MedicalRecords:Authority",
            ["Clinic.Notification.Service"] = "Notification:Authority"
        };

        foreach (var service in services)
        {
            var svcName = service.GetProperty("name").GetString()!;
            var expectedAuthority = gatewayConfig[expectedAuthorityConfigKeys[svcName]];
            Assert.Equal(expectedAuthority, service.GetProperty("authority").GetString());
            Assert.Equal("InternalClusterOnly", service.GetProperty("authorityScope").GetString());
            Assert.Equal("/readyz", service.GetProperty("healthEndpoint").GetString());
            Assert.True(service.GetProperty("routePrefixes").GetArrayLength() > 0);
            Assert.True(service.GetProperty("authenticatedRoutes").GetArrayLength() > 0);
            Assert.Equal("Internal", service.GetProperty("specVisibility").GetString());
            Assert.Equal("Admin", service.GetProperty("openApiAccessPolicy").GetString());
            Assert.False(string.IsNullOrWhiteSpace(service.GetProperty("openApiDocUrl").GetString()));
        }

        var qos = catalogRoot.GetProperty("qoSPolicies");
        Assert.Equal("X-Correlation-ID", qos.GetProperty("correlationHeader").GetString());
        Assert.Equal("Idempotency-Key", qos.GetProperty("idempotencyHeader").GetString());
        Assert.True(qos.GetProperty("rateLimitingEnabled").GetBoolean());

        // Compare against fixed reviewed baseline (without overwriting)
        using var baselineDoc = ReadReviewedBaseline("api-catalog-baseline.json");
        var baselineServices = baselineDoc.RootElement.GetProperty("result").GetProperty("services").EnumerateArray().ToList();
        Assert.Equal(baselineServices.Count, services.Count);
        for (var i = 0; i < services.Count; i++)
        {
            Assert.Equal(
                baselineServices[i].GetProperty("name").GetString(),
                services[i].GetProperty("name").GetString());
            Assert.Equal(
                baselineServices[i].GetProperty("authority").GetString(),
                services[i].GetProperty("authority").GetString());
            Assert.Equal(
                baselineServices[i].GetProperty("authorityScope").GetString(),
                services[i].GetProperty("authorityScope").GetString());
            Assert.Equal(
                baselineServices[i].GetProperty("authPolicy").GetString(),
                services[i].GetProperty("authPolicy").GetString());
            Assert.Equal(
                baselineServices[i].GetProperty("specVisibility").GetString(),
                services[i].GetProperty("specVisibility").GetString());
            Assert.Equal(
                baselineServices[i].GetProperty("openApiAccessPolicy").GetString(),
                services[i].GetProperty("openApiAccessPolicy").GetString());
            Assert.Equal(
                baselineServices[i].GetProperty("routePrefixes").EnumerateArray().Select(x => x.GetString()).ToArray(),
                services[i].GetProperty("routePrefixes").EnumerateArray().Select(x => x.GetString()).ToArray());
        }
    }

    [Fact]
    public async Task Gateway_Serves_Valid_OpenApiDocument_With_RequiredPaths_SecuritySchemes_Headers_AndMatchesBaseline()
    {
        var response = await _anonymousClient.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        WriteSnapshotIfRequested("gateway-openapi-v1.json", json);

        using var runtimeDoc = JsonDocument.Parse(json);
        var root = runtimeDoc.RootElement;

        // 1. OpenAPI version & Info
        Assert.StartsWith("3.", root.GetProperty("openapi").GetString()!);
        var info = root.GetProperty("info");
        Assert.Equal("Clinic API Gateway", info.GetProperty("title").GetString());
        Assert.Equal("1.0.0", info.GetProperty("version").GetString());

        // 2. Security Schemes (Bearer JWT)
        var securitySchemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearerScheme.GetProperty("bearerFormat").GetString());

        // 3. Paths & Security Requirements on Anonymous vs Protected operations
        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/catalog", out var catalogPath));
        Assert.True(paths.TryGetProperty("/auth/login", out var loginPath));
        Assert.True(paths.TryGetProperty("/auth/me", out var authMePath));
        Assert.True(paths.TryGetProperty("/forgot-password", out var forgotPwdPath));
        Assert.True(paths.TryGetProperty("/reset-password", out var resetPwdPath));
        Assert.True(paths.TryGetProperty("/change-password", out var changePwdPath));
        Assert.True(paths.TryGetProperty("/appointments", out var appointmentsPath));
        Assert.True(paths.TryGetProperty("/doctors", out var doctorsPath));
        Assert.True(paths.TryGetProperty("/specialties", out var specialtiesPath));
        Assert.True(paths.TryGetProperty("/employees", out var employeesPath));
        Assert.True(paths.TryGetProperty("/shifts", out var shiftsPath));
        Assert.True(paths.TryGetProperty("/slots", out _));
        Assert.True(paths.TryGetProperty("/schedules", out _));
        Assert.True(paths.TryGetProperty("/queue/call-next/{doctorId}", out _));
        Assert.True(paths.TryGetProperty("/patients", out _));
        Assert.True(paths.TryGetProperty("/medical-reports", out _));
        Assert.True(paths.TryGetProperty("/notifications", out _));

        // Internal OpenAPI proxy routes must be excluded from public Gateway OpenAPI spec
        Assert.False(paths.TryGetProperty("/identity/openapi.json", out _));
        Assert.False(paths.TryGetProperty("/scheduling/openapi.json", out _));
        Assert.False(paths.TryGetProperty("/appointment/openapi.json", out _));
        Assert.False(paths.TryGetProperty("/queue/openapi.json", out _));
        Assert.False(paths.TryGetProperty("/medical-records/openapi.json", out _));
        Assert.False(paths.TryGetProperty("/notification/openapi.json", out _));

        // Anonymous operations must NOT require Bearer security
        Assert.False(HasBearerSecurityRequirement(loginPath.GetProperty("post")));
        Assert.False(HasBearerSecurityRequirement(forgotPwdPath.GetProperty("post")));
        Assert.False(HasBearerSecurityRequirement(resetPwdPath.GetProperty("post")));
        Assert.False(HasBearerSecurityRequirement(catalogPath.GetProperty("get")));
        Assert.False(HasBearerSecurityRequirement(doctorsPath.GetProperty("get")));
        Assert.False(HasBearerSecurityRequirement(specialtiesPath.GetProperty("get")));

        // Protected operations MUST require Bearer security
        Assert.True(HasBearerSecurityRequirement(authMePath.GetProperty("get")));
        Assert.True(HasBearerSecurityRequirement(changePwdPath.GetProperty("post")));
        Assert.True(HasBearerSecurityRequirement(appointmentsPath.GetProperty("post")));
        Assert.True(HasBearerSecurityRequirement(doctorsPath.GetProperty("post")));
        Assert.True(HasBearerSecurityRequirement(specialtiesPath.GetProperty("post")));
        Assert.True(HasBearerSecurityRequirement(employeesPath.GetProperty("get")));
        Assert.True(HasBearerSecurityRequirement(shiftsPath.GetProperty("get")));

        // 4. Headers: X-Correlation-ID & Idempotency-Key
        Assert.True(HasHeaderParameter(appointmentsPath.GetProperty("post"), "X-Correlation-ID"));
        Assert.True(HasHeaderParameter(appointmentsPath.GetProperty("post"), "Idempotency-Key"));
        var checkInOp = paths.GetProperty("/appointments/{appointmentId}/check-in").GetProperty("post");
        Assert.True(HasHeaderParameter(checkInOp, "Idempotency-Key", required: true));

        // Verify canonical AppointmentId encounter key is used on MedicalRecords routes (never {visitId})
        Assert.True(paths.TryGetProperty("/medical-records/encounters/{appointmentId}/draft", out _));
        Assert.True(paths.TryGetProperty("/medical-records/encounters/{appointmentId}", out _));
        Assert.False(paths.TryGetProperty("/medical-records/encounters/{visitId}/draft", out _));
        Assert.False(paths.TryGetProperty("/medical-records/encounters/{visitId}", out _));

        // 5. Compare against fixed reviewed baseline (zero unreviewed drift)
        using var baselineDoc = ReadReviewedBaseline("gateway-openapi-v1.json");
        var report = OpenApiContractDiffer.Compare(baselineDoc.RootElement, root);
        Assert.True(report.BreakingChanges.Count == 0, $"Gateway OpenAPI spec has breaking drift vs baseline:\n{string.Join("\n", report.BreakingChanges)}");
        Assert.True(report.AdditiveChanges.Count == 0, $"Gateway OpenAPI spec has unreviewed additive drift vs baseline:\n{string.Join("\n", report.AdditiveChanges)}");
    }

    [Theory]
    [InlineData("Identity", "/identity/openapi.json", "identity-openapi-v1.json", "Clinic Identity Service API (Internal)", "/v1/auth/login", "/v1/internal/users")]
    [InlineData("Scheduling", "/scheduling/openapi.json", "scheduling-openapi-v1.json", "Clinic Staff & Scheduling Service API (Internal)", "/doctors", "/internal/events/user-profile-updated")]
    [InlineData("Appointment", "/appointment/openapi.json", "appointment-openapi-v1.json", "Clinic Appointment Service API (Internal)", "/appointments", "/internal/events/exam-completed")]
    [InlineData("Queue", "/queue/openapi.json", "queue-openapi-v1.json", "Clinic Queue Service API (Internal)", "/queue/call-next/{doctorId}", "/admin/dashboard-metrics")]
    [InlineData("MedicalRecords", "/medical-records/openapi.json", "medical-records-openapi-v1.json", "Clinic Medical Records Service API (Internal)", "/patients", "/internal/patients/resolve-registration")]
    [InlineData("Notification", "/notification/openapi.json", "notification-openapi-v1.json", "Clinic Notification Service API (Internal)", "/notifications", "/internal/notifications/deliveries/dead-lettered")]
    public async Task Microservice_RealOpenApiSpecs_AreValid_MatchBaselines_AndEnforceInternalVisibilityOnGateway(
        string serviceKey,
        string gatewayProxyUrl,
        string snapshotFileName,
        string expectedTitle,
        string expectedBusinessPath,
        string expectedInternalPath)
    {
        // 1. Direct Service Port Boundary Protection:
        //    - Anonymous direct request to /openapi/v1.json -> 401 Unauthorized
        //    - Invalid service token to /openapi/v1.json -> 401 Unauthorized
        using var anonymousDirectClient = RealMicroserviceOpenApiRegistry.CreateAnonymousServiceClient(serviceKey);
        var directAnonResponse = await anonymousDirectClient.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.Unauthorized, directAnonResponse.StatusCode);

        using var wrongTokenReq = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        wrongTokenReq.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", "forged-wrong-token");
        wrongTokenReq.Headers.TryAddWithoutValidation("X-Identity-Service-Token", "forged-wrong-token");
        var wrongTokenResponse = await anonymousDirectClient.SendAsync(wrongTokenReq);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTokenResponse.StatusCode);

        // 2. Gateway Proxy Visibility & Access Policy:
        //    - Anonymous -> 401 Unauthorized
        //    - Non-Admin (Patient) -> 403 Forbidden
        //    - Admin -> 200 OK (Gateway attaches internal service token to fetch from downstream service)
        var anonResponse = await _anonymousClient.GetAsync(gatewayProxyUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);

        using var patientClient = _factory.CreateRoleClient("Patient");
        var patientResponse = await patientClient.GetAsync(gatewayProxyUrl);
        Assert.Equal(HttpStatusCode.Forbidden, patientResponse.StatusCode);

        // 3. Authorized direct service request (with internal service token) & Admin Gateway proxy request
        using var directServiceClient = RealMicroserviceOpenApiRegistry.CreateServiceClient(serviceKey);
        var directResponse = await directServiceClient.GetAsync("/openapi/v1.json");
        var directJson = await directResponse.Content.ReadAsStringAsync();
        Assert.True(directResponse.StatusCode == HttpStatusCode.OK, $"{serviceKey} /openapi/v1.json returned {(int)directResponse.StatusCode}: {directJson}");

        var adminResponse = await _adminClient.GetAsync(gatewayProxyUrl);
        var proxiedJson = await adminResponse.Content.ReadAsStringAsync();
        Assert.True(adminResponse.StatusCode == HttpStatusCode.OK, $"Gateway proxy {gatewayProxyUrl} returned {(int)adminResponse.StatusCode}: {proxiedJson}");
        Assert.Equal(directJson, proxiedJson);

        // Save to dedicated per-service snapshot only when UPDATE_CONTRACT_SNAPSHOTS=true
        WriteSnapshotIfRequested(snapshotFileName, directJson);

        using var serviceDoc = JsonDocument.Parse(directJson);
        var root = serviceDoc.RootElement;

        Assert.StartsWith("3.", root.GetProperty("openapi").GetString()!);
        var info = root.GetProperty("info");
        Assert.Equal(expectedTitle, info.GetProperty("title").GetString());
        Assert.Equal("1.0.0", info.GetProperty("version").GetString());

        var securitySchemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(securitySchemes.TryGetProperty("Bearer", out _));

        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/healthz", out _));
        Assert.True(paths.TryGetProperty("/readyz", out _));
        Assert.True(paths.TryGetProperty(expectedBusinessPath, out _), $"Expected path '{expectedBusinessPath}' in {serviceKey} OpenAPI spec.");
        Assert.True(paths.TryGetProperty(expectedInternalPath, out _), $"Expected internal path '{expectedInternalPath}' in {serviceKey} OpenAPI spec.");
        if (string.Equals(serviceKey, "MedicalRecords", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(paths.TryGetProperty("/medical-records/encounters/{appointmentId}/draft", out _));
            Assert.True(paths.TryGetProperty("/medical-records/encounters/{appointmentId}", out _));
            Assert.False(paths.TryGetProperty("/medical-records/encounters/{visitId}/draft", out _));
            Assert.False(paths.TryGetProperty("/medical-records/encounters/{visitId}", out _));
        }

        // 4. Compare runtime microservice OpenAPI spec against its dedicated reviewed baseline file
        using var baselineDoc = ReadReviewedBaseline(snapshotFileName);
        var driftReport = OpenApiContractDiffer.Compare(baselineDoc.RootElement, root);
        Assert.True(driftReport.BreakingChanges.Count == 0, $"{serviceKey} OpenAPI spec has breaking drift vs {snapshotFileName}:\n{string.Join("\n", driftReport.BreakingChanges)}");
        Assert.True(driftReport.AdditiveChanges.Count == 0, $"{serviceKey} OpenAPI spec has unreviewed additive drift vs {snapshotFileName}:\n{string.Join("\n", driftReport.AdditiveChanges)}");
    }

    [Fact]
    public async Task ApiInventoryMarkdown_ReconcilesWith_RealGatewayAndServiceOpenApiSpecs()
    {
        var inventoryPath = GetInventoryFilePath();
        Assert.True(File.Exists(inventoryPath), $"API inventory file not found at {inventoryPath}");
        var markdown = await File.ReadAllTextAsync(inventoryPath);

        var serviceDocs = new Dictionary<string, JsonDocument>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var svc in new[] { "Identity", "Scheduling", "Appointment", "Queue", "MedicalRecords", "Notification" })
            {
                using var client = RealMicroserviceOpenApiRegistry.CreateServiceClient(svc);
                serviceDocs[svc] = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
            }

            // 1. Parse Gateway public route table from API_INVENTORY_MEMBER_A.md
            var gatewayTableRows = ParseMarkdownTableSection(markdown, "<!-- GATEWAY_PUBLIC_ROUTES_START -->", "<!-- GATEWAY_PUBLIC_ROUTES_END -->");
            Assert.NotEmpty(gatewayTableRows);

            using var gatewayDoc = JsonDocument.Parse(await _anonymousClient.GetStringAsync("/openapi/v1.json"));
            var gatewayPaths = gatewayDoc.RootElement.GetProperty("paths");

            // 1A. Every documented Gateway route+method must exist in the real Gateway OpenAPI spec,
            //     AND if Service Owner is one of the 6 microservices, its Downstream Route must exist in that service's OpenAPI spec!
            var documentedGatewayOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in gatewayTableRows)
            {
                var path = row[0].Trim('`', ' ');
                var method = row[1].Trim('`', ' ').ToLowerInvariant();
                var ownerService = row[2].Trim('`', ' ');
                var downstreamRoute = row[3].Trim('`', ' ');
                var normalizedKey = $"{method.ToUpperInvariant()} {NormalizeRouteTemplate(path)}";
                documentedGatewayOperations.Add(normalizedKey);

                Assert.True(
                    TryFindMatchingPath(gatewayPaths, path, out var pathElement),
                    $"Documented Gateway route '{path}' in API_INVENTORY_MEMBER_A.md is missing from real Gateway OpenAPI spec.");
                Assert.True(
                    pathElement.TryGetProperty(method, out _),
                    $"Documented HTTP method '{method.ToUpperInvariant()}' for '{path}' in API_INVENTORY_MEMBER_A.md is missing from real Gateway OpenAPI spec.");

                // Automatically verify Downstream Route against Service Owner's real OpenAPI spec
                if (serviceDocs.TryGetValue(ownerService, out var ownerDoc))
                {
                    var ownerPaths = ownerDoc.RootElement.GetProperty("paths");
                    Assert.True(
                        TryFindMatchingPath(ownerPaths, downstreamRoute, out var ownerPathElem),
                        $"Gateway public route '{method.ToUpperInvariant()} {path}' maps to downstream route '{downstreamRoute}' on owner service '{ownerService}', which does not exist in {ownerService}'s real OpenAPI spec.");
                    Assert.True(
                        ownerPathElem.TryGetProperty(method, out _),
                        $"Gateway public route '{method.ToUpperInvariant()} {path}' maps to downstream '{method.ToUpperInvariant()} {downstreamRoute}' on '{ownerService}', but method '{method.ToUpperInvariant()}' is missing in {ownerService}'s real OpenAPI spec.");
                }
                else
                {
                    Assert.Equal("Gateway", ownerService);
                }
            }

            // 1B. Reverse check: Every operation in the real Gateway OpenAPI spec must be documented in API_INVENTORY_MEMBER_A.md
            foreach (var pathProp in gatewayPaths.EnumerateObject())
            {
                foreach (var opProp in pathProp.Value.EnumerateObject())
                {
                    if (!IsHttpMethod(opProp.Name)) continue;
                    var key = $"{opProp.Name.ToUpperInvariant()} {NormalizeRouteTemplate(pathProp.Name)}";
                    Assert.True(
                        documentedGatewayOperations.Contains(key),
                        $"Real Gateway OpenAPI operation '{opProp.Name.ToUpperInvariant()} {pathProp.Name}' is undocumented in API_INVENTORY_MEMBER_A.md.");
                }
            }

            // 2. Two-Way Reconciliation for all 6 Microservices (Section 3 of API_INVENTORY_MEMBER_A.md)
            var internalRows = ParseMarkdownTableSection(markdown, "<!-- SERVICE_INTERNAL_ROUTES_START -->", "<!-- SERVICE_INTERNAL_ROUTES_END -->");
            Assert.NotEmpty(internalRows);

            var documentedServiceOperations = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var svc in serviceDocs.Keys)
            {
                documentedServiceOperations[svc] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // 2A. Forward check: Every row in SERVICE_INTERNAL_ROUTES must exist in that service's real OpenAPI spec
            foreach (var row in internalRows)
            {
                var serviceName = row[0].Trim('`', ' ');
                var endpointPath = row[1].Trim('`', ' ');
                var method = row[2].Trim('`', ' ').ToLowerInvariant();

                Assert.True(serviceDocs.ContainsKey(serviceName), $"Unknown service '{serviceName}' in SERVICE_INTERNAL_ROUTES table.");
                documentedServiceOperations[serviceName].Add($"{method.ToUpperInvariant()} {NormalizeRouteTemplate(endpointPath)}");

                var svcPaths = serviceDocs[serviceName].RootElement.GetProperty("paths");
                Assert.True(
                    TryFindMatchingPath(svcPaths, endpointPath, out var svcPathElem),
                    $"Documented route '{endpointPath}' for service '{serviceName}' is missing from real {serviceName} OpenAPI spec.");
                Assert.True(
                    svcPathElem.TryGetProperty(method, out _),
                    $"Documented method '{method.ToUpperInvariant()}' on '{endpointPath}' for service '{serviceName}' is missing from real {serviceName} OpenAPI spec.");
            }

            // 2B. Reverse check: Every operation in each of the 6 services' real OpenAPI specs must be documented in SERVICE_INTERNAL_ROUTES
            foreach (var (serviceName, svcDoc) in serviceDocs)
            {
                var svcPaths = svcDoc.RootElement.GetProperty("paths");
                foreach (var pathProp in svcPaths.EnumerateObject())
                {
                    foreach (var opProp in pathProp.Value.EnumerateObject())
                    {
                        if (!IsHttpMethod(opProp.Name)) continue;
                        var key = $"{opProp.Name.ToUpperInvariant()} {NormalizeRouteTemplate(pathProp.Name)}";
                        Assert.True(
                            documentedServiceOperations[serviceName].Contains(key),
                            $"Real {serviceName} OpenAPI operation '{opProp.Name.ToUpperInvariant()} {pathProp.Name}' is undocumented in SERVICE_INTERNAL_ROUTES of API_INVENTORY_MEMBER_A.md.");
                    }
                }
            }
        }
        finally
        {
            foreach (var doc in serviceDocs.Values) doc.Dispose();
        }
    }

    [Fact]
    public async Task Gateway_WireResponses_Match_DeclaredOpenApiSchemas_ForWrappedAndProxiedEndpoints()
    {
        using var gatewaySpec = JsonDocument.Parse(await _anonymousClient.GetStringAsync("/openapi/v1.json"));
        var paths = gatewaySpec.RootElement.GetProperty("paths");
        var schemas = gatewaySpec.RootElement.GetProperty("components").GetProperty("schemas");

        // =====================================================================
        // Group 1: Wrapped Controller Success Responses (ApiResponse<T> envelope)
        // =====================================================================
        // 1.1 GET /api/catalog (Anonymous wrapped controller)
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/api/catalog").GetProperty("get").GetProperty("responses").GetProperty("200"), schemas),
            schemas);
        var catalogWire = await _anonymousClient.GetAsync("/api/catalog");
        Assert.Equal(HttpStatusCode.OK, catalogWire.StatusCode);
        using (var doc = JsonDocument.Parse(await catalogWire.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(doc.RootElement, expectSuccess: true);
        }

        // 1.2 POST /auth/login (Anonymous wrapped controller)
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/auth/login").GetProperty("post").GetProperty("responses").GetProperty("200"), schemas),
            schemas);
        var loginWireResponse = await _anonymousClient.PostAsJsonAsync("/auth/login", new { username = "patient", password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, loginWireResponse.StatusCode);
        using (var loginWireJson = JsonDocument.Parse(await loginWireResponse.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(loginWireJson.RootElement, expectSuccess: true);
            Assert.True(loginWireJson.RootElement.GetProperty("result").TryGetProperty("accessToken", out _));
            Assert.True(loginWireJson.RootElement.GetProperty("result").TryGetProperty("roles", out _));
        }

        // 1.3 GET /auth/me (Authenticated wrapped controller)
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/auth/me").GetProperty("get").GetProperty("responses").GetProperty("200"), schemas),
            schemas);
        var meWire = await _adminClient.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meWire.StatusCode);
        using (var doc = JsonDocument.Parse(await meWire.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(doc.RootElement, expectSuccess: true);
            Assert.True(doc.RootElement.GetProperty("result").TryGetProperty("username", out _));
        }

        // 1.4 GET /auth/permissions (Authenticated wrapped controller)
        var permsWireResponse = await _adminClient.GetAsync("/auth/permissions");
        Assert.Equal(HttpStatusCode.OK, permsWireResponse.StatusCode);
        using (var permsWireJson = JsonDocument.Parse(await permsWireResponse.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(permsWireJson.RootElement, expectSuccess: true);
            Assert.Equal(JsonValueKind.Array, permsWireJson.RootElement.GetProperty("result").ValueKind);
        }

        // 1.5 POST /forgot-password (Anonymous wrapped controller returning ApiResponse<ApiMessageResult>)
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/forgot-password").GetProperty("post").GetProperty("responses").GetProperty("200"), schemas),
            schemas);
        var forgotWire = await _anonymousClient.PostAsJsonAsync("/forgot-password", new { phoneNumberOrEmail = "patient@clinic.test" });
        Assert.Equal(HttpStatusCode.OK, forgotWire.StatusCode);
        using (var doc = JsonDocument.Parse(await forgotWire.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(doc.RootElement, expectSuccess: true);
            Assert.True(doc.RootElement.GetProperty("result").TryGetProperty("message", out _));
        }

        // 1.6 GET /admin/ping, GET /admin/roles, GET /admin/users (Admin wrapped controllers)
        foreach (var adminWrappedPath in new[] { "/admin/ping", "/admin/roles", "/admin/users" })
        {
            AssertEnvelopeSchemaHasProperties(
                ResolveResponseSchema(paths.GetProperty(adminWrappedPath).GetProperty("get").GetProperty("responses").GetProperty("200"), schemas),
                schemas);
            var adminWire = await _adminClient.GetAsync(adminWrappedPath);
            Assert.Equal(HttpStatusCode.OK, adminWire.StatusCode);
            using var doc = JsonDocument.Parse(await adminWire.Content.ReadAsStringAsync());
            AssertWireMatchesApiResponseEnvelope(doc.RootElement, expectSuccess: true);
        }

        // =====================================================================
        // Group 2: Wrapped Controller Validation, Header & Auth Errors (400 / 401 / 403)
        // =====================================================================
        // 2.1 POST /appointments/{id}/check-in without Idempotency-Key -> 400 BadRequest wrapped in ApiResponse
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/appointments/{appointmentId}/check-in").GetProperty("post").GetProperty("responses").GetProperty("400"), schemas),
            schemas);
        var checkInWireResponse = await _adminClient.PostAsync($"/appointments/{Guid.NewGuid()}/check-in", null);
        Assert.Equal(HttpStatusCode.BadRequest, checkInWireResponse.StatusCode);
        using (var checkInWireJson = JsonDocument.Parse(await checkInWireResponse.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(checkInWireJson.RootElement, expectSuccess: false);
            Assert.Contains(
                checkInWireJson.RootElement.GetProperty("errorMessages").EnumerateArray().Select(x => x.GetString()),
                msg => msg != null && msg.Contains("Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        }

        // 2.2 POST /schedules/{scheduleId}/reservations without Idempotency-Key -> 400 BadRequest wrapped in ApiResponse
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/schedules/{scheduleId}/reservations").GetProperty("post").GetProperty("responses").GetProperty("400"), schemas),
            schemas);
        var reserveBadWire = await _adminClient.PostAsJsonAsync($"/schedules/{Guid.NewGuid()}/reservations", new { timeSlot = "09:00:00", patientId = ApiContractFactory.PatientId });
        Assert.Equal(HttpStatusCode.BadRequest, reserveBadWire.StatusCode);
        using (var doc = JsonDocument.Parse(await reserveBadWire.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(doc.RootElement, expectSuccess: false);
        }

        // 2.3 POST /medical-records/reports/{reportId}/finalize without Idempotency-Key -> 400 BadRequest wrapped in ApiResponse
        AssertEnvelopeSchemaHasProperties(
            ResolveResponseSchema(paths.GetProperty("/medical-records/reports/{reportId}/finalize").GetProperty("post").GetProperty("responses").GetProperty("400"), schemas),
            schemas);
        var finalizeBadWire = await _adminClient.PostAsync($"/medical-records/reports/{Guid.NewGuid()}/finalize", null);
        Assert.Equal(HttpStatusCode.BadRequest, finalizeBadWire.StatusCode);
        using (var doc = JsonDocument.Parse(await finalizeBadWire.Content.ReadAsStringAsync()))
        {
            AssertWireMatchesApiResponseEnvelope(doc.RootElement, expectSuccess: false);
        }

        // 2.4 Auth error: 401 Unauthorized on anonymous call & 403 Forbidden on insufficient role
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymousClient.GetAsync("/auth/me")).StatusCode);
        using var patientClient = _factory.CreateRoleClient("Patient");
        Assert.Equal(HttpStatusCode.Forbidden, (await patientClient.GetAsync("/admin/ping")).StatusCode);

        // =====================================================================
        // Group 3: Raw Downstream Proxy Responses (SchedulingProxyMiddleware & ContentResult)
        // =====================================================================
        // 3.1 GET /specialties -> paged downstream envelope { isSuccess, result: { items, ... } }
        var specialtiesSpecSchema = ResolveResponseSchema(paths.GetProperty("/specialties").GetProperty("get").GetProperty("responses").GetProperty("200"), schemas);
        Assert.True(specialtiesSpecSchema.GetProperty("properties").TryGetProperty("isSuccess", out _));
        Assert.True(specialtiesSpecSchema.GetProperty("properties").TryGetProperty("result", out _));
        var specialtiesWire = await _anonymousClient.GetAsync("/specialties");
        Assert.Equal(HttpStatusCode.OK, specialtiesWire.StatusCode);
        using (var doc = JsonDocument.Parse(await specialtiesWire.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("result").GetProperty("items").ValueKind);
        }

        // 3.2 GET /doctors -> paged downstream envelope { isSuccess, result: { items, ... } }
        var doctorsWire = await _anonymousClient.GetAsync("/doctors");
        Assert.Equal(HttpStatusCode.OK, doctorsWire.StatusCode);
        using (var doc = JsonDocument.Parse(await doctorsWire.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("result").GetProperty("items").ValueKind);
        }

        // 3.3 GET /doctors/available -> raw JSON array
        var availDoctorsSchema = ResolveResponseSchema(paths.GetProperty("/doctors/available").GetProperty("get").GetProperty("responses").GetProperty("200"), schemas);
        Assert.Equal("array", availDoctorsSchema.GetProperty("type").GetString());
        var availDoctorsWire = await _anonymousClient.GetAsync("/doctors/available");
        Assert.Equal(HttpStatusCode.OK, availDoctorsWire.StatusCode);
        using (var doc = JsonDocument.Parse(await availDoctorsWire.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        }

        // 3.4 GET /doctors/dashboard-stats -> raw JSON object { totalDoctors, activeDoctors, ... }
        var statsWireResponse = await _anonymousClient.GetAsync("/doctors/dashboard-stats");
        Assert.Equal(HttpStatusCode.OK, statsWireResponse.StatusCode);
        using (var statsWireJson = JsonDocument.Parse(await statsWireResponse.Content.ReadAsStringAsync()))
        {
            Assert.True(statsWireJson.RootElement.TryGetProperty("totalDoctors", out _));
            Assert.True(statsWireJson.RootElement.TryGetProperty("activeDoctors", out _));
        }

        // 3.5 GET /slots, GET /patients/me, GET /notifications/unread-count -> raw downstream JSON
        var slotsWire = await _anonymousClient.GetAsync("/slots");
        Assert.Equal(HttpStatusCode.OK, slotsWire.StatusCode);
        using (var doc = JsonDocument.Parse(await slotsWire.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("result").ValueKind);
        }

        var patientMeWire = await _adminClient.GetAsync("/patients/me");
        Assert.Equal(HttpStatusCode.OK, patientMeWire.StatusCode);
        using (var doc = JsonDocument.Parse(await patientMeWire.Content.ReadAsStringAsync()))
        {
            Assert.True(doc.RootElement.TryGetProperty("fullName", out _));
        }

        var unreadWire = await _adminClient.GetAsync("/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, unreadWire.StatusCode);
        using (var doc = JsonDocument.Parse(await unreadWire.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc.RootElement.GetProperty("unreadCount").GetInt32());
        }

        // =====================================================================
        // Group 4: Empty / 204 No Content & 404 Not Found Proxy Responses
        // =====================================================================
        var deactivate204Wire = await _adminClient.PatchAsync($"/specialties/{Guid.NewGuid()}/status", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate204Wire.StatusCode);
        Assert.Equal(string.Empty, await deactivate204Wire.Content.ReadAsStringAsync());

        var deactivate404Wire = await _adminClient.PatchAsync($"/specialties/{Guid.Empty}/status", null);
        Assert.Equal(HttpStatusCode.NotFound, deactivate404Wire.StatusCode);

        var myQueue204Wire = await _adminClient.GetAsync("/me/queue-status");
        Assert.Equal(HttpStatusCode.NoContent, myQueue204Wire.StatusCode);
        var myQueue204Body = await myQueue204Wire.Content.ReadAsStringAsync();
        if (!string.IsNullOrEmpty(myQueue204Body))
        {
            using var noContentEnvelope = JsonDocument.Parse(myQueue204Body);
            Assert.True(noContentEnvelope.RootElement.GetProperty("isSuccess").GetBoolean());
        }

        // =====================================================================
        // Group 5: Exhaustive Static Schema Audit across all Gateway Operations
        // =====================================================================
        foreach (var pathProp in paths.EnumerateObject())
        {
            foreach (var opProp in pathProp.Value.EnumerateObject())
            {
                if (!IsHttpMethod(opProp.Name)) continue;
                var responses = opProp.Value.GetProperty("responses");
                foreach (var respProp in responses.EnumerateObject())
                {
                    if (respProp.Name == "204")
                    {
                        Assert.False(
                            respProp.Value.TryGetProperty("content", out _),
                            $"Operation '{opProp.Name.ToUpperInvariant()} {pathProp.Name}' declares a 204 NoContent response with body content.");
                    }
                    else if (respProp.Value.TryGetProperty("content", out var contentElem) &&
                             contentElem.TryGetProperty("application/json", out var jsonMedia) &&
                             jsonMedia.TryGetProperty("schema", out var schemaElem) &&
                             schemaElem.TryGetProperty("$ref", out var refElem))
                    {
                        Assert.NotEqual(
                            "#/components/schemas/ProblemDetails",
                            refElem.GetString());
                    }
                }
            }
        }
    }

    [Fact]
    public void ContractDrift_DetectsBreakingChanges_AgainstRealOpenApiBaseline()
    {
        using var baselineDoc = ReadReviewedBaseline("gateway-openapi-v1.json");
        var candidateNode = JsonNode.Parse(baselineDoc.RootElement.GetRawText())!.AsObject();

        // Mutation 1: Remove an essential operation (POST /queue/{queueTicketId}/start-exam)
        candidateNode["paths"]![
            "/queue/{queueTicketId}/start-exam"]!.AsObject().Remove("post");

        // Mutation 2: Weaken security on a protected endpoint (GET /auth/me loses Bearer requirement)
        candidateNode["paths"]!["/auth/me"]!["get"]!["security"] = new JsonArray();

        // Mutation 3: Remove required header parameter Idempotency-Key from POST /appointments/{appointmentId}/check-in
        var checkInParams = candidateNode["paths"]!["/appointments/{appointmentId}/check-in"]!["post"]!["parameters"]!.AsArray();
        for (var i = checkInParams.Count - 1; i >= 0; i--)
        {
            if (string.Equals(checkInParams[i]?["name"]?.GetValue<string>(), "Idempotency-Key", StringComparison.OrdinalIgnoreCase))
            {
                checkInParams.RemoveAt(i);
            }
        }

        // Mutation 4: Remove a documented 200 response from GET /appointments/{appointmentId}
        candidateNode["paths"]!["/appointments/{appointmentId}"]!["get"]!["responses"]!.AsObject().Remove("200");

        // Mutation 5: Add a new REQUIRED field to requestBody schema of POST /appointments
        var createApptSchema = candidateNode["components"]!["schemas"]!["CreateAppointmentRequest"]!.AsObject();
        var createApptRequired = createApptSchema["required"]?.AsArray() ?? new JsonArray();
        createApptRequired.Add("mandatoryInsuranceNumber");
        createApptSchema["required"] = createApptRequired;
        createApptSchema["properties"]!.AsObject()["mandatoryInsuranceNumber"] = JsonNode.Parse("""{ "type": "string" }""");

        // Mutation 6: Remove a response property ('gatewayVersion') from ApiCatalogResponse and change type of 'openApiSpecUrl' from string to integer
        var catalogSchemaProps = candidateNode["components"]!["schemas"]!["ApiCatalogResponse"]!["properties"]!.AsObject();
        catalogSchemaProps.Remove("gatewayVersion");
        catalogSchemaProps["openApiSpecUrl"]!["type"] = "integer";

        // Mutation 7: Remove 'application/json' content type from POST /auth/login 200 response
        candidateNode["paths"]!["/auth/login"]!["post"]!["responses"]!["200"]!["content"]!.AsObject().Remove("application/json");

        // Mutation 8: Change property 'format' on CreateAppointmentRequest.workScheduleId from 'uuid' to 'int64'
        candidateNode["components"]!["schemas"]!["CreateAppointmentRequest"]!["properties"]!["workScheduleId"]!["format"] = "int64";

        // Mutation 9: Tighten 'maxLength' on LoginRequest.username (100 -> 20) and 'minLength' on CreateDoctorRequest.password (8 -> 16)
        candidateNode["components"]!["schemas"]!["LoginRequest"]!["properties"]!["username"]!["maxLength"] = 20;
        candidateNode["components"]!["schemas"]!["CreateDoctorRequest"]!["properties"]!["password"]!["minLength"] = 16;

        // Mutation 10: Disallow additionalProperties on LoginRequest and remove an allowed enum value on Gender
        candidateNode["components"]!["schemas"]!["LoginRequest"]!.AsObject()["additionalProperties"] = false;
        baselineDoc.RootElement.TryGetProperty("components", out _);
        var baselineMutable = JsonNode.Parse(baselineDoc.RootElement.GetRawText())!.AsObject();
        baselineMutable["components"]!["schemas"]!["Gender"]!.AsObject()["enum"] = JsonNode.Parse("""[0, 1, 2]""");
        candidateNode["components"]!["schemas"]!["Gender"]!.AsObject()["enum"] = JsonNode.Parse("""[0, 1]""");

        // Mutation 11: Multi-branch oneOf composition where the 2nd branch has a breaking change and a 3rd branch is removed
        baselineMutable["components"]!["schemas"]!["CreateAppointmentRequest"]!["properties"]!.AsObject()["polymorphicTarget"] = JsonNode.Parse("""
        {
          "description": "Composite target",
          "oneOf": [
            { "type": "null" },
            { "$ref": "#/components/schemas/ForgotPasswordRequest" },
            { "$ref": "#/components/schemas/UpdateAppointmentRequest" },
            { "$ref": "#/components/schemas/ReserveSlotRequest" }
          ]
        }
        """);
        candidateNode["components"]!["schemas"]!["CreateAppointmentRequest"]!["properties"]!.AsObject()["polymorphicTarget"] = JsonNode.Parse("""
        {
          "description": "Composite target",
          "oneOf": [
            { "type": "null" },
            { "$ref": "#/components/schemas/ForgotPasswordRequest" },
            { "$ref": "#/components/schemas/UpdateAppointmentRequest" }
          ]
        }
        """);
        // Mutate a property inside the 2nd non-null branch (UpdateAppointmentRequest.reason pattern)
        candidateNode["components"]!["schemas"]!["UpdateAppointmentRequest"]!["properties"]!["reason"]!.AsObject()["pattern"] = "^[A-Z]{5,}$";

        using var mutatedBaselineDoc = JsonDocument.Parse(baselineMutable.ToJsonString());
        using var candidateDoc = JsonDocument.Parse(candidateNode.ToJsonString());
        var report = OpenApiContractDiffer.Compare(mutatedBaselineDoc.RootElement, candidateDoc.RootElement);

        Assert.NotEmpty(report.BreakingChanges);
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed operation: POST /queue/{queueTicketId}/start-exam", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed security requirement 'Bearer' on GET /auth/me", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed parameter 'header:Idempotency-Key' on POST /appointments/{appointmentId}/check-in", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed response status '200' on GET /appointments/{appointmentId}", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Added required request property 'mandatoryInsuranceNumber'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed response property 'gatewayVersion'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Changed property type on 'openApiSpecUrl'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed response content type 'application/json' on POST /auth/login [200]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Changed schema format from 'uuid' to 'int64'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Tightened request maxLength from '100' to '20'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Tightened request minLength from '8' to '16'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Disallowed additionalProperties on request schema", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed allowed request enum value '2'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Removed composition branch '#/components/schemas/ReserveSlotRequest' from oneOf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.BreakingChanges, c => c.Contains("Added restrictive request pattern '^[A-Z]{5,}$'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContractDrift_AllowsAdditiveChanges_AgainstRealOpenApiBaseline_WithoutBreaking()
    {
        using var baselineDoc = ReadReviewedBaseline("gateway-openapi-v1.json");
        var candidateNode = JsonNode.Parse(baselineDoc.RootElement.GetRawText())!.AsObject();

        // Additive 1: Add a new non-breaking endpoint GET /appointments/export
        candidateNode["paths"]!["/appointments/export"] = JsonNode.Parse("""
        {
          "get": {
            "summary": "Export appointments",
            "parameters": [
              {
                "name": "X-Correlation-ID",
                "in": "header",
                "required": false,
                "schema": { "type": "string" }
              }
            ],
            "responses": {
              "200": { "description": "OK" }
            },
            "security": [
              { "Bearer": [] }
            ]
          }
        }
        """);

        // Additive 2: Add an optional query parameter to GET /appointments/{appointmentId}
        var getApptParams = candidateNode["paths"]!["/appointments/{appointmentId}"]!["get"]!["parameters"]!.AsArray();
        getApptParams.Add(JsonNode.Parse("""
        {
          "name": "includeAuditPreview",
          "in": "query",
          "required": false,
          "schema": { "type": "boolean" }
        }
        """));

        // Additive 3: Add an optional property to requestBody schema (CreateAppointmentRequest)
        var createApptProps = candidateNode["components"]!["schemas"]!["CreateAppointmentRequest"]!["properties"]!.AsObject();
        createApptProps["optionalReferralCode"] = JsonNode.Parse("""{ "type": "string", "nullable": true }""");

        // Additive 4: Add a new property to response schema (ApiCatalogResponse)
        var catalogProps = candidateNode["components"]!["schemas"]!["ApiCatalogResponse"]!["properties"]!.AsObject();
        catalogProps["buildCommitSha"] = JsonNode.Parse("""{ "type": "string" }""");

        // Additive 5: Relax request maxLength on LoginRequest.username (100 -> 150)
        candidateNode["components"]!["schemas"]!["LoginRequest"]!["properties"]!["username"]!["maxLength"] = 150;

        using var candidateDoc = JsonDocument.Parse(candidateNode.ToJsonString());
        var report = OpenApiContractDiffer.Compare(baselineDoc.RootElement, candidateDoc.RootElement);

        Assert.Empty(report.BreakingChanges);
        Assert.NotEmpty(report.AdditiveChanges);
        Assert.Contains(report.AdditiveChanges, c => c.Contains("Added path: /appointments/export", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.AdditiveChanges, c => c.Contains("Added optional parameter 'query:includeAuditPreview' on GET /appointments/{appointmentId}", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.AdditiveChanges, c => c.Contains("Added optional request property 'optionalReferralCode'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.AdditiveChanges, c => c.Contains("Added response property 'buildCommitSha'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.AdditiveChanges, c => c.Contains("Relaxed request maxLength from '100' to '150'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CorrelationIdHeader_IsForwarded_And_ReflectedInResponse()
    {
        var testCid = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog");
        request.Headers.Add("X-Correlation-ID", testCid);

        var response = await _anonymousClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var cids));
        Assert.Contains(testCid, cids);
    }

    private static bool HasBearerSecurityRequirement(JsonElement operation)
    {
        if (!operation.TryGetProperty("security", out var securityArray) || securityArray.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in securityArray.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("Bearer", out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHeaderParameter(JsonElement operation, string headerName, bool? required = null)
    {
        if (!operation.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var param in parameters.EnumerateArray())
        {
            var isHeader = param.TryGetProperty("in", out var inProp) && string.Equals(inProp.GetString(), "header", StringComparison.OrdinalIgnoreCase);
            var matchesName = param.TryGetProperty("name", out var nameProp) && string.Equals(nameProp.GetString(), headerName, StringComparison.OrdinalIgnoreCase);
            if (isHeader && matchesName)
            {
                if (required.HasValue)
                {
                    var isReq = param.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.True;
                    return isReq == required.Value;
                }
                return true;
            }
        }

        return false;
    }

    private static JsonElement ResolveResponseSchema(JsonElement responseElement, JsonElement componentsSchemas)
    {
        var schema = responseElement.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var refProp))
        {
            var refName = refProp.GetString()!.Split('/').Last();
            return componentsSchemas.GetProperty(refName);
        }
        return schema;
    }

    private static void AssertEnvelopeSchemaHasProperties(JsonElement schema, JsonElement componentsSchemas)
    {
        var resolved = schema;
        if (resolved.TryGetProperty("$ref", out var refProp))
        {
            var refName = refProp.GetString()!.Split('/').Last();
            resolved = componentsSchemas.GetProperty(refName);
        }

        var props = resolved.GetProperty("properties");
        Assert.True(props.TryGetProperty("statusCode", out _));
        Assert.True(props.TryGetProperty("isSuccess", out _));
        Assert.True(props.TryGetProperty("errorMessages", out _));
        Assert.True(props.TryGetProperty("result", out _));
    }

    private static void AssertWireMatchesApiResponseEnvelope(JsonElement wireRoot, bool expectSuccess)
    {
        Assert.True(wireRoot.TryGetProperty("statusCode", out _));
        Assert.True(wireRoot.TryGetProperty("isSuccess", out var isSuccess));
        Assert.Equal(expectSuccess, isSuccess.GetBoolean());
        Assert.True(wireRoot.TryGetProperty("errorMessages", out var errors));
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.True(wireRoot.TryGetProperty("result", out _));
    }

    private static List<string[]> ParseMarkdownTableSection(string markdown, string startMarker, string endMarker)
    {
        var startIdx = markdown.IndexOf(startMarker, StringComparison.Ordinal);
        var endIdx = markdown.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(startIdx >= 0 && endIdx > startIdx, $"Markers '{startMarker}' .. '{endMarker}' not found in API_INVENTORY_MEMBER_A.md");

        var section = markdown[(startIdx + startMarker.Length)..endIdx];
        var lines = section.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('|') && l.EndsWith('|'))
            .ToList();

        var rows = new List<string[]>();
        foreach (var line in lines.Skip(2)) // Skip header and separator rows
        {
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            rows.Add(cells);
        }
        return rows;
    }

    private static string NormalizeRouteTemplate(string path)
    {
        var clean = "/" + path.Trim().Trim('/');
        // Normalize parameter names inside braces to {} so /patients/{id} and /patients/{patientId} match structurally
        return System.Text.RegularExpressions.Regex.Replace(clean, @"\{[^}]+\}", "{}").ToLowerInvariant();
    }

    private static bool TryFindMatchingPath(JsonElement pathsElement, string targetPath, out JsonElement matchedPathElement)
    {
        if (pathsElement.TryGetProperty(targetPath, out matchedPathElement))
        {
            return true;
        }

        var normalizedTarget = NormalizeRouteTemplate(targetPath);
        foreach (var prop in pathsElement.EnumerateObject())
        {
            if (NormalizeRouteTemplate(prop.Name) == normalizedTarget)
            {
                matchedPathElement = prop.Value;
                return true;
            }
        }

        matchedPathElement = default;
        return false;
    }

    private static bool IsHttpMethod(string name) =>
        name.Equals("get", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("post", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("put", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("patch", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("delete", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Structural & Schema-Aware OpenAPI v3 contract comparison engine that detects breaking vs. additive changes
/// across paths, operations, parameters, request bodies, media types, JSON schemas (properties, required fields, types),
/// responses, and security requirements.
/// </summary>
public static class OpenApiContractDiffer
{
    public sealed record DriftReport(IReadOnlyList<string> BreakingChanges, IReadOnlyList<string> AdditiveChanges);

    public static DriftReport Compare(JsonElement baselineRoot, JsonElement candidateRoot)
    {
        var breaking = new List<string>();
        var additive = new List<string>();

        var baselinePaths = baselineRoot.GetProperty("paths");
        var candidatePaths = candidateRoot.GetProperty("paths");

        foreach (var basePathProp in baselinePaths.EnumerateObject())
        {
            if (!candidatePaths.TryGetProperty(basePathProp.Name, out var candPathElem))
            {
                breaking.Add($"Removed path: {basePathProp.Name}");
                continue;
            }

            foreach (var baseOpProp in basePathProp.Value.EnumerateObject())
            {
                var method = baseOpProp.Name.ToUpperInvariant();
                if (!candPathElem.TryGetProperty(baseOpProp.Name, out var candOpElem))
                {
                    breaking.Add($"Removed operation: {method} {basePathProp.Name}");
                    continue;
                }

                CompareOperation(
                    $"{method} {basePathProp.Name}",
                    baseOpProp.Value,
                    candOpElem,
                    baselineRoot,
                    candidateRoot,
                    breaking,
                    additive);
            }

            foreach (var candOpProp in candPathElem.EnumerateObject())
            {
                if (!basePathProp.Value.TryGetProperty(candOpProp.Name, out _))
                {
                    additive.Add($"Added operation: {candOpProp.Name.ToUpperInvariant()} {basePathProp.Name}");
                }
            }
        }

        foreach (var candPathProp in candidatePaths.EnumerateObject())
        {
            if (!baselinePaths.TryGetProperty(candPathProp.Name, out _))
            {
                additive.Add($"Added path: {candPathProp.Name}");
            }
        }

        return new DriftReport(breaking, additive);
    }

    private static void CompareOperation(
        string opKey,
        JsonElement baseOp,
        JsonElement candOp,
        JsonElement baselineRoot,
        JsonElement candidateRoot,
        List<string> breaking,
        List<string> additive)
    {
        // 1. Parameters
        var baseParams = GetParametersMap(baseOp);
        var candParams = GetParametersMap(candOp);

        foreach (var (paramKey, baseParam) in baseParams)
        {
            if (!candParams.TryGetValue(paramKey, out var candParam))
            {
                breaking.Add($"Removed parameter '{paramKey}' on {opKey}");
                continue;
            }

            var baseReq = baseParam.TryGetProperty("required", out var br) && br.ValueKind == JsonValueKind.True;
            var candReq = candParam.TryGetProperty("required", out var cr) && cr.ValueKind == JsonValueKind.True;
            if (!baseReq && candReq)
            {
                breaking.Add($"Parameter '{paramKey}' became required on {opKey}");
            }

            if (baseParam.TryGetProperty("schema", out var baseParamSchema) &&
                candParam.TryGetProperty("schema", out var candParamSchema))
            {
                CompareSchema(
                    $"{opKey} param[{paramKey}]",
                    baseParamSchema,
                    candParamSchema,
                    baselineRoot,
                    candidateRoot,
                    isRequestContext: true,
                    new HashSet<string>(StringComparer.Ordinal),
                    breaking,
                    additive);
            }
        }

        foreach (var (paramKey, candParam) in candParams)
        {
            if (!baseParams.ContainsKey(paramKey))
            {
                var candReq = candParam.TryGetProperty("required", out var cr) && cr.ValueKind == JsonValueKind.True;
                if (candReq)
                {
                    breaking.Add($"Added required parameter '{paramKey}' on {opKey}");
                }
                else
                {
                    additive.Add($"Added optional parameter '{paramKey}' on {opKey}");
                }
            }
        }

        // 2. Security requirements
        var baseSec = GetSecuritySchemes(baseOp);
        var candSec = GetSecuritySchemes(candOp);
        foreach (var scheme in baseSec.Except(candSec, StringComparer.OrdinalIgnoreCase))
        {
            breaking.Add($"Removed security requirement '{scheme}' on {opKey}");
        }
        foreach (var scheme in candSec.Except(baseSec, StringComparer.OrdinalIgnoreCase))
        {
            breaking.Add($"Added new security requirement '{scheme}' on {opKey}");
        }

        // 3. RequestBody & Request Schemas
        var hasBaseReqBody = baseOp.TryGetProperty("requestBody", out var baseReqBody) && baseReqBody.ValueKind == JsonValueKind.Object;
        var hasCandReqBody = candOp.TryGetProperty("requestBody", out var candReqBody) && candReqBody.ValueKind == JsonValueKind.Object;

        if (!hasBaseReqBody && hasCandReqBody)
        {
            var isRequired = candReqBody.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.True;
            if (isRequired)
            {
                breaking.Add($"Added required requestBody on {opKey}");
            }
            else
            {
                additive.Add($"Added optional requestBody on {opKey}");
            }
        }
        else if (hasBaseReqBody && !hasCandReqBody)
        {
            breaking.Add($"Removed requestBody on {opKey}");
        }
        else if (hasBaseReqBody && hasCandReqBody)
        {
            var baseReq = baseReqBody.TryGetProperty("required", out var br) && br.ValueKind == JsonValueKind.True;
            var candReq = candReqBody.TryGetProperty("required", out var cr) && cr.ValueKind == JsonValueKind.True;
            if (!baseReq && candReq)
            {
                breaking.Add($"RequestBody became required on {opKey}");
            }

            CompareContentMediaTypes(
                $"{opKey} requestBody",
                baseReqBody,
                candReqBody,
                baselineRoot,
                candidateRoot,
                isRequestContext: true,
                breaking,
                additive);
        }

        // 4. Responses, Content Types & Response Schemas
        if (baseOp.TryGetProperty("responses", out var baseResponses))
        {
            var hasCandResponses = candOp.TryGetProperty("responses", out var candResponses);
            foreach (var statusProp in baseResponses.EnumerateObject())
            {
                if (!hasCandResponses || !candResponses.TryGetProperty(statusProp.Name, out var candStatusElem))
                {
                    breaking.Add($"Removed response status '{statusProp.Name}' on {opKey}");
                    continue;
                }

                CompareContentMediaTypes(
                    $"{opKey} [{statusProp.Name}]",
                    statusProp.Value,
                    candStatusElem,
                    baselineRoot,
                    candidateRoot,
                    isRequestContext: false,
                    breaking,
                    additive);
            }

            if (hasCandResponses)
            {
                foreach (var statusProp in candResponses.EnumerateObject())
                {
                    if (!baseResponses.TryGetProperty(statusProp.Name, out _))
                    {
                        additive.Add($"Added response status '{statusProp.Name}' on {opKey}");
                    }
                }
            }
        }
    }

    private static void CompareContentMediaTypes(
        string contextLabel,
        JsonElement baseContainer,
        JsonElement candContainer,
        JsonElement baselineRoot,
        JsonElement candidateRoot,
        bool isRequestContext,
        List<string> breaking,
        List<string> additive)
    {
        var hasBaseContent = baseContainer.TryGetProperty("content", out var baseContent) && baseContent.ValueKind == JsonValueKind.Object;
        var hasCandContent = candContainer.TryGetProperty("content", out var candContent) && candContent.ValueKind == JsonValueKind.Object;

        if (!hasBaseContent)
        {
            if (hasCandContent)
            {
                foreach (var mediaProp in candContent.EnumerateObject())
                {
                    additive.Add($"Added {(isRequestContext ? "request" : "response")} content type '{mediaProp.Name}' on {contextLabel}");
                }
            }
            return;
        }

        if (!hasCandContent)
        {
            foreach (var mediaProp in baseContent.EnumerateObject())
            {
                breaking.Add($"Removed {(isRequestContext ? "request" : "response")} content type '{mediaProp.Name}' on {contextLabel}");
            }
            return;
        }

        foreach (var baseMedia in baseContent.EnumerateObject())
        {
            if (!candContent.TryGetProperty(baseMedia.Name, out var candMediaElem))
            {
                breaking.Add($"Removed {(isRequestContext ? "request" : "response")} content type '{baseMedia.Name}' on {contextLabel}");
                continue;
            }

            var hasBaseSchema = baseMedia.Value.TryGetProperty("schema", out var baseSchema) && baseSchema.ValueKind == JsonValueKind.Object;
            var hasCandSchema = candMediaElem.TryGetProperty("schema", out var candSchema) && candSchema.ValueKind == JsonValueKind.Object;

            if (hasBaseSchema && !hasCandSchema)
            {
                breaking.Add($"Removed schema for '{baseMedia.Name}' on {contextLabel}");
            }
            else if (hasBaseSchema && hasCandSchema)
            {
                CompareSchema(
                    $"{contextLabel} ({baseMedia.Name})",
                    baseSchema,
                    candSchema,
                    baselineRoot,
                    candidateRoot,
                    isRequestContext,
                    new HashSet<string>(StringComparer.Ordinal),
                    breaking,
                    additive);
            }
        }

        foreach (var candMedia in candContent.EnumerateObject())
        {
            if (!baseContent.TryGetProperty(candMedia.Name, out _))
            {
                additive.Add($"Added {(isRequestContext ? "request" : "response")} content type '{candMedia.Name}' on {contextLabel}");
            }
        }
    }

    private static void CompareSchema(
        string contextLabel,
        JsonElement baseSchema,
        JsonElement candSchema,
        JsonElement baselineRoot,
        JsonElement candidateRoot,
        bool isRequestContext,
        HashSet<string> visitedRefs,
        List<string> breaking,
        List<string> additive)
    {
        // First compare any constraints or multi-branch compositions placed directly on the outer schema
        CompareSchemaConstraints(contextLabel, baseSchema, candSchema, isRequestContext, breaking, additive);
        CompareCompositionBranches(contextLabel, baseSchema, candSchema, baselineRoot, candidateRoot, isRequestContext, visitedRefs, breaking, additive);

        var resolvedBase = ResolveSchemaRef(baseSchema, baselineRoot, out var baseRefId);
        var resolvedCand = ResolveSchemaRef(candSchema, candidateRoot, out var candRefId);

        if (baseRefId != null || candRefId != null)
        {
            var pairKey = $"{isRequestContext}:{baseRefId ?? "inline"}->{candRefId ?? "inline"}";
            if (!visitedRefs.Add(pairKey))
            {
                return;
            }

            CompareSchemaConstraints(contextLabel, resolvedBase, resolvedCand, isRequestContext, breaking, additive);
            CompareCompositionBranches(contextLabel, resolvedBase, resolvedCand, baselineRoot, candidateRoot, isRequestContext, visitedRefs, breaking, additive);
        }

        var baseType = GetSchemaTypeSignature(resolvedBase);
        var candType = GetSchemaTypeSignature(resolvedCand);
        if (!string.IsNullOrEmpty(baseType) && !string.IsNullOrEmpty(candType) &&
            !string.Equals(baseType, candType, StringComparison.OrdinalIgnoreCase))
        {
            breaking.Add($"Changed schema type from '{baseType}' to '{candType}' on {contextLabel}");
            return;
        }

        // Compare required fields
        var baseRequired = GetRequiredFieldSet(resolvedBase);
        var candRequired = GetRequiredFieldSet(resolvedCand);

        if (isRequestContext)
        {
            // Adding a new required field to a request schema breaks existing clients
            foreach (var addedReq in candRequired.Except(baseRequired, StringComparer.OrdinalIgnoreCase))
            {
                breaking.Add($"Added required request property '{addedReq}' on {contextLabel}");
            }
        }
        else
        {
            // Dropping a required guarantee from a response schema breaks clients expecting it
            foreach (var droppedReq in baseRequired.Except(candRequired, StringComparer.OrdinalIgnoreCase))
            {
                breaking.Add($"Removed required guarantee for response property '{droppedReq}' on {contextLabel}");
            }
        }

        // Compare object properties
        var baseProps = GetPropertiesMap(resolvedBase);
        var candProps = GetPropertiesMap(resolvedCand);

        foreach (var (propName, basePropSchema) in baseProps)
        {
            if (!candProps.TryGetValue(propName, out var candPropSchema))
            {
                if (isRequestContext)
                {
                    breaking.Add($"Removed request property '{propName}' on {contextLabel}");
                }
                else
                {
                    breaking.Add($"Removed response property '{propName}' on {contextLabel}");
                }
                continue;
            }

            var resolvedBaseProp = ResolveSchemaRef(basePropSchema, baselineRoot, out _);
            var resolvedCandProp = ResolveSchemaRef(candPropSchema, candidateRoot, out _);
            var basePropType = GetSchemaTypeSignature(resolvedBaseProp);
            var candPropType = GetSchemaTypeSignature(resolvedCandProp);

            if (!string.IsNullOrEmpty(basePropType) && !string.IsNullOrEmpty(candPropType) &&
                !string.Equals(basePropType, candPropType, StringComparison.OrdinalIgnoreCase))
            {
                breaking.Add($"Changed property type on '{propName}' from '{basePropType}' to '{candPropType}' on {contextLabel}");
                continue;
            }

            CompareSchema(
                $"{contextLabel}.{propName}",
                basePropSchema,
                candPropSchema,
                baselineRoot,
                candidateRoot,
                isRequestContext,
                visitedRefs,
                breaking,
                additive);
        }

        foreach (var (propName, _) in candProps)
        {
            if (!baseProps.ContainsKey(propName))
            {
                if (isRequestContext)
                {
                    if (!candRequired.Contains(propName))
                    {
                        additive.Add($"Added optional request property '{propName}' on {contextLabel}");
                    }
                }
                else
                {
                    additive.Add($"Added response property '{propName}' on {contextLabel}");
                }
            }
        }

        // Compare additionalProperties when specified as a schema object
        if (resolvedBase.ValueKind == JsonValueKind.Object &&
            resolvedCand.ValueKind == JsonValueKind.Object &&
            resolvedBase.TryGetProperty("additionalProperties", out var baseAddProps) &&
            baseAddProps.ValueKind == JsonValueKind.Object &&
            resolvedCand.TryGetProperty("additionalProperties", out var candAddProps) &&
            candAddProps.ValueKind == JsonValueKind.Object)
        {
            CompareSchema(
                $"{contextLabel}.additionalProperties",
                baseAddProps,
                candAddProps,
                baselineRoot,
                candidateRoot,
                isRequestContext,
                visitedRefs,
                breaking,
                additive);
        }

        // Compare array items schema
        if (resolvedBase.TryGetProperty("items", out var baseItems) && baseItems.ValueKind == JsonValueKind.Object &&
            resolvedCand.TryGetProperty("items", out var candItems) && candItems.ValueKind == JsonValueKind.Object)
        {
            CompareSchema(
                $"{contextLabel}[]",
                baseItems,
                candItems,
                baselineRoot,
                candidateRoot,
                isRequestContext,
                visitedRefs,
                breaking,
                additive);
        }
    }

    private static void CompareCompositionBranches(
        string contextLabel,
        JsonElement baseSchema,
        JsonElement candSchema,
        JsonElement baselineRoot,
        JsonElement candidateRoot,
        bool isRequestContext,
        HashSet<string> visitedRefs,
        List<string> breaking,
        List<string> additive)
    {
        if (baseSchema.ValueKind != JsonValueKind.Object || candSchema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var keyword in new[] { "oneOf", "anyOf", "allOf" })
        {
            var baseBranches = GetNonNullCompositionBranches(baseSchema, keyword);
            var candBranches = GetNonNullCompositionBranches(candSchema, keyword);

            // Single-target nullable wrappers (1 non-null branch + null) are handled by ResolveSchemaRef;
            // multi-branch compositions or allOf compositions are compared across every branch here.
            if (baseBranches.Count <= 1 && candBranches.Count <= 1 && keyword != "allOf")
            {
                continue;
            }

            if (baseBranches.Count == 0 && candBranches.Count == 0)
            {
                continue;
            }

            foreach (var (branchKey, baseBranch) in baseBranches)
            {
                if (!candBranches.TryGetValue(branchKey, out var candBranch))
                {
                    if (keyword == "allOf")
                    {
                        if (!isRequestContext)
                        {
                            breaking.Add($"Removed composition branch '{branchKey}' from {keyword} on {contextLabel}");
                        }
                        else
                        {
                            additive.Add($"Removed request allOf constraint branch '{branchKey}' on {contextLabel}");
                        }
                    }
                    else
                    {
                        if (isRequestContext)
                        {
                            breaking.Add($"Removed composition branch '{branchKey}' from {keyword} on {contextLabel}");
                        }
                        else
                        {
                            additive.Add($"Narrowed response union branch '{branchKey}' from {keyword} on {contextLabel}");
                        }
                    }
                    continue;
                }

                CompareSchema(
                    $"{contextLabel}[{keyword}:{branchKey}]",
                    baseBranch,
                    candBranch,
                    baselineRoot,
                    candidateRoot,
                    isRequestContext,
                    visitedRefs,
                    breaking,
                    additive);
            }

            foreach (var (branchKey, _) in candBranches)
            {
                if (!baseBranches.ContainsKey(branchKey))
                {
                    if (keyword == "allOf")
                    {
                        if (isRequestContext)
                        {
                            breaking.Add($"Added required composition branch '{branchKey}' to {keyword} on {contextLabel}");
                        }
                        else
                        {
                            additive.Add($"Added response composition branch '{branchKey}' to {keyword} on {contextLabel}");
                        }
                    }
                    else
                    {
                        if (isRequestContext)
                        {
                            additive.Add($"Added composition branch '{branchKey}' to {keyword} on {contextLabel}");
                        }
                        else
                        {
                            breaking.Add($"Added unexpected response union branch '{branchKey}' to {keyword} on {contextLabel}");
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<string, JsonElement> GetNonNullCompositionBranches(JsonElement schema, string keyword)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty(keyword, out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty("type", out var t) &&
                t.ValueKind == JsonValueKind.String &&
                string.Equals(t.GetString(), "null", StringComparison.OrdinalIgnoreCase) &&
                item.EnumerateObject().Count() == 1)
            {
                continue;
            }

            string key;
            if (item.TryGetProperty("$ref", out var refProp) && refProp.ValueKind == JsonValueKind.String)
            {
                key = refProp.GetString()!;
            }
            else if (item.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                key = $"type:{typeProp.GetString()}";
            }
            else
            {
                key = $"branch:{index}";
            }

            map[key] = item;
            index++;
        }

        return map;
    }

    private static void CompareSchemaConstraints(
        string contextLabel,
        JsonElement baseSchema,
        JsonElement candSchema,
        bool isRequestContext,
        List<string> breaking,
        List<string> additive)
    {
        if (baseSchema.ValueKind != JsonValueKind.Object || candSchema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // 1. format
        var baseFormat = GetOptionalString(baseSchema, "format");
        var candFormat = GetOptionalString(candSchema, "format");
        if (!string.IsNullOrEmpty(baseFormat) &&
            !string.Equals(baseFormat, candFormat, StringComparison.OrdinalIgnoreCase))
        {
            breaking.Add($"Changed schema format from '{baseFormat}' to '{(string.IsNullOrEmpty(candFormat) ? "none" : candFormat)}' on {contextLabel}");
        }

        // 2. pattern
        var basePattern = GetOptionalString(baseSchema, "pattern");
        var candPattern = GetOptionalString(candSchema, "pattern");
        if (!string.Equals(basePattern, candPattern, StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(basePattern) && !string.IsNullOrEmpty(candPattern))
            {
                if (isRequestContext)
                {
                    breaking.Add($"Added restrictive request pattern '{candPattern}' on {contextLabel}");
                }
                else
                {
                    additive.Add($"Added response pattern guarantee '{candPattern}' on {contextLabel}");
                }
            }
            else if (!string.IsNullOrEmpty(basePattern) && string.IsNullOrEmpty(candPattern))
            {
                if (isRequestContext)
                {
                    additive.Add($"Removed request pattern restriction on {contextLabel}");
                }
                else
                {
                    breaking.Add($"Removed response pattern guarantee '{basePattern}' on {contextLabel}");
                }
            }
            else
            {
                breaking.Add($"Changed schema pattern from '{basePattern}' to '{candPattern}' on {contextLabel}");
            }
        }

        // 3. minLength / maxLength
        var baseMinLen = GetOptionalInt64(baseSchema, "minLength");
        var candMinLen = GetOptionalInt64(candSchema, "minLength");
        if (baseMinLen != candMinLen)
        {
            if (isRequestContext)
            {
                if ((candMinLen ?? 0) > (baseMinLen ?? 0))
                {
                    breaking.Add($"Tightened request minLength from '{baseMinLen?.ToString() ?? "0"}' to '{candMinLen}' on {contextLabel}");
                }
                else
                {
                    additive.Add($"Relaxed request minLength from '{baseMinLen}' to '{candMinLen?.ToString() ?? "0"}' on {contextLabel}");
                }
            }
            else if ((candMinLen ?? 0) < (baseMinLen ?? 0))
            {
                breaking.Add($"Weakened response minLength from '{baseMinLen}' to '{candMinLen?.ToString() ?? "0"}' on {contextLabel}");
            }
        }

        var baseMaxLen = GetOptionalInt64(baseSchema, "maxLength");
        var candMaxLen = GetOptionalInt64(candSchema, "maxLength");
        if (baseMaxLen != candMaxLen)
        {
            if (isRequestContext)
            {
                if (candMaxLen.HasValue && (!baseMaxLen.HasValue || candMaxLen.Value < baseMaxLen.Value))
                {
                    breaking.Add($"Tightened request maxLength from '{baseMaxLen?.ToString() ?? "unbounded"}' to '{candMaxLen}' on {contextLabel}");
                }
                else
                {
                    additive.Add($"Relaxed request maxLength from '{baseMaxLen}' to '{candMaxLen?.ToString() ?? "unbounded"}' on {contextLabel}");
                }
            }
            else if (baseMaxLen.HasValue && (!candMaxLen.HasValue || candMaxLen.Value > baseMaxLen.Value))
            {
                breaking.Add($"Widened response maxLength from '{baseMaxLen}' to '{candMaxLen?.ToString() ?? "unbounded"}' on {contextLabel}");
            }
        }

        // 4. minimum / maximum
        var baseMin = GetOptionalDecimal(baseSchema, "minimum");
        var candMin = GetOptionalDecimal(candSchema, "minimum");
        if (baseMin != candMin)
        {
            if (isRequestContext)
            {
                if (candMin.HasValue && (!baseMin.HasValue || candMin.Value > baseMin.Value))
                {
                    breaking.Add($"Tightened request minimum from '{baseMin?.ToString() ?? "unbounded"}' to '{candMin}' on {contextLabel}");
                }
                else
                {
                    additive.Add($"Relaxed request minimum from '{baseMin}' to '{candMin?.ToString() ?? "unbounded"}' on {contextLabel}");
                }
            }
            else if (baseMin.HasValue && (!candMin.HasValue || candMin.Value < baseMin.Value))
            {
                breaking.Add($"Widened response minimum from '{baseMin}' to '{candMin?.ToString() ?? "unbounded"}' on {contextLabel}");
            }
        }

        var baseMax = GetOptionalDecimal(baseSchema, "maximum");
        var candMax = GetOptionalDecimal(candSchema, "maximum");
        if (baseMax != candMax)
        {
            if (isRequestContext)
            {
                if (candMax.HasValue && (!baseMax.HasValue || candMax.Value < baseMax.Value))
                {
                    breaking.Add($"Tightened request maximum from '{baseMax?.ToString() ?? "unbounded"}' to '{candMax}' on {contextLabel}");
                }
                else
                {
                    additive.Add($"Relaxed request maximum from '{baseMax}' to '{candMax?.ToString() ?? "unbounded"}' on {contextLabel}");
                }
            }
            else if (baseMax.HasValue && (!candMax.HasValue || candMax.Value > baseMax.Value))
            {
                breaking.Add($"Widened response maximum from '{baseMax}' to '{candMax?.ToString() ?? "unbounded"}' on {contextLabel}");
            }
        }

        // 5. enum
        var baseEnum = GetEnumValues(baseSchema);
        var candEnum = GetEnumValues(candSchema);
        if (baseEnum.Count > 0 || candEnum.Count > 0)
        {
            if (baseEnum.Count == 0 && candEnum.Count > 0 && isRequestContext)
            {
                breaking.Add($"Restricted request schema to enum values on {contextLabel}");
            }
            else if (baseEnum.Count > 0 && candEnum.Count == 0 && !isRequestContext)
            {
                breaking.Add($"Removed enum constraint from response schema on {contextLabel}");
            }
            else if (baseEnum.Count > 0 && candEnum.Count > 0)
            {
                foreach (var removedVal in baseEnum.Except(candEnum, StringComparer.Ordinal))
                {
                    if (isRequestContext)
                    {
                        breaking.Add($"Removed allowed request enum value '{removedVal}' on {contextLabel}");
                    }
                    else
                    {
                        additive.Add($"Narrowed response enum value '{removedVal}' on {contextLabel}");
                    }
                }

                foreach (var addedVal in candEnum.Except(baseEnum, StringComparer.Ordinal))
                {
                    if (isRequestContext)
                    {
                        additive.Add($"Added allowed request enum value '{addedVal}' on {contextLabel}");
                    }
                    else
                    {
                        breaking.Add($"Added unexpected response enum value '{addedVal}' on {contextLabel}");
                    }
                }
            }
        }

        // 6. additionalProperties boolean restriction
        var baseAllowsAdditional = !baseSchema.TryGetProperty("additionalProperties", out var baseAddProp) ||
                                   baseAddProp.ValueKind != JsonValueKind.False;
        var candDisallowsAdditional = candSchema.TryGetProperty("additionalProperties", out var candAddProp) &&
                                      candAddProp.ValueKind == JsonValueKind.False;
        if (baseAllowsAdditional && candDisallowsAdditional && isRequestContext)
        {
            breaking.Add($"Disallowed additionalProperties on request schema {contextLabel}");
        }
    }

    private static string? GetOptionalString(JsonElement obj, string propName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propName, out var prop) &&
        prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static long? GetOptionalInt64(JsonElement obj, string propName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propName, out var prop) &&
        prop.ValueKind == JsonValueKind.Number &&
        prop.TryGetInt64(out var val)
            ? val
            : null;

    private static decimal? GetOptionalDecimal(JsonElement obj, string propName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propName, out var prop) &&
        prop.ValueKind == JsonValueKind.Number &&
        prop.TryGetDecimal(out var val)
            ? val
            : null;

    private static HashSet<string> GetEnumValues(JsonElement obj)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty("enum", out var enumArr) &&
            enumArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in enumArr.EnumerateArray())
            {
                set.Add(item.ToString());
            }
        }
        return set;
    }

    private static JsonElement ResolveSchemaRef(JsonElement schema, JsonElement rootDoc, out string? refId)
    {
        refId = null;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return schema;
        }

        if (schema.TryGetProperty("$ref", out var refProp) &&
            refProp.ValueKind == JsonValueKind.String)
        {
            var refStr = refProp.GetString()!;
            refId = refStr;
            const string prefix = "#/components/schemas/";
            if (refStr.StartsWith(prefix, StringComparison.Ordinal) &&
                rootDoc.TryGetProperty("components", out var comp) &&
                comp.TryGetProperty("schemas", out var schemas) &&
                schemas.TryGetProperty(refStr[prefix.Length..], out var target))
            {
                return target;
            }
        }

        // Unwrap single-target nullable composite wrapper (e.g., oneOf: [{type: "null"}, {$ref: "..."}])
        // Multi-branch compositions (>1 non-null branch) are compared branch-by-branch in CompareCompositionBranches.
        foreach (var compositeKeyword in new[] { "oneOf", "anyOf", "allOf" })
        {
            var nonNullBranches = GetNonNullCompositionBranches(schema, compositeKeyword);
            if (nonNullBranches.Count == 1)
            {
                var singleBranch = nonNullBranches.Values.First();
                if (singleBranch.TryGetProperty("$ref", out var nestedRef) &&
                    nestedRef.ValueKind == JsonValueKind.String)
                {
                    return ResolveSchemaRef(singleBranch, rootDoc, out refId);
                }
            }
        }

        return schema;
    }

    private static string GetSchemaTypeSignature(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object) return string.Empty;
        if (!schema.TryGetProperty("type", out var typeProp)) return string.Empty;
        if (typeProp.ValueKind == JsonValueKind.String)
        {
            return typeProp.GetString() ?? string.Empty;
        }
        if (typeProp.ValueKind == JsonValueKind.Array)
        {
            var types = typeProp.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .Where(x => !string.Equals(x, "null", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            return string.Join("|", types);
        }
        return string.Empty;
    }

    private static HashSet<string> GetRequiredFieldSet(JsonElement schema)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("required", out var reqArr) &&
            reqArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in reqArr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                {
                    set.Add(s);
                }
            }
        }
        return set;
    }

    private static Dictionary<string, JsonElement> GetPropertiesMap(JsonElement schema)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("properties", out var props) &&
            props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                map[prop.Name] = prop.Value;
            }
        }
        return map;
    }

    private static Dictionary<string, JsonElement> GetParametersMap(JsonElement op)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (op.TryGetProperty("parameters", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in arr.EnumerateArray())
            {
                var name = p.TryGetProperty("name", out var n) ? n.GetString() : "";
                var loc = p.TryGetProperty("in", out var l) ? l.GetString() : "";
                if (!string.IsNullOrEmpty(name))
                {
                    map[$"{loc}:{name}"] = p;
                }
            }
        }
        return map;
    }

    private static HashSet<string> GetSecuritySchemes(JsonElement op)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (op.TryGetProperty("security", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                    {
                        set.Add(prop.Name);
                    }
                }
            }
        }
        return set;
    }
}
