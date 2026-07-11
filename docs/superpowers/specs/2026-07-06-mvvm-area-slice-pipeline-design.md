# MVVM Refactor: Area-Slice Pipeline — Design

**Date:** 2026-07-06
**Branch:** `refactor/mainwindow-service-extractions`
**Status:** Approved approach (Approach A), pending spec review

## Problem

The refactor toward MVVM is behavior-safe but slow. Current cadence is ~4
interactions per extraction (design turn → confirm → implement → audit →
manual smoke test → commit approval), and the remaining analyzer candidates
are single-method helpers with diminishing returns. Meanwhile the big items
stay parked (dialog pass, `ManagedModPathHelper` family, update/install
coordinator), MainWindow code-behind is still ~11,850 lines across 74
partials, and `MainViewModel.cs` is itself a 4,749-line monolith. There is no
test project, so the only safety net is manual smoke testing — which is also
the throughput ceiling.

## Goal

Move whole responsibility areas at a time instead of single methods, with a
build+test gate replacing most manual verification, so the user's involvement
drops to one review and one smoke test per area slice.

## Non-goals

- Rewriting logic. Every move remains behavior-preserving ("move, don't
  rewrite" still applies).
- Full test coverage. Tests are characterization tests around code being
  moved, written to lock in current behavior — not a coverage project.
- UI/XAML redesign. Bindings and commands may be introduced where an area's
  endpoint calls for it, but views keep their current look and structure.

## Operating model: three phases

### Phase 1 — Test infrastructure (one session)

1. Add an xUnit test project (`VintageStoryModManager.Tests`, net8.0-windows)
   to `ImprovedModMenu.sln`. xUnit + plain asserts; no mocking framework
   unless a concrete test needs one.
2. Prove the harness by writing characterization tests for services already
   extracted this phase — `ModGridSelectionService`, `ModUpdateTargetPathHelper`,
   `ModUpdateOperationHelper`, `FolderOpeningHelper`, and other pure/parameterized
   helpers. These were designed to be testable; they are the cheap wins.
3. Definition of done: `dotnet test` runs green in Release alongside the
   existing zero-warnings build.

Services that require WPF types (`Dispatcher` etc.) get tests only where the
logic can be exercised without a real dispatcher; anything needing UI pumping
is out of scope for Phase 1.

### Phase 2 — Roadmap (one session)

1. Re-run `tools/MainWindowResponsibilityAnalyzer` for current data.
2. Map all `MainWindow.*.cs` partials into ~8–12 named **areas** (e.g. "mod
   update/install", "server sync", "backups", "cloud auth", "grid selection &
   visuals"). Every partial belongs to exactly one area.
3. For each area, record in a living roadmap doc
   (`docs/superpowers/specs/mvvm-roadmap.md`):
   - the partials and approximate line count it covers,
   - the **destination**: which existing service/ViewModel absorbs the logic,
     or what new service/ViewModel is created,
   - the endpoint definition of done: *handlers thin (wire-up only), logic in
     services/ViewModels, covered by characterization tests*,
   - known hazards (dialogs, busy-flag/progress coupling, `_dataDirectory`
     dependencies, dead code flagged in CLAUDE.md),
   - a file-manifest estimate, used later to judge parallel-safety.
4. `MainViewModel.cs` decomposition is itself on the roadmap as one or more
   areas — the goal is not to pour MainWindow logic into a second monolith.
5. The user reviews and approves the roadmap (including slice order) before
   any slice starts.

The roadmap replaces per-session re-discovery from analyzer output. It is a
living document: updated as slices complete, corrected when reality
disagrees with it.

### Phase 3 — Area slices (the steady state)

Per slice, in order:

1. **Slice plan (short):** confirm scope from the roadmap — files touched,
   destination types, what stays behind. Parked per-method rules become
   slice-scoping decisions here: the ≤4-file cap is dropped in favor of "the
   slice's declared manifest"; dialog-touching extractions are allowed when
   the slice is explicitly a dialog-flow slice.
2. **Tests first:** characterization tests around the logic about to move,
   written against the *current* code, passing before any move.
3. **Move:** extract the area's logic to its destination. Multiple methods,
   fields, and files in one pass. Handlers left as thin wire-up.
4. **Gate:** Release build zero-warnings + `dotnet test` green + IDE0005
   clean + `./verify-methods.sh` for moved methods + analyzer re-run when the
   slice touched cross-partial coupling.
5. **One user review + one manual smoke test** for the whole slice.
6. **Commit only on explicit user approval** (unchanged from current
   practice), then tag if the slice closes a milestone.

A behavior bug found at step 5 is bisectable because tests were written in
step 2 against pre-move behavior.

### Parallelism (inside Phase 3, opportunistic)

Two slices may run as parallel worktree sub-agents **only when their roadmap
file manifests are disjoint** — including shared touchpoints: `.csproj`,
`ImprovedModMenu.sln`, `MainWindow.Core.cs`, `MainViewModel.cs`, and the test
project's shared files. Both slices adding files to the same `.csproj` is
acceptable only because SDK-style projects glob sources; anything requiring
an actual shared-file edit disqualifies the pair.

All existing worktree gotchas from CLAUDE.md apply verbatim: agents verify
`git rev-parse --show-toplevel` resolves under `.claude/worktrees/`, get the
current tip hash with permission to `git reset --hard <hash>` in their own
worktree only, stop-and-report on any environment anomaly, and each
completed agent waits for per-agent user review before cherry-pick.

## Constraints carried forward

- Zero errors, zero warnings on Release build, always.
- Never stage `.claude/` or `CLAUDE.md`.
- No commits without explicit user approval.
- Known-dead code stays unless the user asks (e.g.
  `GenerateServerInstallMacroMenuItem_OnClick`).
- `dotnet format whitespace` is not run on `MainWindow.Core.cs`
  (pre-existing indentation would produce a noisy diff).
- Analyzer quirks remain in effect: "Fields written" false-positives on `!`,
  field *reads* missing from Incoming Dependencies — grep the field name
  repo-wide before trusting a report table.

## Risks

- **WPF-coupled logic resists testing.** Mitigation: characterization tests
  cover what is extractable; UI-coupled remainder keeps the manual smoke
  test as its gate. Don't force dispatcher-pumping test infrastructure in.
- **Roadmap wrong in places.** It's a living doc; slices start with a scope
  confirmation step that catches drift before code moves.
- **Parallel slices collide anyway.** Manifests are estimates; the
  cherry-pick + rebuild + verify step after each agent (existing practice)
  is the backstop, and parallelism is optional — sequential slices are the
  default.

## Success criteria

- Phase 1: test project in solution, `dotnet test` green, existing extracted
  services characterized.
- Phase 2: roadmap doc approved by user, every MainWindow partial assigned
  to an area.
- Phase 3 (per slice): area's logic at its destination, handlers thin,
  build+tests green, one smoke test passed, committed with approval.
- Overall: user interactions per extracted responsibility drop from ~4 per
  method to ~2 per area.
