"""Samba chiptune para o Pro Gol! (partida). Gera WAV 44,1 kHz estéreo, em loop perfeito.
Grade de 16 semicolcheias por compasso 4/4 (= dois compassos de samba 2/4)."""
import numpy as np, wave, sys

SR = 44100
BPM = 104
STEP = 60 / BPM / 4            # semicolcheia
BAR = 16
rng = np.random.default_rng(3)

NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6,
        'G': 7, 'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}
def m(n):  # 'E5' -> midi
    return 12 * (int(n[-1]) + 1) + NOTE[n[:-1]]
def hz(midi): return 440 * 2 ** ((midi - 69) / 12)

# ---------- harmonia (acorde por meio compasso) ----------
CH = {
    'C6': ['C4', 'E4', 'G4', 'A4'], 'A7': ['C#4', 'E4', 'G4', 'A4'], 'Dm7': ['D4', 'F4', 'A4', 'C5'],
    'G7': ['B3', 'D4', 'F4', 'G4'], 'F6': ['A3', 'C4', 'D4', 'F4'], 'Fm6': ['Ab3', 'C4', 'D4', 'F4'],
    'Em7': ['D4', 'E4', 'G4', 'B4'], 'E7': ['D4', 'E4', 'G#4', 'B4'],
}
ROOT = {'C6': ('C2', 'G1'), 'A7': ('A1', 'E2'), 'Dm7': ('D2', 'A1'), 'G7': ('G1', 'D2'),
        'F6': ('F1', 'C2'), 'Fm6': ('F1', 'C2'), 'Em7': ('E2', 'B1'), 'E7': ('E2', 'B1')}
A_CH = [('C6', 'C6'), ('A7', 'A7'), ('Dm7', 'Dm7'), ('G7', 'G7'), ('C6', 'C6'), ('A7', 'A7'), ('Dm7', 'G7'), ('C6', 'C6')]
B_CH = [('F6', 'F6'), ('Fm6', 'Fm6'), ('Em7', 'Em7'), ('A7', 'A7'), ('Dm7', 'Dm7'), ('G7', 'G7'), ('C6', 'E7'), ('A7', 'G7')]

# ---------- melodia (passo, duração, nota) por compasso ----------
A_MEL = [
    [(0, 2, 'E5'), (2, 2, 'G5'), (4, 2, 'A5'), (6, 2, 'G5'), (8, 2, 'E5'), (11, 4, 'C5')],
    [(0, 2, 'C#5'), (3, 2, 'E5'), (6, 2, 'A5'), (8, 2, 'G5'), (10, 2, 'E5'), (13, 3, 'C#5')],
    [(0, 3, 'D5'), (3, 2, 'F5'), (6, 2, 'A5'), (8, 2, 'C6'), (10, 2, 'A5'), (12, 4, 'F5')],
    [(0, 2, 'G5'), (2, 2, 'F5'), (4, 2, 'D5'), (6, 2, 'B4'), (8, 4, 'D5'), (12, 2, 'F5'), (14, 2, 'E5')],
    [(0, 2, 'E5'), (2, 2, 'G5'), (4, 2, 'A5'), (6, 2, 'G5'), (8, 2, 'E5'), (11, 4, 'G5')],
    [(0, 2, 'A5'), (3, 2, 'G5'), (6, 2, 'E5'), (8, 2, 'C#5'), (10, 2, 'E5'), (13, 3, 'A5')],
    [(0, 2, 'F5'), (2, 2, 'A5'), (4, 2, 'C6'), (6, 2, 'A5'), (8, 2, 'B5'), (10, 2, 'G5'), (12, 2, 'F5'), (14, 2, 'D5')],
    [(0, 3, 'E5'), (3, 6, 'C5'), (12, 1, 'G4'), (13, 1, 'A4'), (14, 2, 'B4')],
]
B_MEL = [
    [(0, 2, 'A5'), (2, 2, 'C6'), (4, 4, 'D6'), (8, 2, 'C6'), (10, 2, 'A5'), (12, 4, 'F5')],
    [(0, 2, 'Ab5'), (2, 2, 'C6'), (4, 4, 'D6'), (8, 2, 'C6'), (10, 2, 'Ab5'), (12, 4, 'F5')],
    [(0, 3, 'G5'), (3, 2, 'B5'), (6, 2, 'D6'), (8, 2, 'B5'), (10, 2, 'G5'), (12, 4, 'E5')],
    [(0, 2, 'E5'), (2, 2, 'G5'), (4, 2, 'A5'), (6, 2, 'C#6'), (8, 4, 'E6'), (12, 2, 'C#6'), (14, 2, 'A5')],
    [(0, 3, 'F5'), (3, 3, 'A5'), (6, 2, 'D6'), (8, 2, 'C6'), (10, 2, 'A5'), (12, 2, 'F5'), (14, 2, 'D5')],
    [(0, 2, 'B4'), (2, 2, 'D5'), (4, 2, 'F5'), (6, 2, 'A5'), (8, 2, 'G5'), (10, 2, 'F5'), (12, 2, 'D5'), (14, 2, 'B4')],
    [(0, 4, 'C5'), (4, 2, 'E5'), (6, 2, 'G5'), (8, 2, 'B5'), (10, 2, 'G#5'), (12, 4, 'E5')],
    [(0, 2, 'C#5'), (2, 2, 'E5'), (4, 2, 'G5'), (6, 2, 'A5'), (8, 2, 'B5'), (10, 2, 'A5'), (12, 1, 'G5'), (13, 1, 'A5'), (14, 2, 'B5')],
]
# forma do loop: A A B A  (a 2ª vez do A com a melodia uma oitava acima e agogô)
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

