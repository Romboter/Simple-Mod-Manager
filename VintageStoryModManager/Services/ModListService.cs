using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

/// <summary>
/// Service responsible for building, saving, and loading mod presets and modlists.
/// Handles serialization, file I/O, and PDF generation.
/// </summary>
public sealed class ModListService
{
    private static bool _isQuestPdfLicenseInitialized;

    /// <summary>
    /// Builds a serializable preset from the current mod states.
    /// </summary>
    /// <param name="entryName">The name of the preset</param>
    /// <param name="modStates">Collection of current mod states</param>
    /// <param name="includeModVersions">Whether to include mod versions in the preset</param>
    /// <param name="exclusive">Whether this is an exclusive preset (deletes mods not in the list)</param>
    /// <param name="includedConfigurations">Optional configuration snapshots to include</param>
    /// <param name="gameVersion">Optional game version to associate with the preset</param>
    /// <returns>A serializable preset</returns>
    public SerializablePreset BuildSerializablePreset(
        string entryName,
        IReadOnlyList<ModPresetModState> modStates,
        bool includeModVersions,
        bool exclusive,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations = null,
        string? gameVersion = null)
    {
        if (modStates is null) throw new ArgumentNullException(nameof(modStates));

        var mods = new List<SerializablePresetModState>(modStates.Count);
        foreach (var state in modStates)
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
                        GetSafeConfigFileName(state.ConfigurationFileName, normalizedId);
                    serializableState.ConfigurationContent = ModConfigurationEncoding.Encode(
                        ModConfigurationEncoding.Decode(state.ConfigurationContent));
                }
            }

            mods.Add(serializableState);
        }

        var configList = BuildSerializableConfigList(includedConfigurations);

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

    /// <summary>
    /// Saves a preset to a JSON file.
    /// </summary>
    /// <param name="filePath">The destination file path</param>
    /// <param name="preset">The preset to save</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool SavePresetToFile(string filePath, SerializablePreset preset)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        if (preset is null)
            throw new ArgumentNullException(nameof(preset));

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(preset, options);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                      or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Loads a preset from a JSON file.
    /// </summary>
    /// <param name="filePath">The source file path</param>
    /// <param name="preset">The loaded preset if successful</param>
    /// <param name="errorMessage">Error message if loading failed</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool LoadPresetFromFile(string filePath, out SerializablePreset? preset, out string? errorMessage)
    {
        preset = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "File path is required.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = "The file does not exist.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return PdfModlistSerializer.TryDeserializeFromJson(json, out preset, out errorMessage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Builds a modlist JSON string from the current state.
    /// </summary>
    /// <param name="modlistName">The name of the modlist</param>
    /// <param name="modStates">Collection of current mod states</param>
    /// <param name="description">Optional description</param>
    /// <param name="version">Optional version</param>
    /// <param name="uploader">The uploader name</param>
    /// <param name="includedConfigurations">Optional configuration snapshots</param>
    /// <param name="gameVersion">Optional game version</param>
    /// <param name="json">The generated JSON if successful</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool TryBuildModlistJson(
        string modlistName,
        IReadOnlyList<ModPresetModState> modStates,
        string? description,
        string? version,
        string uploader,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
        string? gameVersion,
        out string json)
    {
        json = string.Empty;

        var trimmedName = string.IsNullOrWhiteSpace(modlistName) ? null : modlistName.Trim();
        if (string.IsNullOrEmpty(trimmedName) || modStates is null) return false;

        var serializable = BuildSerializablePreset(trimmedName, modStates, true, true, includedConfigurations, gameVersion);
        serializable.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        serializable.Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        serializable.Uploader = string.IsNullOrWhiteSpace(uploader) ? null : uploader.Trim();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        json = JsonSerializer.Serialize(serializable, options);
        return true;
    }

    /// <summary>
    /// Generates a PDF file from a modlist.
    /// </summary>
    /// <param name="filePath">The destination PDF file path</param>
    /// <param name="listName">The modlist name</param>
    /// <param name="modlistVersion">Optional modlist version</param>
    /// <param name="description">Optional description</param>
    /// <param name="uploaderName">The uploader name</param>
    /// <param name="gameVersion">Optional game version</param>
    /// <param name="mods">Collection of mods to include</param>
    /// <param name="includedConfigurations">Optional configuration snapshots</param>
    /// <param name="modStates">Collection of mod states for building the serializable preset</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool GeneratePdf(
        string filePath,
        string listName,
        string? modlistVersion,
        string? description,
        string uploaderName,
        string? gameVersion,
        IReadOnlyList<ModListItemViewModel> mods,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
        IReadOnlyList<ModPresetModState> modStates)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        if (mods is null || mods.Count == 0)
            return false;

        try
        {
            EnsureQuestPdfLicense();

            var serializable = BuildSerializablePreset(
                listName ?? "Installed Mods",
                modStates,
                true,
                true,
                includedConfigurations,
                gameVersion);

            serializable.Description = description;
            serializable.Version = modlistVersion;
            serializable.Uploader = uploaderName;

            var configList = BuildSerializableConfigList(includedConfigurations);

            GenerateInstalledModsPdf(
                filePath,
                listName ?? "Installed Mods",
                modlistVersion,
                description,
                uploaderName,
                gameVersion,
                mods,
                serializable,
                configList);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SerializableConfigList? BuildSerializableConfigList(
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

    private static void EnsureQuestPdfLicense()
    {
        if (_isQuestPdfLicenseInitialized) return;

        Settings.License = LicenseType.Community;
        _isQuestPdfLicenseInitialized = true;
    }

    private static string[] GetLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<string>();

        var normalized = content.ReplaceLineEndings("\n");
        return normalized.Split('\n');
    }

    private static void GenerateInstalledModsPdf(
        string filePath,
        string listName,
        string? modlistVersion,
        string? description,
        string uploaderName,
        string? gameVersion,
        IReadOnlyList<ModListItemViewModel> mods,
        SerializablePreset serializable,
        SerializableConfigList? configList)
    {
        EnsureQuestPdfLicense();

        var normalizedListName = string.IsNullOrWhiteSpace(listName) ? "Installed Mods" : listName.Trim();
        var normalizedVersion = string.IsNullOrWhiteSpace(modlistVersion) ? null : modlistVersion.Trim();
        var normalizedDescription = description?.Trim() ?? string.Empty;
        var resolvedGameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "Unknown" : gameVersion.Trim();
        var encodedModlist = PdfModlistSerializer.SerializeToBase64(serializable);
        var encodedConfigList =
            configList is null ? null : PdfModlistSerializer.SerializeConfigListToBase64(configList);
        var modlistMetadataValue = PdfModlistSerializer.CreateModlistMetadataValue(encodedModlist);
        var configMetadataValue = PdfModlistSerializer.CreateConfigMetadataValue(encodedConfigList);

        var metadata = new DocumentMetadata
        {
            Title = normalizedListName,
            Author = uploaderName,
            Subject = modlistMetadataValue,
            Keywords = configMetadataValue,
            Creator = "Simple VS Manager",
            Producer = "Simple VS Manager"
        };

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(12));

                page.Content().Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Text(normalizedListName).FontSize(32).Bold();
                    if (!string.IsNullOrEmpty(normalizedVersion))
                        column.Item().Text($"Version: {normalizedVersion}")
                            .FontSize(12)
                            .Italic();
                    column.Item().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(10));
                        text.Span("Generated with Simple VS Manager.");
                        text.EmptyLine();
                        text.Span("Download the app from the ");
                        text.Hyperlink("Vintage Story ModDB", "https://mods.vintagestory.at/simplevsmanager")
                            .FontColor(Colors.Blue.Medium);
                        text.Span(" or ");
                        text.Hyperlink("Github", "https://github.com/Interzoneism/Simple-Mod-Manager")
                            .FontColor(Colors.Blue.Medium);
                        text.Span(" to easily load this pdf as a modlist!");
                    });
                    column.Item().Text($"Made for VS version {resolvedGameVersion}").FontSize(14);
                    column.Item().Text($"Modlist by {uploaderName}").FontSize(14);
                    column.Item().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(10));
                        text.DefaultTextStyle(style => style.Italic());
                        if (string.IsNullOrEmpty(normalizedDescription)) return;

                        var descriptionLines = GetLines(normalizedDescription);

                        for (var index = 0; index < descriptionLines.Length; index++)
                            text.Line(descriptionLines[index]);
                    });

                    column.Item().Text("Mods in this list:").FontSize(12).Bold();
                    column.Item().Column(modColumn =>
                    {
                        modColumn.Spacing(0);

                        foreach (var mod in mods)
                        {
                            if (mod is null) continue;

                            var title = string.IsNullOrWhiteSpace(mod.DisplayName)
                                ? string.IsNullOrWhiteSpace(mod.ModId) ? "Unknown Mod" : mod.ModId.Trim()
                                : mod.DisplayName.Trim();

                            var version = string.IsNullOrWhiteSpace(mod.Version) ? string.Empty : mod.Version.Trim();
                            var modLine = string.IsNullOrEmpty(version) ? title : $"{title} {version}";
                            var modDatabaseUrl = string.IsNullOrWhiteSpace(mod.ModDatabasePageUrl)
                                ? null
                                : mod.ModDatabasePageUrl!.Trim();

                            modColumn.Item().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(10));

                                if (!string.IsNullOrEmpty(modDatabaseUrl))
                                    text.Hyperlink(modLine, modDatabaseUrl)
                                        .FontColor(Colors.Blue.Medium);
                                else
                                    text.Span(modLine);
                            });
                        }
                    });
                });
            });
        }).WithMetadata(metadata).GeneratePdf(filePath);
    }

    private static string GetSafeConfigFileName(string? candidate, string? modId)
    {
        var sanitizedModId = SanitizeForFileName(string.IsNullOrWhiteSpace(modId) ? "modconfig" : modId!.Trim()).Trim();
        if (string.IsNullOrWhiteSpace(sanitizedModId)) sanitizedModId = "modconfig";

        var trimmedCandidate = string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim();
        var extension = ResolveConfigExtension(trimmedCandidate);
        string baseName;

        if (string.IsNullOrWhiteSpace(trimmedCandidate))
        {
            baseName = sanitizedModId;
        }
        else
        {
            var candidateFileName = Path.GetFileName(trimmedCandidate);
            var candidateBase = string.IsNullOrWhiteSpace(candidateFileName)
                ? null
                : Path.GetFileNameWithoutExtension(candidateFileName);
            baseName = string.IsNullOrWhiteSpace(candidateBase) ? sanitizedModId : candidateBase!;
        }

        var sanitizedBase = SanitizeForFileName(baseName).Trim();
        if (string.IsNullOrWhiteSpace(sanitizedBase)) sanitizedBase = sanitizedModId;

        return sanitizedBase + extension;
    }

    private static string ResolveConfigExtension(string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var extension = Path.GetExtension(candidate);
            if (IsSupportedConfigExtension(extension)) return extension;
        }

        return ".json";
    }

    private static bool IsSupportedConfigExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;

        var supportedExtensions = new[] { ".json", ".txt", ".xml", ".cfg", ".config" };

        foreach (var supported in supportedExtensions)
            if (extension.Equals(supported, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    private static string SanitizeForFileName(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value) builder.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch);

        return builder.ToString();
    }
}
