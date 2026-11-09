using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class CircleInvitation
{
    public int InvitationId { get; set; }

    public int CircleId { get; set; }

    public int InviterUserId { get; set; }

    public int InviteeUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual BestieCircle Circle { get; set; } = null!;

    public virtual User InviteeUser { get; set; } = null!;

    public virtual User InviterUser { get; set; } = null!;
}
