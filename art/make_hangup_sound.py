"""Generates the "call ended" sound for the Long Distance Relationship portal from scratch.

Nothing here is sampled or copied from anywhere: the sound is built out of plain square waves by this script, so it is
original and has no licensing strings attached. It is a short chiptune cue: three falling blips, then a low buzz that
fades out, then bit-crushed so it sounds like an old console.

Only needs Python's standard library.

Usage:
    python make_hangup_sound.py output.wav
"""

import array
import math
import sys
import wave

RATE = 44100


def square(freq, seconds, duty, volume):
    """A pulse wave with a quick fade-out so it doesn't click."""
    count = int(RATE * seconds)
    out = []
    for i in range(count):
        phase = (i * freq / RATE) % 1.0
        value = 1.0 if phase < duty else -1.0
        fade = min(1.0, (count - i) / (RATE * 0.012))
        out.append(value * volume * fade)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def crush(samples, hold, bits):
    """Pixelate: hold each kept sample for 'hold' samples and round it to 2**bits levels."""
    levels = 2 ** (bits - 1)
    out = []
    kept = 0.0
    for i, value in enumerate(samples):
        if i % hold == 0:
            kept = round(value * levels) / levels
        out.append(kept)
    return out


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)

    samples = []
    # Three falling blips: G5, D5, G4 (a plain descending G arpeggio), with a thin 25% pulse wave.
    for freq in (784.0, 587.3, 392.0):
        samples += square(freq, 0.075, 0.25, 0.5)
        samples += silence(0.02)
    # A low buzz that fades out: the line going dead.
    buzz = square(110.0, 0.2, 0.5, 0.45)
    for i in range(len(buzz)):
        buzz[i] *= 1.0 - i / len(buzz)
    samples += buzz

    samples = crush(samples, hold=4, bits=5)

    pcm = array.array("h", [max(-32768, min(32767, int(v * 32767))) for v in samples])
    with wave.open(sys.argv[1], "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())

    print(f"wrote {sys.argv[1]} ({len(pcm) / RATE:.2f} seconds)")


if __name__ == "__main__":
    main()
