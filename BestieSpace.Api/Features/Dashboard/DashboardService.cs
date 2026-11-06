using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardDataAsync();
    Task<MissYouResponse> IncrementMissYouCountAsync();
}

public class DashboardService : IDashboardService
{
    private readonly BestieDbContext _context;

    public DashboardService(BestieDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardDto> GetDashboardDataAsync()
    {
        var appStat = await _context.AppStats.OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (appStat == null)
        {
            appStat = new Infrastructure.Data.Entities.AppStat
            {
                MissYouCount = 1000,
                MainLetterTitle = "To My Bestie 🌸",
                MainLetterContent = "Welcome to our space!",
                SenderNickname = "Me 🎀",
                RecipientNickname = "Bestie 🌸",
                LastUpdated = DateTime.UtcNow
            };
            _context.AppStats.Add(appStat);
            await _context.SaveChangesAsync();
        }

        var memories = await _context.Memories
            .OrderBy(m => m.DisplayOrder)
            .ThenByDescending(m => m.MemoryDate)
            .Select(m => new MemoryDto
            {
                Id = m.Id,
                Caption = m.Caption,
                ImageUrl = m.ImageUrl,
                MemoryDate = m.MemoryDate,
                StickerType = m.StickerType,
                RotationAngle = m.RotationAngle,
                DisplayOrder = m.DisplayOrder,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        var notes = await _context.SecretNotes
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

        return new DashboardDto
        {
            AppStats = new AppStatDto
            {
                Id = appStat.Id,
                MissYouCount = appStat.MissYouCount,
                MainLetterTitle = appStat.MainLetterTitle,
                MainLetterContent = appStat.MainLetterContent,
                SenderNickname = appStat.SenderNickname,
                RecipientNickname = appStat.RecipientNickname,
                LastUpdated = appStat.LastUpdated
            },
            Memories = memories,
            SecretNotes = notes
        };
    }

    public async Task<MissYouResponse> IncrementMissYouCountAsync()
    {
        var appStat = await _context.AppStats.OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (appStat == null)
        {
            appStat = new Infrastructure.Data.Entities.AppStat
            {
                MissYouCount = 1001,
                MainLetterTitle = "To My Bestie 🌸",
                MainLetterContent = "Welcome to our space!",
                SenderNickname = "Me 🎀",
                RecipientNickname = "Bestie 🌸",
                LastUpdated = DateTime.UtcNow
            };
            _context.AppStats.Add(appStat);
        }
        else
        {
            appStat.MissYouCount += 1;
            appStat.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return new MissYouResponse
        {
            MissYouCount = appStat.MissYouCount,
            LastUpdated = appStat.LastUpdated
        };
    }
}
