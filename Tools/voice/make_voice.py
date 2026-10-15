"""Generate the scenario's placeholder speech using the installed, cached Kokoro."""

import argparse
from contextlib import redirect_stdout
import hashlib
import json
import os
from pathlib import Path
import sys

# WO-05 amendment: the table-test PC and sandbox must work from the HF cache.
os.environ.setdefault("HF_HUB_OFFLINE", "1")

import numpy as np
import soundfile as sf


# WO-05: American English, 24 kHz mono, 150 ms edge silence and -3 dBFS peak.
LANG_CODE = "a"
SAMPLE_RATE = 24_000
EDGE_SILENCE_SECONDS = 0.150
PEAK_DBFS = -3.0
# WO-05: PM speed 1.0, radio speed 1.1; foremen use Kokoro's default 1.0.
SPEEDS = {"pm": 1.0, "radio": 1.1}
DEFAULT_SPEED = 1.0
# WO-05: a 300-3,400 Hz radio band-pass; implementation choices keep clicks quiet.
RADIO_LOW_HZ = 300
RADIO_HIGH_HZ = 3_400
RADIO_FILTER_TAPS = 257
CLICK_SECONDS = 0.012
CLICK_GAP_SECONDS = 0.020
CLICK_RELATIVE_PEAK = 0.04
CLICK_FREQUENCIES_HZ = (1_100, 2_300)
# A relative -50 dB threshold retains quiet consonants while finding edge silence.
SILENCE_THRESHOLD_DB = -50.0
MODEL_REPO = "hexgrad/Kokoro-82M"


def trim_silence(audio):
    peak = float(np.max(np.abs(audio)))
    if peak == 0 or not np.isfinite(audio).all():
        raise ValueError("Kokoro returned silent or non-finite audio")
    active = np.flatnonzero(np.abs(audio) > peak * 10 ** (SILENCE_THRESHOLD_DB / 20))
    margin = round(EDGE_SILENCE_SECONDS * SAMPLE_RATE)
    return np.pad(audio[active[0] : active[-1] + 1], (margin, margin))


def radio_treatment(audio):
    offsets = np.arange(RADIO_FILTER_TAPS) - (RADIO_FILTER_TAPS - 1) / 2
    low = RADIO_LOW_HZ / SAMPLE_RATE
    high = RADIO_HIGH_HZ / SAMPLE_RATE
    kernel = (2 * high * np.sinc(2 * high * offsets)
              - 2 * low * np.sinc(2 * low * offsets)) * np.hamming(RADIO_FILTER_TAPS)
    filtered = np.convolve(audio, kernel, mode="same")
    time = np.arange(round(CLICK_SECONDS * SAMPLE_RATE)) / SAMPLE_RATE
    # A fixed, windowed burst avoids introducing random output or a loud impulse.
    click = sum(np.sin(2 * np.pi * hz * time) for hz in CLICK_FREQUENCIES_HZ)
    click *= np.hanning(len(click)) * np.exp(-time / (CLICK_SECONDS / 3))
    click *= CLICK_RELATIVE_PEAK * np.max(np.abs(filtered)) / np.max(np.abs(click))
    gap = np.zeros(round(CLICK_GAP_SECONDS * SAMPLE_RATE))
    return np.concatenate((click, gap, filtered, gap, click))


def create_pipeline():
    import spacy

    # Misaki otherwise calls spacy.cli.download, which would violate offline use.
    if not spacy.util.is_package("en_core_web_sm"):
        raise RuntimeError("Missing en_core_web_sm; ask the orchestrator to provision it")
    from kokoro import KPipeline

    return KPipeline(lang_code=LANG_CODE, repo_id=MODEL_REPO)


def generate_audio(pipeline, text, speaker, voice):
    segments = [audio.detach().cpu().numpy()
                for _, _, audio in pipeline(text, voice=voice,
                                             speed=SPEEDS.get(speaker, DEFAULT_SPEED))
                if audio is not None]
    if not segments:
        raise ValueError("Kokoro returned no audio segments")
    audio = np.concatenate(segments).astype(np.float64)
    if not audio.size or not np.isfinite(audio).all() or not np.any(audio):
        raise ValueError("Kokoro returned empty, silent or non-finite audio")
    if speaker == "radio":
        audio = radio_treatment(audio)
    audio = trim_silence(audio)
    audio *= 10 ** (PEAK_DBFS / 20) / np.max(np.abs(audio))
    return audio


def write_manifest(path, manifest):
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                         encoding="utf-8")
    temporary.replace(path)


def make_voice(scenario_path, output_root, force):
    scenario = json.loads(scenario_path.read_text(encoding="utf-8-sig"))
    voices = json.loads(Path(__file__).with_name("voices.json").read_text(encoding="utf-8"))
    stage_dir = output_root / scenario["id"]
    stage_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = stage_dir / "manifest.json"
    manifest = (json.loads(manifest_path.read_text(encoding="utf-8"))
                if manifest_path.exists() else {})
    current_ids = {line["id"] for line in scenario["lines"]}
    for wav in sorted(stage_dir.glob("*.wav")):
        if wav.stem not in current_ids:
            print(f"Removed line {wav.stem}: leaving {wav.as_posix()}", file=sys.stderr)

    pipeline = None
    pipeline_error = None
    failed = False
    for line in scenario["lines"]:
        line_id, speaker, text = line["id"], line["speaker"], line["text"]
        destination = stage_dir / f"{line_id}.wav"
        digest = hashlib.sha1(text.encode("utf-8")).hexdigest()
        previous = manifest.get(line_id, {})
        seconds = 0.0
        status = "failed"
        try:
            if not force and previous.get("sha1") == digest and destination.is_file():
                seconds = sf.info(destination).duration
                status = "skipped"
            else:
                voice = voices[speaker]
                # Keep stdout strictly one status row per scenario line.
                with redirect_stdout(sys.stderr):
                    if pipeline_error is not None:
                        raise RuntimeError(pipeline_error)
                    if pipeline is None:
                        try:
                            pipeline = create_pipeline()
                        except Exception as error:
                            pipeline_error = str(error)
                            raise
                    audio = generate_audio(pipeline, text, speaker, voice)
                temporary = destination.with_suffix(".tmp.wav")
                try:
                    sf.write(temporary, audio, SAMPLE_RATE, subtype="PCM_16")
                    temporary.replace(destination)
                finally:
                    if temporary.exists():
                        temporary.unlink()
                seconds = len(audio) / SAMPLE_RATE
                manifest[line_id] = {"text": text, "sha1": digest,
                                     "voice": voice, "seconds": seconds}
                status = "generated"
        except Exception as error:
            failed = True
            print(f"{line_id}: {error}", file=sys.stderr)
        print(f"{line_id}  {speaker}  {status}  {seconds:.3f}  {destination.as_posix()}",
              flush=True)

    write_manifest(manifest_path, manifest)
    return 1 if failed else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scenario", type=Path)
    parser.add_argument("--out", type=Path, default=Path("Voice"))
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()
    try:
        return make_voice(args.scenario, args.out, args.force)
    except Exception as error:
        print(f"{args.scenario}: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
