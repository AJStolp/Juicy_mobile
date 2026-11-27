"""Synthesizes every sound and music loop in the game from code.

Everything here is original, generated from oscillators and noise, so no
license or credit is owed. Run it from the repo root:

    python3 ArtSource/Audio/build_audio.py

It writes 16 bit mono WAVs into Assets/Resources/Audio/ (sound effects) and
Assets/Resources/Audio/Music/ (three seamless loops). Re-run after any edit;
the output is the same on every run (fixed noise seeds).
"""

import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join("Assets", "Resources", "Audio")
MUSIC = os.path.join(OUT, "Music")
TAU = 2.0 * math.pi


# ---------------------------------------------------------------- buffers

class Buffer:
    """A mono float buffer. Loops wrap notes that run past the end back to
    the start, so the loop point is seamless."""

    def __init__(self, seconds, loop=False):
        self.n = int(round(seconds * RATE))
        self.data = [0.0] * self.n
        self.loop = loop

    def add(self, start_seconds, samples, gain=1.0):
        start = int(round(start_seconds * RATE))
        for i, s in enumerate(samples):
            j = start + i
            if j >= self.n:
                if not self.loop:
                    break
                j %= self.n
            self.data[j] += s * gain

    def normalize(self, peak=0.89):
        top = max(1e-9, max(abs(s) for s in self.data))
        k = peak / top
        self.data = [s * k for s in self.data]

    def write(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with wave.open(path, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(RATE)
            frames = bytearray()
            for s in self.data:
                v = max(-1.0, min(1.0, s))
                frames += struct.pack("<h", int(v * 32767))
            w.writeframes(bytes(frames))
        print("wrote", path, "%.2fs" % (self.n / RATE))


# ---------------------------------------------------------------- voices

def env(i, n, attack, release):
    """Linear attack, exponential style release over the note."""
    t = i / RATE
    a = min(1.0, t / attack) if attack > 0 else 1.0
    left = (n - i) / RATE
    r = min(1.0, left / release) if release > 0 else 1.0
    return a * r


def tone(freq, seconds, shape="sine", attack=0.005, decay=None, release=0.03, vibrato=0.0, glide_to=None):
    """One note. decay: seconds to fall to about a third (plucked); None holds."""
    n = int(seconds * RATE)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = freq if glide_to is None else freq + (glide_to - freq) * (i / n)
        if vibrato:
            f *= 1.0 + vibrato * math.sin(TAU * 5.5 * t)
        phase += TAU * f / RATE
        if shape == "sine":
            s = math.sin(phase)
        elif shape == "tri":
            s = 2.0 / math.pi * math.asin(math.sin(phase))
        elif shape == "square":
            s = 0.7 if math.sin(phase) >= 0 else -0.7
        elif shape == "saw":
            s = ((phase / TAU) % 1.0) * 2.0 - 1.0
            s *= 0.6
        elif shape == "bell":
            s = math.sin(phase) + 0.35 * math.sin(phase * 2.76) + 0.18 * math.sin(phase * 5.4)
            s *= 0.65
        elif shape == "rhodes":
            s = math.sin(phase + 0.9 * math.sin(phase * 1.0) * math.exp(-t * 6)) * 0.8
        else:
            raise ValueError(shape)
        e = env(i, n, attack, release)
        if decay:
            e *= math.exp(-t / decay)
        out[i] = s * e
    return out


def noise(seconds, seed, color="white", attack=0.002, decay=None, release=0.02, cutoff=None, highpass=None):
    rnd = random.Random(seed)
    n = int(seconds * RATE)
    out = [0.0] * n
    low = 0.0
    prev_in = 0.0
    prev_hp = 0.0
    a_lp = None if cutoff is None else 1.0 - math.exp(-TAU * cutoff / RATE)
    a_hp = None if highpass is None else math.exp(-TAU * highpass / RATE)
    for i in range(n):
        x = rnd.uniform(-1.0, 1.0)
        if a_lp is not None:
            low += a_lp * (x - low)
            x = low
        if a_hp is not None:
            y = a_hp * (prev_hp + x - prev_in)
            prev_in = x
            prev_hp = y
            x = y
        t = i / RATE
        e = env(i, n, attack, release)
        if decay:
            e *= math.exp(-t / decay)
        out[i] = x * e
    return out


def lowpass(samples, cutoff):
    a = 1.0 - math.exp(-TAU * cutoff / RATE)
    y = 0.0
    out = []
    for x in samples:
        y += a * (x - y)
        out.append(y)
    return out


def mix(*parts):
    n = max(len(p) for p, _ in parts)
    out = [0.0] * n
    for p, g in parts:
        for i, s in enumerate(p):
            out[i] += s * g
    return out


def note(name):
    """'A4' style names to Hz."""
    names = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}
    pitch = name[:-1]
    octave = int(name[-1])
    semis = names[pitch] + 12 * (octave + 1) - 69
    return 440.0 * 2 ** (semis / 12.0)


def kick(seconds=0.35, punch=1.0):
    return mix((tone(110, seconds, "sine", attack=0.001, decay=0.09, glide_to=42), punch),
               (noise(0.02, 7, cutoff=4000, decay=0.006), 0.3))


def hat(seed, seconds=0.06, open_=False):
    return noise(0.25 if open_ else seconds, seed, highpass=7000, decay=0.08 if open_ else 0.018, release=0.01)


def brush(seed):
    return noise(0.18, seed, highpass=2500, cutoff=9000, attack=0.02, decay=0.07)


def snare(seed):
    return mix((noise(0.22, seed, highpass=1200, decay=0.06), 0.8), (tone(190, 0.12, "tri", decay=0.04), 0.5))


# ---------------------------------------------------------------- effects

def effects():
    fx = {}
    # A correct sort: a bright coin ding (pitch rises with the streak in game).
    fx["sort_ding"] = mix((tone(note("G6"), 0.35, "bell", decay=0.09), 0.7),
                          (tone(note("D7"), 0.25, "sine", decay=0.05), 0.35),
                          (noise(0.015, 1, highpass=5000, decay=0.004), 0.25))
    # A wrong chest: a dull thud with a sour buzz.
    fx["sort_wrong"] = mix((tone(90, 0.3, "sine", decay=0.07, glide_to=60), 0.9),
                           (tone(155, 0.28, "square", decay=0.09), 0.25),
                           (tone(164, 0.28, "square", decay=0.09), 0.25))
    # A gem lost to the ground: a soft falling whoosh and a tap.
    fx["miss"] = mix((lowpass(noise(0.32, 2, attack=0.03, decay=0.12), 1400), 1.0),
                     (tone(70, 0.15, "sine", decay=0.04), 0.5))
    # Dynamite flicked away: a rising whoosh and a fuse fizz.
    fx["flick"] = mix((noise(0.3, 3, highpass=900, attack=0.05, decay=0.12), 0.8),
                      (tone(300, 0.3, "sine", decay=0.1, glide_to=900), 0.25),
                      (noise(0.25, 4, highpass=6000, decay=0.08), 0.3))
    # Dynamite going off: a deep boom with debris.
    fx["boom"] = mix((tone(80, 0.9, "sine", attack=0.002, decay=0.25, glide_to=32), 1.0),
                     (lowpass(noise(0.9, 5, decay=0.22), 900), 0.9),
                     (noise(0.5, 6, highpass=3000, decay=0.1), 0.2))
    # Cash caught: cha-ching.
    fx["cash"] = mix((noise(0.05, 8, highpass=3000, decay=0.015), 0.5),
                     (tone(note("E6"), 0.6, "bell", attack=0.004, decay=0.18), 0.55),
                     (tone(note("B6"), 0.6, "bell", attack=0.004, decay=0.2), 0.45))
    cash_late = tone(note("E7"), 0.5, "bell", decay=0.16)
    b = Buffer(0.8)
    b.add(0, fx["cash"])
    b.add(0.09, cash_late, 0.4)
    fx["cash"] = b.data
    # A streak tier reached: a quick rising arpeggio.
    b = Buffer(0.6)
    for k, n_ in enumerate(["C6", "E6", "G6", "C7"]):
        b.add(k * 0.06, tone(note(n_), 0.3, "bell", decay=0.1), 0.5)
    fx["streak_up"] = b.data
    # Level won: a short fanfare.
    b = Buffer(1.8)
    for k, n_ in enumerate(["C5", "E5", "G5"]):
        b.add(k * 0.1, tone(note(n_), 0.25, "square", decay=0.12), 0.25)
        b.add(k * 0.1, tone(note(n_), 0.25, "bell", decay=0.12), 0.3)
    for n_ in ["C5", "E5", "G5", "C6"]:
        b.add(0.32, tone(note(n_), 1.4, "rhodes", attack=0.01, decay=0.6, release=0.3), 0.22)
    b.add(0.32, tone(note("C6"), 1.2, "bell", decay=0.4), 0.3)
    fx["level_won"] = b.data
    # Level failed: three falling notes and a low hum.
    b = Buffer(1.5)
    for k, n_ in enumerate(["E4", "D#4", "D4"]):
        b.add(k * 0.22, tone(note(n_), 0.4, "tri", decay=0.2), 0.45)
    b.add(0.66, tone(note("C#3"), 0.9, "saw", attack=0.02, decay=0.4, release=0.3), 0.3)
    fx["level_failed"] = b.data
    # The scratch card: a short grainy scrape, played over and over as the
    # finger rubs.
    fx["scratch"] = noise(0.11, 9, highpass=1800, cutoff=7000, attack=0.01, decay=0.05)
    # The clue revealed: sparkles climbing.
    b = Buffer(1.1)
    for k, n_ in enumerate(["A6", "C#7", "E7", "A7", "E7"]):
        b.add(k * 0.07, tone(note(n_), 0.5, "bell", decay=0.15), 0.35)
    b.add(0, noise(0.6, 10, highpass=8000, attack=0.05, decay=0.2), 0.12)
    fx["reveal"] = b.data
    # Pinning the clue to the board: a thumbtack press.
    fx["pin"] = mix((tone(220, 0.12, "sine", decay=0.03, glide_to=160), 0.8),
                    (noise(0.03, 11, highpass=2500, decay=0.008), 0.5))
    # Any tap on a menu: a soft click.
    fx["tap"] = mix((noise(0.02, 12, highpass=3000, decay=0.004), 0.7), (tone(1400, 0.03, "sine", decay=0.008), 0.3))
    # A vault stage locking: a heavy metal clank.
    fx["vault_unbolt"] = mix((noise(0.4, 13, highpass=600, decay=0.07), 0.6),
                             (tone(140, 0.5, "square", decay=0.1, glide_to=110), 0.3),
                             (tone(523, 0.6, "bell", decay=0.15), 0.25),
                             (tone(741, 0.5, "bell", decay=0.12), 0.18))
    # The vault cracked: a boom, a rising rush and a bright chord.
    b = Buffer(2.6)
    b.add(0, fx["boom"], 0.8)
    b.add(0.1, noise(1.2, 14, highpass=600, attack=0.8, decay=0.6), 0.35)
    for n_ in ["C5", "G5", "C6", "E6", "G6"]:
        b.add(0.9, tone(note(n_), 1.6, "bell", decay=0.6, release=0.4), 0.22)
    fx["vault_crack"] = b.data
    return fx


# ---------------------------------------------------------------- music

def swing(beat, amount=0.0):
    """Off beat eighths pushed late for a swing feel."""
    whole = math.floor(beat * 2 + 1e-6)
    if whole % 2 == 1:
        return beat + amount
    return beat


def map_theme():
    """The map: a slow heist jazz loop in D minor, walking bass, brushes,
    rhodes chords and a vibraphone line."""
    bpm = 96
    beat = 60.0 / bpm
    bars = 8
    b = Buffer(bars * 4 * beat, loop=True)
    chords = [["D3", "F3", "A3", "C4"], ["G3", "B3", "D4", "F4"], ["C3", "E3", "G3", "B3"], ["A2", "C#3", "E3", "G3"]] * 2
    walk = [["D2", "F2", "A2", "C3"], ["G2", "A2", "B2", "D3"], ["C2", "E2", "G2", "A2"], ["A1", "C#2", "E2", "G2"]] * 2
    melody = [("A4", 0, 1.5), ("F4", 1.5, 0.5), ("E4", 2, 1), ("D4", 3, 1),
              ("D4", 4, 1), ("F4", 5, 1), ("B4", 6, 1.5), ("A4", 7.5, 0.5),
              ("G4", 8, 2), ("E4", 10, 1), ("C5", 11, 1),
              ("A4", 12, 3), (None, 15, 1)]
    for bar in range(bars):
        t0 = bar * 4 * beat
        for k, n_ in enumerate(chords[bar]):
            b.add(t0 + 0.02 * k, tone(note(n_), 3.6 * beat, "rhodes", attack=0.02, decay=1.4, release=0.4), 0.11)
        for q in range(4):
            b.add(t0 + q * beat, tone(note(walk[bar][q]), beat * 0.95, "tri", attack=0.004, decay=0.35), 0.5)
            b.add(t0 + q * beat, brush(100 + bar * 4 + q), 0.12)
            b.add(t0 + swing(q + 0.5, 0.17) * beat, hat(200 + bar * 4 + q), 0.09)
        if bar % 2 == 0:
            b.add(t0, kick(0.3, 0.7), 0.35)
    for half in range(2):
        for n_, at, length in melody:
            if n_ is None:
                continue
            b.add((half * 16 + at) * beat, tone(note(n_), length * beat, "bell", attack=0.005, decay=0.5, release=0.1), 0.2)
    b.normalize(0.8)
    return b


def level_theme():
    """Levels: a driving loop in A minor, four on the floor, offbeat hats,
    pumping bass and synth stabs."""
    bpm = 124
    beat = 60.0 / bpm
    bars = 8
    b = Buffer(bars * 4 * beat, loop=True)
    roots = ["A1", "A1", "F1", "G1", "A1", "A1", "F1", "E1"]
    stabs = [["A3", "C4", "E4"], ["A3", "C4", "E4"], ["F3", "A3", "C4"], ["G3", "B3", "D4"],
             ["A3", "C4", "E4"], ["A3", "C4", "E4"], ["F3", "A3", "C4"], ["E3", "G#3", "B3"]]
    for bar in range(bars):
        t0 = bar * 4 * beat
        for q in range(4):
            b.add(t0 + q * beat, kick(0.3), 0.55)
            b.add(t0 + (q + 0.5) * beat, hat(300 + bar * 4 + q, open_=(q == 3)), 0.12)
            if q in (1, 3):
                b.add(t0 + q * beat, snare(400 + bar * 4 + q), 0.25)
            root = note(roots[bar])
            for e in range(2):
                b.add(t0 + (q + e * 0.5) * beat, tone(root * (2 if e else 1), beat * 0.45, "saw", attack=0.003, decay=0.12), 0.28)
        for hit in (0.0, 1.5, 3.0):
            for n_ in stabs[bar]:
                b.add(t0 + hit * beat, lowpass(tone(note(n_), beat * 0.4, "saw", attack=0.003, decay=0.09), 2400), 0.08)
    lead = [("E5", 0), ("D5", 0.75), ("C5", 1.5), ("A4", 2.5), ("C5", 4), ("D5", 4.75), ("E5", 5.5), ("G5", 6.5)]
    for rep in range(4):
        for n_, at in lead:
            b.add((rep * 8 + at) * beat, tone(note(n_), 0.6 * beat, "square", attack=0.005, decay=0.15), 0.06)
    b.normalize(0.8)
    return b


def vault_theme():
    """The vault: a tense pulse in C minor, ticking clock, low ostinato and
    rising string stabs."""
    bpm = 138
    beat = 60.0 / bpm
    bars = 8
    b = Buffer(bars * 4 * beat, loop=True)
    ostinato = ["C2", "C2", "D#2", "C2", "G2", "C2", "F#2", "G2"]
    for bar in range(bars):
        t0 = bar * 4 * beat
        for e in range(8):
            n_ = ostinato[e]
            if bar >= 6:
                n_ = ["G#1", "G#1", "C2", "G#1", "D#2", "G#1", "D2", "D#2"][e] if bar == 6 else ["G1", "G1", "B1", "G1", "D2", "G1", "F2", "D2"][e]
            b.add(t0 + e * 0.5 * beat, tone(note(n_), beat * 0.45, "saw", attack=0.002, decay=0.1), 0.32)
            b.add(t0 + e * 0.5 * beat, hat(500 + bar * 8 + e, 0.03), 0.1 if e % 2 else 0.16)
        b.add(t0, kick(0.4, 1.2), 0.6)
        b.add(t0 + 2 * beat, kick(0.4, 1.0), 0.45)
        for n_ in (["C4", "D#4", "G4"] if bar < 6 else (["G#3", "C4", "D#4"] if bar == 6 else ["G3", "B3", "D4"])):
            b.add(t0, tone(note(n_), 3.8 * beat, "saw", attack=0.6, release=0.4, vibrato=0.004), 0.05)
    b.normalize(0.8)
    return b


def main():
    for name, samples in effects().items():
        buf = Buffer(len(samples) / RATE)
        buf.data = list(samples)
        buf.normalize(0.85)
        buf.write(os.path.join(OUT, name + ".wav"))
    map_theme().write(os.path.join(MUSIC, "music_map.wav"))
    level_theme().write(os.path.join(MUSIC, "music_level.wav"))
    vault_theme().write(os.path.join(MUSIC, "music_vault.wav"))


if __name__ == "__main__":
    main()
