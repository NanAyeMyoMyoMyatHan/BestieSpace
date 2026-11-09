using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing a sticker collection category.
/// </summary>
public class StickerCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = "🌸";
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Sticker> Stickers { get; set; } = new List<Sticker>();
}
