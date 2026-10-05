using Scalar.AspNetCore;
using SongVault.Application;
using SongVault.Infrastructure;
using SongVault.Api.ErrorHandling;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SongVault.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SongVaultDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// Échec immédiat et explicite si la configuration est incomplète.
// (L'outillage EF s'arrête juste après Build() : cette vérification ne le gêne pas.)
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("SongVault")))
    throw new InvalidOperationException(
        "Chaîne de connexion 'SongVault' absente : user-secrets en développement, " +
        "variable ConnectionStrings__SongVault dans un conteneur.");

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,                         // AUCUNE vérification : le processus répond, c'est tout
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),   // vérifie la base
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
