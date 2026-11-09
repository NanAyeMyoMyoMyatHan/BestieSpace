using System;
using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

// =============================================================================
// Read Receipt DTOs
// =============================================================================

public class MarkMessageReadRequestDto
{
    public int CircleId { get; set; }
    public int MessageId { get; set; }
}

public class MarkMessagesReadRequestDto
{
    public int CircleId { get; set; }
    public List<int> MessageIds { get; set; } = new();
}

public class MessageReadReceiptDto
{
    public int ChatMessageReadId { get; set; }
    public int ChatMessageId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarSticker { get; set; }
    public DateTime ReadAt { get; set; }
}

public class CircleMemberReadStatusDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarSticker { get; set; }
    public bool HasRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool HasDelivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
}

public class MessageReadDetailsDto
{
    public int MessageId { get; set; }
    public int CircleId { get; set; }
    public int TotalMembers { get; set; }
    public int ReadCount { get; set; }
    public int UnreadCount { get; set; }
    public List<CircleMemberReadStatusDto> Members { get; set; } = new();
}

public class MessageReadEventDto
{
    public int CircleId { get; set; }
    public List<int> MessageIds { get; set; } = new();
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarSticker { get; set; }
    public DateTime ReadAt { get; set; }
}

// =============================================================================
// Delivery Receipt DTOs
// =============================================================================

public class AcknowledgeDeliveryRequestDto
{
    public int CircleId { get; set; }
    public List<int> MessageIds { get; set; } = new();
}

public class MessageDeliveredEventDto
{
    public int CircleId { get; set; }
    public List<int> MessageIds { get; set; } = new();
    public int UserId { get; set; }
    public DateTime DeliveredAt { get; set; }
}

// =============================================================================
// Offline Message Synchronization & Unread Count DTOs
// =============================================================================

public class MessageSyncResponseDto
{
    public int CircleId { get; set; }
    public int AfterMessageId { get; set; }
    public bool HasMore { get; set; }
    public int LatestMessageId { get; set; }
    public List<CircleChatMessageDto> Messages { get; set; } = new();
}

public class CircleUnreadDto
{
    public int CircleId { get; set; }
    public string CircleName { get; set; } = string.Empty;
    public string? CircleIcon { get; set; }
    public int UnreadCount { get; set; }
    public string? LastMessageContent { get; set; }
    public string? LastMessageSender { get; set; }
    public string? LastMessageType { get; set; }
    public DateTime? LastMessageTime { get; set; }
}

public class UnreadSummaryDto
{
    public int TotalUnreadCount { get; set; }
    public List<CircleUnreadDto> Circles { get; set; } = new();
}

public class CircleUnreadCountUpdatedEventDto
{
    public int CircleId { get; set; }
    public int UserId { get; set; }
    public int UnreadCount { get; set; }
    public int TotalUnreadCount { get; set; }
}

// =============================================================================
// Notification DTOs
// =============================================================================

public class NotificationDto
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? TargetUrl { get; set; }
}

public class NotificationListResponseDto
{
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<NotificationDto> Notifications { get; set; } = new();
}

public class NotificationEventDto
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UnreadNotificationCount { get; set; }
}
