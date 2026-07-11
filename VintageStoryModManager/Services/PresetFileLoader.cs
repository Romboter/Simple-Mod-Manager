using System.IO;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class PresetFileLoader
{
    internal static bool TryLoadPresetFromFile(string filePath, string fallbackName, PresetLoadOptions options,
            out ModPreset? preset, out string? errorMessage)
        {
            preset = null;
            errorMessage = null;

            if (!File.Exists(filePath))
            {
                errorMessage = "The selected file could not be found.";
                return false;
            }

            if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                return TryLoadPresetFromPdf(filePath, fallbackName, options, out preset, out errorMessage);

            try
            {
                string json;
                using (var stream = File.OpenRead(filePath))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    json = reader.ReadToEnd();
                }

                if (!PdfModlistSerializer.TryDeserializeFromJson(json, out var data, out errorMessage)) return false;

                var snapshotName = FileNameHelper.GetSnapshotNameFromFilePath(filePath, fallbackName);
                return TryBuildPresetFromSerializable(data!, fallbackName, options, out preset, out errorMessage,
                    snapshotName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

    private static bool TryLoadPresetFromPdf(string filePath, string fallbackName, PresetLoadOptions options,
            out ModPreset? preset, out string? errorMessage)
        {
            preset = null;
            errorMessage = null;

            try
            {
                using var document = PdfDocument.Open(filePath);
                string? json = null;
                string? configJson = null;

                var information = document.Information;
                var hasModlistMetadata = PdfModlistSerializer.TryExtractModlistJsonFromMetadata(
                    information?.Subject,
                    out json,
                    out var modlistMetadataError);
                if (!hasModlistMetadata && modlistMetadataError is not null)
                {
                    errorMessage = modlistMetadataError;
                    return false;
                }

                var hasConfigMetadata = PdfModlistSerializer.TryExtractConfigJsonFromMetadata(
                    information?.Keywords,
                    out configJson,
                    out var configMetadataError);
                if (!hasConfigMetadata && configMetadataError is not null)
                {
                    errorMessage = configMetadataError;
                    return false;
                }

                string? pdfText = null;

                if (!hasModlistMetadata || !hasConfigMetadata)
                {
                    var textBuilder = new StringBuilder();

                    foreach (var page in document.GetPages())
                    {
                        var pageBuilder = new StringBuilder();

                        foreach (var letter in page.Letters)
                        {
                            var value = letter.Value;
                            if (string.IsNullOrEmpty(value)) continue;

                            if (value == "\r") continue;

                            pageBuilder.Append(value);
                        }

                        var pageText = pageBuilder.ToString();
                        if (string.IsNullOrWhiteSpace(pageText)) continue;

                        if (textBuilder.Length > 0) textBuilder.Append('\n');

                        textBuilder.Append(pageText);
                    }

                    pdfText = textBuilder.ToString();
                    if (string.IsNullOrWhiteSpace(pdfText))
                    {
                        errorMessage = "The PDF did not contain any readable text.";
                        return false;
                    }
                }

                if (!hasModlistMetadata)
                    if (!PdfModlistSerializer.TryExtractModlistJson(pdfText!, out json, out var extractionError))
                    {
                        errorMessage = extractionError;
                        return false;
                    }

                if (!hasConfigMetadata)
                    if (!PdfModlistSerializer.TryExtractConfigJson(pdfText!, out configJson, out var configExtractionError))
                    {
                        errorMessage = configExtractionError;
                        return false;
                    }

                var snapshotName = FileNameHelper.GetSnapshotNameFromFilePath(filePath, fallbackName);
                return TryLoadPresetFromJson(json!, fallbackName, options, out preset, out errorMessage, snapshotName,
                    configJson);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errorMessage = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

    internal static bool TryLoadPresetFromJson(
            string json,
            string fallbackName,
            PresetLoadOptions options,
            out ModPreset? preset,
            out string? errorMessage,
            string? sourceName = null,
            string? configJson = null)
        {
            preset = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "The selected file was empty.";
                return false;
            }

            if (!PdfModlistSerializer.TryDeserializeFromJson(json, out var data, out errorMessage)) return false;

            var serializable = data!;

            if (!string.IsNullOrWhiteSpace(configJson))
            {
                if (!PdfModlistSerializer.TryDeserializeConfigListFromJson(configJson, out var configList,
                        out var configError))
                {
                    errorMessage = string.IsNullOrWhiteSpace(configError)
                        ? "The PDF configuration data could not be read."
                        : configError;
                    return false;
                }

                PresetConfigurationSerializer.ApplyConfigListToPreset(serializable, configList);
            }

            return TryBuildPresetFromSerializable(serializable, fallbackName, options, out preset, out errorMessage,
                sourceName);
        }

    private static bool TryBuildPresetFromSerializable(
            SerializablePreset data,
            string fallbackName,
            PresetLoadOptions options,
            out ModPreset? preset,
            out string? errorMessage,
            string? fallbackNameFromSource = null)
        {
            preset = null;
            errorMessage = null;

            var name = !string.IsNullOrWhiteSpace(data.Name)
                ? data.Name!.Trim()
                : !string.IsNullOrWhiteSpace(fallbackNameFromSource)
                    ? fallbackNameFromSource!.Trim()
                    : fallbackName;

            var disabledEntries = new List<string>();
            var seenDisabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (data.DisabledEntries != null)
                foreach (var entry in data.DisabledEntries)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;

                    var trimmed = entry.Trim();
                    if (seenDisabled.Add(trimmed)) disabledEntries.Add(trimmed);
                }

            var presetIndicatesStatus = data.IncludeModStatus
                                        ?? (data.Mods?.Any(entry => entry?.IsActive is not null) ?? false);
            var presetIndicatesVersions = data.IncludeModVersions
                                          ?? (data.Mods?.Any(entry => !string.IsNullOrWhiteSpace(entry?.Version)) ?? false);
            var includeStatus = options.ApplyModStatus && presetIndicatesStatus;
            var includeVersions = options.ApplyModVersions && presetIndicatesVersions;
            var exclusive = options.ForceExclusive;

            var modStates = new List<ModPresetModState>();

            var configurationGroups = new Dictionary<string, List<ModConfigurationSnapshot>>(StringComparer.OrdinalIgnoreCase);
            if (data.Configurations is not null)
                foreach (var configuration in data.Configurations)
                {
                    if (configuration is null || string.IsNullOrWhiteSpace(configuration.ModId)) continue;

                    var trimmedId = configuration.ModId.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedId)) continue;

                    if (!configurationGroups.TryGetValue(trimmedId, out var list))
                    {
                        list = new List<ModConfigurationSnapshot>();
                        configurationGroups[trimmedId] = list;
                    }

                    var fileName = string.IsNullOrWhiteSpace(configuration.FileName)
                        ? null
                        : configuration.FileName.Trim();
                    var relativePath = string.IsNullOrWhiteSpace(configuration.RelativePath)
                        ? null
                        : configuration.RelativePath.Trim();
                    var content = configuration.Content ?? string.Empty;
                    list.Add(new ModConfigurationSnapshot(fileName ?? string.Empty, content, relativePath));
                }

            if (data.Mods != null)
                foreach (var mod in data.Mods)
                {
                    if (mod is null || string.IsNullOrWhiteSpace(mod.ModId)) continue;

                    var modId = mod.ModId.Trim();
                    var version = string.IsNullOrWhiteSpace(mod.Version)
                        ? null
                        : mod.Version!.Trim();
                    var configurationFileName = string.IsNullOrWhiteSpace(mod.ConfigurationFileName)
                        ? null
                        : mod.ConfigurationFileName!.Trim();
                    string? configurationContent = ModConfigurationEncoding.Decode(mod.ConfigurationContent);
                    if (string.IsNullOrWhiteSpace(configurationContent)) configurationContent = null;

                    var mergedConfigurations = new List<ModConfigurationSnapshot>();
                    var seenConfigs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    void AddConfiguration(string? fileName, string? content, string? relativePath = null)
                    {
                        if (string.IsNullOrEmpty(content)) return;

                        var safeName = ModConfigPathHelper.GetSafeConfigFileName(fileName, modId);
                        var normalizedRelativePath = ModConfigPathHelper.NormalizeRelativeConfigPath(relativePath, safeName);
                        var key = $"{safeName}::{normalizedRelativePath}::{content}";
                        if (!seenConfigs.Add(key)) return;

                        mergedConfigurations.Add(new ModConfigurationSnapshot(safeName, content, normalizedRelativePath));
                    }

                    var hasConfigurationList = configurationGroups.TryGetValue(modId, out var extraConfigs);

                    if (!hasConfigurationList)
                        AddConfiguration(configurationFileName, configurationContent);

                    if (hasConfigurationList && extraConfigs is not null)
                        foreach (var config in extraConfigs)
                            AddConfiguration(config.FileName, config.Content, config.RelativePath);

                    if (mergedConfigurations.Count > 0)
                    {
                        configurationFileName ??= mergedConfigurations[0].FileName;
                        configurationContent ??= mergedConfigurations[0].Content;
                    }

                    modStates.Add(new ModPresetModState(modId, version, mod.IsActive, configurationFileName,
                        configurationContent, mergedConfigurations.Count > 0 ? mergedConfigurations : null));
                }

            preset = new ModPreset(name, disabledEntries, modStates, includeStatus, includeVersions, exclusive);
            return true;
        }

}
