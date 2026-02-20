using System;

namespace Deepfake.Domain.DTOs;

public class RabbitMqResultPayload
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? IsDeepfake { get; set; }
    public decimal? CnnConfidence { get; set; }
    public decimal? ElaScore { get; set; }
    public decimal? FftAnomalyScore { get; set; }
    public bool? ExifHasMetadata { get; set; }
    public string? ExifCameraInfo { get; set; }
    public string? ExifSuspiciousIndicators { get; set; }
    public string? GradcamImageBase64 { get; set; }
    public string? ElaImageBase64 { get; set; }
    public string? FftImageBase64 { get; set; }
    public decimal? ProcessingTimeSeconds { get; set; }
}