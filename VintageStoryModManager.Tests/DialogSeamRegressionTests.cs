using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace VintageStoryModManager.Tests;

/// <summary>
///     Guards the dialog seam: new direct <c>WpfMessageBox.Show</c> / <c>ModManagerMessageBox.Show</c>
///     calls in <c>MainWindow.*.cs</c> partials should not appear outside a frozen allowlist of
///     already-known, intentionally-deferred exclusions. New dialogs must go through
///     <c>IConfirmationService</c> instead (see docs/superpowers/specs/2026-07-07-dialog-seam-design.md).
/// </summary>
public sealed class DialogSeamRegressionTests
{
    /// <summary>
    ///     Frozen allowlist of direct-dialog-call counts per file, as of the dialog-seam upgrade.
    ///     Any file not listed here must have zero direct calls. If a legitimate new exclusion is
    ///     needed, update this list deliberately (not silently) alongside the change that adds it.
    /// </summary>
    private static readonly Dictionary<string, int> AllowedDirectDialogCallCounts = new()
    {
        ["MainWindow.PathInitialization.cs"] = 5,
        ["MainWindow.ViewModel.cs"] = 1,
        ["MainWindow.CloudManagement.cs"] = 1,
    };

    private static readonly Regex DirectDialogCallPattern =
        new(@"\b(WpfMessageBox|ModManagerMessageBox)\.Show\b", RegexOptions.Compiled);

    [Fact]
    public void MainWindowPartials_DoNotExceedFrozenDirectDialogCallAllowlist()
    {
        var repoRoot = FindRepoRoot();
        var mainWindowDir = Path.Combine(repoRoot, "VintageStoryModManager", "Views", "MainWindow");

        Assert.True(Directory.Exists(mainWindowDir), $"Expected MainWindow partial directory not found: {mainWindowDir}");

        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(mainWindowDir, "MainWindow.*.cs", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);
            var actualCount = DirectDialogCallPattern.Matches(content).Count;

            var allowedCount = AllowedDirectDialogCallCounts.GetValueOrDefault(fileName, 0);

            if (actualCount > allowedCount)
            {
                offenders.Add($"{fileName}: found {actualCount} direct dialog call(s), allowlist permits {allowedCount}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "New direct WpfMessageBox.Show/ModManagerMessageBox.Show calls were found in MainWindow.*.cs " +
            "partials outside the frozen allowlist. New dialogs must go through IConfirmationService " +
            "(see docs/superpowers/specs/2026-07-07-dialog-seam-design.md). Offending file(s):\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    ///     Walks up from the test assembly's output directory until it finds the directory
    ///     containing ImprovedModMenu.sln, so the test works regardless of build configuration
    ///     or CI checkout layout.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ImprovedModMenu.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate ImprovedModMenu.sln by walking up from {AppContext.BaseDirectory}");
    }
}
