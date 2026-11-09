using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BestieSpace.Api.Features.Memories;

[ApiController]
[Route("api/memories")]
public class MemoriesController : ControllerBase
{
    private readonly IMemoriesService _memoriesService;

    public MemoriesController(IMemoriesService memoriesService)
    {
        _memoriesService = memoriesService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MemoryDto>>> GetAll()
    {
        var memories = await _memoriesService.GetAllMemoriesAsync();
        return Ok(memories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MemoryDto>> GetById(int id)
    {
        var memory = await _memoriesService.GetMemoryByIdAsync(id);
        if (memory == null)
        {
            return NotFound(new { message = $"Memory with ID {id} not found." });
        }
        return Ok(memory);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MemoryDto>> AddMemory(
        [FromForm] IFormFile? photo,
        [FromForm] string? caption,
        [FromForm] string? stickerType,
        [FromForm] DateTime? memoryDate)
    {
        var memory = await _memoriesService.AddMemoryAsync(photo, caption ?? "", stickerType ?? "heart", memoryDate);
        return CreatedAtAction(nameof(GetById), new { id = memory.Id }, memory);
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MemoryDto>> UpdateMemory(
        int id,
        [FromForm] IFormFile? photo,
        [FromForm] string? caption,
        [FromForm] string? stickerType,
        [FromForm] DateTime? memoryDate)
    {
        var updated = await _memoriesService.UpdateMemoryAsync(id, photo, caption, stickerType, memoryDate);
        if (updated == null)
        {
            return NotFound(new { message = $"Memory with ID {id} not found." });
        }
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteMemory(int id)
    {
        var success = await _memoriesService.DeleteMemoryAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Memory with ID {id} not found." });
        }
        return NoContent();
    }
}
