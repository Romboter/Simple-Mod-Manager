# Simple Mod Manager — Incremental MainWindow Refactoring Skill

## Purpose

Continue decomposing and refactoring the Simple Mod Manager WPF application without changing behavior.

The original `MainWindow.xaml.cs` monolith has already been mechanically split into feature-oriented partial class files under:

```text
VintageStoryModManager/Views/MainWindow/
```

The current goal is to gradually remove non-UI responsibilities from those partial files while preserving application behavior and maintaining a successful build after every small change.

This is an incremental refactor, not a rewrite.

---

## Project Context

* Language: C#
* Runtime: .NET 8
* UI framework: WPF
* Project style: SDK-style `.csproj`
* Main application project:

```text
VintageStoryModManager/VintageStoryModManager.csproj
```

* MainWindow XAML:

```text
VintageStoryModManager/Views/MainWindow.xaml
```

* MainWindow partial files:

```text
VintageStoryModManager/Views/MainWindow/
```

* Models:

```text
VintageStoryModManager/Models/
```

* Services:

```text
VintageStoryModManager/Services/
```

* View models:

```text
VintageStoryModManager/ViewModels/
```

* Build command:

```bash
dotnet build
```

---

## Current Refactoring State

The original oversized `MainWindow.xaml.cs` file has already been split into feature-oriented partial files.

Several nested types have already been extracted into standalone files, including types related to:

* Preset loading
* Modlist loading
* Manager deletion
* Debug log identifiers
* Installed-mod columns
* Update release preferences
* Compatibility evaluation
* Mod update results
* Modlist metadata
* Mod usage prompt data

Before extracting another type, search the project to confirm it has not already been moved.

Use:

```bash
rg -n 'TYPE_NAME' VintageStoryModManager
```

Never create a duplicate declaration.

---

## Core Rules

### 1. Never perform a broad rewrite

Do not redesign an entire feature in one pass.

Work in small batches such as:

* One nested type
* One small group of closely related types
* One pure helper method
* One service extraction
* One command or event-handler migration

Every batch must build before continuing.

### 2. Preserve behavior

During structural extraction:

* Copy logic exactly.
* Preserve parameter order.
* Preserve property names.
* Preserve enum member order.
* Preserve visibility unless a visibility change is required for extraction.
* Preserve nullability.
* Preserve exception behavior.
* Preserve async behavior.
* Preserve UI-thread behavior.
* Preserve status and error messages.
* Preserve logging calls.

Do not rename or clean up unrelated code while moving it.

### 3. Build after every batch

After each extraction:

```bash
dotnet build
```

Do not continue if the build reports errors.

Resolve the errors introduced by the current batch before attempting another change.

### 4. Commit small, coherent changes

Each successful extraction should be committed separately.

Examples:

```text
refactor: extract mod usage prompt data
refactor: extract mod update operation result
refactor: extract modlist metadata type
refactor: move compatibility helpers into service
```

Do not combine unrelated refactors in one commit.

### 5. Do not edit generated or build output

Never edit:

```text
bin/
obj/
```

Do not manually edit generated WPF files.

Only edit source files.

### 6. Keep MainWindow partial classes temporary

Partial files are an intermediate decomposition strategy.

Do not create new unrelated logic in `MainWindow` unless necessary for behavior parity.

The long-term goal is:

* UI event handling in views
* UI state and commands in view models
* Business logic in services
* File and network access in infrastructure/services
* Data containers in models

---

## Required Workflow

For every extraction, follow this exact sequence.

### Step 1 — Inspect the declaration

Find the declaration and all usages:

```bash
rg -n -A 40 -B 5 'TYPE_OR_METHOD_NAME' VintageStoryModManager
```

Determine:

* Current namespace
* Current visibility
* Dependencies
* Required `using` directives
* Whether it accesses private `MainWindow` members
* Whether it references WPF controls
* Whether it is data-only or contains behavior

### Step 2 — Choose the correct destination

Use feature-oriented folders.

Examples:

```text
Models/Presets/
Models/Modlists/
Models/Updates/
Models/Compatibility/
Models/Usage/
Models/Debugging/
Services/
ViewModels/
```

Do not place unrelated types in generic files such as:

```text
Utils.cs
Helpers.cs
Common.cs
Misc.cs
```

Prefer one meaningful type per file.

### Step 3 — Create the new file

Copy the original declaration exactly.

When moving a nested private type to a top-level file, usually change:

```csharp
private
```

to:

```csharp
internal
```

Do not make the type `public` unless an actual cross-assembly requirement exists.

Add only the `using` directives required by the new standalone file.

### Step 4 — Remove the original declaration

Remove only the original nested declaration.

Do not change call sites unless required for namespace resolution.

Do not remove surrounding methods, comments, or braces.

For declarations containing nested braces, use a syntax-aware or brace-aware removal method rather than a broad regular expression.

### Step 5 — Confirm one declaration remains

Run:

```bash
rg -n 'class TYPE_NAME|struct TYPE_NAME|record struct TYPE_NAME|enum TYPE_NAME' VintageStoryModManager
```

Expected result:

* Exactly one declaration
* Located in the intended new file

Usages elsewhere are normal.

### Step 6 — Build

Run:

```bash
dotnet build
```

Require:

```text
0 Error(s)
```

Warnings may be accepted only when they existed before the current batch or are clearly unrelated.

### Step 7 — Review the diff

Run:

```bash
git status --short
git diff --stat
git diff
```

Confirm:

