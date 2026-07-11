# Phase 1: Test Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `VintageStoryModManager.Tests` (xUnit) to `ImprovedModMenu.sln` and characterize the services extracted during the service-extraction phase, so future slices are gated by `dotnet test` instead of manual smoke tests alone.

**Architecture:** One test project referencing the app project via `InternalsVisibleTo` (most services are `internal`). Characterization tests lock in *current* behavior — assert what the code does today, never what it "should" do. Filesystem-touching tests use per-test temp directories only.

**Tech Stack:** .NET 8 (`net8.0-windows`), xUnit 2.x, no mocking framework.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-06-mvvm-area-slice-pipeline-design.md` (Phase 1 section).
- Release build of the full solution must stay at zero errors, zero warnings.
- **Commit steps require the user's explicit approval first** (standing project rule). When executing inline, complete all tasks, present results, then commit task-by-task after approval.
- Never stage `.claude/` or `CLAUDE.md`.
- Tests never touch real user data or `%APPDATA%` — temp directories only (`Directory.CreateTempSubdirectory`).
- Characterization only: no production code changes except the one-line `InternalsVisibleTo` in Task 1.
- **Deliberately out of scope (do not "helpfully" add):** `FolderOpeningHelper` (message boxes + shell launch), `ModUpdateOperationHelper` (takes concrete `ModUpdateService`; not fakeable without production change), `DataFolderBackupCoordinator` (thin passthrough over `DataBackupService`), `ModReleaseSelectionHelper` (depends on `ModListItemViewModel.LatestRelease`/`LatestCompatibleRelease` whose setup path is unverified, plus a dialog branch), `ModGridSelectionService.HandleModRowSelection` (reads static `Keyboard.Modifiers`; needs STA/input infrastructure).
- If `ModListItemViewModel` construction fails at test runtime (e.g. demands WPF `Application` state), **stop and report** — do not improvise workarounds.

---

### Task 1: Test project scaffold

**Files:**
- Create: `VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj`
- Create: `VintageStoryModManager.Tests/ServerCommandBuilderTests.cs`
- Modify: `VintageStoryModManager/VintageStoryModManager.csproj` (add `InternalsVisibleTo` ItemGroup)
- Modify: `ImprovedModMenu.sln` (via `dotnet sln add`)

**Interfaces:**
- Consumes: `ServerCommandBuilder.TryBuildInstallCommand(string?, string?)` → `string?`; `ServerCommandBuilder.CanCopyInstallCommand(bool, string?, string?)` → `bool`; `PathRelationshipHelper.IsPathUnderDirectory(string, string?)` → `bool` (internal — proves `InternalsVisibleTo`).
- Produces: a runnable test project all later tasks add files to.

- [ ] **Step 1: Create the test project file**

`VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0-windows</TargetFramework>
		<RuntimeIdentifier>win-x64</RuntimeIdentifier>
		<UseWPF>true</UseWPF>
		<UseWindowsForms>true</UseWindowsForms>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<EnableWindowsTargeting>true</EnableWindowsTargeting>
		<IsPackable>false</IsPackable>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
		<PackageReference Include="xunit" Version="2.9.2" />
		<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\VintageStoryModManager\VintageStoryModManager.csproj" />
	</ItemGroup>
</Project>
```

(`UseWPF`/`UseWindowsForms`/`RuntimeIdentifier` mirror the app project so the reference resolves cleanly; tabs for indentation to match the app csproj.)

- [ ] **Step 2: Grant internals access from the app project**

In `VintageStoryModManager/VintageStoryModManager.csproj`, add after the existing `PackageReference` ItemGroup:

```xml
	<ItemGroup>
		<InternalsVisibleTo Include="VintageStoryModManager.Tests" />
	</ItemGroup>
```

- [ ] **Step 3: Add project to solution**

Run: `dotnet sln ImprovedModMenu.sln add VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 4: Write the first test file (public API + one internal-access probe)**

`VintageStoryModManager.Tests/ServerCommandBuilderTests.cs`:

