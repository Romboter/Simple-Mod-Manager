# Slice 17 (Area M): Enrichment Part 1 — OfflineModDatabaseInfoBuilder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** First of two DB-enrichment slices: move the pure offline-info machinery (~620 lines: offline `ModDatabaseInfo` synthesis from cached release archives, zip/JSON parsing, release merging, compatibility determination) out of `MainViewModel` into a new `OfflineModDatabaseInfoBuilder` service. The refresh **orchestration** (queueing, batching, online fetch, `_modEntriesBySourcePath` writes, busy-state calls) is **explicitly out of scope** — it's Enrichment part 2, to be planned together with the Mod Loading repository core since they share the entry dictionary.

**Architecture:** The machinery is almost entirely `private static` with zero WPF/dispatcher dependency (the cluster report flagged exactly this). Only one real seam exists: `DetermineInstalledGameCompatibility` reads `InstalledGameVersion` + `_configuration.RequireExactVsVersionMatch` — two `Func`s. The orchestration half calls the machinery at exactly **three** entry points (verified at `e504f0d`): `CreateOfflineDatabaseInfo(entry)` (line 2721), `MergeOfflineAndCachedInfo(offlineInfo, cachedInfo)` (2723), `CreateInfoWithoutTags(info)` (2934). Everything else becomes private inside the service.

**Tech Stack:** .NET 8, xUnit. Pure-logic extraction — the most testable slice in area M.

## Global Constraints

- **Base commit: `b8663db`** (post-slice-15/16/18 tip). Re-verified there on 2026-07-07: all 20 mover members present and structurally identical; table line numbers below were written at `e504f0d` and sit ~237 lines higher now — current anchors: three call sites `CreateOfflineDatabaseInfo(entry)` 2484, `MergeOfflineAndCachedInfo(offlineInfo, cachedInfo)` 2486, `CreateInfoWithoutTags(info)` 2697; movers `CreateInfoWithoutTags` 2706, `CreateOfflineDatabaseInfo` 2735, `MergeOfflineAndCachedInfo` 2765, `MergeReleases` 2810, `CreateOfflineReleases` 2842, `EnumerateCachedModReleases` 2893, `TryCreateCachedRelease` 2914, `IsModIdMatch` 3028, `FindArchiveEntry` 3035, `GetString` 3045, `TryGetProperty` 3053, `TryResolveVersionFromMap` 3066, `ParseDependencies` 3087, `AggregateRequiredGameVersions` 3105, `DetermineOfflineLastUpdatedUtc` 3127, `CompareOfflineReleases` 3187, `ExtractRequiredGameVersions` 3207, `DetermineInstalledGameCompatibility` 3222, `TryCreateFileUri` 3246, `TryGetReleaseFileName` 3267, `TryGetLastWriteTimeUtc` 3308. `CompareOfflineReleases` method-group usages confirmed at 2838 (inside `MergeReleases`) and 2866 (inside `CreateOfflineReleases`) — both movers. Foreign stay-put methods confirmed: `TryGetTagSuppressionKey` 2217 (callers 2198/2695/2700, all staying orchestration), `OnInternetAccessChanged` 3140, `RefreshInternetAccessDependentState` 3155. Zero references to the three entry points outside `MainViewModel.cs` (re-confirmed). **Slice 19 runs in parallel but is file-disjoint** (Views/IUserConfigurationService/ThemeMenuViewModel — no overlap). Re-run any grep you depend on; greps beat quoted numbers; stop-and-report on contradictions.
- **Golden rule: `MainViewModel`'s public surface does not change.** Every member moving here is `private` (verified: zero references to the three entry points outside `MainViewModel.cs`), so no pass-throughs are needed — call sites rewire to the service directly. No `Views/` or `.xaml` files change.
- **⚠️ Interleaved foreign code:** the machinery's line range **contains two methods that are NOT part of this cluster and must NOT move**: `OnInternetAccessChanged` + `RefreshInternetAccessDependentState` (~3373–3423, internet-access glue touching `_tabNavigation`) and `TryGetTagSuppressionKey` (2454, static but used only by the staying orchestration at 2435/2932/2937). Skip them; they stay exactly where they are.
- **Move, don't rewrite**; enumerate every non-verbatim change under "Adaptations".
- Release build zero warnings/errors; scoped IDE0005 gate on changed files; method verification via grep across `VintageStoryModManager/` (note: `verify-methods.sh` only scans `MainWindow.*.cs` partials — use `grep -rn 'MethodName(' --include='*.cs' VintageStoryModManager/ | grep -v obj/ | grep -v bin/` and count definitions).
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` in your own worktree only; verify `VintageStoryModManager/ViewModels/TabNavigationViewModel.cs` exists (proves post-slice-14 base); stop-and-report on anomalies. Standing permission to abandon rather than force.

---

### Task 1: Extract `OfflineModDatabaseInfoBuilder`

**Files:**
- Create: `VintageStoryModManager/Services/OfflineModDatabaseInfoBuilder.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Builds offline <see cref="ModDatabaseInfo" /> for installed mods when no online data is
///     available: synthesizes releases from the mod-cache archives (zip/JSON parsing via
///     <see cref="ModCacheLocator" />), determines installed-game compatibility, and merges
///     offline-synthesized info with previously cached online info. Pure logic — no WPF, no
///     dispatcher, no shared mutable state beyond the two injected reads.
/// </summary>
public sealed class OfflineModDatabaseInfoBuilder
{
    public OfflineModDatabaseInfoBuilder(
        Func<string?> installedGameVersionProvider,     // () => InstalledGameVersion
        Func<bool> requireExactVsVersionMatch);         // () => _configuration.RequireExactVsVersionMatch

