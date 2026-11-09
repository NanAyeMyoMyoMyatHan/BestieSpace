namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Associative entity mapping roles to permissions.
/// </summary>
public class RolePermission
{
    public int RolePermissionId { get; set; }

    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    // Navigation properties
    public virtual Role Role { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
