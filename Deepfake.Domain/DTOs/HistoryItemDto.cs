using System;
using System.Collections.Generic;

namespace Deepfake.Domain.DTOs;

public class HistoryItemDto
{
    public Guid AnalysisId { get; set; }
    public bool IsDeepfake { get; set; }
    public decimal CnnConfidence { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

