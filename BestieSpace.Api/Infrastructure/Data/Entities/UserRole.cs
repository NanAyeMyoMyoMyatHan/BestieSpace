namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Associative entity mapping users to roles.
/// </summary>
public class UserRole
{
    public int UserRoleId { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Role Role { get; set; } = null!;
}
