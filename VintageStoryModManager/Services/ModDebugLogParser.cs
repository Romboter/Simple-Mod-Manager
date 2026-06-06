using System.IO;
using System.Text.RegularExpressions;
using VintageStoryModManager;
using VintageStoryModManager.Models;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Services;

internal static class ModDebugLogParser
{
    private const string SummaryKeyPatchModPrefix = "__PATCH_MOD__";

    private const string SummaryKeyLinePrefix = "__PREFIX__";

    private static readonly string[] ExperimentalModDebugLogPrefixes =
        {
            "client-debug",
            "client-main",
            "server-debug",
            "server-main"
        };

    private static readonly string[] ExperimentalModDebugLogExtensions =
        {
            ".txt",
            ".log"
        };

    private static readonly string[] ExperimentalModDebugIgnoredLinePhrases =
        {
            "Check for mod systems in mod ",
            "Loaded assembly ",
            "Instantiate mod systems for ",
            "Starting system:",
            "Mods, sorted by dependency:",
            "External Origins in load order:"
        };

    private static readonly string[] SummarizableLinePrefixes =
        {
            "Patch file",
            "Lang key not found:",
            "[Config lib] Values patched:",
            "Loading sound file, game may stutter",
            "[Config lib] Patched",
            "Block must have a unique code",
            "Failed resolving a blocks blockdrop or smeltedstack",
            "Missing mapping for texture code"
        };

    private static readonly Regex PatchAssetMissingRegex = new(
            @"\bPatch \d+ in (?<mod>[^:\r\n]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static List<ExperimentalModDebugLogLine> CollectExperimentalModDebugLines(string logsDirectory,
            string modId)
        {
            var logLines = new List<ExperimentalModDebugLogLine>();
            foreach (var filePath in GetExperimentalModDebugFilePaths(logsDirectory))
                AppendExperimentalModDebugLines(logLines, filePath, modId);

            return logLines;
        }

    internal static List<ExperimentalModDebugLogLine> CollectInstalledModDebugLines(
            string logsDirectory,
            IReadOnlyList<InstalledModLogIdentifier> modIdentifiers)
        {
            var logLines = new List<ExperimentalModDebugLogLine>();
            if (modIdentifiers.Count == 0) return logLines;

            foreach (var filePath in GetExperimentalModDebugFilePaths(logsDirectory))
                AppendInstalledModDebugLines(logLines, filePath, modIdentifiers);

            return logLines;
        }

