namespace Fundoo.User.Application;

public interface IKafkaProducer
{
    Task ProduceAsync(string topic, string key, string message, CancellationToken ct = default);
}
