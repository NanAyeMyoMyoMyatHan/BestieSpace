using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.SecretNotes;

public interface ISecretNotesService
{
    Task<List<SecretNoteDto>> GetAllNotesAsync();
    Task<SecretNoteDto?> UnlockNoteAsync(int id);
}

public class SecretNotesService : ISecretNotesService
{
    private readonly BestieDbContext _context;

    public SecretNotesService(BestieDbContext context)
    {
        _context = context;
    }

    public async Task<List<SecretNoteDto>> GetAllNotesAsync()
    {
        return await _context.SecretNotes
            .OrderBy(n => n.Id)
            .Select(n => new SecretNoteDto
            {
                Id = n.Id,
                Title = n.Title,
                PreviewHint = n.PreviewHint,
                Content = n.Content,
                StickerIcon = n.StickerIcon,
                IsUnlocked = n.IsUnlocked,
                UnlockedAt = n.UnlockedAt
            })
            .ToListAsync();
    }

    public async Task<SecretNoteDto?> UnlockNoteAsync(int id)
    {
        var note = await _context.SecretNotes.FindAsync(id);
        if (note == null) return null;

        if (!note.IsUnlocked)
        {
            note.IsUnlocked = true;
            note.UnlockedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return new SecretNoteDto
        {
            Id = note.Id,
            Title = note.Title,
            PreviewHint = note.PreviewHint,
            Content = note.Content,
            StickerIcon = note.StickerIcon,
            IsUnlocked = note.IsUnlocked,
            UnlockedAt = note.UnlockedAt
        };
    }
}
