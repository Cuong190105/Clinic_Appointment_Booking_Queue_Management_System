using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Clinic.Notification.Service.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.Notification.Service.Tests;

public sealed class NotificationTestFactory : WebApplicationFactory<Program>
{
    public static readonly SymmetricSecurityKey TestSigningKey =
        new(Encoding.UTF8.GetBytes("notification-secret-test-key-of-at-least-32-bytes-length!"));

    public string DatabaseName { get; } = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabaseName"] = DatabaseName
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Configure JWT Bearer to validate using TestSigningKey without external JWKS calls
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration();
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "http://localhost:5000",
                    ValidateAudience = true,
                    ValidAudience = "Clinic.Services",
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestSigningKey,
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub"
                };
            });
        });
    }

    public string CreateToken(Guid userId, string role = "Patient")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }),
            Issuer = "http://localhost:5000",
            Audience = "Clinic.Services",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
