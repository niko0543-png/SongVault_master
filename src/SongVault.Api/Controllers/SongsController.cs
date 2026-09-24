using Microsoft.AspNetCore.Mvc;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/songs")]
public sealed class SongsController : ControllerBase
{
    // TEMPORAIRE : données codées en dur, remplacées mercredi.
    [HttpGet]
    public IActionResult List() => Ok(new[]
    {
        new { Id = 1, Title = "Nocturne en ré mineur", Artist = "SongVault Band" },
        new { Id = 2, Title = "Lumière froide", Artist = "SongVault Band" },
    });
}
