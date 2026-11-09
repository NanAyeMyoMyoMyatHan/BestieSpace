using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Client.Services;

public class UserSessionStorageModel
{
    public UserSessionDto? User { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}

public class BestieAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "bestie_auth_session_enc";
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IBestieApiClient _apiClient;
    private readonly ILogger<BestieAuthenticationStateProvider> _logger;

    private UserSessionDto? _currentUser;
    private string? _accessToken;
    private string? _refreshToken;
    private bool _hasLoadedFromStorage = false;

    public BestieAuthenticationStateProvider(
        ProtectedSessionStorage sessionStorage,
        IBestieApiClient apiClient,
        ILogger<BestieAuthenticationStateProvider> logger)
    {
        _sessionStorage = sessionStorage;
        _apiClient = apiClient;
        _logger = logger;
    }

    public UserSessionDto? CurrentUser => _currentUser;
    public string? AccessToken => _accessToken;
    public bool IsAuthenticated => _currentUser != null && !string.IsNullOrWhiteSpace(_accessToken);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_hasLoadedFromStorage)
        {
            try
            {
                var result = await _sessionStorage.GetAsync<UserSessionStorageModel>(StorageKey);
                if (result.Success && result.Value?.User != null && !string.IsNullOrWhiteSpace(result.Value.AccessToken))
                {
                    _currentUser = result.Value.User;
                    _accessToken = result.Value.AccessToken;
                    _refreshToken = result.Value.RefreshToken;
                    _apiClient.SetBearerToken(_accessToken);
                }
            }
            catch (Exception ex)
            {
                // ProtectedSessionStorage can fail during prerendering or if encryption keys changed
                _logger.LogDebug(ex, "Could not read session from protected storage during initial state check.");
            }
            finally
            {
                _hasLoadedFromStorage = true;
            }
        }

        if (_currentUser != null && !string.IsNullOrWhiteSpace(_accessToken))
        {
            var identity = CreateClaimsIdentity(_currentUser);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var response = await _apiClient.RegisterAsync(request);
        if (response.IsSuccess && response.User != null && !string.IsNullOrWhiteSpace(response.AccessToken))
        {
            await SetAuthenticatedUserAsync(response.User, response.AccessToken, response.RefreshToken);
        }
        return response;
    }

    public async Task<AuthResponseDto> LoginAsync(string usernameOrEmail, string password)
    {
        var response = await _apiClient.LoginAsync(new LoginRequestDto
        {
            Username = usernameOrEmail,
            Password = password
        });

        if (response.IsSuccess && response.User != null && !string.IsNullOrWhiteSpace(response.AccessToken))
        {
            await SetAuthenticatedUserAsync(response.User, response.AccessToken, response.RefreshToken);
        }

        return response;
    }

    public async Task<AuthResponseDto> PinLoginAsync(string pin, string? roleKey = null)
    {
        var response = await _apiClient.PinLoginAsync(new PinLoginRequestDto
        {
            PinCode = pin,
            RoleKey = roleKey
        });

        if (response.IsSuccess && response.User != null && !string.IsNullOrWhiteSpace(response.AccessToken))
        {
            await SetAuthenticatedUserAsync(response.User, response.AccessToken, response.RefreshToken);
        }

        return response;
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_refreshToken))
            {
                await _apiClient.RevokeTokenAsync(new RevokeTokenRequestDto { RefreshToken = _refreshToken });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke token during logout.");
        }

        _currentUser = null;
        _accessToken = null;
        _refreshToken = null;
        _apiClient.SetBearerToken(null);

        try
        {
            await _sessionStorage.DeleteAsync(StorageKey);
        }
        catch { }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    public async Task<bool> RefreshSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            return false;
        }

        var response = await _apiClient.RefreshTokenAsync(new RefreshTokenRequestDto
        {
            RefreshToken = _refreshToken
        });

        if (response.IsSuccess && response.User != null && !string.IsNullOrWhiteSpace(response.AccessToken))
        {
            await SetAuthenticatedUserAsync(response.User, response.AccessToken, response.RefreshToken);
            return true;
        }

        await LogoutAsync();
        return false;
    }

    public bool HasPermission(string permissionCode)
    {
        if (_currentUser?.Permissions == null || string.IsNullOrWhiteSpace(permissionCode)) return false;
        if (_currentUser.Roles != null && _currentUser.Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase)) return true;
        return _currentUser.Permissions.Contains(permissionCode.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasRole(string roleName)
    {
        if (_currentUser?.Roles == null || string.IsNullOrWhiteSpace(roleName)) return false;
        return _currentUser.Roles.Contains(roleName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task RefreshUserPermissionsAsync()
    {
        if (_currentUser == null) return;
        try
        {
            var myPerms = await _apiClient.GetMyPermissionsAsync();
            if (myPerms != null && myPerms.Any())
            {
                _currentUser.Permissions = myPerms;
                var identity = CreateClaimsIdentity(_currentUser);
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh user permissions.");
        }
    }

    private async Task SetAuthenticatedUserAsync(UserSessionDto user, string accessToken, string? refreshToken)
    {
        _currentUser = user;
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _hasLoadedFromStorage = true;

        _apiClient.SetBearerToken(accessToken);

        try
        {
            await _sessionStorage.SetAsync(StorageKey, new UserSessionStorageModel
            {
                User = user,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist session to protected storage (expected during prerendering).");
        }

        var identity = CreateClaimsIdentity(user);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
    }

    private static ClaimsIdentity CreateClaimsIdentity(UserSessionDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("displayName", user.DisplayName),
            new("avatarSticker", user.AvatarSticker),
            new("roleKey", user.RoleKey)
        };

        if (user.Roles != null)
        {
            foreach (var role in user.Roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        if (user.Permissions != null)
        {
            foreach (var perm in user.Permissions)
            {
                if (!string.IsNullOrWhiteSpace(perm))
                {
                    claims.Add(new Claim("permission", perm));
                }
            }
        }

        return new ClaimsIdentity(claims, "BestieSpaceAuth");
    }
}
