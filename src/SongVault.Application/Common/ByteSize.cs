using System.Globalization;

namespace SongVault.Application.Common;

/// <summary>Tailles lisibles en français, comme formatFileSize côté front : 1,5 Go, 300 Mo.</summary>
public static class ByteSize
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] Units = ["o", "Ko", "Mo", "Go", "To"];

    public static string Format(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value.ToString("0.#", French)} {Units[unit]}";
    }
}