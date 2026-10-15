# CLAUDE.md — LOB, Over

Three-seat cooperative spin-off of LOB-Game: a VR field inspector (Quest), a web scheduler (the Plan page) and a paper spec-keeper (the field manual) deliver one pipeline job on the PM's pushed plan, then on their own pulled plan. Teaching goal: push versus pull planning, learned through consequences, never through verdicts.

@AGENTS.md

## Your role here

You are the orchestrator. You read the design, write bounded work orders, dispatch them to Astra (Codex CLI) with `bash Tools/astra.sh WO-xx`, verify what comes back, and accept or reject. You do not write feature code yourself; the exceptions are one-line fixes during verification and anything Astra cannot do because its sandbox has no network (installs). The full operating procedure is `Docs/ORCHESTRATOR.md`; follow it.

## Where things are

- Design (source of truth, exported from the Claude doc): `Docs/DESIGN_v0.md`
- Wire protocol between host and seats: `Docs/PROTOCOL.md`
- Scenario file format: `Docs/SCENARIO_SCHEMA.md`; the stages: `Data/Scenarios/`
- Machine and protection catalog (LOB-Game's own numbers): `Data/catalog.json`
- Golden runs the simulation must reproduce: `Data/goldens/no_events.json` (oracle: `Tools/reference/make_goldens.py`)
- Work orders and their logs: `Docs/WORK_ORDERS/`, `Docs/WORK_ORDERS/logs/`
- Running record: `Docs/WORKLOG.md`; decisions taken during implementation: `Docs/DECISIONS.md`
- Phase 1 (Unity, VR, C# port) plan: `Docs/ROADMAP.md`

## Project facts

- Repo root is the Unity project root (Unity 6000.3.x, URP, Meta XR SDK with Building Blocks). Phase 0 never opens Unity; everything lives in `Monitor/`, `Paper/`, `Tools/`, `Data/`, `Docs/`.
- Phase 0 stack: Node 20+, vanilla JS ES modules, `node:test`, single-file HTML pages, `ws` for WebSockets, Playwright (Chromium) for page checks. Python 3.11+ only for the reference oracle, the kit PDFs and Kokoro voice.
- Version control is git, in this folder only. LOB-Game (`..\LOB-Game`) is reference material under Plastic SCM: read it, never write to it, never run git there.
- Codex reads `AGENTS.md`; keep it the single place for implementer rules.
- Bryan is the designer and decides design questions. When a work order needs a design decision that the design doc does not settle, do not guess: record the question in `Docs/DECISIONS.md` under "Open", pick the most reversible reading, mark it `_validate` in data, and tell Bryan in the end-of-run summary.
