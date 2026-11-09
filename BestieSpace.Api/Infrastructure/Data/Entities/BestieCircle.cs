using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a private friendship circle (maximum of 9 members including owner).
/// </summary>
public class BestieCircle
{
    public int CircleId { get; set; }

    public string CircleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<BestieCircleMember> Members { get; set; } = new List<BestieCircleMember>();
    public virtual ICollection<CircleInvitation> Invitations { get; set; } = new List<CircleInvitation>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
