# SETUP_CHECKLIST.md — Bryan's 40 minutes before the first `/goal`

Do these by hand, once, on the Windows PC. Nothing here needs Claude. Tick as you go.

## A. The Unity project (the repo root)

- [ ] Unity Hub → New project → editor **6000.3.x** (the same minor as LOB-Game's 6000.3.6f1, so the C# port can reuse its GameLogic without version drift) → template **Universal 3D** (URP) → name `LOB-Over`, location `C:\Users\bryan\Documents\TAMU\02-Research\Creative Care\Unity\LOB-Over`. Create.
- [ ] In the Hub, make sure the editor has **Android Build Support** (with OpenJDK and Android SDK & NDK Tools). In the project: File → Build Profiles → Android → Switch Platform. (Quest 3S is Android, ARM64.)
- [ ] Meta XR: Window → Package Manager → My Assets → **Meta XR All-in-One SDK** (add it to My Assets on the Asset Store first if it is not listed) → Install. It brings the Core SDK, Building Blocks and the Interaction SDK (hands later).
- [ ] Meta → Tools → **Project Setup Tool** → Android tab → Fix All, then Apply All. Repeat on the Windows tab (for Link testing in the Editor).
- [ ] New scene `Assets/LOBOver/Scenes/Site.unity`: Meta → Tools → **Building Blocks** → add **Camera Rig** and **Controller Tracking** (skip Hand Tracking for now; the design is controllers first). Save. Add it to Build Profiles → Scene List. This is only to prove the toolchain; WO-09 builds the real scene.
- [ ] Optional now, needed for Phase 1: install the CoPlay **Unity MCP** package the same way as in ECLIPSE (Package Manager → Add package from git URL) and register the server for Claude Code in this project (`claude mcp add ...` as you did there; it listens on 127.0.0.1:7800 in ECLIPSE, pick another port if both projects will be open). Phase 0 never touches Unity, so this can wait.
- [ ] Close Unity before the next section (Unity holds file locks that annoy git on Windows).

## B. Drop the kit in and make it a repo

- [ ] Unzip `LOB-Over-Kickoff.zip` and copy **its contents** into the project root, beside `Assets/`, `Packages/` and `ProjectSettings/`: `AGENTS.md`, `CLAUDE.md`, `README.md`, `.gitignore`, `.claude/`, `Data/`, `Docs/`, `Monitor/`, `Paper/`, `Tools/`. Not into `Assets/`: Unity would import every `.md`, `.json` and `.js` as an asset, and `Monitor/node_modules` would be a disaster.
- [ ] If Unity already made its own `.gitignore`, replace it with the kit's (it covers Unity, Node, Python and the kit's output folders).
- [ ] Git Bash (or Windows Terminal with Git) in the project root:
  ```
  git init -b main
  git add -A
  git commit -m "Unity project + kickoff kit"
  ```
- [ ] GitHub: create a **private** repo `LOB-Over` in the `tamu-game-lab` organization (no README, no .gitignore), then:
  ```
  git remote add origin https://github.com/tamu-game-lab/LOB-Over.git
  git push -u origin main
  ```
  Codex needs a git repo; GitHub is for backup and for the student later. LOB-Game stays on Plastic SCM; nothing here touches it.

## C. Tools on the PC

- [ ] **Node 20 LTS or newer**: `node --version`, `npm --version`.
- [ ] **Codex CLI** (Astra): `codex --version`, then `codex exec --help` and confirm `--full-auto` and `-C` are listed. `~/.codex/config.toml` already names `gpt-6-astra`; nothing to change. If `--full-auto` is missing, note the replacement flags: the orchestrator sets `CODEX_FLAGS` for `Tools/astra.sh`.
- [ ] **Claude Code**: `claude --version`. Confirm `/goal` exists in this project (`~/.claude/commands/goal.md` makes it global; if it is per-project, copy it from ECLIPSE into `.claude/commands/`). Without it, paste the goal text as an ordinary message.
- [ ] **Python 3.11+** and pip: `python --version`. For voice placeholders (WO-05): install **espeak-ng** from its Windows MSI (Kokoro's fallback phonemizer) and let the orchestrator run `pip install kokoro soundfile`. For the kit PDFs: it runs `pip install reportlab`.
- [ ] Optional: ElevenLabs is not needed for Phase 0.

## D. Launch

- [ ] Git Bash in the project root (Claude Code's Bash tool on Windows runs through Git Bash; `Tools/astra.sh` is a bash script):
  ```
  claude --model fable --effort max
  ```
  (If your Claude Code version does not accept `--effort`, start with `claude --model fable` and set the effort inside the session.)
- [ ] First message:
  ```
  /goal Read Docs/ORCHESTRATOR.md and follow it from step 0. You orchestrate; Astra implements through bash Tools/astra.sh. Dispatch WO-00 through WO-05 in order, verify each with its acceptance checks, accept or reject (max two rejection rounds), commit each accepted WO, keep Docs/WORKLOG.md current. Done when WO-00..WO-05 are ACCEPTED and you have printed READY_FOR_TABLE_TEST, or when a stop condition is reported to me with the exact question. Do not write feature code yourself.
  ```
- [ ] Walk away. Each work order is 5 to 40 minutes of Astra time plus verification. Expect two to four hours wall clock for WO-00 to WO-03, with WO-04 and WO-05 in parallel after WO-01. `Docs/WORKLOG.md` is the place to look when you come back.
- [ ] `.claude/settings.json` in the kit pre-approves the commands the orchestrator needs (`bash Tools/astra.sh`, `npm`, `node`, `git`, `python`, `pip`, `tail`, `grep`, `cmp`, `curl`). Delete it if you would rather approve each command by hand the first time.

## E. After READY_FOR_TABLE_TEST

- [ ] Print `Paper/out/stage1_texas/` and `stage2_texas/` (manual double-sided, cards and tokens on card stock, ledger landscape).
- [ ] Table test: `npm --prefix Monitor run host -- Data/Scenarios/stage1_texas.json`; open `http://<PC-IP>:7331/plan.html` on one laptop, `/site.html` on another or a phone, `/facilitator.html` on the host PC; Rules has the paper. Press Start on the facilitator page. Film it.
- [ ] Write what broke into `Docs/DECISIONS.md` (Open) and the scenario numbers to change; then the Phase 0.5 run and Phase 1 per `Docs/ROADMAP.md`.
