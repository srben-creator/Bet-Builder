using BetBuilder.Application;
using BetBuilder.Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Load secret.json if present
builder.Configuration
    .AddJsonFile("secret.json", optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "secret.json"), optional: true, reloadOnChange: true);

// 1. Configure Logging (Remove Windows EventLog to prevent AggregateException on Windows)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 2. Add Services
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddScoped<BetBuilder.Application.Interfaces.ISyncNotifier, BetBuilder.Api.Hubs.SyncNotifier>();

// 2. Add Layer Dependencies
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 3. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 4. Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "⚽ Bet-Builder API",
        Version = "v1",
        Description = "API de Apostas Quantitativas com Valor Esperado (+EV) e Dixon-Coles Bivariate Poisson"
    });
});

var app = builder.Build();

// 4.1 Apply idempotent schema migrations for PostgreSQL if column is missing
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BetBuilder.Infrastructure.Data.BetBuilderDbContext>();
    if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
    {
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(
            db.Database,
            "ALTER TABLE IF EXISTS value_bets ADD COLUMN IF NOT EXISTS pinnacle_price NUMERIC(8,3);");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Automatic schema migration check skipped: {Message}", ex.Message);
}

// 5. Configure HTTP Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bet-Builder API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");

// 6. Serve SPA Static Files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<BetBuilder.Api.Hubs.SyncHub>("/api/hubs/sync");
app.MapFallbackToFile("index.html");

app.Run();
