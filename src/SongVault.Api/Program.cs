using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using SongVault.Api.Auth;
using SongVault.Api.ErrorHandling;
using SongVault.Api.Security;
using SongVault.Application;
using SongVault.Application.Abstractions;
using SongVault.Application.Common;
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
builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.PublicUrl, UriKind.Absolute, out _),
        "App:PublicUrl doit être une URL absolue (ex. http://localhost:8080) : elle sert aux liens des e-mails.")
    .ValidateOnStart();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SongVaultDbContext>("database", tags: ["ready"]);

// --- Derrière nginx (J31) ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // #6 : X-Forwarded-Host, car nginx envoie l'hôte de l'API dans Host (aiguillage de Container Apps)
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                               | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();              // ancien SDK : KnownNetworks
    options.KnownProxies.Clear();
});

// --- Limitation de débit (J31) ---
builder.Services.AddSongVaultRateLimiting(builder.Configuration);

// --- Clés Data Protection en base (#4) : cookies et liens restent valables après un redémarrage ---
builder.Services.AddDataProtection()
    .SetApplicationName("SongVault")
    .PersistKeysToDbContext<SongVaultDbContext>();

// --- Authentification (J32) ---
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
{
    // #4 : pas de connexion tant que l'adresse n'est pas confirmée (ADR 0010)
    options.SignIn.RequireConfirmedEmail = true;
    options.Tokens.EmailConfirmationTokenProvider = AccountTokens.EmailConfirmationProvider;
})
    .AddEntityFrameworkStores<SongVaultDbContext>()
    .AddTokenProvider<EmailConfirmationTokenProvider>(AccountTokens.EmailConfirmationProvider);

// Lien de réinitialisation : 2 h (fournisseur par défaut d'Identity)
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = AccountTokens.PasswordResetLifetime);
// Mot de passe changé ou réinitialisé : les autres sessions sont coupées en une minute au plus
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
// E-mails d'Identity en français, liens vers le front, envoyés en arrière-plan
builder.Services.AddTransient<IEmailSender<IdentityUser>, IdentityEmailSender>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "songvault.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // Secure dès que la requête d'origine est en HTTPS
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // Une API répond 401/403, elle ne redirige JAMAIS vers une page de connexion
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<HttpBandContext>();
builder.Services.AddScoped<IBandContext>(sp => sp.GetRequiredService<HttpBandContext>());

var app = builder.Build();

// Échec immédiat et explicite si la configuration est incomplète (semaine 5)
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("SongVault")))
    throw new InvalidOperationException(
        "Chaîne de connexion 'SongVault' absente : user-secrets en développement, " +
        "variable ConnectionStrings__SongVault dans un conteneur.");

// ================= Pipeline (l'ordre compte) =================
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ================= Endpoints =================
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// /api/auth/register, /api/auth/login, /api/auth/manage/… (fournis par Identity)
app.MapGroup("/api/auth")
   .MapIdentityApi<IdentityUser>()
   .RequireRateLimiting(RateLimiting.Auth);      // limite les tentatives de connexion par IP

// TOUS les contrôleurs exigent un utilisateur connecté
app.MapControllers().RequireAuthorization();

app.Run();

public partial class Program;