TAMB = [1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1]           # teleco-teco
CAVA = [1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1]           # partido-alto
GANZA_ACC = [0.55, 0.3, 0.38, 0.75]
AGOGO = [(0, 1), (3, 0), (6, 1), (8, 0), (10, 1), (11, 0), (14, 1)]

bar_i = 0
for sec, var in FORM:
    chs = A_CH if sec == 'A' else B_CH
    mel = A_MEL if sec == 'A' else B_MEL
    for b in range(8):
        base = bar_i * BAR
        # percussão
        for s in range(16):
            add(ganza(GANZA_ACC[s % 4]), base + s, pan=0.35, gain=0.25)
            if TAMB[s] and not (sec == 'A' and var == 0 and b < 2):
                add(tamborim(1.0 if s in (0, 8) else 0.7), base + s, pan=-0.3, gain=0.12)
        for half in (0, 8):
            add(surdo(0.55), base + half, gain=0.42)
            add(surdo(1.0), base + half + 4, gain=0.42)
        if var == 1 or sec == 'B':
            for s, hi in AGOGO:
                add(agogo(hi), base + s, pan=0.5, gain=0.06)
        # baixo e cavaquinho por meio compasso
        for h, ch in enumerate(chs[b]):
            hb = base + 8 * h
            r1, r5 = ROOT[ch]
            add(bass(r1, 3, 1.0), hb + 0, gain=0.26)
            add(bass(r1, 1, 0.7), hb + 3, gain=0.26)
            add(bass(r5, 4, 0.9), hb + 4, gain=0.26)
            notes = [m(x) for x in CH[ch]]
            for s in range(8):
                if CAVA[8 * h + s]:
                    acc = 1.0 if s in (0, 3, 6) else 0.75
                    for k, nn in enumerate(notes):
                        add(pluck(nn + 12) * acc, hb + s + k * 0.03, pan=-0.45, gain=0.06)
        # melodia
        for st, du, nt in mel[b]:
            if var == 1:
                add(lead(nt, du, True, 0.125), base + st, pan=0.1, gain=0.11)
            else:
                add(lead(nt, du, False, 0.5), base + st, pan=0.1, gain=0.12)
                if var == 2 or sec == 'B':  # dobra uma 8ª abaixo, mais cheio
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
