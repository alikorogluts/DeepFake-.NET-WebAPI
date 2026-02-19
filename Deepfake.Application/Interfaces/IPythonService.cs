using System;
using System.Threading.Tasks;

namespace Deepfake.Application.Interfaces;

public interface IPythonService
{
    // Python API'ye analiz ID'sini ve Supabase'deki resim URL'ini gönderir
    Task<bool> TriggerAnalysisAsync(Guid analysisId, string imageUrl);
}