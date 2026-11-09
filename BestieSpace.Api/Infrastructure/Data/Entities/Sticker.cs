using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class Sticker
{
    public int StickerId { get; set; }

    public int CategoryId { get; set; }

    public string StickerName { get; set; } = null!;

    public string ImageUrl { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual StickerCategory Category { get; set; } = null!;
}
