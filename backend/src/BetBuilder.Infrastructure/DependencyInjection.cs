using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BetBuilder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var supabaseUrl = configuration["SUPABASE_URL"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
            var supabaseKey = configuration["SUPABASE_KEY"] ?? Environment.GetEnvironmentVariable("SUPABASE_KEY");

            if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey))
            {
                var uri = new Uri(supabaseUrl);
                var projectRef = uri.Host.Split('.')[0];
                connectionString = $"Host=db.{projectRef}.supabase.co;Port=5432;Database=postgres;Username=postgres;Password={supabaseKey};SSL Mode=Require;Trust Server Certificate=true";
            }
        }

        services.AddDbContext<BetBuilderDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            }
        });

        return services;
    }
}
