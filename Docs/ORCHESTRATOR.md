# ORCHESTRATOR.md — operating procedure for Claude Code (Fable 5.1)

Goal of this run: take Phase 0 of LOB, Over from an empty `Monitor/` to a table-testable prototype by dispatching work orders WO-00 through WO-05 to Astra, verifying each, and committing each accepted one. The run is finished when every STATUS line in `Docs/WORKLOG.md` for WO-00..WO-05 reads `ACCEPTED` and you have printed `READY_FOR_TABLE_TEST`, or when you hit a stop condition and have reported it to Bryan.

You orchestrate, verify and decide; Astra implements. Spend your effort on reading results against the design, not on writing code.

## 0. Before the first dispatch (once)

1. Read `CLAUDE.md`, this file, `Docs/DESIGN_v0.md` (whole, once), `Docs/PROTOCOL.md`, `Docs/SCENARIO_SCHEMA.md`, and skim `Docs/WORK_ORDERS/README.md`. Do not re-read the design doc later; grep it.
2. Check the toolchain and record versions in `Docs/WORKLOG.md`:
   - `node --version` (20+), `npm --version`
   - `codex --version` and `codex exec --help` (confirm the flags `Tools/astra.sh` uses: `--full-auto`, `-C`; if a flag is missing, set `CODEX_FLAGS` in the environment for this session and note it in the worklog; do not edit the script's defaults without noting why)
   - `git status` (must be a repo, clean or only untracked kit files)
   - `python --version` (3.11+), `pip --version`
3. Install what Astra cannot (its sandbox has no network). Run from the repo root:
   - `(cd Monitor && npm init -y && npm install ws@8 && npm install -D playwright@1 && npx playwright install chromium)` (the `Monitor/package.json` WO-00 creates may adjust scripts, not dependencies)
   - `pip install kokoro soundfile` (for WO-05; if it fails, note it and continue; WO-05 can be deferred)
   - `pip install reportlab pypdf` (for WO-04 PDFs and their text check)
4. `git add -A && git commit -m "Kickoff kit"` if the kit files are not yet committed.

## 1. The loop, per work order

Dispatch in order: WO-00, WO-01, WO-02, WO-03, WO-04, WO-05. WO-04 and WO-05 touch disjoint folders (`Paper/`, `Tools/voice/` + `Voice/`) and may run in parallel with each other after WO-01 is accepted; everything else is one writer at a time.

For each WO-xx:

1. **Dispatch.** `bash Tools/astra.sh WO-xx` (add a short note as extra arguments on a re-dispatch: `bash Tools/astra.sh WO-02 "Rejection 1: see Docs/WORK_ORDERS/WO-02.rejection-1.md"`). The script writes `Docs/WORK_ORDERS/logs/WO-xx.<timestamp>.log` and prints `LOG=<path>` last. It can take 5 to 40 minutes. Do not poll the log more than once every few minutes; read only its tail.
2. **Read the STATUS block only.** `tail -n 60 <log>`. Do not read the whole log unless the STATUS block is missing or contradictory; if it is missing, treat the run as `PARTIAL` and look at `git status` to see what changed.
3. **Verify yourself.** Run the work order's acceptance checks from the repo root, exactly as written in the WO. Record each check's result in the worklog in one line. Verification rules:
   - Run the tests; do not trust `TESTS` lines you did not reproduce.
   - For pages (WO-03), take a Playwright screenshot of each page at 1280x800 with a live host and look at the PNG (you can read images). Check against the WO's "what the page must show" list and against the no-verdict rule.
   - For the simulation (WO-02), run the golden comparison and read the diff. If any golden misses tolerance, reject; do not loosen the tolerance.
   - Grep every player-facing file for verdict language: `grep -rniE "correct|wrong|right choice|well done|good job|score|mistake|should have" Monitor/web Paper/` and judge each hit. Permit boards, result cards and event cards may state facts only.
   - Determinism: run the headless Stage 1 twice and `diff` the JSON transcripts; any difference is a reject.
   - Scope: `git status --short` must list only files the WO allowed. Anything else is a reject with the list.
4. **Decide.**
   - **ACCEPT**: all checks pass. `git add -A && git commit -m "WO-xx: <one line from the WO title>"`. Write `WO-xx ACCEPTED <date> <commit>` plus one line of notable NOTES in `Docs/WORKLOG.md`.
   - **REJECT**: write `Docs/WORK_ORDERS/WO-xx.rejection-N.md` (N = 1 or 2) listing, for each failed check, the command, the observed output, the expected output, and the file you believe is wrong. Be specific and short; Astra works from this file. Do not fix it yourself unless the fix is one line and obviously mechanical (a typo, a wrong constant copied from the design); if you do, say so in the worklog and still re-run the checks. Then re-dispatch with the note. Maximum two rejection rounds per WO; the third failure is a stop condition.
   - **PARTIAL from Astra with a reasonable blocker** (missing dependency, a schema field the WO forgot): fix the environment or amend the WO text (append an "Amendment" section, never silently rewrite acceptance checks to be weaker), then re-dispatch. This does not count as a rejection round.
5. **Worklog.** After each decision, append to `Docs/WORKLOG.md`: WO id, dispatch time, log path, checks run with results, decision, commit hash. Keep it terse; it is for Bryan to read in two minutes.

## 2. Stop conditions (report to Bryan, then stop)

- A WO fails verification three times.
- Astra reports `BLOCKED` on something that needs a design decision (not an environment fix). Write the question in `Docs/DECISIONS.md` under "Open" with your recommended answer and stop.
- A check reveals that the design doc contradicts itself or the data (for example a scenario trigger that cannot fire with the catalog rates). Record it in `Docs/DECISIONS.md`, propose the fix, stop.
- You would need to change `Data/Scenarios/*.json` numbers to make a check pass. Scenario numbers are Bryan's and Phumpat's; propose, do not change.
- Anything that needs the Unity Editor, the Quest, LOB-Game's folder written to, or network access Astra's sandbox lacks and you cannot provide.

When you stop, the last thing you print is a summary for Bryan: what was accepted (with commits), what is blocked and why, the exact question(s) you need answered, and the one command to resume.

## 3. Finish line

When WO-00 through WO-05 are all `ACCEPTED`:

1. Run the table-test smoke yourself: start the host (`node Monitor/src/host/server.js Data/Scenarios/stage1_texas.json &`), open `http://localhost:7331/plan.html`, `/site.html` and `/facilitator.html` with Playwright, press Start on the facilitator page, let it run 30 seconds of wall time, screenshot all three, and confirm: the day counter advanced, the Plan page's actual line stepped once per day, the Site page shows crews at different stations, and the event card for the Day 6 collapse appeared on the Site page before the Plan page learned anything from the daily report.
2. Confirm `Paper/out/` holds the Stage 1 and Stage 2 kits as PDFs and that the permit codes printed match `Data/Scenarios/*.json`.
3. Confirm `Voice/stage1_texas/` holds one WAV per line in the scenario's `lines` array (or note WO-05 deferred).
4. Write the "Phase 0 complete" entry in `Docs/WORKLOG.md` with the commit hash and the table-test instructions (three laptops or one laptop plus a phone for the Site page; printed kit; 20 minutes).
5. Print `READY_FOR_TABLE_TEST`.

## 4. Token discipline

- Never cat a log, a scenario file or the design doc whole after the first read; use `tail`, `grep -n`, and `sed -n 'a,bp'`.
- Do not open Astra's source files to review style; review behaviour through the acceptance checks. Open a source file only to locate a specific failure for the rejection note.
- One subagent is justified in exactly one place: an independent review of the WO-02 simulation against `Docs/DESIGN_v0.md` "World rules" before you accept it (fresh eyes on the rules the goldens cannot cover: order lag, stop work, conflicts, rain rework, demobilization). Give it the file list and the rules section; ask for a list of rule-by-rule findings, nothing else.
- Do not run Unity, do not install Unity packages, do not write C# in this run. Phase 1 is `Docs/ROADMAP.md` and a separate run.

## 5. If you are resuming

Read `Docs/WORKLOG.md` (last 60 lines), `git log --oneline -15`, and `git status --short`. Continue from the first WO that is not `ACCEPTED`. Logs for earlier attempts are in `Docs/WORK_ORDERS/logs/`.
