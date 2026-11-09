using System.Net.Http.Json;
using BestieSpace.Shared.DTOs;

namespace BestieSpace.Client.Services;

public interface IBestieApiClient
{
    string ApiBaseUrl { get; }
    string? CurrentBearerToken { get; }
    void SetBearerToken(string? token);

    Task<DashboardDto> GetDashboardAsync();
    Task<MissYouResponse?> IncrementMissYouAsync();
    Task<MemoryDto?> AddMemoryAsync(MultipartFormDataContent content);
    Task<SecretNoteDto?> UnlockNoteAsync(int id);
    Task<List<ChatMessageDto>> GetChatMessagesAsync(string? viewer = null);
    Task<ChatMessageDto?> SendTextOrStickerAsync(SendTextOrStickerRequest request);
    Task<ChatMessageDto?> UploadPhotoAsync(MultipartFormDataContent content);
    Task<bool> MarkMessagesReadAsync(MarkMessagesReadRequest request);
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task<AuthResponseDto> RevokeTokenAsync(RevokeTokenRequestDto? request = null);
    Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request);
    Task<PasswordHintDto> GetPasswordHintAsync(string? username = null);
    Task<UserPresenceDto?> GetPresenceAsync(string roleKey);

    // Dynamic RBAC APIs (Phase 3)
    Task<List<string>> GetMyPermissionsAsync();
    Task<List<RoleDto>> GetRolesAsync();
    Task<RoleDetailDto?> GetRoleByIdAsync(int roleId);
    Task<RoleDetailDto?> CreateRoleAsync(CreateRoleRequestDto request);
    Task<RoleDetailDto?> UpdateRoleAsync(int roleId, UpdateRoleRequestDto request);
    Task<RbacOperationResultDto> DeleteRoleAsync(int roleId);
    Task<RoleDetailDto?> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequestDto request);
    Task<List<PermissionDto>> GetPermissionsAsync();
    Task<List<PermissionDto>> GetRolePermissionsAsync(int roleId);
    Task<List<UserRoleDto>> GetUsersWithRolesAsync();
    Task<UserRoleDto?> GetUserRolesDetailAsync(int userId);
    Task<RbacOperationResultDto> AssignRoleToUserAsync(int userId, int roleId);
    Task<RbacOperationResultDto> RemoveRoleFromUserAsync(int userId, int roleId);

    // User Profiles, Social Discovery & Friendships (Phase 4)
    Task<UserProfileDto?> GetMyProfileAsync();
    Task<UserProfileDto?> GetProfileByIdAsync(int userId);
    Task<UserProfileDto?> GetProfileByUsernameAsync(string username);
    Task<UserProfileDto?> UpdateMyProfileAsync(UpdateUserProfileRequestDto request);
    Task<string?> UploadProfilePictureAsync(MultipartFormDataContent content);
    Task<SocialOperationResultDto> SendFriendRequestAsync(int targetUserId);
    Task<SocialOperationResultDto> AcceptFriendRequestAsync(int friendshipId);
    Task<SocialOperationResultDto> RejectFriendRequestAsync(int friendshipId);
    Task<SocialOperationResultDto> CancelFriendRequestAsync(int friendshipId);
    Task<SocialOperationResultDto> RemoveFriendAsync(int friendshipId);
    Task<List<FriendshipDto>> GetIncomingFriendRequestsAsync();
    Task<List<FriendshipDto>> GetOutgoingFriendRequestsAsync();
    Task<List<FriendshipDto>> GetFriendsListAsync(string? query = null);
    Task<SocialOperationResultDto> BlockUserAsync(int targetUserId);
    Task<SocialOperationResultDto> UnblockUserAsync(int targetUserId);
    Task<List<UserBlockDto>> GetBlockedUsersAsync();
    Task<PagedResultDto<UserDiscoveryCardDto>> DiscoverUsersAsync(string? query = null, int pageNumber = 1, int pageSize = 10);
    Task<List<UserDiscoveryCardDto>> GetSuggestedUsersAsync(int limit = 6);

    // Bestie Circles & Membership Management (Phase 5)
    Task<CircleDetailDto?> CreateCircleAsync(CreateCircleRequestDto request);
    Task<List<CircleDto>> GetMyCirclesAsync();
    Task<CircleDetailDto?> GetCircleDetailsAsync(int circleId);
    Task<CircleDetailDto?> UpdateCircleAsync(int circleId, UpdateCircleRequestDto request);
    Task<CircleOperationResultDto> DeleteCircleAsync(int circleId);
    Task<CircleOperationResultDto> SendCircleInvitationAsync(SendCircleInvitationRequestDto request);
    Task<CircleOperationResultDto> AcceptCircleInvitationAsync(int invitationId);
    Task<CircleOperationResultDto> RejectCircleInvitationAsync(int invitationId);
    Task<CircleOperationResultDto> CancelCircleInvitationAsync(int invitationId);
    Task<List<CircleInvitationDto>> GetIncomingCircleInvitationsAsync();
    Task<List<CircleInvitationDto>> GetCircleInvitationsAsync(int circleId);
    Task<List<CircleMemberDto>> GetCircleMembersAsync(int circleId);
    Task<CircleOperationResultDto> RemoveCircleMemberAsync(int circleId, int targetUserId);
    Task<CircleOperationResultDto> LeaveCircleAsync(int circleId);
    Task<CircleOperationResultDto> ChangeCircleMemberRoleAsync(int circleId, ChangeMemberRoleRequestDto request);
    Task<CircleOperationResultDto> TransferCircleOwnershipAsync(int circleId, TransferCircleOwnershipRequestDto request);

    // Real-Time Private Group Chat (Phase 6)
    Task<CircleChatPaginationDto?> GetCircleMessagesAsync(int circleId, int pageNumber = 1, int pageSize = 30);
    Task<CircleChatMessageDto?> SendCircleMessageAsync(int circleId, string content, string? messageType = "text");
    Task<CircleChatMessageDto?> EditCircleMessageAsync(int circleId, int messageId, string newContent);
    Task<bool> DeleteCircleMessageAsync(int circleId, int messageId);
    Task<List<CircleMemberPresenceEventDto>> GetCirclePresenceAsync(int circleId);

    // Multimedia Messaging, Stickers & Media (Phase 7)
    Task<CircleChatMessageDto?> SendCirclePhotoAsync(int circleId, MultipartFormDataContent content);
    Task<CircleChatMessageDto?> SendCirclePhotoBase64Async(int circleId, SendPhotoBase64RequestDto request);
    Task<CircleChatMessageDto?> SendCircleVoiceAsync(int circleId, MultipartFormDataContent content);
    Task<CircleChatMessageDto?> SendCircleVoiceBase64Async(int circleId, SendVoiceBase64RequestDto request);
    Task<CircleChatMessageDto?> SendCircleStickerAsync(int circleId, int stickerId);
    Task<List<StickerCategoryDto>> GetStickerCategoriesAsync();
    string GetMediaUrl(int circleId, int mediaId, bool thumbnail = false);
}

