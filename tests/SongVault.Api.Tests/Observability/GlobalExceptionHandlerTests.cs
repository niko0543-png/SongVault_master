using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SongVault.Api.ErrorHandling;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Api.Tests.Observability;

/// <summary>#18 : une erreur 500 donne à l'utilisateur la référence de sa trace, et rien d'autre.</summary>
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<JsonElement> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
    }

    [Fact]
    public async Task Une_erreur_500_donne_la_reference_de_sa_trace_sans_le_message()
    {
        using var request = new System.Diagnostics.Activity("requête").Start();

        var problem = await HandleAsync(new InvalidOperationException("Login failed for user 'songvaultadmin'"));

        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal(request.TraceId.ToHexString(), problem.GetProperty("reference").GetString());
        Assert.DoesNotContain("songvaultadmin", problem.GetRawText());
    }

    [Fact]
    public async Task Une_erreur_attendue_n_a_pas_de_reference()
    {
        using var request = new System.Diagnostics.Activity("requête").Start();

        var problem = await HandleAsync(new NotFoundException("Morceau", Guid.Empty));

        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.False(problem.TryGetProperty("reference", out _));
    }
}