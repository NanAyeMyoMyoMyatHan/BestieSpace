using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Media;

public interface IStickerService
{
    Task<List<StickerCategoryDto>> GetActiveCategoriesWithStickersAsync();
    Task<List<StickerCategoryDto>> GetAllCategoriesWithStickersAsync();
    Task<StickerCategoryDto> CreateCategoryAsync(CreateStickerCategoryRequestDto request);
    Task<StickerDto> CreateStickerAsync(CreateStickerRequestDto request);
    Task<StickerDto> UpdateStickerAsync(int stickerId, UpdateStickerRequestDto request);
    Task<bool> DeleteStickerAsync(int stickerId);
    Task<Sticker?> GetActiveStickerByIdAsync(int stickerId);
}

public class StickerService : IStickerService
{
    private readonly BestieDbContext _context;
    private readonly ILogger<StickerService> _logger;

    public StickerService(BestieDbContext context, ILogger<StickerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<StickerCategoryDto>> GetActiveCategoriesWithStickersAsync()
    {
        var categories = await _context.StickerCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .Include(c => c.Stickers.Where(s => s.IsActive).OrderBy(s => s.DisplayOrder))
            .ToListAsync();

        return categories.Select(c => new StickerCategoryDto
        {
            CategoryId = c.CategoryId,
            CategoryName = c.CategoryName,
            Icon = c.Icon,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            Stickers = c.Stickers.Select(s => new StickerDto
            {
                StickerId = s.StickerId,
                CategoryId = s.CategoryId,
                StickerName = s.StickerName,
                ImageUrl = s.ImageUrl,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive
            }).ToList()
        }).ToList();
    }

    public async Task<List<StickerCategoryDto>> GetAllCategoriesWithStickersAsync()
    {
        var categories = await _context.StickerCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .Include(c => c.Stickers.OrderBy(s => s.DisplayOrder))
            .ToListAsync();

        return categories.Select(c => new StickerCategoryDto
        {
            CategoryId = c.CategoryId,
            CategoryName = c.CategoryName,
            Icon = c.Icon,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            Stickers = c.Stickers.Select(s => new StickerDto
            {
                StickerId = s.StickerId,
                CategoryId = s.CategoryId,
                StickerName = s.StickerName,
                ImageUrl = s.ImageUrl,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive
            }).ToList()
        }).ToList();
    }

    public async Task<StickerCategoryDto> CreateCategoryAsync(CreateStickerCategoryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            throw new ArgumentException("Category name is required.");
        }

        var exists = await _context.StickerCategories.AnyAsync(c => c.CategoryName.ToLower() == request.CategoryName.Trim().ToLower());
        if (exists)
        {
            throw new InvalidOperationException($"Sticker category '{request.CategoryName}' already exists.");
        }

        var category = new StickerCategory
        {
            CategoryName = request.CategoryName.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "🌸" : request.Icon.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.StickerCategories.Add(category);
        await _context.SaveChangesAsync();

        return new StickerCategoryDto
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            Icon = category.Icon,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            Stickers = new()
        };
    }

    public async Task<StickerDto> CreateStickerAsync(CreateStickerRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StickerName))
        {
            throw new ArgumentException("Sticker name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new ArgumentException("Sticker image URL or emoji representation is required.");
        }

        var category = await _context.StickerCategories.FindAsync(request.CategoryId);
        if (category == null)
        {
            throw new KeyNotFoundException($"Sticker category {request.CategoryId} not found.");
        }

        var sticker = new Sticker
        {
            CategoryId = request.CategoryId,
            StickerName = request.StickerName.Trim(),
            ImageUrl = request.ImageUrl.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Stickers.Add(sticker);
        await _context.SaveChangesAsync();

        return new StickerDto
        {
            StickerId = sticker.StickerId,
            CategoryId = sticker.CategoryId,
            StickerName = sticker.StickerName,
            ImageUrl = sticker.ImageUrl,
            DisplayOrder = sticker.DisplayOrder,
            IsActive = sticker.IsActive
        };
    }

    public async Task<StickerDto> UpdateStickerAsync(int stickerId, UpdateStickerRequestDto request)
    {
        var sticker = await _context.Stickers.FindAsync(stickerId);
        if (sticker == null)
        {
            throw new KeyNotFoundException($"Sticker {stickerId} not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.StickerName))
        {
            sticker.StickerName = request.StickerName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            sticker.ImageUrl = request.ImageUrl.Trim();
        }
        sticker.DisplayOrder = request.DisplayOrder;
        sticker.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return new StickerDto
        {
            StickerId = sticker.StickerId,
            CategoryId = sticker.CategoryId,
            StickerName = sticker.StickerName,
            ImageUrl = sticker.ImageUrl,
            DisplayOrder = sticker.DisplayOrder,
            IsActive = sticker.IsActive
        };
    }

    public async Task<bool> DeleteStickerAsync(int stickerId)
    {
        var sticker = await _context.Stickers.FindAsync(stickerId);
        if (sticker == null)
        {
            return false;
        }

        _context.Stickers.Remove(sticker);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Sticker?> GetActiveStickerByIdAsync(int stickerId)
    {
        return await _context.Stickers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StickerId == stickerId && s.IsActive);
    }
}
