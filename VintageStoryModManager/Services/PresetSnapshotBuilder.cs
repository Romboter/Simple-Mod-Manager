using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class PresetSnapshotBuilder
{
    internal static SerializablePreset BuildModlistPreset(
        IReadOnlyList<ModPresetModState> states,
        string name,
        string? description,
        string? version,
        string? uploader,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations = null,
        string? gameVersion = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var serializable = BuildSerializablePreset(
            states,
            name.Trim(),
            true,
            true,
            includedConfigurations,
            gameVersion);

        serializable.Description =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        serializable.Version =
            string.IsNullOrWhiteSpace(version)
                ? null
                : version.Trim();

        serializable.Uploader =
            string.IsNullOrWhiteSpace(uploader)
                ? null
                : uploader.Trim();

        return serializable;
    }

    internal static SerializablePreset BuildSerializablePreset(
            IReadOnlyList<ModPresetModState> states,
            string entryName,
            bool includeModVersions,
            bool exclusive,
            IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations = null,
            string? gameVersion = null)
        {
            ArgumentNullException.ThrowIfNull(states);

            var mods = new List<SerializablePresetModState>(states.Count);
            foreach (var state in states)
            {
                if (state is null) continue;

                var trimmedId = string.IsNullOrWhiteSpace(state.ModId) ? state.ModId : state.ModId.Trim();
                var serializableState = new SerializablePresetModState
                {
                    ModId = trimmedId,
                    Version = includeModVersions && !string.IsNullOrWhiteSpace(state.Version)
                        ? state.Version!.Trim()
                        : null,
                    IsActive = state.IsActive
                };

                if (!string.IsNullOrWhiteSpace(trimmedId))
                {
                    var normalizedId = trimmedId!;

                    if (includedConfigurations != null
                        && includedConfigurations.TryGetValue(normalizedId, out var snapshots)
                        && snapshots?.Count > 0)
                    {
                        var snapshot = snapshots[0];
                        serializableState.ConfigurationFileName = snapshot.FileName;
                        serializableState.ConfigurationContent =
                            ModConfigurationEncoding.Encode(snapshot.Content);
                    }
                    else if (state.ConfigurationContent is not null)
                    {
                        serializableState.ConfigurationFileName =
                            ModConfigPathHelper.GetSafeConfigFileName(state.ConfigurationFileName, normalizedId);
                        serializableState.ConfigurationContent = ModConfigurationEncoding.Encode(
                            ModConfigurationEncoding.Decode(state.ConfigurationContent));
                    }
                }

                mods.Add(serializableState);
            }

            var configList = PresetConfigurationSerializer.BuildSerializableConfigList(includedConfigurations);

            return new SerializablePreset
            {
                Name = entryName,
                IncludeModStatus = true,
                IncludeModVersions = includeModVersions ? true : null,
                Exclusive = exclusive ? true : null,
                Mods = mods,
                GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? null : gameVersion.Trim(),
                Configurations = configList?.Configurations
            };
        }

}
