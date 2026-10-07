using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;          // NOUVEAU : ForwardedHeaders

using Scalar.AspNetCore;                            // retirez si vous n'utilisez pas Scalar

using SongVault.Api.ErrorHandling;
using SongVault.Api.Security;                       // NOUVEAU : middleware et rate limiting
using SongVault.Application;
using SongVault.Infrastructure;
using SongVault.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ================= Services =================
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SongVaultDbContext>("database", tags: ["ready"]);

// NOUVEAU (31.7) : limitation de débit
builder.Services.AddSongVaultRateLimiting(builder.Configuration);

// NOUVEAU (31.8) : lecture des en-têtes posés par nginx
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // L'API n'est joignable QUE par nginx (réseau Docker interne) :
    // on fait donc confiance à n'importe quel proxy de ce réseau.
    options.KnownIPNetworks.Clear();               // ancien SDK : options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("SongVault")))
    throw new InvalidOperationException(
        "Chaîne de connexion 'SongVault' absente : user-secrets en développement, " +
        "variable ConnectionStrings__SongVault dans un conteneur.");

// ================= Pipeline (l'ordre compte) =================
app.UseForwardedHeaders();                          // NOUVEAU : EN PREMIER
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();     // NOUVEAU

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();                    // retirez si vous n'utilisez pas Scalar
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseRateLimiter();                               // NOUVEAU (31.7)

// ================= Endpoints =================
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapControllers();

app.Run();

public partial class Program;