using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class PresetConfigurationSerializer
{
    internal static SerializableConfigList? BuildSerializableConfigList(
            IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations)
        {
            if (includedConfigurations is null || includedConfigurations.Count == 0) return null;

            var configurations = new List<SerializableModConfiguration>();

            foreach (var pair in includedConfigurations)
            {
                if (pair.Key is null || pair.Value is null || pair.Value.Count == 0) continue;

                var trimmedId = pair.Key.Trim();
                if (string.IsNullOrWhiteSpace(trimmedId)) continue;

                foreach (var snapshot in pair.Value)
                {
                    if (snapshot is null) continue;

                    var fileName = string.IsNullOrWhiteSpace(snapshot.FileName)
                        ? null
                        : snapshot.FileName.Trim();

                    var content = snapshot.Content ?? string.Empty;

                    configurations.Add(new SerializableModConfiguration
                    {
                        ModId = trimmedId,
                        FileName = fileName,
                        RelativePath = snapshot.RelativePath,
                        Content = content
                    });
                }
            }

            if (configurations.Count == 0) return null;

            configurations.Sort((left, right) =>
            {
                var modComparison = string.Compare(left?.ModId, right?.ModId, StringComparison.OrdinalIgnoreCase);
                if (modComparison != 0) return modComparison;

                return string.Compare(left?.FileName, right?.FileName, StringComparison.OrdinalIgnoreCase);
            });

            return new SerializableConfigList
            {
                Configurations = configurations
            };
        }

    internal static void ApplyConfigListToPreset(SerializablePreset preset, SerializableConfigList? configList)
        {
            if (preset is null || configList?.Configurations is null || configList.Configurations.Count == 0) return;

            preset.Configurations ??= new List<SerializableModConfiguration>();

            var existingKeys = new HashSet<string>(preset.Configurations.Select(BuildConfigKey),
                StringComparer.OrdinalIgnoreCase);

            foreach (var configuration in configList.Configurations)
            {
                if (configuration is null || string.IsNullOrWhiteSpace(configuration.ModId) ||
                    configuration.Content is null) continue;

                var key = BuildConfigKey(configuration);
                if (!existingKeys.Add(key)) continue;

                preset.Configurations.Add(configuration);
            }
        }

    private static string BuildConfigKey(SerializableModConfiguration configuration)
        {
            var modId = string.IsNullOrWhiteSpace(configuration.ModId) ? string.Empty : configuration.ModId.Trim();
            var fileName = string.IsNullOrWhiteSpace(configuration.FileName)
                ? string.Empty
                : configuration.FileName.Trim();
            var relativePath = string.IsNullOrWhiteSpace(configuration.RelativePath)
                ? string.Empty
                : configuration.RelativePath.Trim();
            var content = configuration.Content ?? string.Empty;

            return $"{modId}::{relativePath}::{fileName}::{content}";
        }
}
