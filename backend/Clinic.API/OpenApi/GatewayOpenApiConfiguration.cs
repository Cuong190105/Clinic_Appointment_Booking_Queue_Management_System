using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Clinic.API.OpenApi;

/// <summary>
/// Configures the public Clinic API Gateway OpenAPI v1 specification, including:
/// - Document metadata and Bearer security scheme
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

            NormalizeWireResponseSchemas(operation, httpMethod, relativePath, requiresAuth, context.Document);

            return Task.CompletedTask;
        });
    }

    private static void NormalizeWireResponseSchemas(
        OpenApiOperation operation,
        string httpMethod,
        string relativePath,
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

            if (statusKey is "400" or "401" or "403" or "404" or "409" or "429" or "500" or "502" or "503" or "504")
            {
                response.Content ??= new Dictionary<string, OpenApiMediaType>();
                response.Content["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["statusCode"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                            ["isSuccess"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                            ["errorMessages"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
                            ["result"] = new OpenApiSchema { Type = JsonSchemaType.Null | JsonSchemaType.Object }
                        }
                    }
                };
                continue;
            }

            if (statusKey is "200" or "201")
            {
                response.Content ??= new Dictionary<string, OpenApiMediaType>();
                if (!response.Content.ContainsKey("application/json"))
                {
                    response.Content["application/json"] = new OpenApiMediaType
                    {
                        Schema = BuildGenericJsonPayloadSchema()
                    };
                }

                if (httpMethod == "GET")
                {
                    if (relativePath.Equals("/specialties", StringComparison.OrdinalIgnoreCase) ||
                        relativePath.Equals("/doctors", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Content["application/json"].Schema = BuildPagedEnvelopeSchema();
                    }
                    else if (relativePath.Equals("/doctors/available", StringComparison.OrdinalIgnoreCase) ||
                             relativePath.Equals("/doctors/{id}/available", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Content["application/json"].Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchema { Type = JsonSchemaType.Object }
                        };
                    }
                    else if (relativePath.Equals("/doctors/dashboard-stats", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Content["application/json"].Schema = new OpenApiSchema
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
                    }
                }
            }
        }
    }

    private static OpenApiSchema BuildPagedEnvelopeSchema() => new()
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

    private static OpenApiSchema BuildGenericJsonPayloadSchema() => new()
    {
        Type = JsonSchemaType.Object | JsonSchemaType.Array
    };

    private static bool IsIdempotentCommand(string httpMethod, string path)
    {
        return httpMethod switch
        {
            "POST" when path.Equals("/appointments", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.Equals("/appointments/walk-in", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.Equals("/appointments/receptionist", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.EndsWith("/check-in", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.Equals("/schedules/reservations", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.Equals("/queue/check-in", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.StartsWith("/medical-records", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    private static bool IsRequiredIdempotencyCommand(string httpMethod, string path)
    {
        return httpMethod switch
        {
            "POST" when path.Equals("/schedules/reservations", StringComparison.OrdinalIgnoreCase) => true,
            "POST" when path.EndsWith("/check-in", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

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
                Description = "Distributed correlation identifier propagated across all microservice hops",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
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
}
