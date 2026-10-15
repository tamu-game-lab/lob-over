# Work orders

One file per bounded job for Astra. The orchestrator dispatches them in order with `bash Tools/astra.sh WO-xx`, verifies with the acceptance checks written in the WO, and records the decision in `Docs/WORKLOG.md`. Rejections are written as `WO-xx.rejection-N.md` beside the WO. Logs of each dispatch go to `logs/`.

| WO | Title | Depends on | Folders |
| --- | --- | --- | --- |
| WO-00 | Scaffold `Monitor/`: package, scripts, seeded RNG, test runner | — | `Monitor/` |
| WO-01 | Scenario and catalog loader + validator | WO-00 | `Monitor/src/scenario/` |
| WO-02 | Reference simulation (JS) + headless runner + golden parity | WO-01 | `Monitor/src/sim/`, `Monitor/test/` |
| WO-03 | Node host (WebSocket + static), Plan page, 2D Site page, facilitator page, Playwright e2e | WO-02 | `Monitor/src/host/`, `Monitor/web/`, `Monitor/e2e/` |
| WO-04 | Printed kit generator (manual, cards, ledger, tokens) to PDF | WO-01 | `Paper/` |
| WO-05 | Voice line batch with Kokoro | WO-01 | `Tools/voice/`, `Voice/` |

Phase 1 (Unity host, C# port, VR scene, Quest client) is planned in `Docs/ROADMAP.md` and gets its own WO-06 onward after the table test.

## Template for new work orders

```
# WO-xx — <title>
Goal (one paragraph: what exists when this is done and how the orchestrator will know)
Read: exact files/sections
You may change: exact paths
Build: numbered, concrete, no design choices left open
Acceptance checks: commands with expected output; the orchestrator runs these verbatim
Out of scope: what not to do
```
