using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class DependencyRepairHelperTests
{
    private static ModListItemViewModel CreateInstalledDependency(string? version)
    {
        var entry = new ModEntry
        {
            ModId = "depmod",
            Name = "depmod",
            Version = version
        };

        return new ModListItemViewModel(
            entry,
            isActive: false,
            location: "Mods",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(false, null)),
            initializeUserReportState: false);
    }

    [Fact]
    public void IsDependencyMissing_ListedAsMissing_ReturnsTrueRegardlessOfInstalledState()
    {
        var installedDependency = CreateInstalledDependency("2.0.0");

        var result = DependencyRepairHelper.IsDependencyMissing(true, installedDependency, "1.0.0");

        Assert.True(result);
    }

    [Fact]
    public void IsDependencyMissing_InstalledDependencyNull_ReturnsTrue()
    {
        var result = DependencyRepairHelper.IsDependencyMissing(false, null, "1.0.0");

        Assert.True(result);
    }

    [Fact]
    public void IsDependencyMissing_InstalledSatisfiesMinimumVersion_ReturnsFalse()
    {
        var installedDependency = CreateInstalledDependency("2.0.0");

        var result = DependencyRepairHelper.IsDependencyMissing(false, installedDependency, "1.0.0");

        Assert.False(result);
    }

    [Fact]
    public void IsDependencyMissing_InstalledVersionTooOld_ReturnsTrue()
    {
        var installedDependency = CreateInstalledDependency("1.0.0");

        var result = DependencyRepairHelper.IsDependencyMissing(false, installedDependency, "2.0.0");

        Assert.True(result);
    }
}
