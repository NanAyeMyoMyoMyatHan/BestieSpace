namespace BestieSpace.Shared.DTOs;

public class MemoryDto
{
    public int Id { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime MemoryDate { get; set; }
    public string StickerType { get; set; } = "heart";
    public double RotationAngle { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMemoryRequest
{
    public string Caption { get; set; } = string.Empty;
    public string StickerType { get; set; } = "heart";
    public DateTime? MemoryDate { get; set; }
}

public class UpdateMemoryRequest
{
    public string Caption { get; set; } = string.Empty;
    public string StickerType { get; set; } = "heart";
    public DateTime? MemoryDate { get; set; }
}
