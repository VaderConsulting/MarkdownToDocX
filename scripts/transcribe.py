#!/usr/bin/env python3
"""
Transcribe audio files using OpenAI Whisper.

Usage:
    python scripts/transcribe.py                    # All MP3s in data/input
    python scripts/transcribe.py --limit 5          # First 5 only
    python scripts/transcribe.py --file path.mp3    # Single file
    python scripts/transcribe.py --dry-run          # List files only

Output: data/transcriptions/ (preserves input subdirs)
"""

import argparse
import os
import sys
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

import whisper

PROJECT_ROOT = Path(__file__).resolve().parent.parent
INPUT_DIR = PROJECT_ROOT / "data" / "input"
OUTPUT_DIR = PROJECT_ROOT / "data" / "transcriptions"

_model = None


def get_model():
    global _model
    if _model is None:
        print("Loading Whisper model (first run downloads ~140MB)...")
        _model = whisper.load_model("base")
    return _model


def _transcribe_quiet(audio_path: Path, language: str = "de"):
  """Run Whisper without progress bar spam."""
  model = get_model()
  old_stdout, old_stderr = sys.stdout, sys.stderr
  try:
    with open(os.devnull, "w") as devnull:
      with redirect_stdout(devnull):
        with redirect_stderr(devnull):
          return model.transcribe(
            str(audio_path),
            language=language,
            fp16=False,
            verbose=False,
          )
  except Exception:
    sys.stdout, sys.stderr = old_stdout, old_stderr
    raise
  finally:
    sys.stdout, sys.stderr = old_stdout, old_stderr


def transcribe(audio_path: Path, language: str = "de") -> str:
  result = _transcribe_quiet(audio_path, language=language)
  return result["text"].strip()


def collect_jobs(limit: int | None):
  audio_files = sorted(INPUT_DIR.rglob("*.mp3"))
  if limit is not None:
    audio_files = audio_files[:limit]
  jobs = []
  for audio_path in audio_files:
    rel = audio_path.relative_to(INPUT_DIR)
    out = OUTPUT_DIR / rel.with_suffix(".txt")
    jobs.append((audio_path, out, rel))
  return jobs


def main():
  parser = argparse.ArgumentParser(description="Transcribe audio with Whisper")
  parser.add_argument("--limit", type=int, default=None, help="Max files to process")
  parser.add_argument("--file", type=str, default=None, help="Single file under data/input or absolute path")
  parser.add_argument("--dry-run", action="store_true", help="List files only, no transcription")
  args = parser.parse_args()

  language = os.getenv("TRANSCRIBE_LANGUAGE", "de")

  if not INPUT_DIR.exists():
    print(f"Input directory not found: {INPUT_DIR}")
    sys.exit(1)

  if args.file:
    input_path = Path(args.file)
    if not input_path.is_absolute():
      input_path = INPUT_DIR / input_path
    if not input_path.exists():
      print(f"ERROR: File not found: {input_path}")
      sys.exit(1)
    rel = input_path.relative_to(INPUT_DIR) if INPUT_DIR in input_path.parents else Path(input_path.name)
    jobs = [(input_path, OUTPUT_DIR / rel.with_suffix(".txt"), rel)]
  else:
    jobs = collect_jobs(args.limit)

  if not jobs:
    print("No MP3 files found.")
    return

  pending = [(a, o, r) for a, o, r in jobs if not o.exists()]
  print(f"Language: {language}")
  print(f"Jobs: {len(jobs)} total, {len(pending)} to transcribe, {len(jobs) - len(pending)} already done")

  if args.dry_run:
    for audio_path, output_path, rel in jobs:
      status = "skip" if output_path.exists() else "pending"
      print(f"  [{status}] {rel} -> {output_path.relative_to(PROJECT_ROOT)}")
    return

  OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

  for i, (audio_path, output_path, rel) in enumerate(jobs, 1):
    print(f"[{i}/{len(jobs)}] {rel}")
    if output_path.exists():
      print("  Skipped (already exists)")
      continue
    try:
      text = transcribe(audio_path, language=language)
      output_path.parent.mkdir(parents=True, exist_ok=True)
      output_path.write_text(text, encoding="utf-8")
      print(f"  Saved ({len(text)} chars)")
    except Exception as e:
      print(f"  ERROR: {e}")


if __name__ == "__main__":
  main()
