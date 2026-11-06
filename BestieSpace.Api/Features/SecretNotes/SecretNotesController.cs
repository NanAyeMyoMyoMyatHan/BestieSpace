using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BestieSpace.Api.Features.SecretNotes;

[ApiController]
[Route("api/notes")]
public class SecretNotesController : ControllerBase
{
    private readonly ISecretNotesService _secretNotesService;

    public SecretNotesController(ISecretNotesService secretNotesService)
    {
        _secretNotesService = secretNotesService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SecretNoteDto>>> GetAll()
    {
        var notes = await _secretNotesService.GetAllNotesAsync();
        return Ok(notes);
    }

    [HttpPut("{id:int}/unlock")]
    public async Task<ActionResult<SecretNoteDto>> Unlock(int id)
    {
        var note = await _secretNotesService.UnlockNoteAsync(id);
        if (note == null)
        {
            return NotFound(new { message = $"Secret note with ID {id} not found." });
        }
        return Ok(note);
    }
}
