using Microsoft.Extensions.DependencyInjection;
using Stalksville.Application.Advanced;
using Stalksville.Application.Auth;
using Stalksville.Application.Clans;
using Stalksville.Application.Investigations;
using Stalksville.Application.Players;
using Stalksville.Application.System;

namespace Stalksville.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddStalksvilleApplication(this IServiceCollection services)
    {
        services.AddOptions<Advanced.AlertsOptions>().BindConfiguration("Alerts");
        services.AddScoped<PlayerService>();
        services.AddScoped<ClanService>();
        services.AddScoped<AuthService>();
        services.AddScoped<SystemService>();
        services.AddScoped<InvestigationService>();
        services.AddScoped<GraphService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PlayerIntelligenceService>();
        services.AddScoped<HighscoreService>();
        services.AddScoped<RankedService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<Investigations.InvestigationExporter>();
        return services;
    }
}
