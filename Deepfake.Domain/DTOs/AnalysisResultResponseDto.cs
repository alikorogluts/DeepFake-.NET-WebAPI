using System;

namespace Deepfake.Domain.DTOs;

public class AnalysisResultResponseDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsDeepfake { get; set; }
    public decimal CnnConfidence { get; set; }
    
    // Destekleyici Skorlar
    public decimal? ElaScore { get; set; }
    public decimal? FftAnomalyScore { get; set; }
    
    // YENİ EKLENEN: Metadata (EXIF) Bilgileri
    public bool ExifHasMetadata { get; set; }
    public string? ExifCameraInfo { get; set; }
    public string? ExifSuspiciousIndicators { get; set; }
    
    // Dosya Yolları
    public string? OriginalImagePath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? GradcamImagePath { get; set; }
    public string? ElaImagePath { get; set; }
    public string? FftImagePath { get; set; }
    
    // Metrikler ve Zaman Damgaları
    public decimal? ProcessingTimeSeconds { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}