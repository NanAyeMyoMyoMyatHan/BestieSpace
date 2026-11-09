using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BestieSpace.Api.Features.Rbac;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // First check if an existing static policy matches
        var policy = await _fallbackPolicyProvider.GetPolicyAsync(policyName);
        if (policy != null)
        {
            return policy;
        }

        // Dynamically synthesize a permission-based policy for any policy name requested
        // e.g. [Authorize(Policy = "USER_MANAGE")]
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
