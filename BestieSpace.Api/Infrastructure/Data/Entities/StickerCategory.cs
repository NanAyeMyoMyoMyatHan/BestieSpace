using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class StickerCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string Icon { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Sticker> Stickers { get; set; } = new List<Sticker>();
}
