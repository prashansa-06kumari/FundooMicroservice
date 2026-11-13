using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Application.Notes;
using Fundoo.Notes.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    // Sirf temporary DB errors par retry / breaker
    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException || ex is NpgsqlException { IsTransient: true };

    public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotesDb is missing.");

        // One data source for the whole app (Npgsql handles connection pooling itself)
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        // READ pipeline: Bulkhead -> Retry(+jitter) -> Circuit breaker -> Timeout
        services.AddResiliencePipeline("db-read", builder =>
        {
            builder.AddConcurrencyLimiter(30, 20);

            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Exception is { } ex && IsTransient(ex))
            });

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Exception is { } ex && IsTransient(ex))
            });

            builder.AddTimeout(TimeSpan.FromSeconds(8));
        });

        // WRITE pipeline: retry nahi (duplicate insert se bachne ke liye)
        services.AddResiliencePipeline("db-write", builder =>
        {
            builder.AddConcurrencyLimiter(30, 20);

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Exception is { } ex && IsTransient(ex))
            });

            builder.AddTimeout(TimeSpan.FromSeconds(8));
        });

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ILabelService, LabelService>();
        return services;
    }
}