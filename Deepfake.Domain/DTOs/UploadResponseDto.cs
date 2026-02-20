using System;

namespace Deepfake.Domain.DTOs;

public class UploadResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid AnalysisId { get; set; }
    public string? OriginalImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime Timestamp { get; set; }
}