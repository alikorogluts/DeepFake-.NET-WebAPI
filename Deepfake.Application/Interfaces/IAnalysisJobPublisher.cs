using System;
using System.Threading.Tasks;

namespace Deepfake.Application.Interfaces;

public interface IAnalysisJobPublisher
{
    // Asenkron olarak RabbitMQ'ya mesaj fırlatır
    Task<bool> PublishAnalysisJobAsync(Guid analysisId, string imageUrl);
}