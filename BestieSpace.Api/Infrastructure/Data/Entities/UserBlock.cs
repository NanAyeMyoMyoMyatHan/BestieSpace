using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class UserBlock
{
    public int UserBlockId { get; set; }

    public int BlockerUserId { get; set; }

    public int BlockedUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User BlockedUser { get; set; } = null!;

    public virtual User BlockerUser { get; set; } = null!;
}
