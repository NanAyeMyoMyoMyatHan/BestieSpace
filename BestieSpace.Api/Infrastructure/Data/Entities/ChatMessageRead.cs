using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class ChatMessageRead
{
    public int ChatMessageReadId { get; set; }

    public int ChatMessageId { get; set; }

    public int UserId { get; set; }

    public DateTime ReadAt { get; set; }

    public virtual ChatMessage ChatMessage { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
