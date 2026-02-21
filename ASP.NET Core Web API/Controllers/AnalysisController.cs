using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Deepfake.Application.Constants;
using Deepfake.Application.Interfaces;
using Deepfake.Domain.DTOs;
using Deepfake.Domain.Entities;
using Deepfake.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ASP.NET_Core_Web_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalysisController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IAnalysisRepository _repository; 
    private readonly IAnalysisJobPublisher _analysisJobPublisher;

    public AnalysisController(IStorageService storageService, IAnalysisRepository repository, IAnalysisJobPublisher analysisJobPublisher)
    {
        _storageService = storageService;
        _repository = repository;
        _analysisJobPublisher = analysisJobPublisher;
    }

    // YENİ EKLENEN: Magic Numbers (Dosya İmzası) Kontrolü
    private bool IsValidImageSignature(Stream stream)
    {
        if (stream == null || stream.Length < 8) return false;
        
        byte[] buffer = new byte[8];
        stream.Position = 0;
        stream.ReadExactly(buffer, 0, 8);
        stream.Position = 0; // Kaseti başa sar

        // JPEG Magic Numbers: FF D8 FF
        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF) return true;

        // PNG Magic Numbers: 89 50 4E 47 0D 0A 1A 0A
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
            buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A) return true;

        return false;
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile? image)
    {
        if (image == null || image.Length == 0) return BadRequest(new { success = false, message = "Lütfen bir görsel yükleyin." });
        if (image.Length > 10 * 1024 * 1024) return BadRequest(new { success = false, message = "Dosya boyutu 10 MB'ı geçemez." });

        using var imageStream = image.OpenReadStream();
        
        // RAPORA UYGUN: Magic Numbers Kontrolü
        if (!IsValidImageSignature(imageStream)) 
            return BadRequest(new { success = false, message = "Geçersiz dosya imzası. Sadece gerçek PNG ve JPEG kabul edilir." });

        try
        {
            var analysisId = Guid.NewGuid();
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            var originalUrl = await _storageService.UploadFileAsync(imageStream, AppConstants.StorageBucket, $"originals/{analysisId}{extension}", image.ContentType);

            imageStream.Position = 0;

            using var img = await Image.LoadAsync(imageStream);
            // RAPORA UYGUN: 150x150 Thumbnail
            img.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(150, 150), Mode = ResizeMode.Crop }));

            using var thumbStream = new MemoryStream();
            await img.SaveAsync(thumbStream, new JpegEncoder());
            var thumbnailUrl = await _storageService.UploadFileBytesAsync(thumbStream.ToArray(), AppConstants.StorageBucket, $"thumbnails/{analysisId}.jpg");

            var analysisRecord = new AnalysisResult
            {
                Id = analysisId,
                OriginalImagePath = originalUrl,
                ThumbnailPath = thumbnailUrl,
                Status = AnalysisStatus.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(analysisRecord);

            var isQueued = await _analysisJobPublisher.PublishAnalysisJobAsync(analysisId, originalUrl);

            if (!isQueued)
            {
                analysisRecord.Status = AnalysisStatus.Failed;
                analysisRecord.ErrorMessage = "Timeout: İşlem 60 saniye içinde tamamlanamadı";
                await _repository.UpdateAsync(analysisRecord); 
                return StatusCode(500, new { success = false, status = "failed", message = "Analiz işlemi sırasında bir hata oluştu" });
            }

            return Ok(new 
            {
                success = true,
                message = "Görsel başarıyla yüklendi ve analiz sıraya alındı",
                analysisId = analysisId,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Bir hata oluştu.", error = ex.Message });
        }
    }

    [HttpGet("result/{analysisId:guid}")] // RAPORA UYGUN ROUTE
    public async Task<IActionResult> GetAnalysisResult(Guid analysisId)
    {
        var result = await _repository.GetByIdNoTrackingAsync(analysisId);

        if (result == null) return NotFound(new { success = false, message = "Analiz bulunamadı." });

        // RAPORA UYGUN: İşlem devam ediyorsa 202 Accepted
        if (result.Status == AnalysisStatus.Processing)
        {
            return StatusCode(202, new { success = true, status = "processing", message = "Analiz işlemi devam etmektedir" });
        }

        // RAPORA UYGUN: İşlem hatalıysa 500 Internal Server Error
        if (result.Status == AnalysisStatus.Failed)
        {
            return StatusCode(500, new { success = false, status = "failed", message = "Analiz işlemi sırasında bir hata oluştu", errorMessage = result.ErrorMessage });
        }

        // RAPORA UYGUN: Nested EXIF Objesi
        var suspiciousList = string.IsNullOrEmpty(result.ExifSuspiciousIndicators) 
            ? new List<string>() 
            : result.ExifSuspiciousIndicators.Split(';').ToList();

        var detail = new AnalysisDetailDto
        {
            IsDeepfake = result.IsDeepfake,
            CnnConfidence = result.CnnConfidence,
            ElaScore = result.ElaScore,
            FftAnomalyScore = result.FftAnomalyScore,
            ExifAnalysis = new ExifAnalysisDto
            {
                HasMetadata = result.ExifHasMetadata,
                CameraInfo = result.ExifCameraInfo,
                SuspiciousIndicators = suspiciousList
            },
            OriginalImagePath = result.OriginalImagePath,
            GradcamImagePath = result.GradcamImagePath,
            ElaImagePath = result.ElaImagePath,
            FftImagePath = result.FftImagePath,
            ProcessingTimeSeconds = result.ProcessingTimeSeconds,
            CreatedAt = result.CreatedAt
        };

        return Ok(new 
        {
            success = true,
            analysisId = result.Id,
            status = "completed",
            result = detail
        });
    }

    // RAPORA UYGUN VE YENİ: Geçmiş Analiz Kayıtları
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        // Sayfa numarası 1'den küçük olamaz
        if (page < 1) page = 1;
        if (pageSize > 50) pageSize = 50; // Tek seferde max 50 kayıt

        var (totalCount, data) = await _repository.GetHistoryAsync(page, pageSize);
        
        var resultData = data.Select(x => new HistoryItemDto
        {
            AnalysisId = x.Id,
            IsDeepfake = x.IsDeepfake,
            CnnConfidence = x.CnnConfidence,
            ThumbnailPath = x.ThumbnailPath,
            CreatedAt = x.CreatedAt
        }).ToList();

        return Ok(new PaginatedHistoryResponseDto
        {
            Success = true,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Data = resultData
        });
    }
}