namespace Deepfake.Domain.DTOs;


public class AnalysisDetailDto
{
    public bool IsDeepfake { get; set; }
    public decimal CnnConfidence { get; set; }
    public decimal? ElaScore { get; set; }
    public decimal? FftAnomalyScore { get; set; }
    public ExifAnalysisDto ExifAnalysis { get; set; } = new();
    public string? OriginalImagePath { get; set; }
    public string? GradcamImagePath { get; set; }
    public string? ElaImagePath { get; set; }
    public string? FftImagePath { get; set; }
    public decimal? ProcessingTimeSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

