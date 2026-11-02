using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Clinic.API.OpenApi;

/// <summary>
/// Configures the public Clinic API Gateway OpenAPI v1 specification, including:
/// - Document metadata and Bearer security scheme
/// - Public SchedulingProxyMiddleware routes (/specialties, /employees, /doctors, /shifts)
/// - Operation security requirements (Bearer vs AllowAnonymous)
/// - Distributed tracing (X-Correlation-ID) and idempotency (Idempotency-Key) headers
/// </summary>
public static class GatewayOpenApiConfiguration
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Clinic API Gateway";
            document.Info.Version = "1.0.0";
            document.Info.Description = "Public BFF & API Gateway specification for the Clinic Appointment Booking and Queue Management System.";

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "JWT Bearer access token issued by Clinic.Identity.Service"
            };

            AddSchedulingProxyRoutes(document);
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var relativePath = "/" + (context.Description.RelativePath ?? string.Empty).TrimStart('/');
            var httpMethod = context.Description.HttpMethod?.ToUpperInvariant() ?? "GET";
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;

            var allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            var hasAuthorize = metadata.OfType<IAuthorizeData>().Any();
            var isControllerAction = context.Description.ActionDescriptor.GetType().Name.Contains("ControllerActionDescriptor", StringComparison.Ordinal);

            // Controllers in Clinic.API have a global AuthorizeFilter unless [AllowAnonymous] is present.
            var requiresAuth = !allowAnonymous && (hasAuthorize || isControllerAction);
            if (requiresAuth)
            {
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new()
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>()
                    }
                };
            }
            else
            {
                operation.Security = new List<OpenApiSecurityRequirement>();
            }

            EnsureCorrelationHeader(operation);

            // Ensure Idempotency-Key header is declared on retryable command endpoints
            if (IsIdempotentCommand(httpMethod, relativePath))
            {
                EnsureIdempotencyHeader(operation, required: IsRequiredIdempotencyCommand(httpMethod, relativePath));
            }

            NormalizeWireResponseSchemas(operation, requiresAuth, context.Document);

            return Task.CompletedTask;
        });
    }

    private static void NormalizeWireResponseSchemas(
        OpenApiOperation operation,
        bool requiresAuth,
        OpenApiDocument document)
    {
        operation.Responses ??= new OpenApiResponses();

        if (requiresAuth)
        {
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses["401"] = new OpenApiResponse { Description = "Unauthorized" };
            }
            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses["403"] = new OpenApiResponse { Description = "Forbidden" };
            }
        }

        foreach (var (statusKey, responseInterface) in operation.Responses.ToList())
        {
            if (responseInterface is not OpenApiResponse response)
            {
                continue;
            }

            if (statusKey == "204")
            {
                response.Content = null;
                continue;
            }

            if (response.Content is null)
            {
                if (statusKey == "200" || statusKey == "201")
                {
                    response.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = BuildGenericJsonPayloadSchema()
                        }
                    };
                }
                continue;
            }

            foreach (var (mediaTypeKey, mediaType) in response.Content.ToList())
            {
                if (mediaType.Schema is OpenApiSchemaReference schemaRef &&
                    string.Equals(schemaRef.Reference.Id, "ProblemDetails", StringComparison.OrdinalIgnoreCase))
                {
                    // ApiResponseWrapperFilter & GlobalExceptionHandler convert ProblemDetails/ValidationProblemDetails
                    // into the standard ApiResponse wire envelope on Gateway controller errors.
                    mediaType.Schema = document.Components?.Schemas is not null && document.Components.Schemas.ContainsKey("ApiResponse")
                        ? new OpenApiSchemaReference("ApiResponse", document)
                        : BuildApiResponseEnvelopeSchema();
                }
            }
        }
    }

    private static OpenApiSchema BuildApiResponseEnvelopeSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["statusCode"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["isSuccess"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
            ["errorMessages"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String }
            },
            ["result"] = new OpenApiSchema { Type = JsonSchemaType.Object | JsonSchemaType.Null }
        }
    };

    private static OpenApiSchema BuildGenericJsonPayloadSchema() => new()
    {
        Type = JsonSchemaType.Object | JsonSchemaType.Array
    };

    private static bool IsIdempotentCommand(string method, string path)
    {
        if (method is not ("POST" or "PATCH")) return false;
        return path.Equals("/appointments", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/appointments/{appointmentId}/check-in", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/appointments/{appointmentId}", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/receptionist/appointments/walk-in", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/receptionist/appointments", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/schedules/{scheduleId}/reservations", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/medical-records/reports/{reportId}/finalize", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRequiredIdempotencyCommand(string method, string path) =>
        method == "POST" && (
            path.Equals("/appointments/{appointmentId}/check-in", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/schedules/{scheduleId}/reservations", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/medical-records/reports/{reportId}/finalize", StringComparison.OrdinalIgnoreCase));

    private static void EnsureCorrelationHeader(OpenApiOperation operation)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();
        if (!operation.Parameters.Any(p => string.Equals(p.Name, "X-Correlation-ID", StringComparison.OrdinalIgnoreCase)))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Correlation-ID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Correlation ID for end-to-end distributed request tracing",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
    }

    private static void EnsureIdempotencyHeader(OpenApiOperation operation, bool required)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();
        var existing = operation.Parameters.FirstOrDefault(p => string.Equals(p.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = required,
                Description = "Unique idempotency key (UUID) to prevent duplicate command execution on retry",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
        else if (existing is OpenApiParameter mutableParam && required)
        {
            mutableParam.Required = true;
            if (string.IsNullOrWhiteSpace(mutableParam.Description))
            {
                mutableParam.Description = "Unique idempotency key (UUID) to prevent duplicate command execution on retry";
            }
        }
    }

    private static void AddSchedulingProxyRoutes(OpenApiDocument document)
    {
        document.Paths ??= new OpenApiPaths();

        var pagedEnvelopeSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["isSuccess"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                ["result"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["items"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Object } },
                        ["pageNumber"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                        ["pageSize"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                        ["totalCount"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                        ["totalPages"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                        ["hasPrevious"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                        ["hasNext"] = new OpenApiSchema { Type = JsonSchemaType.Boolean }
                    }
                }
            }
        };

        var directPagedSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["items"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Object } },
                ["pageNumber"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["pageSize"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["totalCount"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["totalPages"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["hasPrevious"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                ["hasNext"] = new OpenApiSchema { Type = JsonSchemaType.Boolean }
            }
        };

        var objectArraySchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.Object }
        };

        var dashboardStatsSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["totalDoctors"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["totalDoctorsChange"] = new OpenApiSchema { Type = JsonSchemaType.Number },
                ["activeDoctors"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["activeDoctorsChange"] = new OpenApiSchema { Type = JsonSchemaType.Number }
            }
        };

        // Specialties (proxied by SchedulingProxyMiddleware)
        AddProxyOperation(document, "/specialties", HttpMethod.Get, "List clinical specialties", "Specialties", requiresAuth: false, responseSchema: pagedEnvelopeSchema);
        AddProxyOperation(document, "/specialties", HttpMethod.Post, "Create a clinical specialty", "Specialties", requiresAuth: true, hasJsonBody: true, successStatus: "201");
        AddProxyOperation(document, "/specialties/{id}", HttpMethod.Get, "Get specialty by ID", "Specialties", requiresAuth: false, pathParams: ["id"]);
        AddProxyOperation(document, "/specialties/{id}", HttpMethod.Put, "Update specialty by ID", "Specialties", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
        AddProxyOperation(document, "/specialties/{id}/status", HttpMethod.Patch, "Deactivate specialty", "Specialties", requiresAuth: true, pathParams: ["id"], successStatus: "204");

        // Employees (GET and status proxied by SchedulingProxyMiddleware; POST/PUT in EmployeesController)
        AddProxyOperation(document, "/employees", HttpMethod.Get, "List employees (enriched with Identity roles)", "Employees", requiresAuth: true, responseSchema: directPagedSchema);
        AddProxyOperation(document, "/employees/me", HttpMethod.Get, "Get current employee profile", "Employees", requiresAuth: true);
        AddProxyOperation(document, "/employees/{id}", HttpMethod.Get, "Get employee by ID", "Employees", requiresAuth: true, pathParams: ["id"]);
        AddProxyOperation(document, "/employees/{id}/status", HttpMethod.Post, "Update employee status", "Employees", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");

        // Doctors (GET and shift/status/specialty routes proxied by SchedulingProxyMiddleware; POST/PUT/queue in DoctorsController)
        AddProxyOperation(document, "/doctors", HttpMethod.Get, "List doctors", "Doctors", requiresAuth: false, responseSchema: pagedEnvelopeSchema);
        AddProxyOperation(document, "/doctors/{id}", HttpMethod.Get, "Get doctor by ID", "Doctors", requiresAuth: false, pathParams: ["id"]);
        AddProxyOperation(document, "/doctors/available", HttpMethod.Get, "List available active doctors", "Doctors", requiresAuth: false, responseSchema: objectArraySchema);
        AddProxyOperation(document, "/doctors/{id}/available", HttpMethod.Get, "Get available slots for doctor", "Doctors", requiresAuth: false, pathParams: ["id"], responseSchema: objectArraySchema);
        AddProxyOperation(document, "/doctors/me", HttpMethod.Get, "Get current doctor profile", "Doctors", requiresAuth: true);
        AddProxyOperation(document, "/doctors/shifts", HttpMethod.Get, "List doctor work schedules (paged)", "Doctors", requiresAuth: true, responseSchema: directPagedSchema);
        AddProxyOperation(document, "/doctors/{doctorId}/shifts", HttpMethod.Get, "List shifts for a specific doctor", "Doctors", requiresAuth: false, pathParams: ["doctorId"]);
        AddProxyOperation(document, "/doctors/{doctorId}/shifts", HttpMethod.Post, "Create a work shift for a doctor", "Doctors", requiresAuth: true, pathParams: ["doctorId"], hasJsonBody: true, successStatus: "201");
        AddProxyOperation(document, "/doctors/{doctorId}/suggestions", HttpMethod.Get, "List shift suggestions for a doctor", "Doctors", requiresAuth: true, pathParams: ["doctorId"]);
        AddProxyOperation(document, "/doctors/dashboard-stats", HttpMethod.Get, "Get doctor count statistics", "Doctors", requiresAuth: false, responseSchema: dashboardStatsSchema);
        AddProxyOperation(document, "/doctors/{id}/status", HttpMethod.Patch, "Update doctor status", "Doctors", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
        AddProxyOperation(document, "/doctors/{id}/status", HttpMethod.Post, "Update doctor status (POST alias)", "Doctors", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
        AddProxyOperation(document, "/doctors/{id}/specialty", HttpMethod.Patch, "Update doctor specialty", "Doctors", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
        AddProxyOperation(document, "/doctors/{id}/specialty", HttpMethod.Post, "Update doctor specialty (POST alias)", "Doctors", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");

        // Shifts & Shift Suggestions (proxied by SchedulingProxyMiddleware)
        AddProxyOperation(document, "/shifts", HttpMethod.Get, "List shifts for current doctor", "Shifts", requiresAuth: true);
        AddProxyOperation(document, "/shifts/suggestions", HttpMethod.Get, "List shift suggestions for current doctor", "Shifts", requiresAuth: true);
        AddProxyOperation(document, "/shifts/suggestions", HttpMethod.Post, "Create a shift suggestion", "Shifts", requiresAuth: true, hasJsonBody: true, successStatus: "201");
        AddProxyOperation(document, "/shifts/suggestions/{id}", HttpMethod.Patch, "Update a pending shift suggestion", "Shifts", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
        AddProxyOperation(document, "/shifts/suggestions/{id}/cancel", HttpMethod.Post, "Cancel a shift suggestion", "Shifts", requiresAuth: true, pathParams: ["id"]);
        AddProxyOperation(document, "/shifts/suggestions/{id}/approve", HttpMethod.Post, "Approve a shift suggestion", "Shifts", requiresAuth: true, pathParams: ["id"]);
        AddProxyOperation(document, "/shifts/suggestions/{id}/reject", HttpMethod.Post, "Reject a shift suggestion", "Shifts", requiresAuth: true, pathParams: ["id"]);
        AddProxyOperation(document, "/shifts/{id}", HttpMethod.Put, "Update a work shift", "Shifts", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
        AddProxyOperation(document, "/shifts/{id}/cancel", HttpMethod.Post, "Cancel a work shift", "Shifts", requiresAuth: true, pathParams: ["id"], hasJsonBody: true, successStatus: "204");
    }

    private static void AddProxyOperation(
        OpenApiDocument document,
        string path,
        HttpMethod method,
        string summary,
        string tag,
        bool requiresAuth,
        string[]? pathParams = null,
        bool hasJsonBody = false,
        string successStatus = "200",
        OpenApiSchema? responseSchema = null)
    {
        if (!document.Paths.TryGetValue(path, out var pathItemInterface) || pathItemInterface is not OpenApiPathItem pathItem)
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>()
            };
            document.Paths[path] = pathItem;
        }

        pathItem.Operations ??= new Dictionary<HttpMethod, OpenApiOperation>();
        if (pathItem.Operations.ContainsKey(method))
        {
            return;
        }

        var operation = new OpenApiOperation
        {
            Summary = summary,
            Tags = new HashSet<OpenApiTagReference> { new(tag, document) },
            Parameters = new List<IOpenApiParameter>(),
            Responses = new OpenApiResponses
            {
                [successStatus] = new OpenApiResponse
                {
                    Description = successStatus == "204" ? "No Content" : "Success",
                    Content = successStatus == "204"
                        ? null
                        : new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = responseSchema ?? new OpenApiSchema { Type = JsonSchemaType.Object }
                            }
                        }
                }
            }
        };

        if (pathParams is not null && pathParams.Length > 0)
        {
            operation.Responses["404"] = new OpenApiResponse { Description = "Not Found" };
            foreach (var paramName in pathParams)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = paramName,
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
                });
            }
        }

        EnsureCorrelationHeader(operation);

        if (hasJsonBody)
        {
            operation.Responses["400"] = new OpenApiResponse { Description = "Bad Request" };
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                    }
                }
            };
        }

        if (requiresAuth)
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                }
            };
            operation.Responses["401"] = new OpenApiResponse { Description = "Unauthorized" };
            operation.Responses["403"] = new OpenApiResponse { Description = "Forbidden" };
        }
        else
        {
            operation.Security = new List<OpenApiSecurityRequirement>();
        }

        pathItem.Operations[method] = operation;
    }
}

