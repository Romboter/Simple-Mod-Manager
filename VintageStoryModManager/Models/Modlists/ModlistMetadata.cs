namespace VintageStoryModManager;

internal sealed class ModlistMetadata
{
    public static readonly ModlistMetadata Empty =
        new(null, null, null, null, Array.Empty<string>(), null);

    public ModlistMetadata(
        string? name,
        string? description,
        string? version,
        string? uploader,
        IReadOnlyList<string> mods,
        string? gameVersion)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        Uploader = string.IsNullOrWhiteSpace(uploader) ? null : uploader.Trim();
        Mods = mods ?? Array.Empty<string>();
        GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? null : gameVersion.Trim();
    }

    public string? Name { get; }

    public string? Description { get; }

    public string? Version { get; }

    public string? Uploader { get; }

    public IReadOnlyList<string> Mods { get; }

    public string? GameVersion { get; }
}
