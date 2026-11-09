using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

public class RoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int UserCount { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RoleDetailDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int UserCount { get; set; }
    public List<PermissionDto> Permissions { get; set; } = new();
}

public class CreateRoleRequestDto
{
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class UpdateRoleRequestDto
{
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateRolePermissionsRequestDto
{
    public List<string> PermissionCodes { get; set; } = new();
}

public class PermissionDto
{
    public int PermissionId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
}

public class UserRoleDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarSticker { get; set; } = "🌸";
    public bool IsActive { get; set; }
    public List<RoleDto> Roles { get; set; } = new();
}

public class AssignUserRoleRequestDto
{
    public int RoleId { get; set; }
}

public class RbacOperationResultDto
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
