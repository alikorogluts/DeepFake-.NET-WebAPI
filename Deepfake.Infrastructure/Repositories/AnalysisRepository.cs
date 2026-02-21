using System;
using System.Threading.Tasks;
using Deepfake.Application.Interfaces;
using Deepfake.Domain.Entities;
using Deepfake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Deepfake.Infrastructure.Repositories;

public class AnalysisRepository : IAnalysisRepository
{
    private readonly AppDbContext _dbContext;

    public AnalysisRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AnalysisResult result)
    {
        _dbContext.AnalysisResults.Add(result);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(AnalysisResult result)
    {
        _dbContext.AnalysisResults.Update(result);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<AnalysisResult?> GetByIdAsync(Guid id)
    {
        return await _dbContext.AnalysisResults.FindAsync(id);
    }

    public async Task<AnalysisResult?> GetByIdNoTrackingAsync(Guid id)
    {
        return await _dbContext.AnalysisResults.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }
    public async Task<(int totalCount, List<AnalysisResult> data)> GetHistoryAsync(int page, int pageSize)
    {
        // En yeni analizler en üstte olacak şekilde (CreatedAt DESC) sıralıyoruz
        var query = _dbContext.AnalysisResults.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var count = await query.CountAsync();
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        
        return (count, data);
    }
}