using Deepfake.Application.Constants;
using Deepfake.Application.Interfaces;
using Deepfake.Domain.DTOs;
using Deepfake.Domain.Entities;
using Deepfake.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")] 
[Authorize]
public class AnalysesController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IAnalysisRepository _repository; 
    private readonly IAnalysisJobPublisher _analysisJobPublisher;
    private readonly IImageProcessingService _imageProcessingService;

    public AnalysesController(
        IStorageService storageService, 
        IAnalysisRepository repository, 
        IAnalysisJobPublisher analysisJobPublisher, 
        IImageProcessingService imageProcessingService)
    {
        _storageService = storageService;
        _repository = repository;
        _analysisJobPublisher = analysisJobPublisher;
        _imageProcessingService = imageProcessingService;
    }

    // POST: /api/analyses
    [HttpPost]
    public async Task<IActionResult> CreateAnalysis(IFormFile? image)
    {
        if (image == null || image.Length == 0) return BadRequest(new { success = false, message = "Lütfen bir görsel yükleyin." });
        if (image.Length > 10 * 1024 * 1024) return BadRequest(new { success = false, message = "Dosya boyutu 10 MB'ı geçemez." });

        using var imageStream = image.OpenReadStream();
        bool isValid = await _imageProcessingService.IsValidImageSignatureAsync(imageStream);
        
        if (!isValid) 
            return BadRequest(new { success = false, message = "Geçersiz dosya imzası. Sadece gerçek PNG ve JPEG kabul edilir." });

        try
        {
            var analysisId = Guid.NewGuid();
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            var originalUrl = await _storageService.UploadFileAsync(imageStream, AppConstants.StorageBucket, $"originals/{analysisId}{extension}", image.ContentType);

            // 🚨 BEST PRACTICE: Controller artık resim küçültmeyi bilmiyor, işi Ustasına bıraktı!
            var thumbnailBytes = await _imageProcessingService.CreateThumbnailAsync(imageStream, 150, 150);
            var thumbnailUrl = await _storageService.UploadFileBytesAsync(thumbnailBytes, AppConstants.StorageBucket, $"thumbnails/{analysisId}.jpg");

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
                return StatusCode(500, new { success = false, status = AnalysisStatus.Failed.ToString(), message = "Analiz işlemi sırasında bir hata oluştu(TimeOut)" });
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

    // GET: /api/analyses/{id}
    // 🚨 ÇÖZÜM: Rota çakışmasını önlemek için parametreyi URL'e aldık
    [HttpGet("{id:guid}")] 
    public async Task<IActionResult> GetAnalysisResult(Guid id)
    {
        var result = await _repository.GetByIdNoTrackingAsync(id);

        if (result == null) return NotFound(new { success = false, message = "Analiz bulunamadı." });

        if (result.Status == AnalysisStatus.Processing)
        {
            // .ToString() ekledik
            return StatusCode(202, new { success = true, status = AnalysisStatus.Processing.ToString(), message = "Analiz işlemi devam etmektedir" });
        }

        if (result.Status == AnalysisStatus.Failed)
        {
            return StatusCode(500, new { success = false, status = AnalysisStatus.Failed.ToString(), message = "Analiz işlemi sırasında bir hata oluştu", errorMessage = result.ErrorMessage });
        }

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
            status = AnalysisStatus.Completed.ToString(), // .ToString() eklendi
            result = detail
        });
    }

    // GET: /api/analyses
    // 🚨 ÇÖZÜM: Bu metod URL'den ID almaz, tüm listeyi getirir
    [HttpGet]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize > 50) pageSize = 50; 

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