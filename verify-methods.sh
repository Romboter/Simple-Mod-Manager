#!/usr/bin/env bash
# Verifies that each given method name is defined exactly once across the
# MainWindow.*.cs partials -- the check every refactor move needs afterward.
# Uses the same multi-line-aware regex as next-candidate-scan.sh, so it
# doesn't false-negative on wrapped return types (see CLAUDE.md Gotchas).
#
# Usage: ./verify-methods.sh MethodOne MethodTwo ...

cd "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || {
    echo "Could not cd into script directory"
    pwd
    exit 1
}

if [[ -f ./terminal-helpers.sh ]]; then
    source ./terminal-helpers.sh
else
    heading() { echo; echo "=== $* ==="; }
    success() { echo "$*"; }
    warn() { echo "$*"; }
    fail() { echo; echo "ERROR: $*" >&2; exit 1; }
fi

if [[ $# -eq 0 ]]; then
    fail "Usage: ./verify-methods.sh MethodOne MethodTwo ..."
fi

MAINWINDOW_DIR="VintageStoryModManager/Views/MainWindow"

heading "Method definition counts"

python - "$MAINWINDOW_DIR" "$@" <<'PY'
from pathlib import Path
import re
import sys

mainwindow_dir = Path(sys.argv[1])
targets = sys.argv[2:]

method_pattern = re.compile(
    r"(?ms)^ {4}"
    r"(?:private|protected|public|internal)\s+"
    r"(?:(?:static|async|sealed|override|virtual|partial|extern|new)\s+)*"
    r".*?"
    r"\b(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"\([^;{}]*?\)\s*"
    r"(?:where\s+.*?)*\{"
)

hits = {name: [] for name in targets}

for path in sorted(mainwindow_dir.glob("MainWindow.*.cs")):
    text = path.read_text(encoding="utf-8", errors="replace")
    for match in method_pattern.finditer(text):
        name = match.group("name")
        if name in hits:
            line = text.count("\n", 0, match.start()) + 1
            hits[name].append(f"{path}:{line}")

exit_code = 0
for name in targets:
    locations = hits[name]
    count = len(locations)
    marker = "OK" if count == 1 else "WARN"
    if count != 1:
        exit_code = 1
    print(f"[{marker}] {name}: {count}")
    for loc in locations:
        print(f"       {loc}")

sys.exit(exit_code)
PY
status=$?

if [[ $status -eq 0 ]]; then
    success "All methods defined exactly once."
else
    warn "One or more methods were not found exactly once -- see WARN lines above."
fi

exit $status
