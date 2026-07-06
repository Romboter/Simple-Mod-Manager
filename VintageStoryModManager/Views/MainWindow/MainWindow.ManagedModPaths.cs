#nullable enable

using System;
using System.IO;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private bool TryGetManagedModPath(ModListItemViewModel mod, out string fullPath, out string? errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = null;

        if (_dataDirectory is null)
        {
            errorMessage =
                "The VintagestoryData folder is not available. Please verify it from File > Set Data Folder.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(mod.SourcePath))
        {
            errorMessage = "This mod does not have a known source path and cannot be deleted automatically.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(mod.SourcePath);
        }
        catch (Exception)
        {
            errorMessage = "The mod path is invalid and cannot be deleted automatically.";
            return false;
        }

        if (!ManagedModPathHelper.IsPathWithinManagedMods(_dataDirectory, fullPath))
        {
            errorMessage =
                $"This mod is located outside of the Mods folder and cannot be deleted automatically.{Environment.NewLine}{Environment.NewLine}Location:{Environment.NewLine}{fullPath}";
            return false;
        }

        if (!ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDirectory, fullPath, out errorMessage))
            return false;

        return true;
    }
}
