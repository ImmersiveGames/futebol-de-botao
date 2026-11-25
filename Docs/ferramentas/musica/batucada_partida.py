"""Batucada chiptune para a PARTIDA do Pro Gol!: mais rápida e com mais percussão que a do menu.
WAV 44,1 kHz estéreo, loop perfeito. Grade de 16 semicolcheias por compasso."""
import numpy as np, wave, sys

SR = 44100
BPM = 124
STEP = 60 / BPM / 4
BAR = 16
rng = np.random.default_rng(11)

NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6,
        'G': 7, 'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}
def m(n): return 12 * (int(n[-1]) + 1) + NOTE[n[:-1]]
def hz(midi): return 440 * 2 ** ((midi - 69) / 12)

CH = {'G6': ['B3', 'D4', 'E4', 'G4'], 'E7': ['B3', 'D4', 'E4', 'G#4'], 'Am7': ['C4', 'E4', 'G4', 'A4'],
      'D7': ['C4', 'D4', 'F#4', 'A4'], 'Em7': ['B3', 'D4', 'E4', 'G4'], 'A7': ['C#4', 'E4', 'G4', 'A4'],
      'Cmaj7': ['B3', 'C4', 'E4', 'G4'], 'Cm6': ['A3', 'C4', 'Eb4', 'G4'], 'Bm7': ['A3', 'B3', 'D4', 'F#4']}
ROOT = {'G6': ('G1', 'D2'), 'E7': ('E2', 'B1'), 'Am7': ('A1', 'E2'), 'D7': ('D2', 'A1'), 'Em7': ('E2', 'B1'),
        'A7': ('A1', 'E2'), 'Cmaj7': ('C2', 'G1'), 'Cm6': ('C2', 'G1'), 'Bm7': ('B1', 'F#2')}
A_CH = [('G6', 'G6'), ('E7', 'E7'), ('Am7', 'Am7'), ('D7', 'D7'), ('G6', 'G6'), ('Em7', 'Em7'), ('A7', 'A7'), ('D7', 'D7')]
B_CH = [('Cmaj7', 'Cmaj7'), ('Cm6', 'Cm6'), ('Bm7', 'Bm7'), ('E7', 'E7'), ('Am7', 'Am7'), ('D7', 'D7'), ('G6', 'G6'), None]

A_MEL = [
    [(0, 2, 'B5'), (2, 2, 'D6'), (4, 2, 'B5'), (6, 2, 'G5'), (8, 1, 'A5'), (10, 2, 'B5'), (12, 4, 'D6')],
    [(0, 2, 'G#5'), (2, 2, 'B5'), (4, 2, 'D6'), (7, 2, 'E6'), (10, 2, 'D6'), (12, 4, 'B5')],
    [(0, 2, 'C6'), (2, 2, 'A5'), (4, 2, 'E5'), (6, 2, 'A5'), (8, 3, 'C6'), (11, 2, 'B5'), (13, 3, 'A5')],
    [(0, 2, 'F#5'), (2, 2, 'A5'), (4, 2, 'C6'), (6, 2, 'A5'), (8, 4, 'F#5'), (12, 2, 'E5'), (14, 2, 'D5')],
    [(0, 2, 'B5'), (2, 2, 'D6'), (4, 2, 'B5'), (6, 2, 'G5'), (8, 1, 'A5'), (10, 2, 'B5'), (12, 4, 'E6')],
    [(0, 2, 'D6'), (2, 2, 'B5'), (4, 2, 'G5'), (6, 2, 'E5'), (8, 2, 'G5'), (10, 2, 'B5'), (12, 4, 'D6')],
    [(0, 2, 'C#6'), (2, 2, 'E6'), (4, 2, 'C#6'), (6, 2, 'A5'), (8, 2, 'G5'), (10, 2, 'E5'), (12, 2, 'C#5'), (14, 2, 'E5')],
    [(0, 4, 'D5'), (4, 2, 'F#5'), (6, 2, 'A5'), (8, 2, 'C6'), (10, 2, 'A5'), (12, 1, 'F#5'), (13, 1, 'G5'), (14, 2, 'A5')],
]
B_MEL = [
    [(0, 3, 'E6'), (3, 3, 'D6'), (6, 2, 'B5'), (8, 2, 'G5'), (10, 2, 'B5'), (12, 4, 'E6')],
    [(0, 3, 'Eb6'), (3, 3, 'D6'), (6, 2, 'A5'), (8, 2, 'G5'), (10, 2, 'A5'), (12, 4, 'Eb6')],
    [(0, 3, 'D6'), (3, 3, 'B5'), (6, 2, 'F#5'), (8, 2, 'A5'), (10, 2, 'B5'), (12, 4, 'D6')],
    [(0, 2, 'E6'), (2, 2, 'D6'), (4, 2, 'B5'), (6, 2, 'G#5'), (8, 4, 'B5'), (12, 4, 'E5')],
    [(0, 2, 'A5'), (2, 2, 'C6'), (4, 2, 'E6'), (6, 2, 'C6'), (8, 2, 'A5'), (10, 2, 'G5'), (12, 4, 'E5')],
    [(0, 2, 'F#5'), (2, 2, 'A5'), (4, 2, 'C6'), (6, 2, 'D6'), (8, 2, 'C6'), (10, 2, 'A5'), (12, 2, 'F#5'), (14, 2, 'A5')],
    [(0, 4, 'G5'), (4, 2, 'B5'), (6, 2, 'D6'), (8, 4, 'D6'), (12, 4, 'B5')],
    [],   # breque: só o repique chama
]
FORM = [('A', 0), ('A', 1), ('B', 0), ('A', 2)]

