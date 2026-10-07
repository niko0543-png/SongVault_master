using System.Text.Json;
using System.Text.Json.Serialization;

namespace SongVault.Api.Tests;

/// <summary>Mêmes options JSON que l'API : camelCase et enums en texte ("Audio", "Demo").</summary>
internal static class TestJson
{
    public static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}