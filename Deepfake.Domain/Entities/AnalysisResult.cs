using Deepfake.Domain.Enums;

namespace Deepfake.Domain.Entities;

public class AnalysisResult
{
    // Temel Bilgiler
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsDeepfake { get; set; }
    public decimal CnnConfidence { get; set; }
    
    // Destekleyici Analiz Skorları
    public decimal? ElaScore { get; set; }
    public decimal? FftAnomalyScore { get; set; }
    
    // Metadata Bilgileri
    public bool ExifHasMetadata { get; set; }
    public string? ExifCameraInfo { get; set; }
    public string? ExifSuspiciousIndicators { get; set; }
    
    // Dosya Yolları (Supabase URL'leri tutulacak)
    public string OriginalImagePath { get; set; } = string.Empty;
    public string? GradcamImagePath { get; set; }
    public string? ElaImagePath { get; set; }
    public string? FftImagePath { get; set; }
    public string? ThumbnailPath { get; set; }
    
    // İşlem Durumu ve Metrikler
    public decimal? ProcessingTimeSeconds { get; set; }
    public AnalysisStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Zaman Damgaları
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}