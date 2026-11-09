using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Rbac;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RbacController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;
    private readonly IUserAuthorizationService _userAuthService;
    private readonly ILogger<RbacController> _logger;

    public RbacController(
        IRoleService roleService,
        IPermissionService permissionService,
        IUserAuthorizationService userAuthService,
        ILogger<RbacController> logger)
    {
        _roleService = roleService;
        _permissionService = permissionService;
        _userAuthService = userAuthService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    // =========================================================================
    // CURRENT USER PERMISSIONS & ROLES
    // =========================================================================

    [HttpGet("my-permissions")]
    public async Task<ActionResult<List<string>>> GetMyPermissions()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var permissions = await _userAuthService.GetUserPermissionsAsync(userId.Value);
        return Ok(permissions);
    }

    // =========================================================================
    // ROLE MANAGEMENT
    // =========================================================================

    [HttpGet("roles")]
    [Authorize(Policy = "ROLE_VIEW")]
    public async Task<ActionResult<List<RoleDto>>> GetAllRoles()
    {
        var roles = await _roleService.GetAllRolesAsync();
        return Ok(roles);
    }

    [HttpGet("roles/{id:int}")]
    [Authorize(Policy = "ROLE_VIEW")]
    public async Task<ActionResult<RoleDetailDto>> GetRoleById(int id)
    {
        var role = await _roleService.GetRoleByIdAsync(id);
        if (role == null)
        {
            return NotFound(new { message = $"Role with ID {id} not found." });
        }
        return Ok(role);
    }

    [HttpPost("roles")]
    [Authorize(Policy = "ROLE_CREATE")]
    public async Task<ActionResult<RoleDetailDto>> CreateRole([FromBody] CreateRoleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            return BadRequest(new { message = "Role name cannot be empty." });
        }

        try
        {
            var created = await _roleService.CreateRoleAsync(request);
            return CreatedAtAction(nameof(GetRoleById), new { id = created.RoleId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create role");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("roles/{id:int}")]
    [Authorize(Policy = "ROLE_UPDATE")]
    public async Task<ActionResult<RoleDetailDto>> UpdateRole(int id, [FromBody] UpdateRoleRequestDto request)
    {
        try
        {
            var updated = await _roleService.UpdateRoleAsync(id, request);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role {RoleId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("roles/{id:int}")]
    [Authorize(Policy = "ROLE_DELETE")]
    public async Task<ActionResult<RbacOperationResultDto>> DeleteRole(int id)
    {
        try
        {
            await _roleService.DeleteRoleAsync(id);
            return Ok(new RbacOperationResultDto { IsSuccess = true, Message = "Role successfully deleted." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete role {RoleId}", id);
            return BadRequest(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
    }

    [HttpPut("roles/{id:int}/permissions")]
    [Authorize(Policy = "PERMISSION_ASSIGN")]
    public async Task<ActionResult<RoleDetailDto>> UpdateRolePermissions(int id, [FromBody] UpdateRolePermissionsRequestDto request)
    {
        try
        {
            var updated = await _roleService.UpdateRolePermissionsAsync(id, request);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update permissions for role {RoleId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================================
    // PERMISSION CATALOG
    // =========================================================================

    [HttpGet("permissions")]
    [Authorize(Policy = "PERMISSION_VIEW")]
    public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions()
    {
        var permissions = await _permissionService.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    [HttpGet("roles/{id:int}/permissions")]
    [Authorize(Policy = "PERMISSION_VIEW")]
    public async Task<ActionResult<List<PermissionDto>>> GetRolePermissions(int id)
    {
        var permissions = await _permissionService.GetPermissionsByRoleIdAsync(id);
        return Ok(permissions);
    }

    // =========================================================================
    // USER ROLES MANAGEMENT
    // =========================================================================

    [HttpGet("users")]
    [Authorize(Policy = "USER_VIEW")]
    public async Task<ActionResult<List<UserRoleDto>>> GetAllUsersWithRoles()
    {
        var users = await _userAuthService.GetAllUsersWithRolesAsync();
        return Ok(users);
    }

    [HttpGet("users/{userId:int}/roles")]
    [Authorize(Policy = "USER_VIEW")]
    public async Task<ActionResult<UserRoleDto>> GetUserRoles(int userId)
    {
        var detail = await _userAuthService.GetUserRolesDetailAsync(userId);
        if (detail == null)
        {
            return NotFound(new { message = $"User with ID {userId} not found." });
        }
        return Ok(detail);
    }

    [HttpPost("users/{userId:int}/roles")]
    [Authorize(Policy = "USER_ROLE_MANAGE")]
    public async Task<ActionResult<RbacOperationResultDto>> AssignRoleToUser(int userId, [FromBody] AssignUserRoleRequestDto request)
    {
        var actingUserId = GetCurrentUserId();

        try
        {
            await _userAuthService.AssignRoleToUserAsync(userId, request.RoleId, actingUserId);
            return Ok(new RbacOperationResultDto { IsSuccess = true, Message = "Role assigned successfully." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign role {RoleId} to user {UserId}", request.RoleId, userId);
            return BadRequest(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
    }

    [HttpDelete("users/{userId:int}/roles/{roleId:int}")]
    [Authorize(Policy = "USER_ROLE_MANAGE")]
    public async Task<ActionResult<RbacOperationResultDto>> RemoveRoleFromUser(int userId, int roleId)
    {
        var actingUserId = GetCurrentUserId();

        try
        {
            await _userAuthService.RemoveRoleFromUserAsync(userId, roleId, actingUserId);
            return Ok(new RbacOperationResultDto { IsSuccess = true, Message = "Role removed successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove role {RoleId} from user {UserId}", roleId, userId);
            return BadRequest(new RbacOperationResultDto { IsSuccess = false, Message = ex.Message });
        }
    }
}
