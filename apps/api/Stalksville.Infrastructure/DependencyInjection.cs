using System.Threading.RateLimiting;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Stalksville.Application.Abstractions;
using Stalksville.Infrastructure.Auditing;
using Stalksville.Infrastructure.Caching;
using Stalksville.Infrastructure.Persistence;
using Stalksville.Infrastructure.Persistence.Stores;
using Stalksville.Infrastructure.Security;
using Stalksville.Infrastructure.Wolvesville;

namespace Stalksville.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStalksvilleInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WolvesvilleOptions>(configuration.GetSection(WolvesvilleOptions.SectionName));
        services.Configure<Ai.AiOptions>(configuration.GetSection(Ai.AiOptions.SectionName));

        // Connection string and client mode resolve lazily from the final IConfiguration so
        // integration-test overrides (and env-var swaps) apply regardless of registration order.
        services.AddDbContextFactory<StalksvilleDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.")));
        // Scoped context for stores resolves through the singleton factory (thread-safe, avoids
        // the scoped-options/singleton-factory conflict of registering both helpers).
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<StalksvilleDbContext>>().CreateDbContext());

        services.AddMemoryCache();
        services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
        services.AddSingleton<IAuditLog, AuditLogger>();
        services.AddHttpContextAccessor();

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();

        services.AddScoped<IPlayerStore, PlayerStore>();
        services.AddScoped<IClanStore, ClanStore>();
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IDerivationStore, DerivationStore>();
        services.AddScoped<IInvestigationStore, InvestigationStore>();
        services.AddScoped<IAlertStore, AlertStore>();
        services.AddScoped<Seeding.StalksvilleSeeder>();

        RegisterWolvesvilleClient(services, configuration);

        return services;
    }

    private static void RegisterWolvesvilleClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<HostValidationHandler>();
        services.AddTransient<RequestLoggingHandler>();
        services.AddSingleton<Wolvesville.Mock.MockWolvesvilleClient>();

        services.AddHttpClient<WolvesvilleClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WolvesvilleOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline owns timeouts

            // The Wolvesville API requires "Authorization: Bot <key>" plus JSON headers.
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", opts.ApiKey);
            }

            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<HostValidationHandler>()
        .AddHttpMessageHandler<RequestLoggingHandler>()
        .AddResilienceHandler("wolvesville", (builder, context) =>
        {
            var opts = context.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WolvesvilleOptions>>().Value;

            // Outbound throttle first (outermost), then the standard retry/circuit-breaker/timeout set.
            builder.AddRateLimiter(new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, opts.MaxRequestsPerSecond),
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 5,
                    QueueLimit = 50,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

            builder.AddTimeout(TimeSpan.FromSeconds(30)); // total request timeout

            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1)
            });

            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30)
            });

            builder.AddTimeout(TimeSpan.FromSeconds(10)); // per attempt
        });

        // Mode selection happens at resolution time (IOptions is final there), so test-host and
        // environment overrides always take effect.
        services.AddScoped<IWolvesvilleClient>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WolvesvilleOptions>>().Value;
            return options.IsMock
                ? sp.GetRequiredService<Wolvesville.Mock.MockWolvesvilleClient>()
                : sp.GetRequiredService<WolvesvilleClient>();
        });

        RegisterAiNarrators(services);
    }

    private static void RegisterAiNarrators(IServiceCollection services)
    {
        services.AddSingleton<Ai.TemplateNarrator>();

        // Host-guarded client for LLM narration — same outbound security rules as Wolvesville.
        services.AddHttpClient<Ai.OpenAiCompatibleNarrator>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Ai.AiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);
            }
        }).AddHttpMessageHandler<Wolvesville.HostValidationHandler>();

        services.AddScoped<IAiNarrator>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Ai.AiOptions>>().Value;
            if (string.Equals(options.Provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return sp.GetRequiredService<Ai.OpenAiCompatibleNarrator>();
            }

            return sp.GetRequiredService<Ai.TemplateNarrator>();
        });
    }
}
