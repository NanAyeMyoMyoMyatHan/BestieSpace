using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing an individual sticker within a sticker category.
/// </summary>
public class Sticker
{
    public int StickerId { get; set; }
    public int CategoryId { get; set; }
    public string StickerName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual StickerCategory Category { get; set; } = null!;
}
