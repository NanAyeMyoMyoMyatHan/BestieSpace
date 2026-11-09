using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Rbac;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserAuthorizationService _userAuthService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IUserAuthorizationService userAuthService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _userAuthService = userAuthService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Dynamic RBAC check failed: authenticated user missing valid numeric NameIdentifier/sub claim.");
            return;
        }

        try
        {
            // Evaluate permission dynamically against current database or cached state
            var hasPermission = await _userAuthService.HasPermissionAsync(userId, requirement.Permission);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} denied dynamic permission '{Permission}'", userId, requirement.Permission);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission '{Permission}' for user {UserId}", requirement.Permission, userId);
        }
    }
}