    public ModDatabaseInfo? CreateOfflineDatabaseInfo(ModEntry entry);
    public static ModDatabaseInfo? MergeOfflineAndCachedInfo(ModDatabaseInfo? offlineInfo, ModDatabaseInfo? cachedInfo);
    public static ModDatabaseInfo CreateInfoWithoutTags(ModDatabaseInfo source);
    internal bool DetermineInstalledGameCompatibility(IReadOnlyList<ModDependencyInfo> dependencies); // internal for tests
}
```

**Moves from `MainViewModel` (bodies verbatim; lines at `e504f0d`):**

*Instance methods (adapt the two reads):*

| Member (line) | Substitutions |
|---|---|
| `CreateOfflineDatabaseInfo` (2972–3000) | none — becomes the public entry point |
| `CreateOfflineReleases` (3079–…) | none (private) |
| `EnumerateCachedModReleases` (3130–3149) | none (private; keeps its direct `ModCacheLocator` static calls) |
| `TryCreateCachedRelease` (3151–…) | none (private) |
| `DetermineInstalledGameCompatibility` (3459–3481) | `InstalledGameVersion` reads → `_installedGameVersionProvider()`; `_configuration.RequireExactVsVersionMatch` → `_requireExactVsVersionMatch()`; accessibility `private`→`internal` (tests) |

*Static methods (move as-is, `private static` unless listed as public above):* `CreateInfoWithoutTags` (2943–2970, → `public static`), `MergeOfflineAndCachedInfo` (3002–3045, → `public static`), `MergeReleases` (3047–3077), `IsModIdMatch` (3265), `FindArchiveEntry` (3272), `GetString` (3282), `TryGetProperty` (3290), `TryResolveVersionFromMap` (3303), `ParseDependencies` (3324), `AggregateRequiredGameVersions` (3342), `DetermineOfflineLastUpdatedUtc` (3364–3372), `CompareOfflineReleases` (3424–3442), `ExtractRequiredGameVersions` (3444–3457), `TryCreateFileUri` (3483), `TryGetReleaseFileName` (3504), `TryGetLastWriteTimeUtc` (3545–…).

**Pre-move verification (run before editing; stop-and-report on surprises):**

```bash
# CompareOfflineReleases showed only 1 parenthesized ref — find its method-group usage:
grep -n 'CompareOfflineReleases' VintageStoryModManager/ViewModels/MainViewModel.cs
# Expect a Sort/OrderBy method-group reference inside CreateOfflineReleases or similar. It must be
# inside a method that is also moving; if its caller is NOT in the move list, stop and report.

