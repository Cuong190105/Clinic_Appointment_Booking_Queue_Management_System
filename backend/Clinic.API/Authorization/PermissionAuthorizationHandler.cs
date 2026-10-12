using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Clinic.API.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            // Admin bypass
            if (context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check permission claim - pass nếu có BẤT KỲ permission nào trong requirement (OR).
            if (context.User.HasClaim(c => c.Type == "permission" && requirement.Permissions.Contains(c.Value)))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    }
}