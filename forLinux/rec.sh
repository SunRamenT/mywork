#!/bin/bash
# ミーティングの画面を録画する用
set -euo pipefail

if [ $# -lt 1 ]; then
  echo "usage: $0 <output-name>"
  exit 1
fi

VIDEO=$(mktemp ~/.video-XXXXXX.mp4)
AUDIO=$(mktemp ~/.audio-XXXXXX.wav)
OUTPUT=~/research/note/"$1".mp4

cleanup() {
  echo "Stopping..."
  kill $(jobs -p) 2>/dev/null || true
  wait || true
  echo "Merging..."
  if ffmpeg -y -i "$VIDEO" -i "$AUDIO" -c:v copy -c:a aac "$OUTPUT"; then
    echo "Done."
  else
    echo "ffmpeg merge failed" >&2
  fi
  rm -f "$VIDEO" "$AUDIO"
}

trap cleanup SIGINT

wf-recorder -f "$VIDEO" &
pw-record "$AUDIO" &
wait
