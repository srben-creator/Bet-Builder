using BetBuilder.Application.Interfaces;
using BetBuilder.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BetBuilder.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValueBetsService, ValueBetsService>();
        services.AddScoped<ILadderService, LadderService>();
        services.AddScoped<IFixturesService, FixturesService>();
        services.AddScoped<IPerformanceService, PerformanceService>();
        services.AddScoped<IBacktestService, BacktestService>();
        services.AddScoped<IDataSyncService, DataSyncService>();

        return services;
    }
}
