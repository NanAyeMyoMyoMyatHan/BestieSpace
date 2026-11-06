using BestieSpace.Shared.DTOs;
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
