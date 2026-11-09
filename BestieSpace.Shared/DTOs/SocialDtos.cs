using System;
using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

/// <summary>
/// Detailed profile representation for user accounts.
/// Sensitive fields (password hashes, refresh tokens) are never included.
/// </summary>
public class UserProfileDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; }
    public string AvatarSticker { get; set; } = "🌸";
    public string? Bio { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public int FriendCount { get; set; }
    public string FriendshipStatusWithViewer { get; set; } = "None"; // "None", "PendingSent", "PendingReceived", "Accepted", "Blocked", "BlockedBy", "Self"
    public int? ActiveFriendshipId { get; set; }
}

/// <summary>
/// Request payload to update own profile.
/// </summary>
public class UpdateUserProfileRequestDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string AvatarSticker { get; set; } = "🌸";
}

/// <summary>
/// Represents a user card in user discovery and search results.
/// </summary>
public class UserDiscoveryCardDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; }
    public string AvatarSticker { get; set; } = "🌸";
    public string? Bio { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public int MutualFriendCount { get; set; }
    public string FriendshipStatus { get; set; } = "None"; // "None", "PendingSent", "PendingReceived", "Accepted", "Blocked", "BlockedBy"
    public int? ActiveFriendshipId { get; set; }
}

/// <summary>
/// Generic server-side pagination result wrapper.
/// </summary>
public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Detailed friendship entity DTO.
/// </summary>
public class FriendshipDto
{
    public int FriendshipId { get; set; }
    
    // Requester (User who initiated the request)
    public int RequesterUserId { get; set; }
    public string RequesterUsername { get; set; } = string.Empty;
    public string RequesterDisplayName { get; set; } = string.Empty;
    public string? RequesterProfilePicture { get; set; }
    public string RequesterAvatarSticker { get; set; } = "🌸";

    // Addressee (User who received the request)
    public int AddresseeUserId { get; set; }
    public string AddresseeUsername { get; set; } = string.Empty;
    public string AddresseeDisplayName { get; set; } = string.Empty;
    public string? AddresseeProfilePicture { get; set; }
    public string AddresseeAvatarSticker { get; set; } = "🌸";

    /// <summary>
    /// Status: "Pending", "Accepted", "Rejected"
    /// </summary>
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Other user in this friendship relative to viewer
    /// </summary>
    public int FriendUserId(int currentUserId) => currentUserId == RequesterUserId ? AddresseeUserId : RequesterUserId;
    public string FriendUsername(int currentUserId) => currentUserId == RequesterUserId ? AddresseeUsername : RequesterUsername;
    public string FriendDisplayName(int currentUserId) => currentUserId == RequesterUserId ? AddresseeDisplayName : RequesterDisplayName;
    public string? FriendProfilePicture(int currentUserId) => currentUserId == RequesterUserId ? AddresseeProfilePicture : RequesterProfilePicture;
    public string FriendAvatarSticker(int currentUserId) => currentUserId == RequesterUserId ? AddresseeAvatarSticker : RequesterAvatarSticker;
}

/// <summary>
/// Request to send a friend request.
/// </summary>
public class SendFriendRequestDto
{
    public int TargetUserId { get; set; }
}

/// <summary>
/// Blocked user summary.
/// </summary>
public class UserBlockDto
{
    public int UserBlockId { get; set; }
    public int BlockedUserId { get; set; }
    public string BlockedUsername { get; set; } = string.Empty;
    public string BlockedDisplayName { get; set; } = string.Empty;
    public string? BlockedProfilePicture { get; set; }
    public string BlockedAvatarSticker { get; set; } = "🌸";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Generic result for social actions (friend request, accept, reject, block, etc.)
/// </summary>
public class SocialOperationResultDto
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FriendshipStatus { get; set; }
    public int? FriendshipId { get; set; }
}
