using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents an invitation for a user to join a Bestie Circle.
/// </summary>
public class CircleInvitation
{
    public int InvitationId { get; set; }

    public int CircleId { get; set; }

    public int InviterUserId { get; set; }

    public int InviteeUserId { get; set; }

    /// <summary>
    /// Status: "Pending", "Accepted", "Declined", "Cancelled"
    /// </summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }

    // Navigation properties
    public virtual BestieCircle Circle { get; set; } = null!;
    public virtual User InviterUser { get; set; } = null!;
    public virtual User InviteeUser { get; set; } = null!;
}
