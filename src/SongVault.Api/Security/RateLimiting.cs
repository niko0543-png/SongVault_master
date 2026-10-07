using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SongVault.Api.Security;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public int UploadsPerMinute { get; set; } = 10;
    public int AuthPerMinute { get; set; } = 10;
}

public static class RateLimiting
{
    public const string Uploads = "uploads";
    public const string Auth = "auth";

    public static IServiceCollection AddSongVaultRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = 429, Title = "Trop de requêtes", Detail = "Réessayez dans une minute." },
                });
            };

            options.AddPolicy(Uploads, http => PerMinute(http, o => o.UploadsPerMinute));
            options.AddPolicy(Auth, http => PerMinute(http, o => o.AuthPerMinute));
        });
        return services;
    }

    // Partition : l'utilisateur connecté s'il y en a un, sinon l'adresse IP
    private static RateLimitPartition<string> PerMinute(HttpContext http, Func<RateLimitingOptions, int> limit)
    {
        var options = http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var key = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? http.Connection.RemoteIpAddress?.ToString()
                  ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit(options),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,                     // pas de file d'attente : refus immédiat
        });
    }
}