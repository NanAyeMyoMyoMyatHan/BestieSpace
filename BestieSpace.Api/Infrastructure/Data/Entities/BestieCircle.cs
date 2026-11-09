using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class BestieCircle
{
    public int CircleId { get; set; }

    public string CircleName { get; set; } = null!;

    public string? Description { get; set; }

    public string CircleIcon { get; set; } = null!;

    public string? CircleImage { get; set; }

    public int OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<BestieCircleMember> BestieCircleMembers { get; set; } = new List<BestieCircleMember>();

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<CircleInvitation> CircleInvitations { get; set; } = new List<CircleInvitation>();

    public virtual ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public virtual User OwnerUser { get; set; } = null!;
}
