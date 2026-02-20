using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
[EnableRateLimiting("IpRateLimiter")]
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

    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile? image)
    {
        if (image == null || image.Length == 0) return BadRequest(new { success = false, message = "Lütfen bir görsel yükleyin." });
        if (image.Length > 10 * 1024 * 1024) return BadRequest(new { success = false, message = "Dosya boyutu 10 MB'ı geçemez." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension)) return BadRequest(new { success = false, message = "Sadece PNG, JPEG ve JPG desteklenir." });

        try
        {
            var analysisId = Guid.NewGuid();

            // 1. IFormFile'ı API kapısında evrensel Stream'e (Akış) çeviriyoruz
            using var imageStream = image.OpenReadStream();

            // 2. Altyapıya IFormFile DEĞİL, Stream gönderiyoruz (UploadFileAsync metodu ile)
            var originalUrl = await _storageService.UploadFileAsync(imageStream, AppConstants.StorageBucket, $"originals/{analysisId}{extension}", image.ContentType);

            // 3. Supabase resmi okurken Stream'in sonuna geldi. 
            // Thumbnail için resmi baştan okumak adına kaseti başa sarıyoruz (Position = 0)
            imageStream.Position = 0;

            // 4. Thumbnail (Küçük Resim) İşlemleri
            using var img = await Image.LoadAsync(imageStream);
            img.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(256, 256), Mode = ResizeMode.Crop }));

            using var thumbStream = new MemoryStream();
            await img.SaveAsync(thumbStream, new JpegEncoder());
            
            var thumbnailUrl = await _storageService.UploadFileBytesAsync(thumbStream.ToArray(), AppConstants.StorageBucket, $"thumbnails/{analysisId}.jpg");

            // 5. Veritabanı Kaydı
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

            // 6. RabbitMQ'ya Görev Bırakma
            var isQueued = await _analysisJobPublisher.PublishAnalysisJobAsync(analysisId, originalUrl);

            if (!isQueued)
            {
                analysisRecord.Status = AnalysisStatus.Failed;
                analysisRecord.ErrorMessage = "Sistem yoğunluğu: Analiz sıraya alınamadı.";
                await _repository.UpdateAsync(analysisRecord); 
                return StatusCode(503, new { success = false, message = "Sistem meşgul, lütfen tekrar deneyin." });
            }

            return Ok(new UploadResponseDto
            {
                Success = true,
                Message = "Görsel başarıyla yüklendi ve analiz kuyruğuna alındı.",
                AnalysisId = analysisId,
                OriginalImageUrl = originalUrl,
                ThumbnailUrl = thumbnailUrl,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Bir hata oluştu.", error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAnalysisResult(Guid id)
    {
        var result = await _repository.GetByIdNoTrackingAsync(id);

        if (result == null) return NotFound(new { success = false, message = "Analiz bulunamadı." });

        return Ok(new AnalysisResultResponseDto
        {
            Id = result.Id,
            Status = result.Status.ToString(),
            IsDeepfake = result.IsDeepfake,
            CnnConfidence = result.CnnConfidence,
            ElaScore = result.ElaScore,
            FftAnomalyScore = result.FftAnomalyScore,
            
            // 👇 İŞTE EKSİK OLAN VE YENİ EKLENEN 3 SATIR BURASI 👇
            ExifHasMetadata = result.ExifHasMetadata,
            ExifCameraInfo = result.ExifCameraInfo,
            ExifSuspiciousIndicators = result.ExifSuspiciousIndicators,
            // 👆 ============================================== 👆

            OriginalImagePath = result.OriginalImagePath,
            ThumbnailPath = result.ThumbnailPath,
            GradcamImagePath = result.GradcamImagePath,
            ElaImagePath = result.ElaImagePath,
            FftImagePath = result.FftImagePath,
            ProcessingTimeSeconds = result.ProcessingTimeSeconds,
            ErrorMessage = result.ErrorMessage,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt
        });
    }
}