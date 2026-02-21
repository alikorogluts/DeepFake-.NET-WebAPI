using System;
using System.Collections.Generic;
namespace Deepfake.Domain.DTOs;

    

public class PaginatedHistoryResponseDto
{
    public bool Success { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<HistoryItemDto> Data { get; set; } = new();
}

