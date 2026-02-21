using System;

namespace Deepfake.Domain.DTOs;

public class AnalysisResultResponseDto
{
    public Guid AnalysisId { get; set; } 
    public string Status { get; set; } = string.Empty;
    public object? Result { get; set; } 
    public string? Message { get; set; } 
    public string? ErrorMessage { get; set; }
}
