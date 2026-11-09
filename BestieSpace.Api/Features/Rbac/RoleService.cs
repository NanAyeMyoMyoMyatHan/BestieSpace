using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Rbac;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllRolesAsync();
    Task<RoleDetailDto?> GetRoleByIdAsync(int roleId);
    Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequestDto request);
    Task<RoleDetailDto> UpdateRoleAsync(int roleId, UpdateRoleRequestDto request);
    Task<bool> DeleteRoleAsync(int roleId);
    Task<RoleDetailDto> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequestDto request);
}

public class RoleService : IRoleService
{
    private readonly BestieDbContext _context;
    private readonly IUserAuthorizationService _userAuthService;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        BestieDbContext context,
        IUserAuthorizationService userAuthService,
        ILogger<RoleService> logger)
    {
        _context = context;
        _userAuthService = userAuthService;
        _logger = logger;
    }

    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.RoleId)
            .ToListAsync();

        return roles.Select(r => new RoleDto
        {
            RoleId = r.RoleId,
            RoleName = r.RoleName,
            Description = r.Description,
            IsSystemRole = r.IsSystemRole,
            UserCount = r.UserRoles.Count,
            Permissions = r.RolePermissions.Select(rp => rp.Permission.PermissionCode).OrderBy(p => p).ToList()
        }).ToList();
    }

    public async Task<RoleDetailDto?> GetRoleByIdAsync(int roleId)
    {
        var role = await _context.Roles
            .AsNoTracking()
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null) return null;

        return new RoleDetailDto
        {
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            UserCount = role.UserRoles.Count,
            Permissions = role.RolePermissions
                .Select(rp => new PermissionDto
                {
                    PermissionId = rp.Permission.PermissionId,
                    PermissionCode = rp.Permission.PermissionCode,
                    Description = rp.Permission.Description,
                    Category = PermissionService.ResolveCategory(rp.Permission.PermissionCode)
                })
                .OrderBy(p => p.PermissionCode)
                .ToList()
        };
    }

    public async Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            throw new ArgumentException("Role name cannot be empty.");
        }

        var normalizedName = request.RoleName.Trim();
        var exists = await _context.Roles.AnyAsync(r => r.RoleName.ToLower() == normalizedName.ToLower());
        if (exists)
        {
            throw new InvalidOperationException($"Role '{normalizedName}' already exists.");
        }

        var newRole = new Role
        {
            RoleName = normalizedName,
            Description = request.Description?.Trim(),
            IsSystemRole = false
        };

        _context.Roles.Add(newRole);
        await _context.SaveChangesAsync();

        if (request.Permissions != null && request.Permissions.Any())
        {
            var requestedUpper = request.Permissions.Select(p => p.Trim().ToUpperInvariant()).Distinct().ToList();
            var validPermissions = await _context.Permissions
                .Where(p => requestedUpper.Contains(p.PermissionCode.ToUpper()))
                .ToListAsync();

            foreach (var perm in validPermissions)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = newRole.RoleId,
                    PermissionId = perm.PermissionId
                });
            }

            await _context.SaveChangesAsync();
        }

        _userAuthService.InvalidateAllCache();
        _logger.LogInformation("Created new role '{RoleName}' with ID {RoleId}", newRole.RoleName, newRole.RoleId);

        return (await GetRoleByIdAsync(newRole.RoleId))!;
    }

    public async Task<RoleDetailDto> UpdateRoleAsync(int roleId, UpdateRoleRequestDto request)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);
        if (role == null)
        {
            throw new ArgumentException($"Role with ID {roleId} not found.");
        }

        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            throw new ArgumentException("Role name cannot be empty.");
        }

        var normalizedName = request.RoleName.Trim();

        // System role protection: do not allow renaming system roles
        if (role.IsSystemRole && !string.Equals(role.RoleName, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot rename protected system role '{role.RoleName}'.");
        }

        if (!string.Equals(role.RoleName, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _context.Roles.AnyAsync(r => r.RoleId != roleId && r.RoleName.ToLower() == normalizedName.ToLower());
            if (exists)
            {
                throw new InvalidOperationException($"Another role named '{normalizedName}' already exists.");
            }
            role.RoleName = normalizedName;
        }

        role.Description = request.Description?.Trim();
        await _context.SaveChangesAsync();

        _userAuthService.InvalidateAllCache();
        _logger.LogInformation("Updated role ID {RoleId} ('{RoleName}')", roleId, role.RoleName);

        return (await GetRoleByIdAsync(roleId))!;
    }

    public async Task<bool> DeleteRoleAsync(int roleId)
    {
        var role = await _context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null)
        {
            throw new ArgumentException($"Role with ID {roleId} not found.");
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException($"Cannot delete protected system role '{role.RoleName}'.");
        }

        if (role.UserRoles.Any())
        {
            throw new InvalidOperationException($"Cannot delete role '{role.RoleName}' because {role.UserRoles.Count} user(s) are currently assigned to it. Reassign or remove their roles first.");
        }

        // Delete associated role-permissions
        var rolePermissions = await _context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
        _context.RolePermissions.RemoveRange(rolePermissions);

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        _userAuthService.InvalidateAllCache();
        _logger.LogInformation("Deleted custom role ID {RoleId} ('{RoleName}')", roleId, role.RoleName);

        return true;
    }

    public async Task<RoleDetailDto> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequestDto request)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null)
        {
            throw new ArgumentException($"Role with ID {roleId} not found.");
        }

        // Remove existing permissions
        _context.RolePermissions.RemoveRange(role.RolePermissions);

        // Add selected permissions
        if (request.PermissionCodes != null && request.PermissionCodes.Any())
        {
            var requestedUpper = request.PermissionCodes.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
            var matchedPermissions = await _context.Permissions
                .Where(p => requestedUpper.Contains(p.PermissionCode.ToUpper()))
                .ToListAsync();

            foreach (var perm in matchedPermissions)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = perm.PermissionId
                });
            }
        }

        await _context.SaveChangesAsync();
        _userAuthService.InvalidateAllCache();

        _logger.LogInformation("Updated permissions for role ID {RoleId} ('{RoleName}'). Assigned {Count} permissions.",
            roleId, role.RoleName, request.PermissionCodes?.Count ?? 0);

        return (await GetRoleByIdAsync(roleId))!;
    }
}
