using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.SecretNotes;

public interface ISecretNotesService
{
    Task<List<SecretNoteDto>> GetAllNotesAsync();
    Task<SecretNoteDto?> GetNoteByIdAsync(int id);
    Task<SecretNoteDto> CreateNoteAsync(CreateSecretNoteRequest request);
    Task<SecretNoteDto?> UpdateNoteAsync(int id, UpdateSecretNoteRequest request);
    Task<SecretNoteDto?> UnlockNoteAsync(int id);
    Task<bool> DeleteNoteAsync(int id);
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

    public async Task<SecretNoteDto?> GetNoteByIdAsync(int id)
    {
        var note = await _context.SecretNotes.FindAsync(id);
        if (note == null) return null;

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

    public async Task<SecretNoteDto> CreateNoteAsync(CreateSecretNoteRequest request)
    {
        var note = new SecretNote
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "A sweet little surprise 💌" : request.Title.Trim(),
            PreviewHint = string.IsNullOrWhiteSpace(request.PreviewHint) ? "Tap to break the seal and read 💕" : request.PreviewHint.Trim(),
            Content = string.IsNullOrWhiteSpace(request.Content) ? "With all my love, forever & always! 🌸" : request.Content.Trim(),
            StickerIcon = string.IsNullOrWhiteSpace(request.StickerIcon) ? "💌" : request.StickerIcon.Trim(),
            IsUnlocked = false,
            UnlockedAt = null
        };

        _context.SecretNotes.Add(note);
        await _context.SaveChangesAsync();

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

    public async Task<SecretNoteDto?> UpdateNoteAsync(int id, UpdateSecretNoteRequest request)
    {
        var note = await _context.SecretNotes.FindAsync(id);
        if (note == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Title))
            note.Title = request.Title.Trim();

        if (!string.IsNullOrWhiteSpace(request.PreviewHint))
            note.PreviewHint = request.PreviewHint.Trim();

        if (!string.IsNullOrWhiteSpace(request.Content))
            note.Content = request.Content.Trim();

        if (!string.IsNullOrWhiteSpace(request.StickerIcon))
            note.StickerIcon = request.StickerIcon.Trim();

        if (request.IsUnlocked.HasValue)
        {
            note.IsUnlocked = request.IsUnlocked.Value;
            if (!note.IsUnlocked)
            {
                note.UnlockedAt = null;
            }
            else if (note.UnlockedAt == null)
            {
                note.UnlockedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

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

    public async Task<bool> DeleteNoteAsync(int id)
    {
        var note = await _context.SecretNotes.FindAsync(id);
        if (note == null) return false;

        _context.SecretNotes.Remove(note);
        await _context.SaveChangesAsync();
        return true;
    }
}