# ---------- osciladores ----------
def pulse(f, n, duty, ph=0.0):
    t = (np.arange(n) / SR) * f + ph
    return np.where((t % 1.0) < duty, 1.0, -1.0)
def tri(f, n):
    p = (np.arange(n) / SR * f) % 1.0
    return np.where(p < 0.5, 4 * p - 1, 3 - 4 * p)
def env(n, a=0.004, d=0.0, s=1.0, r=0.03, hold=None):
    t = np.arange(n) / SR
    e = np.minimum(1, t / a) if a > 0 else np.ones(n)
    if d > 0: e = e * (s + (1 - s) * np.exp(-t / d))
    if hold is not None:
        rel = np.clip((t - hold) / r, 0, 1)
        e = e * (1 - rel)
    return e
def onepole(x, a):
    y = np.empty_like(x); acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc); y[i] = acc
    return y

total_bars = len(FORM) * 8
L = int(round(total_bars * BAR * STEP * SR))
mixL = np.zeros(L); mixR = np.zeros(L)

def add(sig, start_step, pan=0.0, gain=1.0):
    s0 = int(round(start_step * STEP * SR))
    idx = (np.arange(len(sig)) + s0) % L          # tail dá a volta: loop perfeito
    gl = gain * np.sqrt(0.5 * (1 - pan)); gr = gain * np.sqrt(0.5 * (1 + pan))
    np.add.at(mixL, idx, sig * gl); np.add.at(mixR, idx, sig * gr)

def lead(note, steps, octave_up, duty):
    f = hz(m(note) + (12 if octave_up else 0))
    dur = steps * STEP * 0.92
    n = int((dur + 0.08) * SR)
    t = np.arange(n) / SR
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - 0.12) / 0.1, 0, 1)
    ph = np.cumsum(f * vib / SR)
    w = np.where((ph % 1) < duty, 1.0, -1.0)
    w = onepole(w, 0.35)
    return w * env(n, a=0.006, d=0.25, s=0.75, r=0.06, hold=dur)

def pluck(midi, length=0.11):
    n = int(length * SR)
    w = onepole(pulse(hz(midi), n, 0.25), 0.4)
    return w * env(n, a=0.002, d=0.05, s=0.0)

def bass(note, steps, acc=1.0):
    dur = steps * STEP * 0.9
    n = int((dur + 0.03) * SR)
    return acc * tri(hz(m(note)), n) * env(n, a=0.003, d=0.18, s=0.6, r=0.03, hold=dur)

def surdo(acc):
    n = int(0.35 * SR); t = np.arange(n) / SR
    f = 55 + 45 * np.exp(-t / 0.04)
    return acc * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.16)

def ganza(acc):
    n = int(0.06 * SR)
    w = rng.uniform(-1, 1, n); w = w - onepole(w, 0.25)       # passa-alta
    return acc * w * env(n, a=0.004, d=0.018, s=0.0)

def tamborim(acc):
    n = int(0.07 * SR)
    w = onepole(pulse(1050, n, 0.5), 0.5) + 0.3 * rng.uniform(-1, 1, n)
    return acc * w * env(n, a=0.001, d=0.022, s=0.0)

def agogo(high):
    n = int(0.16 * SR); f = 1180 if high else 880
    t = np.arange(n) / SR
    w = np.sin(2 * np.pi * f * t) + 0.4 * np.sin(2 * np.pi * f * 2.76 * t)
    return w * np.exp(-t / 0.05)


def caixa(acc):
    n = int(0.09 * SR); t = np.arange(n) / SR
    w = rng.uniform(-1, 1, n); w = w - onepole(w, 0.15)
    tone = np.sin(2 * np.pi * 330 * t) * np.exp(-t / 0.02)
    return acc * (0.8 * w * np.exp(-t / 0.035) + 0.3 * tone)

def repique(acc):
    n = int(0.12 * SR); t = np.arange(n) / SR
    w = rng.uniform(-1, 1, n); w = w - onepole(w, 0.3)
    ring = np.sin(2 * np.pi * 620 * t) * np.exp(-t / 0.05)
    return acc * (0.7 * w * np.exp(-t / 0.03) + 0.6 * ring)

