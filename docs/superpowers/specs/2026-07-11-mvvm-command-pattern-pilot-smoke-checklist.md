# MVVM Command Pattern Pilot — Smoke Test Checklist

Covers the Area H (Server Sync) command conversion. Run against a Server-type profile
and a Local-type profile.

- [ ] On a Local profile: "Sync to Server..." is disabled (grayed out) in the File menu.
- [ ] Toggle "Enable server options" on: "Server Targets...", "Sync to Server...", and both
      separators around them become visible in the File menu.
- [ ] Toggle "Enable server options" off: those same items/separators disappear again.
- [ ] Toggle "Enable server options" on, restart the app: the setting persisted (still on).
- [ ] Click "Server Targets...": the Manage Server Targets dialog opens, owned by the main
      window (Alt-Tab shows it grouped with the main window; it stays on top of it).
- [ ] Add or edit a server target, close the dialog: "Sync to Server..." enablement updates
      immediately to reflect whether the active profile now has a usable target.
- [ ] Switch the active profile to Server type with a configured target: "Sync to Server..."
      becomes enabled without needing to reopen any menu.
- [ ] Switch to a Server-type profile with NO server target configured, click
      "Sync to Server...": expect it to be disabled (can't click it) — this is the
      CanSyncToServer() gate.
- [ ] On a Server-type profile with a valid target but no data directory configured
      (if reachable), click "Sync to Server...": expect the themed warning dialog
      "No data directory is configured for this profile."
- [ ] On a Server-type profile with a valid target and data directory, click
      "Sync to Server...": the Sync to Server dialog opens, owned by the main window,
      and functions as before (preview/execute steps unchanged).
- [ ] Copy for Server button (unchanged by this pilot): still works exactly as before —
      confirms the deferred 4th handler wasn't broken by this pass.
