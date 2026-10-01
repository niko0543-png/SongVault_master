using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Files;
using SongVault.Application.Files;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/files/policy")]
public sealed class FilePolicyController : ControllerBase
{
    [HttpGet]
    public ActionResult<FilePolicyResponse> Get()
        => Ok(new FilePolicyResponse([.. FileTypePolicy.AllowedExtensions.Order()], FileTypePolicy.MaxFileSizeBytes));
}