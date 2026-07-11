namespace VintageStoryModManager.Services;

internal static class UploaderNameResolver
{
    internal static string Resolve(string? playerName, string? suffixSource)
    {
        if (!string.IsNullOrWhiteSpace(playerName)) return playerName.Trim();

        if (!string.IsNullOrWhiteSpace(suffixSource))
        {
            var trimmedSpan = suffixSource.AsSpan().Trim();
            if (!trimmedSpan.IsEmpty)
            {
                var suffix = trimmedSpan.Length <= 4
                    ? trimmedSpan.ToString()
                    : trimmedSpan.Slice(trimmedSpan.Length - 4, 4).ToString();

                if (string.IsNullOrWhiteSpace(suffix)) suffix = "0000";

                return $"Anonymous{suffix}";
            }
        }

        return "Anonymous0000";
    }
}
