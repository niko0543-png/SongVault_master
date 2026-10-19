namespace SongVault.Domain.Files;

/// <summary>Une piste du mélangeur (#23) : un fichier audio de la version et le nom affiché (« Voix », « Batterie »).</summary>
public sealed record StemChoice(Guid FileId, string Name);