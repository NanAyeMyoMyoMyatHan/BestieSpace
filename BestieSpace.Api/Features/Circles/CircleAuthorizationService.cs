using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Circles;

public interface ICircleAuthorizationService
{
    Task<bool> HasCirclePermissionAsync(int userId, int circleId, string permissionCode);
    Task<string?> GetCircleRoleNameAsync(int userId, int circleId);
    Task<BestieCircleMember?> GetActiveMembershipAsync(int userId, int circleId);
    Task<bool> IsCircleOwnerAsync(int userId, int circleId);
    Task<bool> IsCircleMemberAsync(int userId, int circleId);
    Task<List<string>> GetCircleUserPermissionsAsync(int userId, int circleId);
}

public class CircleAuthorizationService : ICircleAuthorizationService
{
    private readonly BestieDbContext _context;
    private readonly ILogger<CircleAuthorizationService> _logger;

    public CircleAuthorizationService(
        BestieDbContext context,
        ILogger<CircleAuthorizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasCirclePermissionAsync(int userId, int circleId, string permissionCode)
    {
        if (userId <= 0 || circleId <= 0 || string.IsNullOrWhiteSpace(permissionCode)) return false;

        var perms = await GetCircleUserPermissionsAsync(userId, circleId);
        return perms.Contains(permissionCode.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetCircleUserPermissionsAsync(int userId, int circleId)
    {
        var circle = await _context.BestieCircles.AsNoTracking().FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null) return new List<string>();

        var membership = await _context.BestieCircleMembers
            .AsNoTracking()
            .Include(m => m.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == userId && m.IsActive);

        if (membership == null)
        {
            return new List<string>(); // Strict privacy: non-members have zero access
        }

        // If user is owner, they always hold all circle permissions
        if (circle.OwnerUserId == userId || membership.Role.RoleName == "CircleOwner" || membership.Role.RoleName == "Owner")
        {
            return await _context.Permissions
                .AsNoTracking()
                .Where(p => p.PermissionCode.StartsWith("CIRCLE_"))
                .Select(p => p.PermissionCode.ToUpperInvariant())
                .Distinct()
                .ToListAsync();
        }

        return membership.Role.RolePermissions
            .Select(rp => rp.Permission.PermissionCode.ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    public async Task<string?> GetCircleRoleNameAsync(int userId, int circleId)
    {
        var membership = await GetActiveMembershipAsync(userId, circleId);
        return membership?.Role.RoleName;
    }

    public async Task<BestieCircleMember?> GetActiveMembershipAsync(int userId, int circleId)
    {
        return await _context.BestieCircleMembers
            .AsNoTracking()
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == userId && m.IsActive);
    }

    public async Task<bool> IsCircleOwnerAsync(int userId, int circleId)
    {
        var circle = await _context.BestieCircles.AsNoTracking().FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        return circle != null && circle.OwnerUserId == userId;
    }

    public async Task<bool> IsCircleMemberAsync(int userId, int circleId)
    {
        return await _context.BestieCircleMembers
            .AnyAsync(m => m.CircleId == circleId && m.UserId == userId && m.IsActive);
    }
}
