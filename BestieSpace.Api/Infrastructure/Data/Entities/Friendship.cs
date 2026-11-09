using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class Friendship
{
    public int FriendshipId { get; set; }

    public int UserId { get; set; }

    public int FriendUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int UserA { get; set; }

    public int UserB { get; set; }

    public virtual User FriendUser { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
