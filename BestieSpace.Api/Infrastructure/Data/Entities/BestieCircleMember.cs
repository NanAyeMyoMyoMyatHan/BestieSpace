using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a membership in a Bestie Circle.
/// Maximum 9 members (including owner) is enforced at the service layer transactionally in Phase 2.
/// </summary>
public class BestieCircleMember
{
    public int CircleMemberId { get; set; }

    public int CircleId { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual BestieCircle Circle { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Role Role { get; set; } = null!;
}
