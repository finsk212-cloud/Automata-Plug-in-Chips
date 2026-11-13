"""Turns a normal WAV file into a "pixelated" (8-bit console style) version.

Two things make a sound feel pixelated:
  * a very low sample rate: the sound is rebuilt from few samples, each one held until the next ("sample and hold"),
    which turns smooth waves into stair-steps and adds that gritty, aliased buzz;
  * a very low bit depth: each sample is rounded to one of only a few levels.

Only needs Python's standard library. Works on 16-bit PCM WAV files (the usual kind). The result keeps the
original sample rate, so it plays at the right speed and tModLoader needs no special handling.

Usage:
    python pixelate_sound.py input.wav output.wav [--rate 6000] [--bits 5] [--mono]

    --rate   effective sample rate in Hz (lower = more pixelated, default 6000)
    --bits   bit depth to round to (lower = more crunchy, default 5)
    --mono   mix the sound down to a single channel
"""

import argparse
import array
import sys
import wave


def pixelate(samples, channels, source_rate, target_rate, bits):
    # Hold each kept sample for 'step' source samples (frames, not individual channel values).
    step = max(1, round(source_rate / target_rate))
    levels = 2 ** (bits - 1)  # positive levels; the sound is signed
    frames = len(samples) // channels
    out = array.array("h", [0] * len(samples))

    held = [0] * channels
    for frame in range(frames):
        if frame % step == 0:
            for ch in range(channels):
                value = samples[frame * channels + ch] / 32768.0
                held[ch] = round(value * levels) / levels
        for ch in range(channels):
            out[frame * channels + ch] = max(-32768, min(32767, int(held[ch] * 32767)))
    return out


def main():
    parser = argparse.ArgumentParser(description="Make a WAV file sound pixelated (low sample rate + low bit depth).")
    parser.add_argument("input")
    parser.add_argument("output")
    parser.add_argument("--rate", type=int, default=6000, help="effective sample rate in Hz (default 6000)")
    parser.add_argument("--bits", type=int, default=5, help="bit depth (default 5)")
    parser.add_argument("--mono", action="store_true", help="mix down to one channel")
    args = parser.parse_args()

    with wave.open(args.input, "rb") as src:
        channels = src.getnchannels()
        width = src.getsampwidth()
        rate = src.getframerate()
        raw = src.readframes(src.getnframes())

    if width != 2:
        sys.exit("Only 16-bit WAV files are supported. Re-export the sound as 16-bit PCM WAV and try again.")

    samples = array.array("h")
    samples.frombytes(raw)

    if args.mono and channels > 1:
        mixed = array.array("h")
        for frame in range(len(samples) // channels):
            total = sum(samples[frame * channels + ch] for ch in range(channels))
            mixed.append(int(total / channels))
        samples, channels = mixed, 1

    result = pixelate(samples, channels, rate, args.rate, args.bits)

    with wave.open(args.output, "wb") as dst:
        dst.setnchannels(channels)
        dst.setsampwidth(2)
        dst.setframerate(rate)
        dst.writeframes(result.tobytes())

    print(f"{args.input} -> {args.output}  ({rate} Hz source, held to ~{args.rate} Hz, {args.bits}-bit, {channels} channel(s))")


if __name__ == "__main__":
    main()
