using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.Memories;

public interface IMemoriesService
{
    Task<List<MemoryDto>> GetAllMemoriesAsync();
    Task<MemoryDto> AddMemoryAsync(IFormFile? photo, string caption, string stickerType, DateTime? memoryDate);
}

public class MemoriesService : IMemoriesService
{
    private readonly BestieDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public MemoriesService(BestieDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<List<MemoryDto>> GetAllMemoriesAsync()
    {
        return await _context.Memories
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
    }

    public async Task<MemoryDto> AddMemoryAsync(IFormFile? photo, string caption, string stickerType, DateTime? memoryDate)
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "memories");
        Directory.CreateDirectory(uploadsDir);

        string imageUrl;

        if (photo != null && photo.Length > 0)
        {
            var ext = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            imageUrl = $"/uploads/memories/{fileName}";
        }
        else
        {
            // Fallback cute default SVG memory illustration
            imageUrl = "/images/sample-memories/photobooth.svg";
        }

        // Generate small random-looking slight Polaroid tilt angle: -3.0 to +3.0
        var random = new Random();
        var angle = Math.Round((random.NextDouble() * 5.0) - 2.5, 1);
        if (Math.Abs(angle) < 0.5) angle = angle < 0 ? -1.8 : 1.8;

        var maxOrder = await _context.Memories.AnyAsync() 
            ? await _context.Memories.MaxAsync(m => m.DisplayOrder) 
            : 0;

        var memory = new Memory
        {
            Caption = string.IsNullOrWhiteSpace(caption) ? "Our cute little moment! 💖" : caption.Trim(),
            ImageUrl = imageUrl,
            MemoryDate = memoryDate ?? DateTime.UtcNow,
            StickerType = string.IsNullOrWhiteSpace(stickerType) ? "heart" : stickerType.ToLower().Trim(),
            RotationAngle = angle,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Memories.Add(memory);
        await _context.SaveChangesAsync();

        return new MemoryDto
        {
            Id = memory.Id,
            Caption = memory.Caption,
            ImageUrl = memory.ImageUrl,
            MemoryDate = memory.MemoryDate,
            StickerType = memory.StickerType,
            RotationAngle = memory.RotationAngle,
            DisplayOrder = memory.DisplayOrder,
            CreatedAt = memory.CreatedAt
        };
    }
}
