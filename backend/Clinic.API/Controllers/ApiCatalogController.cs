using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Clinic.API.Controllers
{
    /// <summary>
    /// Provides catalog metadata, system topology, and routing rules for API consumers.
    /// Provides identical contract to the microservices API gateway.
    /// </summary>
    [ApiController]
    [Route("api/catalog")]
    [AllowAnonymous]
    public sealed class ApiCatalogController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetCatalog()
        {
            var catalog = new
            {
                gatewayVersion = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                openApiSpecUrl = "/openapi/v1.json",
                openApiSpecVisibility = "Public",
                swaggerUiUrl = "/swagger/index.html",
                services = new object[]
                {
                    new
                    {
                        name = "Clinic.Monolith.Backend",
                        role = "Unified Clinic Management, Scheduling, Appointments, Queue, and Medical Records",
                        authority = "http://localhost:5100",
                        healthEndpoint = "/health",
                        routePrefixes = new[]
                        {
                            "/auth",
                            "/specialties",
                            "/doctors",
                            "/employees",
                            "/shifts",
                            "/slots",
                            "/appointments",
                            "/queue",
                            "/patients",
                            "/medical-reports"
                        },
                        authPolicy = "Mixed"
                    }
                },
                qoSPolicies = new
                {
                    maxRequestTimeoutSeconds = 15,
                    maxRetryAttempts = 3,
                    rateLimitingEnabled = true,
                    rateLimitWindowMinutes = 1,
                    defaultRateLimitPermits = 100,
                    correlationHeader = "X-Correlation-ID",
                    idempotencyHeader = "Idempotency-Key"
                }
            };

            return Ok(catalog);
        }
    }
}