# Confirm no staying code calls the generic-named statics:
grep -n 'GetString(\|TryGetProperty(\|TryGetLastWriteTimeUtc(\|TryCreateFileUri(' VintageStoryModManager/ViewModels/MainViewModel.cs
# Every hit must be inside a member in the move list above. Any hit in staying code → stop and report.
```

**`MainViewModel` rewires:**
1. New field + ctor wiring (near the other service constructions):

```csharp
private readonly OfflineModDatabaseInfoBuilder _offlineInfoBuilder;
// ctor:
_offlineInfoBuilder = new OfflineModDatabaseInfoBuilder(
    () => InstalledGameVersion,
    () => _configuration.RequireExactVsVersionMatch);
```

2. Three call-site rewires (all in staying orchestration code):
   - `PopulateOfflineInfoForEntryAsync` (2721): `CreateOfflineDatabaseInfo(entry)` → `_offlineInfoBuilder.CreateOfflineDatabaseInfo(entry)`
   - Same method (2723): `MergeOfflineAndCachedInfo(offlineInfo, cachedInfo)` → `OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo(offlineInfo, cachedInfo)`
   - `PrepareDatabaseInfoForVisibility` (2934): `CreateInfoWithoutTags(info)` → `OfflineModDatabaseInfoBuilder.CreateInfoWithoutTags(info)`
3. Remove now-unused `using` directives (`System.IO.Compression` for `ZipArchive`, possibly `System.Text.Json` — let IDE0005 decide; only remove what it flags).

**Specified adaptations (report them all):**
1. `InstalledGameVersion` → `_installedGameVersionProvider()` (in `DetermineInstalledGameCompatibility` only — verify no other moved body reads it; if one does, apply the same substitution and list it).
2. `_configuration.RequireExactVsVersionMatch` → `_requireExactVsVersionMatch()`.
3. `CreateInfoWithoutTags`/`MergeOfflineAndCachedInfo` accessibility `private static` → `public static`; `DetermineInstalledGameCompatibility` → `internal`.
4. Anything else = deviation, flag with justification.

- [ ] **Step 1:** Pre-move verification greps above.
- [ ] **Step 2:** Create the service; move members; rewire the three call sites. Stale-reference gate:

```bash
grep -n 'CreateOfflineDatabaseInfo\|MergeOfflineAndCachedInfo\|CreateInfoWithoutTags\|CreateOfflineReleases\|EnumerateCachedModReleases\|TryCreateCachedRelease\|DetermineInstalledGameCompatibility\|MergeReleases\|IsModIdMatch\|FindArchiveEntry\|GetString(\|TryGetProperty(\|TryResolveVersionFromMap\|ParseDependencies\|AggregateRequiredGameVersions\|DetermineOfflineLastUpdatedUtc\|CompareOfflineReleases\|ExtractRequiredGameVersions\|TryCreateFileUri\|TryGetReleaseFileName\|TryGetLastWriteTimeUtc' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: **only** the field declaration, the ctor wiring, and the three rewired call sites (`_offlineInfoBuilder.` / `OfflineModDatabaseInfoBuilder.` prefixed). Zero bare-name hits.

- [ ] **Step 3:** Sanity-check the foreign interleaved methods survived in place:

