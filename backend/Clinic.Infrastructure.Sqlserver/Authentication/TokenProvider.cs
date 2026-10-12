using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Clinic.Infrastructure.Authentication
{
    public class TokenProvider : ITokenProvider
    {
        private readonly IConfiguration _configuration;
        public TokenProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>
            {
                new(
                    JwtRegisteredClaimNames.Sub,
                    user.Id.ToString()),
                new(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString())
            };

            foreach (var userRole in user.UserRoles)
            {
                var role = userRole.Role;
                claims.Add(new Claim(ClaimTypes.Role, role.Name));
            }

            var personId = user.Person.Id;
            claims.Add(new Claim("personId", personId.ToString()));

            // Nếu user là Patient, thêm claim patientId để dùng cho ownership check (vd: appointment.cancel.own).
            var patientId = user.Person?.Patient?.Id;
            if (patientId is not null)
            {
                claims.Add(new Claim("patientId", patientId.Value.ToString()));
            }

            // Nếu user là Doctor (qua Employee), thêm claim doctorId tương tự.
            var doctorId = user.Person?.Employee?.Doctor?.Id;
            if (doctorId is not null)
            {
                claims.Add(new Claim("doctorId", doctorId.Value.ToString()));
            }

            var employeeId = user.Person?.Employee?.Id;
            if (employeeId is not null)
            {
                claims.Add(new Claim("employeeId", employeeId.Value.ToString()));
            }

            // Add permission claims from role -> rolepermissions if available
            var permissionClaimType = "permission";
            var addedPermissions = new HashSet<string>();
            foreach (var userRole in user.UserRoles)
            {
                var role = userRole.Role;
                if (role.RolePermissions == null) continue;
                foreach (var rp in role.RolePermissions)
                {
                    var permName = rp.Permission?.Name;
                    if (string.IsNullOrWhiteSpace(permName)) continue;
                    if (addedPermissions.Add(permName))
                    {
                        claims.Add(new Claim(permissionClaimType, permName));
                    }
                }
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!
                ));

            var credentials =
                new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256);

            var token =
                new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(15),
                    signingCredentials: credentials);


            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);

            return Convert.ToBase64String(randomBytes);
        }

        public string HashToken(string refreshToken)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(refreshToken);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}