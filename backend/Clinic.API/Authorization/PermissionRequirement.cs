using Microsoft.AspNetCore.Authorization;
using System;

namespace Clinic.API.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string[] Permissions { get; }

        public PermissionRequirement(string policyValue)
        {
            Permissions = policyValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}