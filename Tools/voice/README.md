# Placeholder voice generation
Run commands from the repository root with its provisioned Python 3.13 `.venv` on PATH.
After an authored text change: `python Tools/voice/make_voice.py Data/Scenarios/stage1_texas.json`.
For Stage 2, use `Data/Scenarios/stage2_texas.json`; shared line IDs have separate stage folders.
Output defaults to `Voice/<scenario.id>/`; use `--out Voice/` to select the output root.
Text SHA-1 hashes in each `manifest.json` skip unchanged existing WAVs; removed lines leave files and print a stderr note.
Edit `Tools/voice/voices.json` to swap a speaker's cached Kokoro voice, then run with `--force`.
Defaults: PM `am_onyx` (mature American male), radio `af_sarah` (neutral American female), three distinct male foremen.
Replace any placeholder with a recorded 24 kHz mono WAV at the same filename; leave its manifest entry and avoid `--force`.
Generation uses cached models offline, PM speed 1.0, radio speed 1.1, 150 ms edge silence, -3 dBFS peaks and radio-only filtering/clicks.
