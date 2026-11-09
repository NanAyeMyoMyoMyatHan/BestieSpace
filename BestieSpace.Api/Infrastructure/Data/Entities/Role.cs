using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a security / circle role in the system.
/// </summary>
public class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this is a protected system role (e.g. SuperAdmin, Admin, User) that cannot be deleted or renamed.
    /// </summary>
    public bool IsSystemRole { get; set; } = false;

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public virtual ICollection<BestieCircleMember> CircleMembers { get; set; } = new List<BestieCircleMember>();
}