```csharp
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ServerCommandBuilderTests
{
    [Theory]
    [InlineData("primitivesurvival", "3.8.6", "/moddb install primitivesurvival@3.8.6")]
    [InlineData(" spaced ", " 1.0 ", "/moddb install spaced@1.0")]
    public void TryBuildInstallCommand_ValidInputs_BuildsCommand(string modId, string version, string expected)
    {
        Assert.Equal(expected, ServerCommandBuilder.TryBuildInstallCommand(modId, version));
    }

    [Theory]
    [InlineData(null, "1.0")]
    [InlineData("", "1.0")]
    [InlineData("   ", "1.0")]
    [InlineData("mod", null)]
    [InlineData("mod", "")]
    [InlineData("mod", "   ")]
    public void TryBuildInstallCommand_MissingParts_ReturnsNull(string? modId, string? version)
    {
        Assert.Null(ServerCommandBuilder.TryBuildInstallCommand(modId, version));
    }

    [Fact]
    public void CanCopyInstallCommand_RequiresServerOptionsAndValidCommand()
    {
        Assert.True(ServerCommandBuilder.CanCopyInstallCommand(true, "mod", "1.0"));
        Assert.False(ServerCommandBuilder.CanCopyInstallCommand(false, "mod", "1.0"));
        Assert.False(ServerCommandBuilder.CanCopyInstallCommand(true, null, "1.0"));
    }

    [Fact]
    public void InternalsAreVisibleToTestAssembly()
    {
        // Probes InternalsVisibleTo wiring; PathRelationshipHelper is internal.
        Assert.False(PathRelationshipHelper.IsPathUnderDirectory("", null));
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj -c Release`
Expected: PASS, 10 test cases (2 + 6 theory cases, 2 facts), 0 failed.

- [ ] **Step 6: Verify the full solution still builds clean**

Run: `dotnet build ImprovedModMenu.sln -c Release`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 7: Commit (after user approval)**

```bash
git add VintageStoryModManager.Tests/ VintageStoryModManager/VintageStoryModManager.csproj ImprovedModMenu.sln
git commit -m "test: add VintageStoryModManager.Tests project with first characterization tests

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Pure helper characterization — PathRelationshipHelper + FileNameHelper

**Files:**
- Create: `VintageStoryModManager.Tests/PathRelationshipHelperTests.cs`
- Create: `VintageStoryModManager.Tests/FileNameHelperTests.cs`

**Interfaces:**
- Consumes: `PathRelationshipHelper.IsPathUnderDirectory(string, string?)`, `.IsSameDirectory(string?, string?)`, `.IsPathWithinDirectory(string, string)` — all `internal static bool`. `FileNameHelper.SanitizeFileName(string?, string)`, `.EnsureUniqueFilePath(string)`, `.EnsureUniqueDirectoryPath(string)` — `internal static string`.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write PathRelationshipHelper tests**

`VintageStoryModManager.Tests/PathRelationshipHelperTests.cs`:

```csharp
using VintageStoryModManager.Helpers;
using Xunit;

namespace VintageStoryModManager.Tests;

