using Confluent.Kafka;
using Fundoo.User.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Kafka;

public class KafkaProducer : IKafkaProducer
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            MessageTimeoutMs = 3000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync(string topic, string key, string message, CancellationToken ct = default)
    {
        try
        {
            await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = message
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send Kafka message to topic {Topic}: {Message}", topic, ex.Message);
        }
    }
}
