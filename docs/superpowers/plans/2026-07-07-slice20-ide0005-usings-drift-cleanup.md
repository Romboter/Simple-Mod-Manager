# Slice 20: Solution-Wide IDE0005 Usings-Drift Cleanup + Roslynator Dead-Code Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** (A) Clear the pre-existing solution-wide unused-`using` backlog (~85 IDE0005 findings measured at `3cc0a3f`, all in files untouched by recent slices) so the solution-wide gate `dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes` exits clean and future slices can use it unscoped. (B) Run a **read-only** Roslynator CLI unused-symbol audit and report the findings — the extraction slices' stale-reference greps only catch names the plan author enumerated; this sweeps for leftover dead privates across the whole solution.

**Architecture:** Task 1 is a single mechanical `dotnet format` auto-fix pass — no logic changes, no new files, no tests; the only judgment call is the diff eyeball (every hunk a pure `using`-line deletion). Task 2 is analysis-only: the Roslynator CLI is a dotnet tool invocation, **no NuGet analyzer packages are added to the solution** (that would surface a findings backlog against the zero-warning gate — a separate product decision), and **nothing found gets deleted in this slice** — findings go in the report for per-item review, same as any dead-code deletion (each needs its own re-grep verification before removal, and some known dead code is intentionally kept, e.g. `GenerateServerInstallMacroMenuItem_OnClick`).

**Tech Stack:** `dotnet format` (IDE0005 auto-fix mode); Roslynator.DotNet.Cli (read-only audit).

## Global Constraints

- **DISPATCH ORDER CONSTRAINT: run this slice ONLY when no other slice is in flight.** It touches arbitrary files solution-wide and will conflict with any concurrent extraction (slices 17/19 both edit files that carry drift). The orchestrator supplies the base tip after the queue drains.
- **Base commit: orchestrator-supplied tip.** No line anchors needed (the tool finds its own targets).
- **Usings-only diff.** Any hunk that is not a `using`-directive deletion (or blank-line collapse adjacent to one) is a stop-and-report. Known-safe precedent: the roadmap log (slice 4, 2026-07-06) verified IDE0005 auto-fix does NOT churn `Core.cs`'s non-standard indentation — only the `whitespace` formatter does; still confirm via `git diff --stat` + eyeball.
- Note: `ImplicitUsings=enable` means explicit repeats of global usings (`System.Linq`, `System.Threading`, etc.) are legitimately flagged — deleting them is correct, not noise.
- Release build zero warnings/errors; full test suite passes.
- **Worktree environment check (first action, if dispatched to a worktree agent):** standard — `git rev-parse --show-toplevel` under `.claude/worktrees/`, `git reset --hard <TIP-COMMIT>` in own worktree only, stop-and-report on anomalies. (This slice is also safe to run inline in the main worktree on a clean tree — orchestrator's choice.)
- Do NOT touch `CLAUDE.md` or stage `.claude/`.

---

### Task 1: Auto-fix, verify, commit

- [ ] **Step 1: Baseline count** (also proves the tree builds before you change anything):

```bash
dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal 2>&1 | grep -c IDE0005
```

Expected: a nonzero count (~85 at `3cc0a3f`; may have shifted with landed slices). If zero, the backlog is already clear — stop and report, nothing to do.

- [ ] **Step 2: Auto-fix** (same command, no `--verify-no-changes`):

```bash
dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verbosity minimal
```

- [ ] **Step 3: Diff eyeball.** `git diff --stat` then `git diff`. Every hunk must be a `using`-line deletion (blank-line collapses adjacent to a removed using are fine). Anything else — indentation churn, code changes, XAML — stop, `git checkout -- <file>` is NOT the fix; report with the offending hunk instead.

- [ ] **Step 4: Gate re-run** — the solution-wide check must now exit clean:

```bash
dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
```

Expected: exit code 0, no IDE0005 lines.

- [ ] **Step 5: Build + full suite:**

```bash
dotnet build ./ImprovedModMenu.sln --configuration Release
dotnet test ./ImprovedModMenu.sln --configuration Release
```

Expected: 0 warnings/0 errors; all tests pass (count = whatever the base tip carries; no new tests in this slice). Known flake: WPF `*_wpftmp` CS0103 from stale Debug obj — re-run once, then `dotnet clean ./ImprovedModMenu.sln --configuration Debug`.

- [ ] **Step 6: Commit** (stage the changed `.cs` files explicitly or `git add -u` — never `git add .`, which would grab `.claude/`/`CLAUDE.md`):

```bash
git add -u
git commit -m "chore: clear solution-wide IDE0005 unused-using backlog"
```

---

### Task 2: Roslynator unused-symbol audit (read-only — changes NOTHING)

Run this AFTER Task 1's commit so the usings cleanup doesn't muddy the results.

- [ ] **Step 1: Install the CLI as a global dotnet tool** (tool-only; do NOT add any package to the solution or edit any `.csproj`/`Directory.Build.props`):

```bash
dotnet tool install -g roslynator.dotnet.cli
roslynator --version
```

If the tool is already installed, `dotnet tool update -g roslynator.dotnet.cli` is fine. If installation fails (offline/policy), skip this task and report that it was skipped and why — Task 1 stands alone.

- [ ] **Step 2: Run the unused-symbol analysis** (RCS1213 = remove unused member declaration; IDE0051/IDE0052 = unused/unread private members — supported via the roslyn analyzers the CLI bundles):

```bash
roslynator analyze ./ImprovedModMenu.sln \
  --supported-diagnostics RCS1213 IDE0051 IDE0052 \
  --severity-level info \
  --verbosity minimal 2>&1 | tee roslynator-unused-symbols.txt
```

(If `--supported-diagnostics` rejects the IDE ids in the installed version, fall back to `roslynator analyze ./ImprovedModMenu.sln --supported-diagnostics RCS1213` plus `roslynator find-symbol ./ImprovedModMenu.sln --symbol-kind member --visibility private --without-attributes --unused` — check `roslynator find-symbol --help` for the exact flag spelling in the installed version and report which invocation you used.)

- [ ] **Step 3: Confirm nothing changed:** `git status --porcelain` → identical to before Step 1 apart from the untracked `roslynator-unused-symbols.txt` (leave it untracked — do NOT commit it; it's a report artifact for the orchestrator).

- [ ] **Step 4: Summarize findings in the report**, grouped by file, each with a one-line judgment: *likely-dead* (no references found), *intentionally-kept dead code* (cross-check the known list: `GenerateServerInstallMacroMenuItem_OnClick` in `MainWindow.ServerMacro.cs`, `DeleteCloudAuthMenuItem_OnClick` in `MainWindow.CloudManagement.cs`), or *false positive* (e.g. reflection/XAML-referenced, test-only via `InternalsVisibleTo`). **Delete nothing.** Deletions are per-item user decisions for a future slice.

---

### Task 3: Report

- [ ] Report: base tip used, before/after IDE0005 finding counts, `git diff --stat` summary (files touched / lines deleted), build + test results, any non-using hunks encountered (should be none), and the Task 2 unused-symbol summary (or why Task 2 was skipped). No push.

---

## Smoke test (orchestrator/user, post-merge)

None beyond launch-and-glance — usings-only deletions can't change behavior if the Release build is clean. Launch the app once to confirm it starts.

## Post-merge follow-up (orchestrator)

Update the CLAUDE.md/handoff note that says "solution-wide IDE0005 has known pre-existing drift — use scoped checks only": after this slice, unscoped solution-wide IDE0005 is the standard gate.
