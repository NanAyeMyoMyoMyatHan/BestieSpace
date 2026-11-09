using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Media;

[ApiController]
[Route("api/stickers")]
[Authorize]
public class StickerController : ControllerBase
{
    private readonly IStickerService _stickerService;
    private readonly ILogger<StickerController> _logger;

    public StickerController(IStickerService stickerService, ILogger<StickerController> logger)
    {
        _stickerService = stickerService;
        _logger = logger;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<StickerCategoryDto>>> GetCategories()
    {
        try
        {
            var categories = await _stickerService.GetActiveCategoriesWithStickersAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sticker categories");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<StickerCategoryDto>>> GetAllCategories()
    {
        try
        {
            var categories = await _stickerService.GetAllCategoriesWithStickersAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load all sticker categories (Admin)");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("categories")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<StickerCategoryDto>> CreateCategory([FromBody] CreateStickerCategoryRequestDto request)
    {
        try
        {
            var created = await _stickerService.CreateCategoryAsync(request);
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sticker category");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<StickerDto>> CreateSticker([FromBody] CreateStickerRequestDto request)
    {
        try
        {
            var created = await _stickerService.CreateStickerAsync(request);
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sticker");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<StickerDto>> UpdateSticker(int id, [FromBody] UpdateStickerRequestDto request)
    {
        try
        {
            var updated = await _stickerService.UpdateStickerAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sticker {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> DeleteSticker(int id)
    {
        try
        {
            var success = await _stickerService.DeleteStickerAsync(id);
            if (!success) return NotFound();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete sticker {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }
}
