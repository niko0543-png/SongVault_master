using Microsoft.AspNetCore.Http;

namespace SongVault.Api.Localization;

/// <summary>
/// #26 : branché sur AddProblemDetails, traduit chaque réponse d'erreur (exceptions, ValidationProblem, limite de
/// débit) : titre, détail et messages par champ, plus « code », stable d'une langue à l'autre.
/// </summary>
internal static class ProblemTranslation
{
    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var language = RequestLanguage.From(context.HttpContext.Request);

        string? code = null;
        if (problem.Detail is { } detail)
            (code, problem.Detail) = ErrorTranslator.Translate(detail, language);
        if (problem.Title is { } title)
        {
            var (titleCode, text) = ErrorTranslator.Translate(title, language);
            problem.Title = text;
            code ??= titleCode;
        }
        if (problem is HttpValidationProblemDetails validation)
            foreach (var (field, messages) in validation.Errors.ToList())
                validation.Errors[field] = [.. messages.Select(m => ErrorTranslator.Translate(m, language).Text)];

        if (code is not null) problem.Extensions["code"] = code;
        if (!context.HttpContext.Response.HasStarted)
            context.HttpContext.Response.Headers.ContentLanguage = language == Language.English ? "en" : "fr";
    }
}
