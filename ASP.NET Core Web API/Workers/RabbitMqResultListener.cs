using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Deepfake.Application.Constants;
using Deepfake.Application.Interfaces;
using Deepfake.Domain.DTOs;
using Deepfake.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ASP.NET_Core_Web_API.Workers;

public class RabbitMqResultListener : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqResultListener> _logger;

    public RabbitMqResultListener(IConfiguration config, IServiceScopeFactory scopeFactory, ILogger<RabbitMqResultListener> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory; 
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMq:Host"],
            Port = int.Parse(_config["RabbitMq:Port"] ?? "5672"),
            UserName = _config["RabbitMq:Username"],
            Password = _config["RabbitMq:Password"]
        };

        var connection = await factory.CreateConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Sihirli metin GİTTİ -> AppConstants.ResultQueue GELDİ
        await channel.QueueDeclareAsync(queue: AppConstants.ResultQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        
        consumer.ReceivedAsync += async (sender, args) =>
        {
            try
            {
                var body = args.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var payload = JsonSerializer.Deserialize<RabbitMqResultPayload>(message);

                if (payload != null)
                {
                    await ProcessResultAsync(payload);
                }

                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                _logger.LogInformation($"Başarıyla işlendi: {payload?.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Mesaj işlenirken hata: {ex.Message}");
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        // Sihirli metin GİTTİ -> AppConstants.ResultQueue GELDİ
        await channel.BasicConsumeAsync(queue: AppConstants.ResultQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        
        _logger.LogInformation("🚀 .NET RabbitMQ Result Listener başlatıldı...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessResultAsync(RabbitMqResultPayload payload)
    {
        using var scope = _scopeFactory.CreateScope();
        
        // AppDbContext GİTTİ -> IAnalysisRepository GELDİ!
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        // FindAsync GİTTİ -> GetByIdAsync GELDİ!
        var record = await repository.GetByIdAsync(payload.Id);
        if (record == null) return;

        if (payload.Status == "Failed")
        {
            record.Status = AnalysisStatus.Failed;
            record.ErrorMessage = payload.ErrorMessage;
        }
        else
        {
            record.Status = AnalysisStatus.Completed;
            record.IsDeepfake = payload.IsDeepfake ?? false;
            record.CnnConfidence = payload.CnnConfidence ?? 0;
            record.ElaScore = payload.ElaScore;
            record.FftAnomalyScore = payload.FftAnomalyScore;
            record.ExifHasMetadata = payload.ExifHasMetadata ?? false;
            record.ExifCameraInfo = payload.ExifCameraInfo;
            record.ExifSuspiciousIndicators = payload.ExifSuspiciousIndicators;
            record.ProcessingTimeSeconds = payload.ProcessingTimeSeconds;

            // Sihirli metin olan "analysis-images" GİTTİ -> AppConstants.StorageBucket GELDİ!
            if (!string.IsNullOrEmpty(payload.GradcamImageBase64))
            {
                var bytes = Convert.FromBase64String(payload.GradcamImageBase64);
                record.GradcamImagePath = await storageService.UploadFileBytesAsync(bytes, AppConstants.StorageBucket, $"gradcam/{payload.Id}.jpg");
            }

            if (!string.IsNullOrEmpty(payload.ElaImageBase64))
            {
                var bytes = Convert.FromBase64String(payload.ElaImageBase64);
                record.ElaImagePath = await storageService.UploadFileBytesAsync(bytes, AppConstants.StorageBucket, $"ela/{payload.Id}.jpg");
            }

            if (!string.IsNullOrEmpty(payload.FftImageBase64))
            {
                var bytes = Convert.FromBase64String(payload.FftImageBase64);
                record.FftImagePath = await storageService.UploadFileBytesAsync(bytes, AppConstants.StorageBucket, $"fft/{payload.Id}.jpg");
            }
        }

        record.UpdatedAt = DateTime.UtcNow;
        
        // SaveChangesAsync GİTTİ -> UpdateAsync GELDİ!
        await repository.UpdateAsync(record);
    }
}