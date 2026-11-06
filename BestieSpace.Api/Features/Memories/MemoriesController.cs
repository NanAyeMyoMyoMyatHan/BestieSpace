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

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MemoryDto>> AddMemory(
        [FromForm] IFormFile? photo,
        [FromForm] string? caption,
        [FromForm] string? stickerType,
        [FromForm] DateTime? memoryDate)
    {
        var memory = await _memoriesService.AddMemoryAsync(photo, caption ?? "", stickerType ?? "heart", memoryDate);
        return CreatedAtAction(nameof(GetAll), new { id = memory.Id }, memory);
    }
}
