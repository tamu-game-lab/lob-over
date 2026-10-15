#!/usr/bin/env bash
# Dispatch one work order to Astra (Codex CLI) and log the whole run.
#
#   bash Tools/astra.sh WO-02
#   bash Tools/astra.sh WO-02 "Rejection 1: see Docs/WORK_ORDERS/WO-02.rejection-1.md"
#
# Environment overrides:
#   CODEX_FLAGS   flags placed before the prompt (default: --full-auto). If `codex exec --help`
#                 shows no --full-auto on this machine, use e.g.:
#                 CODEX_FLAGS="--sandbox workspace-write --ask-for-approval never"
#   CODEX_MODEL   adds "-m <model>" (default: none; ~/.codex/config.toml decides, gpt-6-astra here)
#
# Notes: Codex needs a git repo (this folder is one), has no network in its sandbox (the orchestrator
# installs dependencies first), reads AGENTS.md on its own, and hangs on some terminals without the
# stdin redirect below. Runs in the foreground; expect 5-40 minutes. The log's last lines are Astra's
# STATUS block; the orchestrator reads only that.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WO="${1:-}"; shift || true
NOTE="${*:-}"
if [[ -z "$WO" ]]; then echo "usage: bash Tools/astra.sh WO-xx [note]"; exit 2; fi
SPEC="$ROOT/Docs/WORK_ORDERS/$WO.md"
if [[ ! -f "$SPEC" ]]; then echo "no work order at $SPEC"; exit 2; fi
if ! command -v codex >/dev/null 2>&1; then echo "codex not on PATH (npm i -g @openai/codex)"; exit 2; fi
mkdir -p "$ROOT/Docs/WORK_ORDERS/logs"
STAMP="$(date +%Y%m%d-%H%M%S)"
LOG="$ROOT/Docs/WORK_ORDERS/logs/$WO.$STAMP.log"
FLAGS="${CODEX_FLAGS:---full-auto}"
MODEL_FLAG=""
if [[ -n "${CODEX_MODEL:-}" ]]; then MODEL_FLAG="-m $CODEX_MODEL"; fi

read -r -d '' PROMPT <<EOF || true
You are Astra, the implementer on this repository. Read AGENTS.md first and obey it.
Then read Docs/WORK_ORDERS/$WO.md and carry out that work order exactly: only the files it allows,
only the behaviour it specifies, every acceptance check run by you before you finish.
Do not commit. Do not touch Data/ unless the work order says so. Do not add dependencies.
If something outside your scope blocks you, stop and report BLOCKED with the exact reason.
Finish with the STATUS block in the format AGENTS.md gives, as the very last thing you print.
$( [[ -n "$NOTE" ]] && printf 'Orchestrator note for this dispatch: %s\n' "$NOTE" )
EOF

{
  echo "=== $WO dispatched $STAMP"
  echo "=== root: $ROOT"
  echo "=== flags: $FLAGS $MODEL_FLAG"
  echo "=== note: ${NOTE:-none}"
  echo
} | tee "$LOG"

cd "$ROOT"
# shellcheck disable=SC2086
codex exec $FLAGS $MODEL_FLAG -C "$ROOT" "$PROMPT" < /dev/null 2>&1 | tee -a "$LOG"
RC=${PIPESTATUS[0]}
{
  echo
  echo "=== codex exit code: $RC"
  echo "=== finished $(date +%Y%m%d-%H%M%S)"
} | tee -a "$LOG"
echo "LOG=$LOG"
exit "$RC"
