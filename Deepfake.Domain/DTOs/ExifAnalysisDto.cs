using System.Collections.Generic;

namespace Deepfake.Domain.DTOs;

public class ExifAnalysisDto
{
    public bool HasMetadata { get; set; }
    public string? CameraInfo { get; set; }
    public List<string> SuspiciousIndicators { get; set; } = new();
}