```bash
grep -n 'private void RefreshInternetAccessDependentState\|private void OnInternetAccessChanged\|private static bool TryGetTagSuppressionKey' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: all three present in `MainViewModel.cs`.

- [ ] **Step 4:** `dotnet build ./ImprovedModMenu.sln --configuration Release` → 0/0. Method-count check (grep-based, see Global Constraints): each moved method defined exactly once, in the service.
- [ ] **Step 5:** Format gate + commit:

```bash
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/Services/OfflineModDatabaseInfoBuilder.cs VintageStoryModManager/ViewModels/MainViewModel.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
git add VintageStoryModManager/Services/OfflineModDatabaseInfoBuilder.cs VintageStoryModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: extract offline mod-database-info builder from MainViewModel (slice 17, area M)"
```

---

### Task 2: Tests

**Files:**
- Create: `VintageStoryModManager.Tests/OfflineModDatabaseInfoBuilderTests.cs`

**Interfaces — Consumes:** Task 1's builder. Construct with settable captured locals (`string? gameVersion`, `bool exactMatch`). `ModEntry`/`ModDatabaseInfo`/`ModReleaseInfo`/`ModDependencyInfo` are plain models — construct directly (see `TestData.cs` for `ModEntry` shape). Cache-dependent paths (`EnumerateCachedModReleases`) hit `ModCacheLocator.GetModCacheDirectory(modId)` — use a mod id guaranteed to have no cache directory so the enumeration yields nothing; do NOT try to fake the cache filesystem in this slice.

Test list (~8 — read each implementation body before asserting; the expectations below describe the shape, the moved code is the truth):

1. `CreateOfflineDatabaseInfo_NullEntry_ReturnsNull`.
2. `CreateOfflineDatabaseInfo_NoCache_SynthesizesFromEntry` — entry with a version and game dependency, no cached releases: result is `IsOfflineOnly = true`, `LatestVersion == entry.Version`, `Side == entry.Side`, `RequiredGameVersions` contains the dependency's version.
3. `MergeOfflineAndCachedInfo_NullHandling` — (null, cached) → cached; (offline, null) → offline; (null, null) → null.
4. `MergeOfflineAndCachedInfo_CachedTagsWin_OfflineVersionsWin` — both sides populated: `Tags`/`AssetId`/`ModPageUrl`/`Downloads` come from cached, `LatestVersion`/`LatestCompatibleVersion` prefer offline, `IsOfflineOnly` matches the moved body's choice (read it).
5. `CreateInfoWithoutTags_StripsTags_KeepsEverythingElse` — result `Tags` empty, other fields reference-equal/equal to source.
6. `DetermineInstalledGameCompatibility_MinimumVersionRule` — dep requires 1.21.0: game 1.21.4 → true; game 1.20.9 → false; no game version set → true; empty deps → true.
7. `DetermineInstalledGameCompatibility_ExactMatchMode` — `exactMatch = true`, dep 1.21.3 vs game 1.21.4 → false (first-three-digits rule); 1.21.4 vs 1.21.4 → true.
8. `CreateOfflineDatabaseInfo_RequiredVersionsAggregated` — entry deps with two game-version requirements: `RequiredGameVersions` reflects `AggregateRequiredGameVersions`' behavior over them (read the body; assert the actual aggregation).

- [ ] **Step 1:** Write tests; `dotnet test VintageStoryModManager.Tests --filter OfflineModDatabaseInfoBuilderTests` → all pass. (Known `*_wpftmp` flake: re-run once, then `dotnet clean ... --configuration Debug`.)
- [ ] **Step 2:** Full suite Release → all pass.
- [ ] **Step 3:** Commit:

```bash
git add VintageStoryModManager.Tests/OfflineModDatabaseInfoBuilderTests.cs
git commit -m "test: cover OfflineModDatabaseInfoBuilder (slice 17, area M)"
```

---

### Task 3: Final gates + report

- [ ] Release build 0/0; scoped IDE0005 clean.
- [ ] `git diff --name-only` off base: exactly the service file, `MainViewModel.cs`, and the test file.
- [ ] Report: worktree path + branch, commit hashes, Adaptations list, `MainViewModel.cs` line delta (expect roughly −600), the `CompareOfflineReleases` method-group finding, test names/results, deviations. No push; no `CLAUDE.md`/`.claude/`.

---

## Smoke test (orchestrator/user, post-merge)

Best exercised offline: disable Internet Access (or run with no network), load mods that have cached release archives → grid still shows version/compatibility/release data synthesized from cache (offline info path); re-enable internet, force a details refresh → online data overlays correctly (merge path: tags/downloads appear, versions stay sane); check a mod whose installed version differs from its latest cached release → update indicator consistent with compatibility rules; toggle "Require exact VS version" and refresh → compatibility flags tighten accordingly.

## Follow-up (Enrichment part 2 — not this slice)

The orchestration half: `QueueDatabaseInfoRefresh`, progressive/batch refresh, `ApplyDatabaseInfo*`, the batching timer, `NeedsDatabaseRefresh`/`ShouldSkipOnlineDatabaseRefresh`/`TryGetTagSuppressionKey`, `_suppressedTagEntries`, and the `_modEntriesBySourcePath` writes. Plan it together with the Mod Loading repository core (the shared-write hazard on the entry dictionary is the boundary decision that couples them — see hot-fields.md).