    private static List<string> GetExperimentalModDebugFilePaths(string logsDirectory)
        {
            var filePaths = new List<string>();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prefix in ExperimentalModDebugLogPrefixes)
            {
                foreach (var extension in ExperimentalModDebugLogExtensions)
                {
                    var pattern = extension.StartsWith('.') ? $"{prefix}*{extension}" : $"{prefix}*.{extension}";
                    try
                    {
                        foreach (var path in Directory.EnumerateFiles(logsDirectory, pattern,
                                     SearchOption.TopDirectoryOnly))
                            if (processedFiles.Add(path))
                                filePaths.Add(path);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                var directPath = Path.Combine(logsDirectory, prefix);
                if (File.Exists(directPath) && processedFiles.Add(directPath)) filePaths.Add(directPath);
            }

            filePaths.Sort(StringComparer.OrdinalIgnoreCase);
            return filePaths;
        }

    private static void AppendExperimentalModDebugLines(
            List<ExperimentalModDebugLogLine> logLines,
            string filePath,
            string modId)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var matchedLines = new List<(string Line, int LineNumber)>();
                var lineNumber = 0;
                foreach (var line in File.ReadLines(filePath))
                {
                    lineNumber++;
                    if (ShouldIgnoreExperimentalModDebugLine(line)) continue;

                    if (line.IndexOf(modId, StringComparison.OrdinalIgnoreCase) >= 0) matchedLines.Add((line, lineNumber));
                }

                AppendExperimentalModDebugFileSectionWithModId(logLines, filePath, fileName, matchedLines, modId);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

    private static void AppendInstalledModDebugLines(
            List<ExperimentalModDebugLogLine> logLines,
            string filePath,
            IReadOnlyList<InstalledModLogIdentifier> modIdentifiers)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var matchedLines = new List<(string Line, string ModName, int LineNumber)>();
                var lineNumber = 0;
                foreach (var line in File.ReadLines(filePath))
                {
                    lineNumber++;
                    if (ShouldIgnoreExperimentalModDebugLine(line)) continue;

                    foreach (var identifier in modIdentifiers)
                        if (line.IndexOf(identifier.SearchValue, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedLines.Add((line, identifier.DisplayLabel, lineNumber));
                            break;
                        }
                }

                AppendExperimentalModDebugFileSectionWithMods(logLines, filePath, fileName, matchedLines);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

    private static void AppendExperimentalModDebugFileSection(
            List<ExperimentalModDebugLogLine> logLines,
            string fileName,
            List<string> matchedLines)
        {
            if (matchedLines.Count == 0) return;

            var processedLines = SummarizePatchMissingLines(matchedLines);
            if (processedLines.Count == 0) return;

            logLines.Add(ExperimentalModDebugLogLine.FromPlainText($"**{fileName}**"));
            foreach (var line in processedLines) logLines.Add(ExperimentalModDebugLogLine.FromLogEntry(line));
        }

    private static void AppendExperimentalModDebugFileSectionWithModId(
            List<ExperimentalModDebugLogLine> logLines,
            string filePath,
            string fileName,
            List<(string Line, int LineNumber)> matchedLines,
            string modId)
        {
            if (matchedLines.Count == 0) return;

            var processedLines = SummarizePatchMissingLinesWithLineNumbers(matchedLines);
            if (processedLines.Count == 0) return;

            logLines.Add(ExperimentalModDebugLogLine.FromPlainText($"**{fileName}**"));

            foreach (var (line, lineNumber) in processedLines)
                logLines.Add(ExperimentalModDebugLogLine.FromLogEntry(line, modId, filePath, lineNumber));
        }

    private static void AppendExperimentalModDebugFileSectionWithMods(
            List<ExperimentalModDebugLogLine> logLines,
            string filePath,
            string fileName,
            List<(string Line, string ModName, int LineNumber)> matchedLines)
        {
            if (matchedLines.Count == 0) return;

            var processedLines = SummarizePatchMissingLinesWithModAndLineNumbers(matchedLines);
            if (processedLines.Count == 0) return;

            logLines.Add(ExperimentalModDebugLogLine.FromPlainText($"**{fileName}**"));

            foreach (var (line, modName, lineNumber) in processedLines)
                logLines.Add(ExperimentalModDebugLogLine.FromLogEntry(line, modName, filePath, lineNumber));
        }

    private static bool ShouldIgnoreExperimentalModDebugLine(string line)
        {
            foreach (var phrase in ExperimentalModDebugIgnoredLinePhrases)
                if (line.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }

    private static List<string> SummarizePatchMissingLines(List<string> matchedLines)
        {
            if (matchedLines.Count == 0) return matchedLines;

            var summarized = new List<string>(matchedLines.Count);
            var lineSummaries = new Dictionary<string, (int Index, int HiddenCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in matchedLines)
            {
                // Try to match "Patch X in [mod]" pattern
                var patchMatch = PatchAssetMissingRegex.Match(line);
                if (patchMatch.Success)
                {
                    var modId = patchMatch.Groups["mod"].Value;
                    if (!string.IsNullOrEmpty(modId))
                    {
                        var summaryKey = $"{SummaryKeyPatchModPrefix}{modId.Trim()}";
                        AddOrIncrementSummary(summarized, lineSummaries, line, summaryKey);
                        continue;
                    }
                }

                // Try to match summarizable prefixes
                string? matchedPrefix = null;
                foreach (var prefix in SummarizableLinePrefixes)
                    if (line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedPrefix = prefix;
                        break;
                    }

                if (matchedPrefix != null)
                {
                    // Create a summary key based on the prefix
                    var summaryKey = $"{SummaryKeyLinePrefix}{matchedPrefix}";
                    AddOrIncrementSummary(summarized, lineSummaries, line, summaryKey);
                    continue;
                }

                // No pattern matched, add line as-is
                summarized.Add(line);
            }

            // Append hidden count to summarized lines
            foreach (var value in lineSummaries.Values)
            {
                if (value.HiddenCount <= 0) continue;

                var index = value.Index;
                if (index >= 0 && index < summarized.Count)
                    summarized[index] = $"{summarized[index]} ({value.HiddenCount} similar lines hidden...)";
            }

            return summarized;
        }

    private static List<(string Line, int LineNumber)> SummarizePatchMissingLinesWithLineNumbers(
            List<(string Line, int LineNumber)> matchedLines)
        {
            if (matchedLines.Count == 0) return matchedLines;

            var summarized = new List<(string Line, int LineNumber)>(matchedLines.Count);
            var lineSummaries = new Dictionary<string, (int Index, int HiddenCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var (line, lineNumber) in matchedLines)
            {
                // Try to match "Patch X in [mod]" pattern
                var patchMatch = PatchAssetMissingRegex.Match(line);
                if (patchMatch.Success)
                {
                    var modId = patchMatch.Groups["mod"].Value;
                    if (!string.IsNullOrEmpty(modId))
                    {
                        var summaryKey = $"{SummaryKeyPatchModPrefix}{modId.Trim()}";
                        AddOrIncrementSummaryWithLineNumber(summarized, lineSummaries, line, lineNumber, summaryKey);
                        continue;
                    }
                }

                // Try to match summarizable prefixes
                string? matchedPrefix = null;
                foreach (var prefix in SummarizableLinePrefixes)
                    if (line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedPrefix = prefix;
                        break;
                    }

                if (matchedPrefix != null)
                {
                    // Create a summary key based on the prefix
                    var summaryKey = $"{SummaryKeyLinePrefix}{matchedPrefix}";
                    AddOrIncrementSummaryWithLineNumber(summarized, lineSummaries, line, lineNumber, summaryKey);
                    continue;
                }

                // No pattern matched, add line as-is with its line number
                summarized.Add((line, lineNumber));
            }

            // Append hidden count to summarized lines
            foreach (var value in lineSummaries.Values)
            {
                if (value.HiddenCount <= 0) continue;

                var index = value.Index;
                if (index >= 0 && index < summarized.Count)
                {
                    var (line, lineNumber) = summarized[index];
                    summarized[index] = ($"{line} ({value.HiddenCount} similar lines hidden...)", lineNumber);
                }
            }

            return summarized;
        }

    private static void AddOrIncrementSummary(
            List<string> summarized,
            Dictionary<string, (int Index, int HiddenCount)> summaries,
            string line,
            string summaryKey)
        {
            if (summaries.TryGetValue(summaryKey, out var entry))
            {
                summaries[summaryKey] = (entry.Index, entry.HiddenCount + 1);
            }
            else
            {
                summaries[summaryKey] = (summarized.Count, 0);
                summarized.Add(line);
            }
        }

    private static void AddOrIncrementSummaryWithLineNumber(
            List<(string Line, int LineNumber)> summarized,
            Dictionary<string, (int Index, int HiddenCount)> summaries,
            string line,
            int lineNumber,
            string summaryKey)
        {
            if (summaries.TryGetValue(summaryKey, out var entry))
            {
                summaries[summaryKey] = (entry.Index, entry.HiddenCount + 1);
            }
            else
            {
                summaries[summaryKey] = (summarized.Count, 0);
                summarized.Add((line, lineNumber));
            }
        }

    private static List<(string Line, string ModName, int LineNumber)> SummarizePatchMissingLinesWithModAndLineNumbers(
            List<(string Line, string ModName, int LineNumber)> matchedLines)
        {
            if (matchedLines.Count == 0) return matchedLines;

            var summarized = new List<(string Line, string ModName, int LineNumber)>(matchedLines.Count);
            var lineSummaries = new Dictionary<string, (int Index, int HiddenCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var (line, modName, lineNumber) in matchedLines)
            {
                // Try to match "Patch X in [mod]" pattern
                var patchMatch = PatchAssetMissingRegex.Match(line);
                if (patchMatch.Success)
                {
                    var modId = patchMatch.Groups["mod"].Value;
                    if (!string.IsNullOrEmpty(modId))
                    {
                        var summaryKey = $"{SummaryKeyPatchModPrefix}{modId.Trim()}";
                        AddOrIncrementSummaryWithModAndLineNumber(summarized, lineSummaries, line, modName, lineNumber,
                            summaryKey);
                        continue;
                    }
                }

                // Try to match summarizable prefixes
                string? matchedPrefix = null;
                foreach (var prefix in SummarizableLinePrefixes)
                    if (line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedPrefix = prefix;
                        break;
                    }

                if (matchedPrefix != null)
                {
                    // Create a summary key based on the prefix
                    var summaryKey = $"{SummaryKeyLinePrefix}{matchedPrefix}";
                    AddOrIncrementSummaryWithModAndLineNumber(summarized, lineSummaries, line, modName, lineNumber,
                        summaryKey);
                    continue;
                }

                // No pattern matched, add line as-is with its mod name and line number
                summarized.Add((line, modName, lineNumber));
            }

            // Append hidden count to summarized lines
            foreach (var value in lineSummaries.Values)
            {
                if (value.HiddenCount <= 0) continue;

                var index = value.Index;
                if (index >= 0 && index < summarized.Count)
                {
                    var (line, modName, lineNumber) = summarized[index];
                    summarized[index] = ($"{line} ({value.HiddenCount} similar lines hidden...)", modName, lineNumber);
                }
            }

            return summarized;
        }

    private static void AddOrIncrementSummaryWithModAndLineNumber(
            List<(string Line, string ModName, int LineNumber)> summarized,
            Dictionary<string, (int Index, int HiddenCount)> summaries,
            string line,
            string modName,
            int lineNumber,
            string summaryKey)
        {
            if (summaries.TryGetValue(summaryKey, out var entry))
            {
                summaries[summaryKey] = (entry.Index, entry.HiddenCount + 1);
            }
            else
            {
                summaries[summaryKey] = (summarized.Count, 0);
                summarized.Add((line, modName, lineNumber));
            }
        }
}
