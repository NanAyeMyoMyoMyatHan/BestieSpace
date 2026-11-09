using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Social;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserProfileController : ControllerBase
{
    private readonly IUserProfileService _profileService;
    private readonly ILogger<UserProfileController> _logger;

    public UserProfileController(
        IUserProfileService profileService,
        ILogger<UserProfileController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpGet("me")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<UserProfileDto>> GetMyProfile()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var profile = await _profileService.GetMyProfileAsync(userId.Value);
        if (profile == null) return NotFound(new { message = "User not found." });

        return Ok(profile);
    }

    [HttpGet("{userId:int}")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<UserProfileDto>> GetProfileById(int userId)
    {
        var currentUserId = GetCurrentUserId();
        var profile = await _profileService.GetProfileByIdAsync(userId, currentUserId);
        if (profile == null) return NotFound(new { message = "User not found or account is deactivated." });

        return Ok(profile);
    }

    [HttpGet("by-username/{username}")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<UserProfileDto>> GetProfileByUsername(string username)
    {
        var currentUserId = GetCurrentUserId();
        var profile = await _profileService.GetProfileByUsernameAsync(username, currentUserId);
        if (profile == null) return NotFound(new { message = "User not found or account is deactivated." });

        return Ok(profile);
    }

    [HttpPut("me")]
    [Authorize(Policy = "PROFILE_UPDATE")]
    public async Task<ActionResult<UserProfileDto>> UpdateMyProfile([FromBody] UpdateUserProfileRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        try
        {
            var updated = await _profileService.UpdateProfileAsync(userId.Value, request, userId.Value);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile for user {UserId}", userId);
            return BadRequest(new { message = "Could not update profile." });
        }
    }

    [HttpPost("me/picture")]
    [Authorize(Policy = "PROFILE_UPDATE")]
    public async Task<ActionResult<object>> UploadProfilePicture(IFormFile? file)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No image file provided." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var relativeUrl = await _profileService.UploadProfilePictureAsync(
                userId.Value,
                stream,
                file.FileName,
                file.ContentType,
                file.Length);

            return Ok(new { profilePictureUrl = relativeUrl, message = "Profile picture updated successfully! ✨" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload profile picture for user {UserId}", userId);
            return BadRequest(new { message = "Failed to upload image. Please try again." });
        }
    }
}
