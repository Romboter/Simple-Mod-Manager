namespace VintageStoryModManager.Services;

internal static class ModDatabaseSlugParser
{
    internal static string? TryExtractModSlug(string? modDatabasePageUrl)
    {
        if (string.IsNullOrWhiteSpace(modDatabasePageUrl)) return null;

        if (Uri.TryCreate(modDatabasePageUrl, UriKind.Absolute, out var uri))
        {
            var fromUri = ExtractSlugFromPath(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(fromUri)) return fromUri;
        }

        return ExtractSlugFromPath(modDatabasePageUrl);
    }

    private static string? ExtractSlugFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var trimmed = path.Trim();

        var fragmentIndex = trimmed.IndexOf('#');
        if (fragmentIndex >= 0) trimmed = trimmed[..fragmentIndex];

        var queryIndex = trimmed.IndexOf('?');
        if (queryIndex >= 0) trimmed = trimmed[..queryIndex];

        trimmed = trimmed.Trim('/');

        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash >= 0) trimmed = trimmed[(lastSlash + 1)..];

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
