using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Rbac;

public interface IUserAuthorizationService
{
    Task<bool> HasPermissionAsync(int userId, string permissionCode);
    Task<List<string>> GetUserPermissionsAsync(int userId);
    Task<List<string>> GetUserRolesAsync(int userId);
    Task<List<UserRoleDto>> GetAllUsersWithRolesAsync();
    Task<UserRoleDto?> GetUserRolesDetailAsync(int userId);
    Task<bool> AssignRoleToUserAsync(int userId, int roleId, int? actingUserId = null);
    Task<bool> RemoveRoleFromUserAsync(int userId, int roleId, int? actingUserId = null);
    void InvalidateUserCache(int userId);
    void InvalidateAllCache();
}

public class UserAuthorizationService : IUserAuthorizationService
{
    private readonly BestieDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserAuthorizationService> _logger;

    private static long _globalVersion = 1;

    public UserAuthorizationService(
        BestieDbContext context,
        IMemoryCache cache,
        ILogger<UserAuthorizationService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permissionCode)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(permissionCode)) return false;

        var permissions = await GetUserPermissionsAsync(userId);
        var upperCode = permissionCode.Trim().ToUpperInvariant();

        return permissions.Contains(upperCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetUserPermissionsAsync(int userId)
    {
        var cacheKey = $"rbac_perms_v{Volatile.Read(ref _globalVersion)}_user_{userId}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached != null)
        {
            return cached;
        }

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user == null)
        {
            return new List<string>();
        }

        var isSuperAdmin = user.UserRoles.Any(ur => ur.Role.RoleName == "SuperAdmin");
        List<string> permissions;

        if (isSuperAdmin)
        {
            // SuperAdmin dynamically has all permissions in the catalog
            permissions = await _context.Permissions
                .AsNoTracking()
                .Select(p => p.PermissionCode.ToUpperInvariant())
                .Distinct()
                .ToListAsync();
        }
        else
        {
            permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.PermissionCode.ToUpperInvariant())
                .Distinct()
                .ToList();
        }

        _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(10));
        return permissions;
    }

    public async Task<List<string>> GetUserRolesAsync(int userId)
    {
        var cacheKey = $"rbac_roles_v{Volatile.Read(ref _globalVersion)}_user_{userId}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached != null)
        {
            return cached;
        }

        var roles = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.RoleName)
            .Distinct()
            .ToListAsync();

        _cache.Set(cacheKey, roles, TimeSpan.FromMinutes(10));
        return roles;
    }

    public async Task<List<UserRoleDto>> GetAllUsersWithRolesAsync()
    {
        var users = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.UserId)
            .ToListAsync();

        return users.Select(u => new UserRoleDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            DisplayName = u.DisplayName,
            AvatarSticker = u.AvatarSticker,
            IsActive = u.IsActive,
            Roles = u.UserRoles.Select(ur => new RoleDto
            {
                RoleId = ur.Role.RoleId,
                RoleName = ur.Role.RoleName,
                Description = ur.Role.Description,
                IsSystemRole = ur.Role.IsSystemRole
            }).ToList()
        }).ToList();
    }

    public async Task<UserRoleDto?> GetUserRolesDetailAsync(int userId)
    {
        var u = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (u == null) return null;

        return new UserRoleDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            DisplayName = u.DisplayName,
            AvatarSticker = u.AvatarSticker,
            IsActive = u.IsActive,
            Roles = u.UserRoles.Select(ur => new RoleDto
            {
                RoleId = ur.Role.RoleId,
                RoleName = ur.Role.RoleName,
                Description = ur.Role.Description,
                IsSystemRole = ur.Role.IsSystemRole
            }).ToList()
        };
    }

    public async Task<bool> AssignRoleToUserAsync(int userId, int roleId, int? actingUserId = null)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            throw new ArgumentException("User not found or inactive.");
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);
        if (role == null)
        {
            throw new ArgumentException("Role not found.");
        }

        // Security check: users cannot modify their own roles
        if (actingUserId.HasValue && actingUserId.Value == userId)
        {
            throw new InvalidOperationException("Users cannot assign roles to themselves.");
        }

        // Security check: only SuperAdmin can assign SuperAdmin role
        if (role.RoleName == "SuperAdmin" && actingUserId.HasValue)
        {
            var actingIsSuper = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == actingUserId.Value && ur.Role.RoleName == "SuperAdmin");
            if (!actingIsSuper)
            {
                throw new UnauthorizedAccessException("Only an existing SuperAdmin can assign the SuperAdmin role.");
            }
        }

        // Prevent duplicate role assignment
        var alreadyHasRole = await _context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (alreadyHasRole)
        {
            return true; // Already assigned, safe idempotent return
        }

        _context.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId
        });

        await _context.SaveChangesAsync();
        InvalidateUserCache(userId);

        _logger.LogInformation("Assigned role '{RoleName}' (RoleId: {RoleId}) to user '{Username}' (UserId: {UserId}) by Actor: {ActorId}",
            role.RoleName, roleId, user.Username, userId, actingUserId);

        return true;
    }

    public async Task<bool> RemoveRoleFromUserAsync(int userId, int roleId, int? actingUserId = null)
    {
        var userRole = await _context.UserRoles
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole == null)
        {
            return true; // Not assigned, safe idempotent return
        }

        // Security check: users cannot modify their own roles
        if (actingUserId.HasValue && actingUserId.Value == userId)
        {
            throw new InvalidOperationException("Users cannot remove roles from themselves.");
        }

        // Critical safety rule: Cannot remove the last SuperAdmin account in the system!
        if (userRole.Role.RoleName == "SuperAdmin")
        {
            var totalSuperAdmins = await _context.UserRoles
                .CountAsync(ur => ur.Role.RoleName == "SuperAdmin" && ur.UserId != userId);

            if (totalSuperAdmins <= 0)
            {
                throw new InvalidOperationException("Cannot remove the last SuperAdmin role in the system! At least one active SuperAdmin must remain.");
            }
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();
        InvalidateUserCache(userId);

        _logger.LogInformation("Removed role '{RoleName}' from user UserId: {UserId} by Actor: {ActorId}",
            userRole.Role.RoleName, userId, actingUserId);

        return true;
    }

    public void InvalidateUserCache(int userId)
    {
        var version = Volatile.Read(ref _globalVersion);
        _cache.Remove($"rbac_perms_v{version}_user_{userId}");
        _cache.Remove($"rbac_roles_v{version}_user_{userId}");
    }

    public void InvalidateAllCache()
    {
        // Instantly invalidates all permission and role caches for all users
        Interlocked.Increment(ref _globalVersion);
        _logger.LogInformation("Global RBAC permission cache invalidated.");
    }
}
