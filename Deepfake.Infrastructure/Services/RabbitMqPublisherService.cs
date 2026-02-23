using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Deepfake.Application.Constants;
using Deepfake.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Deepfake.Infrastructure.Services;

public class RabbitMqPublisherService : IAnalysisJobPublisher
{
    private readonly IConfiguration _config;
    private static IConnection? _connection;
    private static readonly object _lock = new();

    public RabbitMqPublisherService(IConfiguration config)
    {
        _config = config;
    }

    private async Task<IConnection> GetConnectionAsync()
    {
        if (_connection != null && _connection.IsOpen)
            return _connection;

        var rabbitUrl = _config["RABBITMQ_URL"];

        ConnectionFactory factory;

        // 🔥 URL varsa onu kullan
        if (!string.IsNullOrEmpty(rabbitUrl))
        {
            factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl)
            };
        }
        else
        {
            // 🔥 URL yoksa eski sistem
            factory = new ConnectionFactory
            {
                HostName = _config["RabbitMq:Host"],
                Port = int.Parse(_config["RabbitMq:Port"] ?? "5672"),
                UserName = _config["RabbitMq:Username"],
                Password = _config["RabbitMq:Password"]
            };
        }

        _connection = await factory.CreateConnectionAsync();
        return _connection;
    }

    public async Task<bool> PublishAnalysisJobAsync(Guid analysisId, string imageUrl)
    {
        try
        {
            var connection = await GetConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: AppConstants.AnalysisQueue,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            var payload = new
            {
                id = analysisId.ToString(),
                image_url = imageUrl
            };

            var jsonBody = JsonSerializer.Serialize(payload);
            var body = Encoding.UTF8.GetBytes(jsonBody);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: AppConstants.AnalysisQueue,
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RabbitMQ Publish Error: {ex.Message}");
            return false;
        }
    }
}