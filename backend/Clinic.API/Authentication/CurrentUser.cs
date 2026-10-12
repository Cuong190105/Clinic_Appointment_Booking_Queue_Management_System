using Clinic.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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

                return Guid.TryParse(value, out var id) ? id : null;
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
    }
}