* The new file contains the original logic.
* The old declaration was removed.
* No unrelated formatting changed.
* No accidental file deletions occurred.
* No generated or build files are staged.

### Step 8 — Commit

Stage only files related to the current extraction:

```bash
git add <new files>
git add <modified source files>
```

Then:

```bash
git diff --cached --stat
git commit -m "refactor: <specific extraction>"
```

Push only after the commit succeeds:

```bash
git push
```

---

## Type Extraction Rules

A type is safe to extract when it:

* Is an enum, record, struct, or plain data class
* Does not directly access private `MainWindow` fields
* Does not reference WPF controls owned by the window
* Does not require an implicit outer-class instance
* Can compile with normal namespace imports

Types with factory methods are still safe to extract if their dependencies are available through imports.

Types that reference a view model may remain models temporarily, but their namespace dependency must be explicit.

Example:

```csharp
using VintageStoryModManager.ViewModels;
```

---

## Method Extraction Rules

After nested types are extracted, prefer pure or near-pure methods first.

Good candidates:

* Path normalization
* Version comparison
* Compatibility calculations
* Metadata parsing
* String formatting
* JSON conversion
* Selection calculations
* Release filtering
* Filename generation
* DTO construction

Avoid extracting these first:

* Event handlers
* Methods that manipulate WPF controls
* Methods that access many private fields
* Large orchestration methods
* Methods that mix UI, filesystem, network, and business logic

### Pure helper extraction process

1. Identify all inputs used by the method.
2. Pass those inputs as parameters.
3. Return the result instead of modifying window state.
4. Create a feature-specific service or utility class.
5. Replace the original method body with a call to the new class.
6. Build and verify behavior.
7. Commit separately.

Do not convert several unrelated helpers at once.

---

## Service Extraction Rules

Create a service when a group of methods shares a clear external responsibility.

Likely future services include:

```text
IModlistService
IPresetService
IModCompatibilityService
IModConfigurationService
IModBackupService
IGameLaunchService
IManagerDataService
ICloudModlistService
```

Do not introduce an interface automatically.

Use an interface when:

* The project needs dependency injection
* The service is already mocked in tests
* Multiple implementations are expected
* The abstraction clearly improves dependency direction

A concrete internal service is acceptable for the first extraction.

### Service boundary rule

A service must not depend on:

* `MainWindow`
* WPF controls
* Named XAML elements
* Message boxes
* Window-owned dependency properties

The view or view model should handle user interaction and pass plain values into the service.

---

## UI and ViewModel Migration Rules

Do not move an event handler directly into a service.

Preferred flow:

```text
WPF event handler
    → ViewModel command or coordinator
        → Service
```

During gradual migration, a `MainWindow` event handler may remain as a thin adapter:

```csharp
private async void UpdateModsButton_OnClick(
    object sender,
    RoutedEventArgs e)
{
    await _viewModel.UpdateModsCommand.ExecuteAsync(null);
}
```

Do not migrate all event handlers at once.

Choose one feature area at a time.

---

## Suggested Refactoring Order

Continue in this order:

1. Remaining nested types
2. Pure formatting and parsing helpers
3. Compatibility calculations
4. Path and filename helpers
5. Metadata serialization/deserialization
6. Backup file operations
7. Preset parsing
8. Modlist parsing
9. Update release selection
10. Network and cloud operations
11. ViewModel commands
12. Thin remaining event handlers

Large orchestrators such as bulk updates or preset application should be handled only after their smaller dependencies have been extracted.

---

## Validation Requirements

After structural changes:

```bash
dotnet build -c Debug
dotnet build -c Release
```

For UI-affecting changes, also run:

```bash
dotnet run --project VintageStoryModManager/VintageStoryModManager.csproj
```

Perform a smoke test appropriate to the changed feature.

Examples:

* Main window opens
* Mod list loads
* Menus open
* Sorting works
* Presets load
* Modlists save
* Updates start
* Application closes cleanly

Do not claim behavior parity unless the relevant workflow was actually exercised.

---

## Failure Handling

If a build fails:

1. Stop.
2. Do not begin another extraction.
3. Inspect the first compiler error.
4. Determine whether the new file is missing a namespace import.
5. Search for the referenced type:

```bash
rg -n 'class TYPE|struct TYPE|record TYPE|enum TYPE' VintageStoryModManager
```

6. Add the correct `using` directive or namespace qualification.
7. Rebuild.

If the extraction becomes unexpectedly complex:

```bash
git restore <modified files>
git clean -fd <new extraction files>
```

Only use `git clean` with an explicit path. Never run a broad destructive clean without reviewing untracked files.

---

## Prohibited Actions

Do not:

* Replace the entire architecture in one change
* Convert the whole application to MVVM at once
* Rename hundreds of members
* Run formatting across the entire repository
* Change public behavior during a move
* Modify XAML unless the current extraction specifically requires it
* Add new frameworks without approval
* Add dependency injection infrastructure prematurely
* Suppress compiler errors
* Commit a failing build
* Commit `bin`, `obj`, temporary scripts, or generated preview output unless explicitly requested
* Delete working code merely because it appears unused without proving it is unreachable

---

## Completion Criteria for Each Batch

A batch is complete only when:

* The source type or method exists in its new location.
* The original declaration is removed.
* The declaration exists exactly once.
* Required namespace imports are correct.
* `dotnet build` reports zero errors.
* The diff contains only related changes.
* The change is committed with a specific message.
* The branch remains pushable and recoverable.