def surdo2(acc):  # surdo de segunda (mais agudo, responde no 1º tempo)
    n = int(0.3 * SR); t = np.arange(n) / SR
    f = 80 + 50 * np.exp(-t / 0.035)
    return acc * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.13)

def apito(n_sec=0.09):
    n = int(n_sec * SR); t = np.arange(n) / SR
    f = 2900 + 120 * np.sign(np.sin(2 * np.pi * 38 * t))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env(n, a=0.005, d=0.0, s=1.0, r=0.02, hold=n_sec - 0.02)

TAMB = [1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1]
CAVA = [1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1]
CAIXA_ACC = [1.0, 0.35, 0.5, 0.8, 0.35, 0.5, 0.9, 0.35, 1.0, 0.35, 0.5, 0.8, 0.35, 0.5, 0.9, 0.4]
AGOGO = [(0, 1), (3, 0), (6, 1), (8, 0), (10, 1), (11, 0), (14, 1)]
CHAMADA = [(0, 1.0), (3, 0.8), (6, 1.0), (8, 0.7), (10, 0.8), (11, 0.8), (12, 0.9), (13, 0.9), (14, 1.0), (15, 1.0)]

bar_i = 0
for sec, var in FORM:
    chs = A_CH if sec == 'A' else B_CH
    mel = A_MEL if sec == 'A' else B_MEL
    for b in range(8):
        base = bar_i * BAR
        breque = chs[b] is None
        if breque:
            for s, acc in CHAMADA:
                add(repique(acc), base + s, pan=-0.2, gain=0.16)
            add(surdo(1.0), base + 12, gain=0.42); add(surdo(1.0), base + 14, gain=0.42)
            bar_i += 1
            continue
        for s in range(16):
            add(ganza(0.6 if s % 4 == 3 else 0.35), base + s, pan=0.35, gain=0.28)
            add(caixa(CAIXA_ACC[s]), base + s, pan=0.2, gain=0.12)
            if TAMB[s]:
                add(tamborim(1.0 if s in (0, 8) else 0.7), base + s, pan=-0.35, gain=0.12)
        for half in (0, 8):
            add(surdo2(0.8), base + half, gain=0.30)
            add(surdo(1.0), base + half + 4, gain=0.45)
            if half == 8 and b % 2 == 1:                     # surdo de terceira: virada
                add(surdo2(0.6), base + half + 6, gain=0.25); add(surdo2(0.7), base + half + 7, gain=0.25)
        if var == 1 or sec == 'B':
            for s, hi in AGOGO:
                add(agogo(hi), base + s, pan=0.5, gain=0.07)
        if sec == 'B' and b == 0:
            for s in (0, 2, 4):
                add(apito(), base + s, pan=0.0, gain=0.05)
        for h, ch in enumerate(chs[b]):
            hb = base + 8 * h
            r1, r5 = ROOT[ch]
            add(bass(r1, 2, 1.0), hb + 0, gain=0.26)
            add(bass(r1, 1, 0.7), hb + 3, gain=0.26)
            add(bass(r5, 2, 0.9), hb + 4, gain=0.26)
            add(bass(r1, 1, 0.6), hb + 7, gain=0.26)
            notes = [m(x) for x in CH[ch]]
            for s in range(8):
                if CAVA[8 * h + s]:
                    acc = 1.0 if s in (0, 3, 6) else 0.75
                    for k, nn in enumerate(notes):
                        add(pluck(nn + 12, 0.09) * acc, hb + s + k * 0.025, pan=-0.45, gain=0.06)
        for st, du, nt in mel[b]:
            if var == 1:
                add(lead(nt, du, False, 0.125), base + st, pan=0.1, gain=0.12)
                add(lead(nt[:-1] + str(int(nt[-1]) - 1), du, False, 0.5), base + st, pan=-0.1, gain=0.05)
            else:
                add(lead(nt, du, False, 0.5), base + st, pan=0.1, gain=0.12)
                if var == 2 or sec == 'B':
                    add(lead(nt[:-1] + str(int(nt[-1]) - 1), du, False, 0.25), base + st, pan=-0.1, gain=0.05)
        bar_i += 1

# eco leve na melodia/geral (estéreo) e normalização
d = int(STEP * 3 * SR)
echoL = np.roll(mixR, d) * 0.18; echoR = np.roll(mixL, d) * 0.18
outL = mixL + echoL; outR = mixR + echoR
peak = max(np.abs(outL).max(), np.abs(outR).max())
outL = outL / peak * 0.85; outR = outR / peak * 0.85
out = np.stack([outL, outR], axis=1)
path = sys.argv[1] if len(sys.argv) > 1 else 'samba.wav'
with wave.open(path, 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((out * 32767).astype('<i2').tobytes())
print(path, round(L / SR, 1), 's')
