using System;
using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

/// <summary>
/// DTO representing an individual sticker item.
/// </summary>
public class StickerDto
{
    public int StickerId { get; set; }
    public int CategoryId { get; set; }
    public string StickerName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO representing a sticker category collection with nested stickers.
/// </summary>
public class StickerCategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public List<StickerDto> Stickers { get; set; } = new();
}

/// <summary>
/// Request DTO for creating a new sticker category (Admin).
/// </summary>
public class CreateStickerCategoryRequestDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = "🌸";
    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Request DTO for adding a new sticker into a category (Admin).
/// </summary>
public class CreateStickerRequestDto
{
    public int CategoryId { get; set; }
    public string StickerName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Request DTO for updating an existing sticker (Admin).
/// </summary>
public class UpdateStickerRequestDto
{
    public string StickerName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Response returned after successfully uploading or creating a media file.
/// </summary>
public class MediaUploadResponseDto
{
    public int MediaId { get; set; }
    public int CircleId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public double? DurationSeconds { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request DTO for sending a sticker message to a Bestie Circle.
/// </summary>
public class SendStickerMessageRequestDto
{
    public int CircleId { get; set; }
    public int StickerId { get; set; }
}

/// <summary>
/// Request DTO for sending a photo message with base64 payload (used when multipart is inconvenient).
/// </summary>
public class SendPhotoBase64RequestDto
{
    public int CircleId { get; set; }
    public string? Caption { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public string Base64Data { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for sending a voice message with base64 payload.
/// </summary>
public class SendVoiceBase64RequestDto
{
    public int CircleId { get; set; }
    public double DurationSeconds { get; set; }
    public string FileName { get; set; } = "voice.webm";
    public string ContentType { get; set; } = "audio/webm";
    public string Base64Data { get; set; } = string.Empty;
}
