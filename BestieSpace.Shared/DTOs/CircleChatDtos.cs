using System;
using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

/// <summary>
/// DTO representing a real-time circle chat message.
/// </summary>
public class CircleChatMessageDto
{
    public int Id { get; set; }
    public int ChatMessageId => Id;
    public int CircleId { get; set; }
    public int SenderId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string? SenderAvatarSticker { get; set; }
    public string? SenderProfilePicture { get; set; }
    public string MessageType { get; set; } = "text"; // "text", "system", etc.
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsOwnMessage { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

/// <summary>
/// Request model for sending a new message to a Bestie Circle.
/// </summary>
public class SendCircleMessageRequestDto
{
    public int CircleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MessageType { get; set; } = "text";
}

/// <summary>
/// Request model for editing an existing message in a Bestie Circle.
/// </summary>
public class EditCircleMessageRequestDto
{
    public int MessageId { get; set; }
    public int CircleId { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Request model for soft-deleting a message in a Bestie Circle.
/// </summary>
public class DeleteCircleMessageRequestDto
{
    public int MessageId { get; set; }
    public int CircleId { get; set; }
}

/// <summary>
/// Paginated message history response DTO.
/// </summary>
public class CircleChatPaginationDto
{
    public int CircleId { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasMore { get; set; }
    public List<CircleChatMessageDto> Messages { get; set; } = new();
}

/// <summary>
/// SignalR real-time event for when a message is soft-deleted.
/// </summary>
public class CircleMessageDeletedEventDto
{
    public int MessageId { get; set; }
    public int CircleId { get; set; }
    public int DeletedByUserId { get; set; }
    public DateTime DeletedAt { get; set; }
}

/// <summary>
/// SignalR real-time event for member presence changes (online / offline).
/// </summary>
public class CircleMemberPresenceEventDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarSticker { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public int? CircleId { get; set; }
}

/// <summary>
/// SignalR real-time event when a user joins the circle's live chat group.
/// </summary>
public class CircleMemberJoinedEventDto
{
    public int CircleId { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

/// <summary>
/// SignalR real-time event when a user leaves the circle's live chat group.
/// </summary>
public class CircleMemberLeftEventDto
{
    public int CircleId { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
