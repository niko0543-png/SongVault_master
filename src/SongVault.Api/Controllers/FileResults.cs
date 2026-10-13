using Microsoft.AspNetCore.Mvc;

using SongVault.Application.Files;

namespace SongVault.Api.Controllers;

internal static class FileResults
{
    /// <summary>Redirection vers l'URL signée, ou contenu servi par l'API (stockage local).</summary>
    public static IActionResult ToResult(this ControllerBase controller, FileDownload file, bool asAttachment, bool enableRangeProcessing)
    {
        if (file.RedirectUrl is not null)
        {
            // Jamais en cache : l'URL signée expire, la redirection doit être redemandée à l'API
            controller.Response.Headers.CacheControl = "no-store";
            return controller.Redirect(file.RedirectUrl.ToString());
        }

        return controller.File(file.Content!, file.ContentType, asAttachment ? file.FileName : null, enableRangeProcessing);
    }
}