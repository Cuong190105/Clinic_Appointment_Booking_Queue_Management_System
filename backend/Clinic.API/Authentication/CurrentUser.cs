using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Clinic.API.Authentication
{
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        public Guid? UserId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?
                    .FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public Guid? PatientId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?
                    .FindFirst("patientId")?.Value;

                if (Guid.TryParse(value, out var id)) return id;

                // Fallback: look up Patient by PersonId claim
                var personValue = _httpContextAccessor.HttpContext?.User?
                    .FindFirst("personId")?.Value;
                if (Guid.TryParse(personValue, out var personId))
                {
                    if (_httpContextAccessor.HttpContext?.Items.TryGetValue("CachedPatientId", out var cached) == true && cached is Guid cachedId)
                    {
                        return cachedId;
                    }

                    var dbContext = _httpContextAccessor.HttpContext?.RequestServices.GetService(typeof(Clinic.Infrastructure.Sqlserver.Persistence.ApplicationDbContext)) as Clinic.Infrastructure.Sqlserver.Persistence.ApplicationDbContext;
                    if (dbContext != null)
                    {
                        var pt = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(dbContext.Patients)
                            .FirstOrDefault(p => p.PersonId == personId);
                        if (pt != null)
                        {
                            _httpContextAccessor.HttpContext?.Items["CachedPatientId"] = pt.Id;
                            return pt.Id;
                        }
                    }
                }

                return null;
            }
        }

        public Guid? EmployeeId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?
                    .FindFirst("employeeId")?.Value;

                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public Guid? DoctorId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?
                    .FindFirst("doctorId")?.Value;

                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public Guid? PersonId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?
                    .FindFirst("personId")?.Value;

                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public bool HasPermission(string permission)
        {
            return _httpContextAccessor.HttpContext?.User?
                .HasClaim(c => c.Type == "permission" && c.Value == permission)
                ?? false;
        }

        public bool IsInRole(string role)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return false;
            return user.IsInRole(role)
                || user.HasClaim(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyCollection<string> Roles =>
            _httpContextAccessor.HttpContext?.User?
                .FindAll(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? (IReadOnlyCollection<string>)Array.Empty<string>();

        public IReadOnlyCollection<string> Permissions =>
            _httpContextAccessor.HttpContext?.User?
                .FindAll(c => c.Type == "permission")
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? (IReadOnlyCollection<string>)Array.Empty<string>();
    }
}
