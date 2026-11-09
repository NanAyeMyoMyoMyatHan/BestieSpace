using System.Security.Claims;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BestieSpace.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("logout")]
    [HttpPost("revoke-token")]
    public async Task<ActionResult<AuthResponseDto>> Logout([FromBody] RevokeTokenRequestDto? request)
    {
        int? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(sub, out int parsedId))
        {
            userId = parsedId;
        }

        var result = await _authService.RevokeTokenAsync(request, userId);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserSessionDto>> GetCurrentUser()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out int userId))
        {
            return Unauthorized();
        }

        var session = await _authService.GetCurrentUserSessionAsync(userId);
        if (session == null)
        {
            return NotFound();
        }

        return Ok(session);
    }

    [HttpPost("pin-login")]
    public async Task<ActionResult<AuthResponseDto>> PinLogin([FromBody] PinLoginRequestDto request)
    {
        var result = await _authService.PinLoginAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("hint")]
    public async Task<ActionResult<PasswordHintDto>> GetHint([FromQuery] string? username)
    {
        var hint = await _authService.GetHintAsync(username);
        return Ok(hint);
    }
}
