using System;
using System.Threading.Tasks;
using Deepfake.Domain.Entities;

namespace Deepfake.Application.Interfaces;

public interface IAnalysisRepository
{
    Task AddAsync(AnalysisResult result);
    Task UpdateAsync(AnalysisResult result);
    Task<AnalysisResult?> GetByIdAsync(Guid id);
    Task<AnalysisResult?> GetByIdNoTrackingAsync(Guid id); // Sadece okuma işlemleri (GET) için hızlı versiyon
    Task<(int totalCount, List<AnalysisResult> data)> GetHistoryAsync(int page, int pageSize);
}