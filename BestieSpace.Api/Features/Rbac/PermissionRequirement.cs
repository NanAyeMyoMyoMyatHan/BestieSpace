using Microsoft.AspNetCore.Authorization;

namespace BestieSpace.Api.Features.Rbac;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
