using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.Rbac;

public interface IPermissionService
{
    Task<List<PermissionDto>> GetAllPermissionsAsync();
    Task<PermissionDto?> GetPermissionByCodeAsync(string permissionCode);
    Task<List<PermissionDto>> GetPermissionsByRoleIdAsync(int roleId);
}

public class PermissionService : IPermissionService
{
    private readonly BestieDbContext _context;

    public PermissionService(BestieDbContext context)
    {
        _context = context;
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync()
    {
        var permissions = await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.PermissionCode)
            .ToListAsync();

        return permissions.Select(p => new PermissionDto
        {
            PermissionId = p.PermissionId,
            PermissionCode = p.PermissionCode,
            Description = p.Description,
            Category = ResolveCategory(p.PermissionCode)
        }).ToList();
    }

    public async Task<PermissionDto?> GetPermissionByCodeAsync(string permissionCode)
    {
        var p = await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PermissionCode == permissionCode.Trim().ToUpperInvariant());

        if (p == null) return null;

        return new PermissionDto
        {
            PermissionId = p.PermissionId,
            PermissionCode = p.PermissionCode,
            Description = p.Description,
            Category = ResolveCategory(p.PermissionCode)
        };
    }

    public async Task<List<PermissionDto>> GetPermissionsByRoleIdAsync(int roleId)
    {
        var permissions = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.PermissionCode)
            .ToListAsync();

        return permissions.Select(p => new PermissionDto
        {
            PermissionId = p.PermissionId,
            PermissionCode = p.PermissionCode,
            Description = p.Description,
            Category = ResolveCategory(p.PermissionCode)
        }).ToList();
    }

    public static string ResolveCategory(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return "General";
        var upper = permissionCode.ToUpperInvariant();

        if (upper.StartsWith("USER_ROLE_") || upper.StartsWith("ROLE_") || upper.StartsWith("PERMISSION_"))
            return "Roles & Permissions";
        if (upper.StartsWith("USER_"))
            return "User Management";
        if (upper.StartsWith("FRIEND_"))
            return "Friendships";
        if (upper.StartsWith("CIRCLE_"))
            return "Bestie Circles";
        if (upper.StartsWith("CHAT_"))
            return "Clubhouse Chat";
        if (upper.StartsWith("MEMORY_"))
            return "Memories Gallery";
        if (upper.StartsWith("SECRET_NOTE_"))
            return "Secret Notes";
        if (upper.StartsWith("SYSTEM_"))
            return "System & Metrics";

        return "General";
    }
}
