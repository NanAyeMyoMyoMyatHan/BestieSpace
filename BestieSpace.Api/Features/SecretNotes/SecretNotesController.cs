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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SecretNoteDto>> GetById(int id)
    {
        var note = await _secretNotesService.GetNoteByIdAsync(id);
        if (note == null)
        {
            return NotFound(new { message = $"Secret note with ID {id} not found." });
        }
        return Ok(note);
    }

    [HttpPost]
    public async Task<ActionResult<SecretNoteDto>> Create([FromBody] CreateSecretNoteRequest request)
    {
        var created = await _secretNotesService.CreateNoteAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SecretNoteDto>> Update(int id, [FromBody] UpdateSecretNoteRequest request)
    {
        var updated = await _secretNotesService.UpdateNoteAsync(id, request);
        if (updated == null)
        {
            return NotFound(new { message = $"Secret note with ID {id} not found." });
        }
        return Ok(updated);
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

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var success = await _secretNotesService.DeleteNoteAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Secret note with ID {id} not found." });
        }
        return NoContent();
    }
}
