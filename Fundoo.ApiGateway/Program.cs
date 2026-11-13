using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;

namespace Fundoo.ApiGateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

            builder.Services
                .AddAuthentication()
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            // .NET built-in rate limiting (incoming)
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // 1) Per-IP fixed window (pehle jaisa)
                var perIpLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    if (context.Request.Path.StartsWithSegments("/health"))
                        return RateLimitPartition.GetNoLimiter("health");

                    var isAuth = context.Request.Path.StartsWithSegments("/api/auth");
                    var key = isAuth ? $"auth-{ip}" : $"general-{ip}";

                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isAuth ? 5 : 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
                });

                // 2) NEW: incoming concurrency limit (ek saath max 100 requests andar, 50 queue mein)
                var concurrencyLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (context.Request.Path.StartsWithSegments("/health"))
                        return RateLimitPartition.GetNoLimiter("health");

                    return RateLimitPartition.GetConcurrencyLimiter("gateway-concurrency", _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 100,
                        QueueLimit = 50,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
                });

                // Dono limiters ek saath lagenge
                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(perIpLimiter, concurrencyLimiter);

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();
                    }
                    await context.HttpContext.Response.WriteAsync(
                        "Too many requests. Please try again later.", token);
                };
            });

            // Outgoing resilience (Polly v8) on Ocelot downstream calls
            builder.Services.AddOcelot()
                .AddDelegatingHandler<GatewayResilienceHandler>(true);

            var app = builder.Build();

            app.UseRateLimiter();

            app.Map("/health", branch =>
            {
                branch.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync("Healthy");
                });
            });

            await app.UseOcelot();
            app.Run();
        }
    }

    public class GatewayResilienceHandler : DelegatingHandler
    {
        private static readonly HashSet<HttpMethod> Retryable =
            new() { HttpMethod.Get, HttpMethod.Head, HttpMethod.Options, HttpMethod.Delete };

        private static readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> Pipelines = new();

        private static PredicateBuilder<HttpResponseMessage> Handle()
        {
            return new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r => (int)r.StatusCode >= 500);
        }

        private static ResiliencePipeline<HttpResponseMessage> Build(bool withRetry)
        {
            var b = new ResiliencePipelineBuilder<HttpResponseMessage>();

            // Bulkhead: har downstream service ka alag pool (Authority ke hisaab se)
            b.AddConcurrencyLimiter(50, 20);

            if (withRetry)
            {
                b.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = Handle(),
                    OnRetry = args =>
                    {
                        args.Outcome.Result?.Dispose();
                        return default;
                    }
                });
            }

            b.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = Handle()
            });

            b.AddTimeout(TimeSpan.FromSeconds(10));

            return b.Build();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var withRetry = Retryable.Contains(request.Method);
            var key = request.RequestUri?.Authority + "|" + withRetry;
            var pipeline = Pipelines.GetOrAdd(key, _ => Build(withRetry));

            try
            {
                return await pipeline.ExecuteAsync(
                    async token => await base.SendAsync(request, token), ct);
            }
            catch (TimeoutRejectedException)
            {
                return Error(HttpStatusCode.GatewayTimeout, "Downstream service timed out.");
            }
            catch (BrokenCircuitException)
            {
                return Error(HttpStatusCode.ServiceUnavailable, "Service temporarily unavailable.");
            }
            catch (RateLimiterRejectedException)
            {
                return Error(HttpStatusCode.ServiceUnavailable, "Service is busy.");
            }
        }

        private static HttpResponseMessage Error(HttpStatusCode code, string msg)
        {
            return new HttpResponseMessage(code) { Content = new StringContent(msg) };
        }
    }
}