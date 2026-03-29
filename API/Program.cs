using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StartRef.Api;
using StartRef.Api.Data;
using StartRef.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: new[] { 40613 });
        }));

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    const int maxMigrationAttempts = 8;
    for (var attempt = 1; attempt <= maxMigrationAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (IsTransientSqlStartupError(ex) && attempt < maxMigrationAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
            app.Logger.LogWarning(
                ex,
                "Database migration attempt {Attempt}/{MaxAttempts} failed due to transient SQL startup issue. Retrying in {DelaySeconds}s.",
                attempt,
                maxMigrationAttempts,
                (int)delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

app.UseResponseCompression();
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapRootEndpoints();
app.MapStatusEndpoints();
app.MapCompetitionEndpoints();
app.MapRunnerEndpoints();
app.MapLookupEndpoints();
app.MapChangeLogEndpoints();

app.Run();

static bool IsTransientSqlStartupError(Exception ex)
{
    if (ex is SqlException sqlException)
    {
        return sqlException.Number is 40613 or -2 or 10928 or 10929 or 40197 or 40501 or 4060;
    }

    if (ex.InnerException is not null)
    {
        return IsTransientSqlStartupError(ex.InnerException);
    }

    return false;
}
