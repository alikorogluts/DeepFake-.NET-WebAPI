using Deepfake.Application.Interfaces;
using Deepfake.Domain.Entities;
using Deepfake.Domain.Enums;
using Deepfake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ASP.NET_Core_Web_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("IpRateLimiter")]
public class AnalysisController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly AppDbContext _dbContext;
    private readonly IPythonService _pythonService; // YENİ: Python servisimizi ekledik

    // Constructor'a IPythonService dahil edildi
    public AnalysisController(IStorageService storageService, AppDbContext dbContext, IPythonService pythonService)
    {
        _storageService = storageService;
        _dbContext = dbContext;
        _pythonService = pythonService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile? image)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new { success = false, message = "Lütfen bir görsel yükleyin." });

        var maxFileSize = 10 * 1024 * 1024;
        if (image.Length > maxFileSize)
            return BadRequest(new { success = false, message = "Dosya boyutu 10 MB'ı geçemez." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { success = false, message = "Sadece PNG, JPEG ve JPG formatları desteklenmektedir." });

        try
        {
            var analysisId = Guid.NewGuid();
            var fileName = $"originals/{analysisId}{extension}";

            // 1. Supabase Storage'a Yükle
            var imageUrl = await _storageService.UploadImageAsync(image, "analysis-images", fileName);

            // 2. Veritabanına "Processing" Olarak Kaydet
            var analysisRecord = new AnalysisResult
            {
                Id = analysisId,
                OriginalImagePath = imageUrl,
                Status = AnalysisStatus.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.AnalysisResults.Add(analysisRecord);
            await _dbContext.SaveChangesAsync();

            // 3. PYTHON AI SERVİSİNİ TETİKLE (YENİ EKLENEN KISIM)
            var isTriggered = await _pythonService.TriggerAnalysisAsync(analysisId, imageUrl);

            // Eğer Python servisine ulaşılamazsa (Kapalıysa veya ağ hatası varsa)
            if (!isTriggered)
            {
                analysisRecord.Status = AnalysisStatus.Failed;
                analysisRecord.ErrorMessage = "AI analiz servisine (Python) şu anda ulaşılamıyor.";
                analysisRecord.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                return StatusCode(503, new { success = false, message = "AI analiz servisi şu an yanıt vermiyor. Lütfen daha sonra tekrar deneyin." });
            }

            // 4. Her şey yolundaysa kullanıcıya başarı mesajı dön
            return Ok(new
            {
                success = true,
                message = "Görsel başarıyla yüklendi ve AI analizi başladı.",
                analysisId = analysisId,
                imageUrl = imageUrl,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Yükleme sırasında bir hata oluştu.", error = ex.Message });
        }
    }
}