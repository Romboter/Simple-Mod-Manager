using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using Colors = QuestPDF.Helpers.Colors;

namespace VintageStoryModManager.Services;

internal static class InstalledModsPdfGenerator
{
    private static bool _isQuestPdfLicenseInitialized;

    internal static string BuildCloudModlistName()
        {
            return $"Modlist {DateTime.Now:yyyy-MM-dd HH:mm}";
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

    internal static void GenerateInstalledModsPdf(
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
}
