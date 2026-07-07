using System.Globalization;
using System.Security.Cryptography;

namespace VintageStoryModManager.Services;

internal static class SurprisePaletteGenerator
{
    internal static IReadOnlyDictionary<string, string> GenerateSurprisePalette()
        {
            var defaults = UserConfigurationService.GetDefaultThemePalette(ColorTheme.VintageStory);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in defaults) result[pair.Key] = GenerateRandomColor(pair.Value);

            return result;
        }

    private static string GenerateRandomColor(string baseColor)
        {
            byte alpha = 0xFF;

            if (!string.IsNullOrWhiteSpace(baseColor) && baseColor.Length == 9)
            {
                var alphaComponent = baseColor.AsSpan(1, 2);
                if (byte.TryParse(alphaComponent, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                        out var parsedAlpha)) alpha = parsedAlpha;
            }

            Span<byte> rgb = stackalloc byte[3];
            RandomNumberGenerator.Fill(rgb);

            return $"#{alpha:X2}{rgb[0]:X2}{rgb[1]:X2}{rgb[2]:X2}";
        }
}
