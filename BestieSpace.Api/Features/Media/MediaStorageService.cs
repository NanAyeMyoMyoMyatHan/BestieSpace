using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Media;

public interface IMediaStorageService
{
    Task<(string storageKey, string thumbStorageKey, string contentType, long fileSize)> SaveImageAsync(Stream stream, string originalFileName, int circleId);
    Task<(string storageKey, string contentType, long fileSize)> SaveVoiceAsync(Stream stream, string originalFileName, int circleId);
    Task<(Stream stream, string contentType, string fileName)?> GetMediaStreamAsync(string storageKey, string originalFileName, string contentType);
    Task<bool> DeleteMediaFilesAsync(string storageKey, string? thumbStorageKey = null);
    bool ValidateImageMagicBytes(byte[] headerBytes, string extension);
    bool ValidateVoiceMagicBytes(byte[] headerBytes, string extension);
}

public class MediaStorageService : IMediaStorageService
{
    private readonly string _storageBasePath;
    private readonly ILogger<MediaStorageService> _logger;

    public const long MaxImageSize = 10 * 1024 * 1024; // 10MB
    public const long MaxVoiceSize = 15 * 1024 * 1024; // 15MB

    public MediaStorageService(IWebHostEnvironment env, ILogger<MediaStorageService> logger)
    {
        _logger = logger;
        // Securely store outside wwwroot under App_Data/media
        _storageBasePath = Path.Combine(env.ContentRootPath, "App_Data", "media");
        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
        }
    }

    public async Task<(string storageKey, string thumbStorageKey, string contentType, long fileSize)> SaveImageAsync(Stream stream, string originalFileName, int circleId)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
        {
            throw new ArgumentException("Uploaded image cannot be empty.");
        }

        if (bytes.Length > MaxImageSize)
        {
            throw new ArgumentException($"Image exceeds maximum allowed size of {MaxImageSize / (1024 * 1024)}MB.");
        }

        var ext = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
        if (ext == "jpg") ext = "jpeg";

        if (ext != "jpeg" && ext != "png" && ext != "webp")
        {
            throw new ArgumentException("Unsupported image format. Allowed formats: JPEG, PNG, WebP.");
        }

        // Validate magic bytes
        var headerBytes = bytes.Take(16).ToArray();
        if (!ValidateImageMagicBytes(headerBytes, ext))
        {
            _logger.LogWarning("Magic byte inspection failed for image {FileName} with extension {Ext}", originalFileName, ext);
            throw new ArgumentException("Invalid file content: the uploaded file does not match a valid image structure.");
        }

        var contentType = ext switch
        {
            "jpeg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            _ => "image/jpeg"
        };

        var dateDir = DateTime.UtcNow.ToString("yyyyMMdd");
        var relativeFolder = Path.Combine("images", circleId.ToString(), dateDir);
        var targetDir = Path.Combine(_storageBasePath, relativeFolder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var uniqueId = Guid.NewGuid().ToString("N");
        var fileName = $"{uniqueId}.{ext}";
        var thumbFileName = $"{uniqueId}_thumb.{ext}";

        var filePath = Path.Combine(targetDir, fileName);
        var thumbFilePath = Path.Combine(targetDir, thumbFileName);

        await File.WriteAllBytesAsync(filePath, bytes);
        // Save thumbnail file copy
        await File.WriteAllBytesAsync(thumbFilePath, bytes);

        var storageKey = Path.Combine(relativeFolder, fileName).Replace('\\', '/');
        var thumbStorageKey = Path.Combine(relativeFolder, thumbFileName).Replace('\\', '/');

        _logger.LogInformation("Saved image {FileName} to storage key {StorageKey} ({Bytes} bytes)", originalFileName, storageKey, bytes.Length);

        return (storageKey, thumbStorageKey, contentType, bytes.Length);
    }

    public async Task<(string storageKey, string contentType, long fileSize)> SaveVoiceAsync(Stream stream, string originalFileName, int circleId)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
        {
            throw new ArgumentException("Uploaded voice note cannot be empty.");
        }

        if (bytes.Length > MaxVoiceSize)
        {
            throw new ArgumentException($"Voice message exceeds maximum allowed size of {MaxVoiceSize / (1024 * 1024)}MB.");
        }

        var ext = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) ext = "webm";

        var allowedExtensions = new[] { "webm", "ogg", "wav", "mp3", "m4a", "mp4" };
        if (!allowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Unsupported audio format. Allowed formats: WebM, OGG, WAV, MP3, M4A.");
        }

        // Validate magic bytes
        var headerBytes = bytes.Take(16).ToArray();
        if (!ValidateVoiceMagicBytes(headerBytes, ext))
        {
            _logger.LogWarning("Magic byte inspection failed for audio {FileName} with extension {Ext}", originalFileName, ext);
            throw new ArgumentException("Invalid audio content: the uploaded file does not match a valid audio format.");
        }

        var contentType = ext switch
        {
            "webm" => "audio/webm",
            "ogg" => "audio/ogg",
            "wav" => "audio/wav",
            "mp3" => "audio/mpeg",
            "m4a" or "mp4" => "audio/mp4",
            _ => "audio/webm"
        };

        var dateDir = DateTime.UtcNow.ToString("yyyyMMdd");
        var relativeFolder = Path.Combine("voice", circleId.ToString(), dateDir);
        var targetDir = Path.Combine(_storageBasePath, relativeFolder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var uniqueId = Guid.NewGuid().ToString("N");
        var fileName = $"{uniqueId}.{ext}";
        var filePath = Path.Combine(targetDir, fileName);

        await File.WriteAllBytesAsync(filePath, bytes);

        var storageKey = Path.Combine(relativeFolder, fileName).Replace('\\', '/');

        _logger.LogInformation("Saved voice message {FileName} to storage key {StorageKey} ({Bytes} bytes)", originalFileName, storageKey, bytes.Length);

        return (storageKey, contentType, bytes.Length);
    }

    public Task<(Stream stream, string contentType, string fileName)?> GetMediaStreamAsync(string storageKey, string originalFileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return Task.FromResult<(Stream stream, string contentType, string fileName)?>(null);

        // Sanitize to prevent path traversal
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_storageBasePath, normalizedKey));

        // Security check: ensure path stays within _storageBasePath
        if (!fullPath.StartsWith(Path.GetFullPath(_storageBasePath), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected with storage key: {Key}", storageKey);
            return Task.FromResult<(Stream stream, string contentType, string fileName)?>(null);
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<(Stream stream, string contentType, string fileName)?>(null);
        }

        Stream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var safeFileName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = Path.GetFileName(fullPath);
        }

        return Task.FromResult<(Stream stream, string contentType, string fileName)?>((fileStream, contentType, safeFileName));
    }

    public Task<bool> DeleteMediaFilesAsync(string storageKey, string? thumbStorageKey = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(storageKey))
            {
                var fullPath = Path.GetFullPath(Path.Combine(_storageBasePath, storageKey.Replace('/', Path.DirectorySeparatorChar)));
                if (fullPath.StartsWith(Path.GetFullPath(_storageBasePath), StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(thumbStorageKey))
            {
                var thumbPath = Path.GetFullPath(Path.Combine(_storageBasePath, thumbStorageKey.Replace('/', Path.DirectorySeparatorChar)));
                if (thumbPath.StartsWith(Path.GetFullPath(_storageBasePath), StringComparison.OrdinalIgnoreCase) && File.Exists(thumbPath))
                {
                    File.Delete(thumbPath);
                }
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete physical media files for {Key}", storageKey);
            return Task.FromResult(false);
        }
    }

    public bool ValidateImageMagicBytes(byte[] headerBytes, string extension)
    {
        if (headerBytes == null || headerBytes.Length < 4) return false;

        switch (extension.ToLowerInvariant())
        {
            case "jpg":
            case "jpeg":
                // JPEG starts with FF D8 FF
                return headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF;

            case "png":
                // PNG starts with 89 50 4E 47 0D 0A 1A 0A
                if (headerBytes.Length < 8) return false;
                return headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47 &&
                       headerBytes[4] == 0x0D && headerBytes[5] == 0x0A && headerBytes[6] == 0x1A && headerBytes[7] == 0x0A;

            case "webp":
                // WebP starts with RIFF (bytes 0..3) and WEBP at bytes 8..11
                if (headerBytes.Length < 12) return false;
                var isRiff = headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46;
                var isWebp = headerBytes[8] == 0x57 && headerBytes[9] == 0x45 && headerBytes[10] == 0x42 && headerBytes[11] == 0x50;
                return isRiff && isWebp;

            default:
                return false;
        }
    }

    public bool ValidateVoiceMagicBytes(byte[] headerBytes, string extension)
    {
        if (headerBytes == null || headerBytes.Length < 4) return false;

        switch (extension.ToLowerInvariant())
        {
            case "webm":
                // EBML Header 1A 45 DF A3
                return headerBytes[0] == 0x1A && headerBytes[1] == 0x45 && headerBytes[2] == 0xDF && headerBytes[3] == 0xA3;

            case "ogg":
                // OggS 4F 67 67 53
                return headerBytes[0] == 0x4F && headerBytes[1] == 0x67 && headerBytes[2] == 0x67 && headerBytes[3] == 0x53;

            case "wav":
                // RIFF....WAVE
                if (headerBytes.Length < 12) return false;
                var isRiff = headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46;
                var isWave = headerBytes[8] == 0x57 && headerBytes[9] == 0x41 && headerBytes[10] == 0x56 && headerBytes[11] == 0x45;
                return isRiff && isWave;

            case "mp3":
                // ID3 tag or MPEG sync word FF FB / FF F3 / FF F2
                if (headerBytes[0] == 0x49 && headerBytes[1] == 0x44 && headerBytes[2] == 0x33) return true;
                return headerBytes[0] == 0xFF && (headerBytes[1] == 0xFB || headerBytes[1] == 0xF3 || headerBytes[1] == 0xF2 || headerBytes[1] == 0xE3);

            case "m4a":
            case "mp4":
                // Contains ftyp at byte offset 4
                if (headerBytes.Length < 8) return false;
                return headerBytes[4] == 0x66 && headerBytes[5] == 0x74 && headerBytes[6] == 0x79 && headerBytes[7] == 0x70;

            default:
                return false;
        }
    }
}