public class BestieApiClient : IBestieApiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<BestieApiClient> _logger;
    private readonly string _baseUrl;
    private string? _bearerToken;

    public string ApiBaseUrl => _baseUrl;
    public string? CurrentBearerToken => _bearerToken;

    public void SetBearerToken(string? token)
    {
        _bearerToken = token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public BestieApiClient(HttpClient http, IConfiguration config, ILogger<BestieApiClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _baseUrl = _config["ApiBaseUrl"]?.TrimEnd('/') ?? "http://127.0.0.1:5000";
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<DashboardDto>($"{_baseUrl}/api/dashboard");
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch dashboard from API, using fallback data.");
        }

        return CreateFallbackDashboard();
    }

    public async Task<MissYouResponse?> IncrementMissYouAsync()
    {
        try
        {
            var res = await _http.PostAsync($"{_baseUrl}/api/dashboard/miss-you", null);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<MissYouResponse>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment miss you count.");
        }
        return null;
    }

    public async Task<MemoryDto?> AddMemoryAsync(MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync($"{_baseUrl}/api/memories", content);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<MemoryDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload memory.");
        }
        return null;
    }

    public async Task<SecretNoteDto?> UnlockNoteAsync(int id)
    {
        try
        {
            var res = await _http.PutAsync($"{_baseUrl}/api/notes/{id}/unlock", null);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<SecretNoteDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock note ID {Id}", id);
        }
        return null;
    }

    public async Task<List<ChatMessageDto>> GetChatMessagesAsync(string? viewer = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(viewer) 
                ? $"{_baseUrl}/api/chat/messages" 
                : $"{_baseUrl}/api/chat/messages?viewer={Uri.EscapeDataString(viewer)}";

            var res = await _http.GetFromJsonAsync<List<ChatMessageDto>>(url);
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch chat messages from API, using default list.");
        }
        return CreateFallbackMessages();
    }

    public async Task<ChatMessageDto?> SendTextOrStickerAsync(SendTextOrStickerRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/chat/send", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<ChatMessageDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send text or sticker message.");
        }
        return null;
    }

    public async Task<ChatMessageDto?> UploadPhotoAsync(MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync($"{_baseUrl}/api/chat/upload-photo", content);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<ChatMessageDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo chat message.");
        }
        return null;
    }

    public async Task<bool> MarkMessagesReadAsync(MarkMessagesReadRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/chat/mark-read", request);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark messages as read.");
            return false;
        }
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/register", request);
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (content != null)
            {
                if (content.IsSuccess && !string.IsNullOrWhiteSpace(content.AccessToken))
                {
                    SetBearerToken(content.AccessToken);
                }
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration request failed.");
        }

        return new AuthResponseDto
        {
            IsSuccess = false,
            Message = "Unable to connect to the BestieSpace server. Please ensure the API is running."
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/login", request);
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (content != null)
            {
                if (content.IsSuccess && !string.IsNullOrWhiteSpace(content.AccessToken))
                {
                    SetBearerToken(content.AccessToken);
                }
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login API request failed.");
        }

        return new AuthResponseDto
        {
            IsSuccess = false,
            Message = "Unable to connect to the BestieSpace server. Please check your connection."
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/refresh-token", request);
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (content != null)
            {
                if (content.IsSuccess && !string.IsNullOrWhiteSpace(content.AccessToken))
                {
                    SetBearerToken(content.AccessToken);
                }
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh token request failed.");
        }

        return new AuthResponseDto
        {
            IsSuccess = false,
            Message = "Failed to refresh token."
        };
    }

    public async Task<AuthResponseDto> RevokeTokenAsync(RevokeTokenRequestDto? request = null)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/revoke-token", request ?? new RevokeTokenRequestDto());
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            SetBearerToken(null);
            if (content != null) return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revoke token request failed.");
        }

        SetBearerToken(null);
        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = "Logged out successfully."
        };
    }

    public async Task<PasswordHintDto> GetPasswordHintAsync(string? username = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(username) 
                ? $"{_baseUrl}/api/auth/hint" 
                : $"{_baseUrl}/api/auth/hint?username={Uri.EscapeDataString(username)}";
            var res = await _http.GetFromJsonAsync<PasswordHintDto>(url);
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch password hint from API.");
        }

        var clean = username?.Trim().ToLowerInvariant();
        if (clean == "mochi_maker")
        {
            return new PasswordHintDto
            {
                Username = "mochi_maker",
                Hint = "Friends forever (bestie4ever!)"
            };
        }

        return new PasswordHintDto
        {
            Username = "strawberry_bestie",
            Hint = "How much I miss you (missyou3000!)"
        };
    }

    public async Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/pin-login", request);
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (content != null)
            {
                if (content.IsSuccess && !string.IsNullOrWhiteSpace(content.AccessToken))
                {
                    SetBearerToken(content.AccessToken);
                }
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PIN Login API request failed.");
        }

        return new AuthResponseDto
        {
            IsSuccess = false,
            Message = "Unable to connect to the BestieSpace server. Please check your connection."
        };
    }

    public async Task<UserPresenceDto?> GetPresenceAsync(string roleKey)
    {
        try
        {
            var clean = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
            var res = await _http.GetFromJsonAsync<UserPresenceDto>($"{_baseUrl}/api/chat/presence/{clean}");
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get presence from API.");
        }

        var isBestie = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase);
        return new UserPresenceDto
        {
            RoleKey = isBestie ? "Bestie" : "Me",
            DisplayName = isBestie ? "Sweet Bestie 🌸" : "Your Bestie 🎀",
            IsOnline = false,
            LastSeenAt = DateTime.UtcNow.AddMinutes(-10)
        };
    }

    // =========================================================================
    // DYNAMIC RBAC API CLIENT IMPLEMENTATIONS (PHASE 3)
    // =========================================================================

    public async Task<List<string>> GetMyPermissionsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<string>>("/api/rbac/my-permissions");
            return res ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current user permissions.");
            return new List<string>();
        }
    }

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<RoleDto>>("/api/rbac/roles");
            return res ?? new List<RoleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load roles from API.");
            return new List<RoleDto>();
        }
    }

    public async Task<RoleDetailDto?> GetRoleByIdAsync(int roleId)
    {
        try
        {
            return await _http.GetFromJsonAsync<RoleDetailDto>($"/api/rbac/roles/{roleId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get role details for ID {RoleId}", roleId);
            return null;
        }
    }

    public async Task<RoleDetailDto?> CreateRoleAsync(CreateRoleRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/rbac/roles", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<RoleDetailDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create role: {Error}", err);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", request.RoleName);
            return null;
        }
    }

    public async Task<RoleDetailDto?> UpdateRoleAsync(int roleId, UpdateRoleRequestDto request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/rbac/roles/{roleId}", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<RoleDetailDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update role: {Error}", err);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role ID {RoleId}", roleId);
            return null;
        }
    }

    public async Task<RbacOperationResultDto> DeleteRoleAsync(int roleId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/rbac/roles/{roleId}");
            if (res.IsSuccessStatusCode)
            {
                var ok = await res.Content.ReadFromJsonAsync<RbacOperationResultDto>();
                return ok ?? new RbacOperationResultDto { IsSuccess = true, Message = "Role deleted successfully." };
            }
            var err = await res.Content.ReadFromJsonAsync<RbacOperationResultDto>();
            return err ?? new RbacOperationResultDto { IsSuccess = false, Message = "Failed to delete role." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", roleId);
            return new RbacOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<RoleDetailDto?> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequestDto request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/rbac/roles/{roleId}/permissions", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<RoleDetailDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update role permissions: {Error}", err);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for role {RoleId}", roleId);
            return null;
        }
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<PermissionDto>>("/api/rbac/permissions");
            return res ?? new List<PermissionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load permissions from API.");
            return new List<PermissionDto>();
        }
    }

    public async Task<List<PermissionDto>> GetRolePermissionsAsync(int roleId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<PermissionDto>>($"/api/rbac/roles/{roleId}/permissions");
            return res ?? new List<PermissionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load permissions for role {RoleId}", roleId);
            return new List<PermissionDto>();
        }
    }

    public async Task<List<UserRoleDto>> GetUsersWithRolesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<UserRoleDto>>("/api/rbac/users");
            return res ?? new List<UserRoleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users with roles.");
            return new List<UserRoleDto>();
        }
    }

    public async Task<UserRoleDto?> GetUserRolesDetailAsync(int userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<UserRoleDto>($"/api/rbac/users/{userId}/roles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user role details for user {UserId}", userId);
            return null;
        }
    }

    public async Task<RbacOperationResultDto> AssignRoleToUserAsync(int userId, int roleId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/rbac/users/{userId}/roles", new AssignUserRoleRequestDto { RoleId = roleId });
            if (res.IsSuccessStatusCode)
            {
                var ok = await res.Content.ReadFromJsonAsync<RbacOperationResultDto>();
                return ok ?? new RbacOperationResultDto { IsSuccess = true, Message = "Role assigned successfully." };
            }
            var err = await res.Content.ReadFromJsonAsync<RbacOperationResultDto>();
            return err ?? new RbacOperationResultDto { IsSuccess = false, Message = "Failed to assign role." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
            return new RbacOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<RbacOperationResultDto> RemoveRoleFromUserAsync(int userId, int roleId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/rbac/users/{userId}/roles/{roleId}");
            if (res.IsSuccessStatusCode)
            {
                var ok = await res.Content.ReadFromJsonAsync<RbacOperationResultDto>();
                return ok ?? new RbacOperationResultDto { IsSuccess = true, Message = "Role removed successfully." };
            }
            var err = await res.Content.ReadFromJsonAsync<RbacOperationResultDto>();
            return err ?? new RbacOperationResultDto { IsSuccess = false, Message = "Failed to remove role." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
            return new RbacOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    // =========================================================================
    // PHASE 4: USER PROFILES, SOCIAL DISCOVERY & FRIENDSHIPS
    // =========================================================================

    public async Task<UserProfileDto?> GetMyProfileAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<UserProfileDto>("/api/userprofile/me");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get own profile.");
            return null;
        }
    }

    public async Task<UserProfileDto?> GetProfileByIdAsync(int userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<UserProfileDto>($"/api/userprofile/{userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get profile for user {UserId}", userId);
            return null;
        }
    }

    public async Task<UserProfileDto?> GetProfileByUsernameAsync(string username)
    {
        try
        {
            return await _http.GetFromJsonAsync<UserProfileDto>($"/api/userprofile/by-username/{Uri.EscapeDataString(username)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get profile for username {Username}", username);
            return null;
        }
    }

    public async Task<UserProfileDto?> UpdateMyProfileAsync(UpdateUserProfileRequestDto request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync("/api/userprofile/me", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<UserProfileDto>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile.");
            return null;
        }
    }

    public async Task<string?> UploadProfilePictureAsync(MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync("/api/userprofile/me/picture", content);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (json.TryGetProperty("profilePictureUrl", out var prop))
                {
                    return prop.GetString();
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload profile picture.");
            return null;
        }
    }

    public async Task<SocialOperationResultDto> SendFriendRequestAsync(int targetUserId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/friendship/request", new SendFriendRequestDto { TargetUserId = targetUserId });
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending friend request to user {UserId}", targetUserId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<SocialOperationResultDto> AcceptFriendRequestAsync(int friendshipId)
    {
        try
        {
            var res = await _http.PostAsync($"/api/friendship/{friendshipId}/accept", null);
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting friend request {FriendshipId}", friendshipId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<SocialOperationResultDto> RejectFriendRequestAsync(int friendshipId)
    {
        try
        {
            var res = await _http.PostAsync($"/api/friendship/{friendshipId}/reject", null);
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting friend request {FriendshipId}", friendshipId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<SocialOperationResultDto> CancelFriendRequestAsync(int friendshipId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/friendship/{friendshipId}/cancel");
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling friend request {FriendshipId}", friendshipId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<SocialOperationResultDto> RemoveFriendAsync(int friendshipId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/friendship/{friendshipId}/remove");
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing friend for friendship {FriendshipId}", friendshipId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<List<FriendshipDto>> GetIncomingFriendRequestsAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<FriendshipDto>>("/api/friendship/incoming");
            return list ?? new List<FriendshipDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load incoming friend requests.");
            return new List<FriendshipDto>();
        }
    }

    public async Task<List<FriendshipDto>> GetOutgoingFriendRequestsAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<FriendshipDto>>("/api/friendship/outgoing");
            return list ?? new List<FriendshipDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load outgoing friend requests.");
            return new List<FriendshipDto>();
        }
    }

    public async Task<List<FriendshipDto>> GetFriendsListAsync(string? query = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(query)
                ? "/api/friendship/friends"
                : $"/api/friendship/friends?query={Uri.EscapeDataString(query)}";

            var list = await _http.GetFromJsonAsync<List<FriendshipDto>>(url);
            return list ?? new List<FriendshipDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load friends list.");
            return new List<FriendshipDto>();
        }
    }

    public async Task<SocialOperationResultDto> BlockUserAsync(int targetUserId)
    {
        try
        {
            var res = await _http.PostAsync($"/api/userblock/{targetUserId}", null);
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to block user {UserId}", targetUserId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<SocialOperationResultDto> UnblockUserAsync(int targetUserId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/userblock/{targetUserId}");
            var result = await res.Content.ReadFromJsonAsync<SocialOperationResultDto>();
            return result ?? new SocialOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unblock user {UserId}", targetUserId);
            return new SocialOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<List<UserBlockDto>> GetBlockedUsersAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<UserBlockDto>>("/api/userblock");
            return list ?? new List<UserBlockDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blocked users list.");
            return new List<UserBlockDto>();
        }
    }

    public async Task<PagedResultDto<UserDiscoveryCardDto>> DiscoverUsersAsync(string? query = null, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            var url = $"/api/userdiscovery?pageNumber={pageNumber}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(query))
            {
                url += $"&query={Uri.EscapeDataString(query)}";
            }
            var result = await _http.GetFromJsonAsync<PagedResultDto<UserDiscoveryCardDto>>(url);
            return result ?? new PagedResultDto<UserDiscoveryCardDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover users.");
            return new PagedResultDto<UserDiscoveryCardDto>();
        }
    }

    public async Task<List<UserDiscoveryCardDto>> GetSuggestedUsersAsync(int limit = 6)
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<UserDiscoveryCardDto>>($"/api/userdiscovery/suggested?limit={limit}");
            return list ?? new List<UserDiscoveryCardDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load suggested users.");
            return new List<UserDiscoveryCardDto>();
        }
    }

    // =========================================================================
    // PHASE 5: BESTIE CIRCLES & MEMBERSHIP MANAGEMENT
    // =========================================================================

    public async Task<CircleDetailDto?> CreateCircleAsync(CreateCircleRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/bestiecircle", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleDetailDto>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create circle.");
            return null;
        }
    }

    public async Task<List<CircleDto>> GetMyCirclesAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<CircleDto>>("/api/bestiecircle/my");
            return list ?? new List<CircleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load circles.");
            return new List<CircleDto>();
        }
    }

    public async Task<CircleDetailDto?> GetCircleDetailsAsync(int circleId)
    {
        try
        {
            return await _http.GetFromJsonAsync<CircleDetailDto>($"/api/bestiecircle/{circleId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get circle details for circle {CircleId}", circleId);
            return null;
        }
    }

    public async Task<CircleDetailDto?> UpdateCircleAsync(int circleId, UpdateCircleRequestDto request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/bestiecircle/{circleId}", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleDetailDto>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update circle {CircleId}", circleId);
            return null;
        }
    }

    public async Task<CircleOperationResultDto> DeleteCircleAsync(int circleId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/bestiecircle/{circleId}");
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete circle {CircleId}", circleId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> SendCircleInvitationAsync(SendCircleInvitationRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/circleinvitation", request);
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending circle invitation");
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> AcceptCircleInvitationAsync(int invitationId)
    {
        try
        {
            var res = await _http.PostAsync($"/api/circleinvitation/{invitationId}/accept", null);
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting circle invitation {InvitationId}", invitationId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> RejectCircleInvitationAsync(int invitationId)
    {
        try
        {
            var res = await _http.PostAsync($"/api/circleinvitation/{invitationId}/reject", null);
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining circle invitation {InvitationId}", invitationId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> CancelCircleInvitationAsync(int invitationId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/circleinvitation/{invitationId}/cancel");
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling circle invitation {InvitationId}", invitationId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<List<CircleInvitationDto>> GetIncomingCircleInvitationsAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<CircleInvitationDto>>("/api/circleinvitation/incoming");
            return list ?? new List<CircleInvitationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load incoming circle invitations.");
            return new List<CircleInvitationDto>();
        }
    }

    public async Task<List<CircleInvitationDto>> GetCircleInvitationsAsync(int circleId)
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<CircleInvitationDto>>($"/api/circleinvitation/circle/{circleId}");
            return list ?? new List<CircleInvitationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load circle invitations for circle {CircleId}", circleId);
            return new List<CircleInvitationDto>();
        }
    }

    public async Task<List<CircleMemberDto>> GetCircleMembersAsync(int circleId)
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<CircleMemberDto>>($"/api/circlemember/circle/{circleId}");
            return list ?? new List<CircleMemberDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load members for circle {CircleId}", circleId);
            return new List<CircleMemberDto>();
        }
    }

    public async Task<CircleOperationResultDto> RemoveCircleMemberAsync(int circleId, int targetUserId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/circlemember/circle/{circleId}/user/{targetUserId}");
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from circle {CircleId}", targetUserId, circleId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> LeaveCircleAsync(int circleId)
    {
        try
        {
            var res = await _http.PostAsync($"/api/circlemember/circle/{circleId}/leave", null);
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving circle {CircleId}", circleId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> ChangeCircleMemberRoleAsync(int circleId, ChangeMemberRoleRequestDto request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/circlemember/circle/{circleId}/role", request);
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing role for user {UserId} in circle {CircleId}", request.TargetUserId, circleId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<CircleOperationResultDto> TransferCircleOwnershipAsync(int circleId, TransferCircleOwnershipRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/circlemember/circle/{circleId}/transfer-ownership", request);
            var result = await res.Content.ReadFromJsonAsync<CircleOperationResultDto>();
            return result ?? new CircleOperationResultDto { IsSuccess = false, Message = "No response from server." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring ownership of circle {CircleId}", circleId);
            return new CircleOperationResultDto { IsSuccess = false, Message = ex.Message };
        }
    }

    // =========================================================================
    // Real-Time Private Circle Group Chat (Phase 6)
    // =========================================================================

    public async Task<CircleChatPaginationDto?> GetCircleMessagesAsync(int circleId, int pageNumber = 1, int pageSize = 30)
    {
        try
        {
            return await _http.GetFromJsonAsync<CircleChatPaginationDto>($"/api/circles/{circleId}/messages?pageNumber={pageNumber}&pageSize={pageSize}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for circle {CircleId}", circleId);
            return null;
        }
    }

    public async Task<CircleChatMessageDto?> SendCircleMessageAsync(int circleId, string content, string? messageType = "text")
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/circles/{circleId}/messages", new SendCircleMessageRequestDto
            {
                CircleId = circleId,
                Content = content,
                MessageType = messageType
            });
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to circle {CircleId}", circleId);
        }
        return null;
    }

    public async Task<CircleChatMessageDto?> EditCircleMessageAsync(int circleId, int messageId, string newContent)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/circles/{circleId}/messages/{messageId}", new EditCircleMessageRequestDto
            {
                CircleId = circleId,
                MessageId = messageId,
                Content = newContent
            });
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit message {MessageId} in circle {CircleId}", messageId, circleId);
        }
        return null;
    }

    public async Task<bool> DeleteCircleMessageAsync(int circleId, int messageId)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/circles/{circleId}/messages/{messageId}");
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId} in circle {CircleId}", messageId, circleId);
            return false;
        }
    }

    public async Task<List<CircleMemberPresenceEventDto>> GetCirclePresenceAsync(int circleId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<CircleMemberPresenceEventDto>>($"/api/circles/{circleId}/presence") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get presence for circle {CircleId}", circleId);
            return new();
        }
    }

    // =========================================================================
    // Multimedia Messaging, Stickers & Media (Phase 7)
    // =========================================================================

    public async Task<CircleChatMessageDto?> SendCirclePhotoAsync(int circleId, MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync($"/api/circles/{circleId}/media/photo", content);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogError("Failed to upload circle photo: {Err}", err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo for circle {CircleId}", circleId);
        }
        return null;
    }

    public async Task<CircleChatMessageDto?> SendCirclePhotoBase64Async(int circleId, SendPhotoBase64RequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/circles/{circleId}/media/photo-base64", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogError("Failed to upload circle photo base64: {Err}", err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload base64 photo for circle {CircleId}", circleId);
        }
        return null;
    }

    public async Task<CircleChatMessageDto?> SendCircleVoiceAsync(int circleId, MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync($"/api/circles/{circleId}/media/voice", content);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogError("Failed to upload circle voice note: {Err}", err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload voice note for circle {CircleId}", circleId);
        }
        return null;
    }

    public async Task<CircleChatMessageDto?> SendCircleVoiceBase64Async(int circleId, SendVoiceBase64RequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/circles/{circleId}/media/voice-base64", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogError("Failed to upload circle voice base64: {Err}", err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload base64 voice for circle {CircleId}", circleId);
        }
        return null;
    }

    public async Task<CircleChatMessageDto?> SendCircleStickerAsync(int circleId, int stickerId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/circles/{circleId}/media/sticker", new SendStickerMessageRequestDto
            {
                CircleId = circleId,
                StickerId = stickerId
            });
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<CircleChatMessageDto>();
            }
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogError("Failed to send circle sticker: {Err}", err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sticker for circle {CircleId}", circleId);
        }
        return null;
    }

    public async Task<List<StickerCategoryDto>> GetStickerCategoriesAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<StickerCategoryDto>>("/api/stickers/categories") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sticker categories");
            return new();
        }
    }

    public string GetMediaUrl(int circleId, int mediaId, bool thumbnail = false)
    {
        var suffix = thumbnail ? "/thumb" : "";
        var tokenParam = !string.IsNullOrEmpty(_bearerToken) ? $"?access_token={Uri.EscapeDataString(_bearerToken)}" : "";
        return $"{_baseUrl}/api/circles/{circleId}/media/{mediaId}{suffix}{tokenParam}";
    }

    private static List<ChatMessageDto> CreateFallbackMessages()
    {
        var now = DateTime.UtcNow;
        return new List<ChatMessageDto>
        {
            new()
            {
                Id = 1,
                SenderName = "Me 🎀",
                MessageType = "text",
                Content = "Hi sweet bestie! Welcome to our secret digital clubhouse! 🌸 I built this so we always have a special cute place together.",
                Timestamp = now.AddMinutes(-30),
                IsRead = true,
                ReadAt = now.AddMinutes(-20)
            },
            new()
            {
                Id = 2,
                SenderName = "Bestie 🌸",
                MessageType = "sticker",
                Content = "hugging-bear",
                Timestamp = now.AddMinutes(-25),
                IsRead = true,
                ReadAt = now.AddMinutes(-20)
            },
            // Fallback unread sample messages
            new()
            {
                Id = 3,
                SenderName = "Me 🎀",
                MessageType = "text",
                Content = "Good morning sunshine! ☀️ I woke up today thinking about our late night laughs and missed you a ton! Hope your day is filled with sweet smiles 🌸",
                Timestamp = now.AddMinutes(-12),
                IsRead = false,
                ReadAt = null
            },
            new()
            {
                Id = 4,
                SenderName = "Me 🎀",
                MessageType = "sticker",
                Content = "sparkling-heart",
                Timestamp = now.AddMinutes(-10),
                IsRead = false,
                ReadAt = null
            },
            new()
            {
                Id = 5,
                SenderName = "Me 🎀",
                MessageType = "photo",
                Content = "I found this aesthetic strawberry boba spot for us! We HAVE to go this weekend 🍓🧋",
                MediaUrl = "/images/sample-memories/boba-run.svg",
                Timestamp = now.AddMinutes(-7),
                IsRead = false,
                ReadAt = null
            },
            new()
            {
                Id = 6,
                SenderName = "Me 🎀",
                MessageType = "voice",
                Content = "A little voice hug for you 🎤",
                MediaUrl = "/uploads/chat-voices/sweet-voice-note.wav",
                VoiceDurationSeconds = 5,
                Timestamp = now.AddMinutes(-3),
                IsRead = false,
                ReadAt = null
            }
        };
    }

    private static DashboardDto CreateFallbackDashboard()
    {
        return new DashboardDto
        {
            AppStats = new AppStatDto
            {
                Id = 1,
                MissYouCount = 1000,
                MainLetterTitle = "To My Favorite Human in the Entire World 🌸",
                MainLetterContent = "Dear Bestie,\n\nI built this cozy, pastel corner of the internet just for us! No matter how many miles stand between us or how crazy life gets, you will always be my safest place, my funniest laugh, and my favorite sister at heart.\n\nEvery time you miss me, press that little pink heart button below so I can feel your virtual hug! Browse through our polaroid memories, open the mystery envelopes when you need an instant smile, and send me silly stickers, photos, and voice notes anytime in our chat.\n\nI miss you more than words, boba cups, and stars in the sky combined. Thank you for being my constant sunshine! 💖✨",
                SenderNickname = "Me 🎀",
                RecipientNickname = "Bestie 🌸",
                LastUpdated = DateTime.UtcNow
            },
            Memories = new List<MemoryDto>
            {
                new()
                {
                    Id = 1,
                    Caption = "Spontaneous late-night boba run & uncontrollable laughing fit! 🧋✨",
                    ImageUrl = "/images/sample-memories/boba-run.svg",
                    MemoryDate = DateTime.UtcNow.AddMonths(-3),
                    StickerType = "bow",
                    RotationAngle = -2.5,
                    DisplayOrder = 1
                },
                new()
                {
                    Id = 2,
                    Caption = "Golden hour picnic in the park with homemade cookies & cute outfits 🌸🧺",
                    ImageUrl = "/images/sample-memories/picnic-day.svg",
                    MemoryDate = DateTime.UtcNow.AddMonths(-2),
                    StickerType = "sparkle",
                    RotationAngle = 2.0,
                    DisplayOrder = 2
                },
                new()
                {
                    Id = 3,
                    Caption = "Crying-laughing photobooth session trying on silly oversized glasses 🍓📸",
                    ImageUrl = "/images/sample-memories/photobooth.svg",
                    MemoryDate = DateTime.UtcNow.AddMonths(-1),
                    StickerType = "bear",
                    RotationAngle = -1.8,
                    DisplayOrder = 3
                },
                new()
                {
                    Id = 4,
                    Caption = "Watching the pastel sunset by the beach making 10-year pinky promises 🌅💖",
                    ImageUrl = "/images/sample-memories/sunset-promise.svg",
                    MemoryDate = DateTime.UtcNow.AddDays(-14),
                    StickerType = "heart",
                    RotationAngle = 2.4,
                    DisplayOrder = 4
                }
            },
            SecretNotes = new List<SecretNoteDto>
            {
                new()
                {
                    Id = 1,
                    Title = "Open when you miss me 💌",
                    PreviewHint = "A warm, gentle reminder when you feel a little lonely...",
                    Content = "Close your eyes, take a deep breath, and wrap your arms tight around yourself. That is a giant, warm bear hug from me flying across the miles! Remember all those late-night FaceTime calls where we laughed until we couldn't breathe? I am always just one text or call away. You are never alone, bestie! 🧸💖",
                    StickerIcon = "💌",
                    IsUnlocked = false
                },
                new()
                {
                    Id = 2,
                    Title = "My absolute favorite memory of us 🌟",
                    PreviewHint = "That unforgettable day we couldn't stop laughing...",
                    Content = "Do you remember that sudden rainy afternoon when we got completely drenched running for cover, burst into that tiny aesthetic bakery shivering, ordered huge marshmallow hot chocolates, and laughed until tears streamed down our faces? I replay that memory in my mind whenever I need instant happiness. You make ordinary days pure magic! ☕✨",
                    StickerIcon = "🌟",
                    IsUnlocked = false
                },
                new()
                {
                    Id = 3,
                    Title = "10 reasons why you're my bestie 🌸",
                    PreviewHint = "A quick list of why nobody could ever replace you...",
                    Content = "1. You know exactly what I'm thinking with just one eye contact.\n2. Your laugh is completely contagious.\n3. You hype me up even on my worst hair days.\n4. You never judge my weird food combinations.\n5. You always save the best bite for me.\n6. You send the most chaotic memes at 2 AM.\n7. You listen with your entire heart.\n8. You give the warmest, safest hugs.\n9. You believe in my dreams even when I doubt myself.\n10. Simply because you are YOU, and there will never be anyone like you! 🎀👑",
                    StickerIcon = "🌸",
                    IsUnlocked = false
                },
                new()
                {
                    Id = 4,
                    Title = "A pinky promise to you 🤞",
                    PreviewHint = "A lifetime promise sealed with love and sprinkles...",
                    Content = "I promise to celebrate every single one of your victories like they are my own. I promise to sit quietly beside you whenever life feels heavy. And I promise that when we are old grandmas with matching knitted cardigans, we will still be sipping tea and gossiping! No distance, time, or busy schedule will ever change our bond. Pinky swear forever and ever! 👭💕",
                    StickerIcon = "🤞",
                    IsUnlocked = false
                }
            }
        };
    }
}
