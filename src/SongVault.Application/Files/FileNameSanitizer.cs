namespace SongVault.Application.Files;

/// <summary>Nom d'origine nettoyé, utilisé UNIQUEMENT pour l'affichage et le téléchargement.</summary>
public static class FileNameSanitizer
{
    public static string Sanitize(string fileName)
    {
        // Sous Linux, Path.GetFileName ne coupe pas sur "\" : on normalise d'abord
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        var cleaned = new string([.. name.Where(c => !char.IsControl(c) && c is not '"' and not '/')]).Trim();
        return string.IsNullOrEmpty(cleaned) ? "fichier" : cleaned;
    }
}