using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Files;
using SongVault.Application.Files;

namespace SongVault.Api.Controllers;

/// <summary>
/// Politique de fichiers : extensions autorisées et taille maximale.
/// </summary>
[ApiController]
[Route("api/files/policy")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class FilePolicyController : ControllerBase
{
    /// <summary>
    /// Obtient la liste des extensions de fichiers autorisées et la taille maximale.
    /// </summary>
    /// <remarks>
    /// Cette information doit être affichée au utilisateur avant le téléchargement d'un fichier.
    /// </remarks>
    /// <response code="200">Politique de fichiers.</response>
    [HttpGet]
    [ProducesResponseType<FilePolicyResponse>(StatusCodes.Status200OK)]
    public ActionResult<FilePolicyResponse> Get()
        => Ok(new FilePolicyResponse([.. FileTypePolicy.AllowedExtensions.Order()], FileTypePolicy.MaxFileSizeBytes));
}