public class PathRelationshipHelperTests
{
    [Theory]
    [InlineData(@"C:\data\Mods\mod.zip", @"C:\data\Mods", true)]
    [InlineData(@"C:\data\Mods", @"C:\data\Mods", true)] // the directory itself counts
    [InlineData(@"C:\data\Mods\", @"C:\data\Mods", true)]
    [InlineData(@"C:\data\ModsOther\mod.zip", @"C:\data\Mods", false)] // prefix, not child
    [InlineData(@"C:\data\mods\MOD.zip", @"C:\DATA\Mods", true)] // case-insensitive
    [InlineData(@"C:\data\Mods\sub\..\mod.zip", @"C:\data\Mods", true)] // normalized
    [InlineData(@"C:\other\mod.zip", @"C:\data\Mods", false)]
    public void IsPathUnderDirectory_KnownCases(string path, string directory, bool expected)
    {
        Assert.Equal(expected, PathRelationshipHelper.IsPathUnderDirectory(path, directory));
    }

    [Theory]
    [InlineData("", @"C:\data")]
    [InlineData("   ", @"C:\data")]
    [InlineData(@"C:\data", null)]
    [InlineData(@"C:\data", "")]
    public void IsPathUnderDirectory_MissingInput_ReturnsFalse(string path, string? directory)
    {
        Assert.False(PathRelationshipHelper.IsPathUnderDirectory(path, directory));
    }

    [Fact]
    public void IsSameDirectory_NormalizesSeparatorsAndCase()
    {
        Assert.True(PathRelationshipHelper.IsSameDirectory(@"C:\Data\Mods\", @"c:\data\MODS"));
        Assert.False(PathRelationshipHelper.IsSameDirectory(@"C:\Data\Mods", @"C:\Data\Mods2"));
        Assert.False(PathRelationshipHelper.IsSameDirectory(null, @"C:\Data"));
        Assert.False(PathRelationshipHelper.IsSameDirectory(@"C:\Data", "   "));
    }

    [Fact]
    public void IsPathWithinDirectory_ExcludesTheDirectoryItself()
    {
        // Characterization: unlike IsPathUnderDirectory, the directory itself is NOT within.
        Assert.True(PathRelationshipHelper.IsPathWithinDirectory(@"C:\data\Mods", @"C:\data\Mods\mod.zip"));
        Assert.False(PathRelationshipHelper.IsPathWithinDirectory(@"C:\data\Mods", @"C:\data\Mods"));
    }
}
```

- [ ] **Step 2: Write FileNameHelper tests**

`VintageStoryModManager.Tests/FileNameHelperTests.cs`:

```csharp
using System.IO;
using VintageStoryModManager.Helpers;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class FileNameHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-test-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Theory]
    [InlineData("mod:1.zip", "fallback.zip", "mod_1.zip")] // ':' invalid on Windows
    [InlineData("a<b>c.zip", "fallback.zip", "a_b_c.zip")]
    [InlineData(null, "fallback.zip", "fallback.zip")]
    [InlineData("   ", "fallback.zip", "fallback.zip")]
    [InlineData("normal.zip", "fallback.zip", "normal.zip")]
    [InlineData("  padded.zip  ", "fallback.zip", "padded.zip")] // trimmed
    public void SanitizeFileName_ReplacesInvalidCharsOrFallsBack(string? name, string fallback, string expected)
    {
        Assert.Equal(expected, FileNameHelper.SanitizeFileName(name, fallback));
    }

    [Fact]
    public void EnsureUniqueFilePath_ReturnsOriginalWhenFree()
    {
        var path = Path.Combine(_dir, "mod.zip");
        Assert.Equal(path, FileNameHelper.EnsureUniqueFilePath(path));
    }

    [Fact]
    public void EnsureUniqueFilePath_AppendsCounterWhenTaken()
    {
        var path = Path.Combine(_dir, "mod.zip");
        File.WriteAllText(path, "");
        Assert.Equal(Path.Combine(_dir, "mod (1).zip"), FileNameHelper.EnsureUniqueFilePath(path));

        File.WriteAllText(Path.Combine(_dir, "mod (1).zip"), "");
        Assert.Equal(Path.Combine(_dir, "mod (2).zip"), FileNameHelper.EnsureUniqueFilePath(path));
    }

    [Fact]
    public void EnsureUniqueDirectoryPath_AppendsCounterWhenTaken()
    {
        var path = Path.Combine(_dir, "backup");
        Assert.Equal(path, FileNameHelper.EnsureUniqueDirectoryPath(path));

        Directory.CreateDirectory(path);
        Assert.Equal($"{path} (1)", FileNameHelper.EnsureUniqueDirectoryPath(path));
    }
}
```

- [ ] **Step 3: Run the new tests**

Run: `dotnet test VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj -c Release --filter "FullyQualifiedName~PathRelationshipHelperTests|FullyQualifiedName~FileNameHelperTests"`
Expected: PASS, 0 failed. If any assertion fails, the test's expectation is wrong, not the code — fix the test to match actual behavior (characterization), and note the surprising behavior in the commit message.

- [ ] **Step 4: Commit (after user approval)**

```bash
git add VintageStoryModManager.Tests/PathRelationshipHelperTests.cs VintageStoryModManager.Tests/FileNameHelperTests.cs
git commit -m "test: characterize PathRelationshipHelper and FileNameHelper

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: ManagedModPathHelper characterization

**Files:**
- Create: `VintageStoryModManager.Tests/ManagedModPathHelperTests.cs`

**Interfaces:**
- Consumes: `ManagedModPathHelper.IsPathWithinManagedMods(string?, string)` → `bool`; `ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(string?, string, out string?)` → `bool` — both `internal static`.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the tests**

`VintageStoryModManager.Tests/ManagedModPathHelperTests.cs`:

```csharp
using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ManagedModPathHelperTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("smm-data-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dataDir, true);
    }

    [Fact]
    public void IsPathWithinManagedMods_AcceptsModsAndModsByServer()
    {
        Assert.True(ManagedModPathHelper.IsPathWithinManagedMods(
            _dataDir, Path.Combine(_dataDir, "Mods", "m.zip")));
        Assert.True(ManagedModPathHelper.IsPathWithinManagedMods(
            _dataDir, Path.Combine(_dataDir, "ModsByServer", "srv", "m.zip")));
    }

    [Fact]
    public void IsPathWithinManagedMods_RejectsOutsidersAndNullDataDir()
    {
        Assert.False(ManagedModPathHelper.IsPathWithinManagedMods(
            _dataDir, Path.Combine(_dataDir, "Saves", "m.zip")));
        Assert.False(ManagedModPathHelper.IsPathWithinManagedMods(
            null, Path.Combine(_dataDir, "Mods", "m.zip")));
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_NonExistentPath_IsSafe()
    {
        var ok = ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(
            _dataDir, Path.Combine(_dataDir, "Mods", "missing.zip"), out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_RegularFileAndDirectory_AreSafe()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        var file = Path.Combine(modsDir, "m.zip");
        File.WriteAllText(file, "");

        Assert.True(ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, file, out var fileError));
        Assert.Null(fileError);
        Assert.True(ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, modsDir, out var dirError));
        Assert.Null(dirError);
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_SymlinkOutsideManagedMods_IsRejected()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_dataDir, "Outside")).FullName;
        var link = Path.Combine(modsDir, "linked");
        if (!TryCreateSymlink(link, outside)) return; // needs Developer Mode/admin; skip when unavailable

        var ok = ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, link, out var error);
        Assert.False(ok);
        Assert.Contains("points outside", error);
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_SymlinkInsideManagedMods_IsSafe()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(modsDir, "real")).FullName;
        var link = Path.Combine(modsDir, "linked");
        if (!TryCreateSymlink(link, target)) return; // needs Developer Mode/admin; skip when unavailable

        var ok = ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, link, out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    private static bool TryCreateSymlink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 2: Run the new tests**

Run: `dotnet test VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj -c Release --filter "FullyQualifiedName~ManagedModPathHelperTests"`
Expected: PASS, 6 tests, 0 failed. Report whether the two symlink tests actually exercised the symlink branch or early-returned (depends on machine privileges).

- [ ] **Step 3: Commit (after user approval)**

```bash
git add VintageStoryModManager.Tests/ManagedModPathHelperTests.cs
git commit -m "test: characterize ManagedModPathHelper safety checks

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Path-target helpers — ModUpdateTargetPathHelper + ModInstallTargetPathHelper (introduces TestData factory)

**Files:**
- Create: `VintageStoryModManager.Tests/TestData.cs`
- Create: `VintageStoryModManager.Tests/ModUpdateTargetPathHelperTests.cs`
- Create: `VintageStoryModManager.Tests/ModInstallTargetPathHelperTests.cs`

**Interfaces:**
- Consumes: `ModUpdateTargetPathHelper.TryGetUpdateTargetPath(ModListItemViewModel, ModReleaseInfo, string, out string, out string?)` → `bool`; `ModUpdateTargetPathHelper.TryResolveUpdateTarget(ModListItemViewModel, ModReleaseInfo, string, out string, out bool, out string?, out string?)` → `bool`; `ModInstallTargetPathHelper.TryGetInstallTargetPath(string?, ModListItemViewModel, ModReleaseInfo, out string, out string?)` → `bool`; `ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(string?, string, ModReleaseInfo, out string, out string?)` → `bool`.
- Produces: `TestData.CreateMod(string modId = "testmod", string sourcePath = @"C:\data\Mods\testmod.zip", ModSourceKind sourceKind = ModSourceKind.ZipArchive, string location = "Mods")` → `ModListItemViewModel`; `TestData.CreateRelease(string version = "1.2.3", string? fileName = "testmod_1.2.3.zip")` → `ModReleaseInfo`. Task 5 uses both.

- [ ] **Step 1: Write the TestData factory**

`VintageStoryModManager.Tests/TestData.cs`:

```csharp
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Tests;

internal static class TestData
{
    internal static ModListItemViewModel CreateMod(
        string modId = "testmod",
        string sourcePath = @"C:\data\Mods\testmod.zip",
        ModSourceKind sourceKind = ModSourceKind.ZipArchive,
        string location = "Mods")
    {
        var entry = new ModEntry
        {
            ModId = modId,
            Name = modId,
            SourcePath = sourcePath,
            SourceKind = sourceKind
        };

        return new ModListItemViewModel(
            entry,
            false,
            location,
            (_, _) => Task.FromResult<ActivationResult>(default!),
            initializeUserReportState: false);
    }

    internal static ModReleaseInfo CreateRelease(
        string version = "1.2.3",
        string? fileName = "testmod_1.2.3.zip")
    {
        return new ModReleaseInfo
        {
            Version = version,
            DownloadUri = new Uri("https://example.invalid/testmod.zip"),
            FileName = fileName
        };
    }
}
```

Note: `ActivationResult`'s definition hasn't been inspected — if `Task.FromResult<ActivationResult>(default!)` doesn't compile, adapt to its actual shape (the handler is never invoked by these tests). If `ModListItemViewModel` construction *throws at runtime* (WPF Application/dispatcher requirements), STOP and report back rather than working around it.

- [ ] **Step 2: Write ModUpdateTargetPathHelper tests**

`VintageStoryModManager.Tests/ModUpdateTargetPathHelperTests.cs`:

```csharp
using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModUpdateTargetPathHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-upd-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void TryGetUpdateTargetPath_UsesReleaseFileNameInExistingPathDirectory()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(fileName: "testmod_2.0.0.zip"),
            @"C:\data\Mods\testmod_1.0.0.zip", out var fullPath, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(@"C:\data\Mods\testmod_2.0.0.zip", fullPath);
    }

    [Fact]
    public void TryGetUpdateTargetPath_BlankExistingPath_Fails()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(), "   ", out _, out var error);

        Assert.False(ok);
        Assert.Equal("The mod path could not be determined.", error);
    }

    [Fact]
    public void TryGetUpdateTargetPath_NoReleaseFileName_FallsBackToModIdDashVersion()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(version: "2.0.0", fileName: null),
            @"C:\data\Mods\old.zip", out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(@"C:\data\Mods\testmod-2.0.0.zip", fullPath);
    }

    [Fact]
    public void TryGetUpdateTargetPath_FileNameWithoutExtension_GetsZipAppended()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(fileName: "testmod_2"),
            @"C:\data\Mods\old.zip", out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(@"C:\data\Mods\testmod_2.zip", fullPath);
    }

    [Fact]
    public void TryGetUpdateTargetPath_FileNameWithDirectory_IsStrippedToLeafName()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(fileName: @"nested\dir\payload.zip"),
            @"C:\data\Mods\old.zip", out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(@"C:\data\Mods\payload.zip", fullPath);
    }

    [Fact]
    public void TryResolveUpdateTarget_ExistingDirectory_TargetsTheDirectoryItself()
    {
        var modDir = Directory.CreateDirectory(Path.Combine(_dir, "testmod")).FullName;

        var ok = ModUpdateTargetPathHelper.TryResolveUpdateTarget(
            TestData.CreateMod(sourceKind: ModSourceKind.Folder), TestData.CreateRelease(),
            modDir, out var targetPath, out var targetIsDirectory, out var existingPath, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(targetIsDirectory);
        Assert.Equal(modDir, targetPath);
        Assert.Null(existingPath);
    }

    [Fact]
    public void TryResolveUpdateTarget_MissingPathForFolderMod_TreatedAsDirectory()
    {
        var missing = Path.Combine(_dir, "gone");

        var ok = ModUpdateTargetPathHelper.TryResolveUpdateTarget(
            TestData.CreateMod(sourceKind: ModSourceKind.Folder), TestData.CreateRelease(),
            missing, out var targetPath, out var targetIsDirectory, out _, out _);

        Assert.True(ok);
        Assert.True(targetIsDirectory);
        Assert.Equal(missing, targetPath);
    }

    [Fact]
    public void TryResolveUpdateTarget_ExistingFile_ResolvesNewPathBesideIt()
    {
        var existing = Path.Combine(_dir, "testmod_1.0.0.zip");
        File.WriteAllText(existing, "");

        var ok = ModUpdateTargetPathHelper.TryResolveUpdateTarget(
            TestData.CreateMod(), TestData.CreateRelease(fileName: "testmod_2.0.0.zip"),
            existing, out var targetPath, out var targetIsDirectory, out var existingPath, out _);

        Assert.True(ok);
        Assert.False(targetIsDirectory);
        Assert.Equal(Path.Combine(_dir, "testmod_2.0.0.zip"), targetPath);
        Assert.Equal(existing, existingPath);
    }
}
```

- [ ] **Step 3: Write ModInstallTargetPathHelper tests**

`VintageStoryModManager.Tests/ModInstallTargetPathHelperTests.cs`:

```csharp
using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModInstallTargetPathHelperTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("smm-inst-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dataDir, true);
    }

    [Fact]
    public void TryGetInstallTargetPath_NullDataDirectory_Fails()
    {
        var ok = ModInstallTargetPathHelper.TryGetInstallTargetPath(
            null, TestData.CreateMod(), TestData.CreateRelease(), out _, out var error);

        Assert.False(ok);
        Assert.Contains("VintagestoryData folder is not available", error);
    }

    [Fact]
    public void TryGetInstallTargetPath_CreatesModsDirectoryAndBuildsPath()
    {
        var ok = ModInstallTargetPathHelper.TryGetInstallTargetPath(
            _dataDir, TestData.CreateMod(), TestData.CreateRelease(), out var fullPath, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(Path.Combine(_dataDir, "Mods", "testmod_1.2.3.zip"), fullPath);
        Assert.True(Directory.Exists(Path.Combine(_dataDir, "Mods")));
    }

    [Fact]
    public void TryGetInstallTargetPath_ExistingFile_GetsUniqueSuffix()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        File.WriteAllText(Path.Combine(modsDir, "testmod_1.2.3.zip"), "");

        var ok = ModInstallTargetPathHelper.TryGetInstallTargetPath(
            _dataDir, TestData.CreateMod(), TestData.CreateRelease(), out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(Path.Combine(modsDir, "testmod_1.2.3 (1).zip"), fullPath);
    }

    [Fact]
    public void TryGetDependencyInstallTargetPath_NoFileName_FallsBackToModIdDashVersion()
    {
        var ok = ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(
            _dataDir, "depmod", TestData.CreateRelease(fileName: null), out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(Path.Combine(_dataDir, "Mods", "depmod-1.2.3.zip"), fullPath);
    }

    [Fact]
    public void TryGetDependencyInstallTargetPath_BlankModId_FallsBackToLiteralMod()
    {
        var ok = ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(
            _dataDir, "  ", TestData.CreateRelease(fileName: null), out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(Path.Combine(_dataDir, "Mods", "mod-1.2.3.zip"), fullPath);
    }
}
```

- [ ] **Step 4: Run the new tests**

Run: `dotnet test VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj -c Release --filter "FullyQualifiedName~ModUpdateTargetPathHelperTests|FullyQualifiedName~ModInstallTargetPathHelperTests"`
Expected: PASS, 13 tests, 0 failed. This is the first task that constructs `ModListItemViewModel` — if construction throws, STOP and report (see Global Constraints).

- [ ] **Step 5: Commit (after user approval)**

```bash
git add VintageStoryModManager.Tests/TestData.cs VintageStoryModManager.Tests/ModUpdateTargetPathHelperTests.cs VintageStoryModManager.Tests/ModInstallTargetPathHelperTests.cs
git commit -m "test: characterize mod update/install target path helpers

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: ModGridSelectionService characterization

**Files:**
- Create: `VintageStoryModManager.Tests/ModGridSelectionServiceTests.cs`

**Interfaces:**
- Consumes: `TestData.CreateMod(...)` from Task 4. `ModGridSelectionService` ctor `(Dispatcher, Action, Action<ModListItemViewModel>, Action<ModListItemViewModel>)`; members: `SelectedMods` (`IReadOnlyList<ModListItemViewModel>`), `SelectionAnchor` (`ModListItemViewModel?`), `AddToSelection(mod)`, `RemoveFromSelection(mod)`, `ClearSelection(bool resetAnchor = false)`, `ClearModDatabaseSelections()`, `SelectAllModsInCurrentView(bool isApplyingPreset, List<ModListItemViewModel>)`, `RestoreSelection(IReadOnlyList<ModListItemViewModel>, string?)`.
- Produces: nothing consumed later.

Deliberately NOT covered: `HandleModRowSelection` (reads static `Keyboard.Modifiers` — WPF input infrastructure; its range/ctrl logic largely delegates to the covered Add/Remove/Clear primitives).

- [ ] **Step 1: Write the tests**

`VintageStoryModManager.Tests/ModGridSelectionServiceTests.cs`:

```csharp
using System.Windows.Threading;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ModGridSelectionServiceTests
{
    private int _selectionChangedCount;
    private readonly ModGridSelectionService _service;

    public ModGridSelectionServiceTests()
    {
        _service = new ModGridSelectionService(
            Dispatcher.CurrentDispatcher,
            () => _selectionChangedCount++,
            _ => { },
            _ => { });
    }

    [Fact]
    public void AddToSelection_SelectsMarksAndNotifies_IgnoresDuplicates()
    {
        var mod = TestData.CreateMod();

        _service.AddToSelection(mod);
        Assert.Single(_service.SelectedMods, mod);
        Assert.True(mod.IsSelected);
        Assert.Equal(1, _selectionChangedCount);

        _service.AddToSelection(mod);
        Assert.Single(_service.SelectedMods);
        Assert.Equal(1, _selectionChangedCount); // duplicate add does not re-notify
    }

    [Fact]
    public void RemoveFromSelection_UnmarksAndNotifies_IgnoresNonMembers()
    {
        var mod = TestData.CreateMod();
        _service.AddToSelection(mod);
        _selectionChangedCount = 0;

        _service.RemoveFromSelection(mod);
        Assert.Empty(_service.SelectedMods);
        Assert.False(mod.IsSelected);
        Assert.Equal(1, _selectionChangedCount);

        _service.RemoveFromSelection(mod);
        Assert.Equal(1, _selectionChangedCount); // removing a non-member does not notify
    }

    [Fact]
    public void SelectAllModsInCurrentView_SelectsAllAndAnchorsLast()
    {
        var mods = new List<ModListItemViewModel>
        {
            TestData.CreateMod("a", @"C:\data\Mods\a.zip"),
            TestData.CreateMod("b", @"C:\data\Mods\b.zip"),
            TestData.CreateMod("c", @"C:\data\Mods\c.zip")
        };

        _service.SelectAllModsInCurrentView(false, mods);

        Assert.Equal(mods, _service.SelectedMods);
        Assert.All(mods, m => Assert.True(m.IsSelected));
        Assert.Same(mods[2], _service.SelectionAnchor);
    }

    [Fact]
    public void SelectAllModsInCurrentView_WhileApplyingPreset_DoesNothing()
    {
        var mods = new List<ModListItemViewModel> { TestData.CreateMod() };

        _service.SelectAllModsInCurrentView(true, mods);

        Assert.Empty(_service.SelectedMods);
        Assert.Equal(0, _selectionChangedCount);
    }

    [Fact]
    public void ClearSelection_UnmarksAll_AnchorSurvivesUnlessReset()
    {
        var mods = new List<ModListItemViewModel>
        {
            TestData.CreateMod("a", @"C:\data\Mods\a.zip"),
            TestData.CreateMod("b", @"C:\data\Mods\b.zip")
        };
        _service.SelectAllModsInCurrentView(false, mods);

        _service.ClearSelection();
        Assert.Empty(_service.SelectedMods);
        Assert.All(mods, m => Assert.False(m.IsSelected));
        Assert.Same(mods[1], _service.SelectionAnchor); // characterization: anchor kept by default

        _service.SelectAllModsInCurrentView(false, mods);
        _service.ClearSelection(true);
        Assert.Null(_service.SelectionAnchor);
    }

    [Fact]
    public void ClearModDatabaseSelections_RemovesOnlyDatabaseEntries_AndClearsDatabaseAnchor()
    {
        var local = TestData.CreateMod("local", @"C:\data\Mods\local.zip");
        var db = TestData.CreateMod("dbmod", @"C:\data\Mods\dbmod.zip", location: "Mod Database");
        _service.AddToSelection(local);
        // RestoreSelection with matching anchorSourcePath makes the database entry the anchor.
        _service.RestoreSelection(new[] { local, db }, db.SourcePath);
        Assert.Same(db, _service.SelectionAnchor);
        _selectionChangedCount = 0;

        _service.ClearModDatabaseSelections();

        Assert.Single(_service.SelectedMods, local);
        Assert.False(db.IsSelected);
        Assert.True(local.IsSelected);
        Assert.Null(_service.SelectionAnchor);
        Assert.Equal(1, _selectionChangedCount);
    }

    [Fact]
    public void RestoreSelection_ReplacesSelectionAndAnchorsBySourcePathCaseInsensitively()
    {
        var old = TestData.CreateMod("old", @"C:\data\Mods\old.zip");
        _service.AddToSelection(old);

        var a = TestData.CreateMod("a", @"C:\data\Mods\a.zip");
        var b = TestData.CreateMod("b", @"C:\data\Mods\b.zip");
        _service.RestoreSelection(new[] { a, b }, @"C:\DATA\MODS\A.ZIP");

        Assert.Equal(new[] { a, b }, _service.SelectedMods);
        Assert.False(old.IsSelected);
        Assert.Same(a, _service.SelectionAnchor);
    }

    [Fact]
    public void RestoreSelection_NoAnchorMatch_AnchorsLast_EmptyRestoreClearsAnchor()
    {
        var a = TestData.CreateMod("a", @"C:\data\Mods\a.zip");
        var b = TestData.CreateMod("b", @"C:\data\Mods\b.zip");

        _service.RestoreSelection(new[] { a, b }, @"C:\data\Mods\unknown.zip");
        Assert.Same(b, _service.SelectionAnchor);

        _service.RestoreSelection(Array.Empty<ModListItemViewModel>(), null);
        Assert.Empty(_service.SelectedMods);
        Assert.Null(_service.SelectionAnchor);
    }

    [Fact]
    public void RestoreSelection_IdenticalSelection_DoesNotNotify()
    {
        var a = TestData.CreateMod("a", @"C:\data\Mods\a.zip");
        var b = TestData.CreateMod("b", @"C:\data\Mods\b.zip");
        var mods = new[] { a, b };
        _service.RestoreSelection(mods, null);
        _selectionChangedCount = 0;

        _service.RestoreSelection(mods, null);

        Assert.Equal(0, _selectionChangedCount);
        Assert.Equal(mods, _service.SelectedMods);
    }
}
```

- [ ] **Step 2: Run the new tests**

Run: `dotnet test VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj -c Release --filter "FullyQualifiedName~ModGridSelectionServiceTests"`
Expected: PASS, 9 tests, 0 failed.

- [ ] **Step 3: Run the ENTIRE suite plus a clean solution build (Phase 1 definition of done)**

Run: `dotnet test VintageStoryModManager.Tests/VintageStoryModManager.Tests.csproj -c Release`
Expected: all tests pass, 0 failed.
Run: `dotnet build ImprovedModMenu.sln -c Release`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 4: Commit (after user approval)**

```bash
git add VintageStoryModManager.Tests/ModGridSelectionServiceTests.cs
git commit -m "test: characterize ModGridSelectionService selection behavior

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
