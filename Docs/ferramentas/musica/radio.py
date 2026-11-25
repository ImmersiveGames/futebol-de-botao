"""Rádio do Pro Gol!: rock, funk, sertanejo, frevo e forró em estilo chiptune (8 bits).
Uso: python3 radio.py <rock|funk|sertanejo|frevo|forro|axe|carimbo|boi|todas> [pasta]
Cada música: WAV 44,1 kHz estéreo, loop perfeito, forma A A' B A (32 compassos de 16 semicolcheias)."""
import numpy as np, wave, sys, os

SR = 44100
NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6,
        'G': 7, 'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}
def m(n): return 12 * (int(n[-1]) + 1) + NOTE[n[:-1]]
def hz(x): return 440 * 2 ** ((x - 69) / 12)

def bars(text):
    """'E5:4 G5:2 r:2 | ...' -> lista de compassos [(passo, duração, nota)]; confere 16 por compasso."""
    out = []
    for i, bar in enumerate(text.split('|')):
        pos, notes = 0, []
        for tok in bar.split():
            n, d = tok.split(':'); d = float(d)
            if n != 'r': notes.append((pos, d, n))
            pos += d
        assert abs(pos - 16) < 1e-6, f'compasso {i + 1} soma {pos}: {bar}'
        out.append(notes)
    return out

# ---------------- síntese ----------------
class Song:
    def __init__(self, bpm, nbars, seed):
        self.step = 60 / bpm / 4
        self.L = int(round(nbars * 16 * self.step * SR))
        self.l = np.zeros(self.L); self.r = np.zeros(self.L)
        self.rng = np.random.default_rng(seed)
    def add(self, sig, at, pan=0.0, gain=1.0):
        s0 = int(round(at * self.step * SR))
        idx = (np.arange(len(sig)) + s0) % self.L
        np.add.at(self.l, idx, sig * gain * np.sqrt(0.5 * (1 - pan)))
        np.add.at(self.r, idx, sig * gain * np.sqrt(0.5 * (1 + pan)))
    def noise(self, n): return self.rng.uniform(-1, 1, n)
    def write(self, path, echo=0.15, echo_steps=3):
        d = int(self.step * echo_steps * SR)
        L = self.l + np.roll(self.r, d) * echo; R = self.r + np.roll(self.l, d) * echo
        peak = max(np.abs(L).max(), np.abs(R).max())
        out = np.stack([L, R], 1) / peak * 0.85
        with wave.open(path, 'wb') as w:
            w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes((out * 32767).astype('<i2').tobytes())

def onepole(x, a):
    """Passa-baixa de um polo; 'a' pode ser número ou um valor por amostra (filtro que fecha com o tempo)."""
    y = np.empty_like(x); acc = 0.0
    a = np.broadcast_to(np.asarray(a, dtype=float), x.shape)
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc); y[i] = acc
    return y
def env(n, a=0.004, d=0.0, s=1.0, r=0.03, hold=None):
    t = np.arange(n) / SR
    e = np.minimum(1, t / a) if a > 0 else np.ones(n)
    if d > 0: e = e * (s + (1 - s) * np.exp(-t / d))
    if hold is not None: e = e * (1 - np.clip((t - hold) / r, 0, 1))
    return e
def pulse_ph(ph, duty): return np.where((ph % 1) < duty, 1.0, -1.0)
def tri_ph(ph):
    p = ph % 1; return np.where(p < 0.5, 4 * p - 1, 3 - 4 * p)
def phase(f, n, vib=0.0, vib_delay=0.12):
    t = np.arange(n) / SR
    v = 1 + vib * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - vib_delay) / 0.1, 0, 1)
    return np.cumsum(f * v / SR)

# instrumentos genéricos
def lead(S, note, steps, duty=0.5, vib=0.006, octave=0, legato=0.92, lp=0.35, attack=0.006, sustain=0.75):
    dur = steps * S.step * legato; n = int((dur + 0.08) * SR)
    w = onepole(pulse_ph(phase(hz(m(note) + 12 * octave), n, vib), duty), lp)
    return w * env(n, a=attack, d=0.25, s=sustain, r=0.06, hold=dur)
def accordion(S, note, steps, octave=0):          # sanfona: dois pulsos desafinados + tremolo
    dur = steps * S.step * 0.95; n = int((dur + 0.06) * SR); f = hz(m(note) + 12 * octave)
    t = np.arange(n) / SR
    w = pulse_ph(phase(f * 1.004, n, 0.004), 0.35) + pulse_ph(phase(f * 0.996, n, 0.004), 0.25)
    w = onepole(w, 0.3) * (1 + 0.12 * np.sin(2 * np.pi * 6 * t))
    return 0.5 * w * env(n, a=0.02, d=0.3, s=0.85, r=0.05, hold=dur)
def brass(S, note, steps, octave=0):              # metais do frevo: ataque rápido, brilho que fecha
    dur = steps * S.step * 0.85; n = int((dur + 0.05) * SR); f = hz(m(note) + 12 * octave)
    t = np.arange(n) / SR
    w = pulse_ph(phase(f, n, 0.004, 0.08), 0.5) + 0.5 * pulse_ph(phase(f * 2.003, n), 0.25)
    w = onepole(w, 0.25 + 0.35 * np.exp(-t / 0.05))
    return 0.6 * w * env(n, a=0.008, d=0.12, s=0.7, r=0.04, hold=dur)
def guitar(S, notes, steps, mute=False):         # guitarra distorcida (power chord)
    dur = steps * S.step * (0.5 if mute else 0.95); n = int((dur + 0.05) * SR)
    w = sum(pulse_ph(phase(hz(x) * (1 + 0.003 * k), n), 0.5) for k, x in enumerate(notes))
    w = np.tanh(2.5 * onepole(w, 0.45))
    return w * env(n, a=0.003, d=0.08 if mute else 0.4, s=0.3 if mute else 0.7, r=0.03, hold=dur)
def pluck(S, midi, length=0.11, duty=0.25, lp=0.4):
    n = int(length * SR)
    return onepole(pulse_ph(phase(hz(midi), n), duty), lp) * env(n, a=0.002, d=length * 0.45, s=0.0)
def bass_tri(S, note, steps, legato=0.9):
    dur = steps * S.step * legato; n = int((dur + 0.03) * SR)
    return tri_ph(phase(hz(m(note)), n)) * env(n, a=0.003, d=0.18, s=0.6, r=0.03, hold=dur)
def bass_pulse(S, note, steps, legato=0.85):
    dur = steps * S.step * legato; n = int((dur + 0.03) * SR)
    w = onepole(pulse_ph(phase(hz(m(note)), n), 0.5), 0.15) + 0.6 * tri_ph(phase(hz(m(note)), n))
    return 0.6 * w * env(n, a=0.003, d=0.12, s=0.7, r=0.03, hold=dur)
def bass808(S, note, steps):
    dur = steps * S.step; n = int((dur + 0.05) * SR); t = np.arange(n) / SR
    f = hz(m(note)) * (1 + 1.5 * np.exp(-t / 0.03))
    w = np.tanh(1.6 * np.sin(2 * np.pi * np.cumsum(f) / SR))
    return w * env(n, a=0.002, d=0.0, s=1.0, r=0.05, hold=dur - 0.05) * np.exp(-t / 0.9)
def kick(S, acc=1.0, low=50, punch=90, decay=0.12):
    n = int(0.3 * SR); t = np.arange(n) / SR
    f = low + punch * np.exp(-t / 0.03)
    return acc * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay)
def snare(S, acc=1.0, tone=200, decay=0.06):
    n = int(0.2 * SR); t = np.arange(n) / SR
    w = S.noise(n); w = w - onepole(w, 0.2)
    return acc * (0.8 * w * np.exp(-t / decay) + 0.4 * np.sin(2 * np.pi * tone * t) * np.exp(-t / 0.03))
def clap(S, acc=1.0):
    n = int(0.18 * SR); t = np.arange(n) / SR
    w = S.noise(n); w = w - onepole(w, 0.3)
    e = np.exp(-t / 0.05) * (1 + 0.8 * ((t % 0.011) < 0.004) * (t < 0.03))
    return acc * w * e
def hat(S, acc=1.0, open_=False):
    n = int((0.18 if open_ else 0.05) * SR); t = np.arange(n) / SR
    w = S.noise(n); w = w - onepole(w, 0.6)
    return acc * w * np.exp(-t / (0.06 if open_ else 0.012))
def crash(S):
    n = int(1.2 * SR); t = np.arange(n) / SR
    w = S.noise(n); w = w - onepole(w, 0.4)
    return w * np.exp(-t / 0.4)
def shaker(S, acc=1.0):
    n = int(0.06 * SR)
    w = S.noise(n); w = w - onepole(w, 0.25)
    return acc * w * env(n, a=0.004, d=0.018, s=0.0)
def tom(S, acc=1.0, f0=140):
    n = int(0.25 * SR); t = np.arange(n) / SR
    f = f0 * (1 + 0.6 * np.exp(-t / 0.04))
    return acc * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.1)
def triangle_inst(S, acc=1.0, open_=True):     # triângulo do forró
    n = int((0.35 if open_ else 0.05) * SR); t = np.arange(n) / SR
    w = sum(np.sin(2 * np.pi * f * t) for f in (3200, 4870, 6400))
    return acc * w / 3 * np.exp(-t / (0.12 if open_ else 0.012))
def zabumba(S, acc=1.0):
    n = int(0.3 * SR); t = np.arange(n) / SR
    f = 70 + 40 * np.exp(-t / 0.03)
    return acc * (np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.14) + 0.15 * S.noise(n) * np.exp(-t / 0.01))
def bacalhau(S, acc=1.0):                       # varinha no aro de baixo da zabumba
    n = int(0.05 * SR); t = np.arange(n) / SR
    return acc * (0.6 * S.noise(n) + np.sin(2 * np.pi * 1800 * t)) * np.exp(-t / 0.01)

def chord_notes(names): return [m(x) for x in names]
def form_loop(S, form, A, B, do_bar):
    """Percorre a forma chamando do_bar(base, seção, variação, índice, melodia)."""
    bi = 0
    for sec, var in form:
        mel = A if sec == 'A' else B
        for b in range(8):
            do_bar(bi * 16, sec, var, b, mel[b]); bi += 1

FORM = [('A', 0), ('A', 1), ('B', 0), ('A', 2)]

# =============================== ROCK ===============================
def rock(path):
    S = Song(140, 32, 21)
    A_CH = ['Em', 'C', 'G', 'D', 'Em', 'C', 'G', 'D']
    B_CH = ['C', 'D', 'Em', 'Em', 'C', 'D', 'B7', 'B7']
    PW = {'Em': ['E3', 'B3', 'E4'], 'C': ['C3', 'G3', 'C4'], 'G': ['G2', 'D3', 'G3'], 'D': ['D3', 'A3', 'D4'], 'B7': ['B2', 'F#3', 'B3']}
    BS = {'Em': 'E2', 'C': 'C2', 'G': 'G1', 'D': 'D2', 'B7': 'B1'}
    A = bars('E5:4 G5:2 A5:2 B5:6 A5:2 | G5:4 E5:4 r:2 E5:2 G5:2 A5:2 | B5:4 D6:4 B5:2 A5:2 G5:4 | A5:6 F#5:2 D5:4 r:4 |'
             'E5:4 G5:2 A5:2 B5:4 E6:4 | D6:2 C6:2 B5:2 G5:2 E5:4 G5:4 | B5:4 A5:2 G5:2 A5:4 B5:4 | F#5:2 A5:2 D6:4 C6:2 B5:2 A5:4')
    B = bars('G5:2 E5:2 G5:2 C6:4 B5:2 A5:2 G5:2 | F#5:2 D5:2 F#5:2 A5:4 G5:2 F#5:2 E5:2 | B5:8 G5:4 E5:4 | r:8 E5:2 F#5:2 G5:2 A5:2 |'
             'G5:2 E5:2 G5:2 C6:4 D6:2 C6:2 B5:2 | A5:2 F#5:2 A5:2 D6:4 C6:2 B5:2 A5:2 | B5:4 A5:2 G5:2 F#5:4 D#5:4 | F#5:8 r:4 B4:2 D#5:2')
    def bar(base, sec, var, b, mel):
        ch = (A_CH if sec == 'A' else B_CH)[b]
        if b == 0: S.add(crash(S), base, pan=0.3, gain=0.10)
        for s in range(0, 16, 2):
            S.add(hat(S, 1.0 if s % 4 == 0 else 0.6), base + s, pan=0.4, gain=0.22)
        for s in (0, 6, 8, 10 if b % 2 else 11):
            S.add(kick(S, 1.0), base + s, gain=0.30)
        for s in (4, 12): S.add(snare(S, 1.0), base + s, pan=-0.1, gain=0.50)
        if b == 7: [S.add(tom(S, 0.8, f), base + s, gain=0.3) for s, f in ((12, 180), (13, 150), (14, 120), (15, 100))]
        notes = chord_notes(PW[ch])
        for s in range(0, 16, 2):
            S.add(guitar(S, notes, 2, mute=(sec == 'A' and s % 8 != 0)), base + s, pan=-0.5, gain=0.10)
            S.add(bass_pulse(S, BS[ch], 2), base + s, gain=0.17)
        for st, du, nt in mel:
            S.add(lead(S, nt, du, duty=0.25 if var == 1 else 0.5, vib=0.01, lp=0.45), base + st, pan=0.25, gain=0.12)
            if var == 1 or sec == 'B': S.add(lead(S, nt, du, duty=0.125, octave=-1), base + st, pan=-0.25, gain=0.05)
    form_loop(S, FORM, A, B, bar); S.write(path)

# =============================== FUNK ===============================
VOWEL = {'o': (450, 800, 2600), 'e': (560, 1850, 2550), 'a': (750, 1250, 2550)}
def voice(S, note, steps, vowel_from='o', vowel_to='e', glide=0.35, octave=0, detune=0.0, legato=0.9):
    """Voz de coro por síntese de formantes (soma de harmônicos pesados por 3 formantes); vogal desliza de -> para."""
    dur = steps * S.step * legato; n = int((dur + 0.06) * SR); t = np.arange(n) / SR
    f0 = hz(m(note) + 12 * octave) * (1 + detune)
    ph = phase(f0, n, 0.012, 0.15)
    k = np.clip(t / max(glide * dur, 1e-3), 0, 1)[None, :]
    F = [(1 - k) * VOWEL[vowel_from][i] + k * VOWEL[vowel_to][i] for i in range(3)]
    H = int(4200 // f0)
    h = np.arange(1, H + 1)[:, None]
    fh = h * f0
    amp = sum(g * np.exp(-((fh - Fi) / bw) ** 2) for Fi, g, bw in zip(F, (1.0, 0.6, 0.25), (130, 180, 250)))
    amp = amp * (1 / h ** 0.3)
    w = (amp * np.sin(2 * np.pi * h * ph[None, :])).sum(0)
    w = w / (np.abs(w).max() + 1e-9)
    return w * env(n, a=0.02, d=0.2, s=0.85, r=0.07, hold=dur)

def tuim(S):                                       # o "tuim" do funk: laser descendo
    n = int(0.35 * SR); t = np.arange(n) / SR
    f = 2400 * np.exp(-t / 0.08) + 300
    return pulse_ph(np.cumsum(f) / SR, 0.5) * np.exp(-t / 0.12)

def funk(path):
    """Funk carioca (tamborzão) com refrão de arquibancada: "ô-lê, ô-lê-ô-lê, ô-lêêê"."""
    S = Song(130, 32, 22)
    V_CH = ['Am', 'Am', 'F', 'F', 'G', 'G', 'Em', 'Em']          # verso (riff)
    R_CH = ['F', 'G', 'Am', 'Em', 'F', 'G', 'Am', 'Em']          # refrão (coro)
    BS = {'Am': 'A1', 'F': 'F1', 'G': 'G1', 'Em': 'E1'}
    STAB = {'Am': ['A4', 'C5', 'E5'], 'F': ['A4', 'C5', 'F5'], 'G': ['B4', 'D5', 'G5'], 'Em': ['B4', 'E5', 'G5']}
    riff1 = 'A5:1 r:1 A5:1 r:1 C6:2 A5:2 r:2 E6:2 D6:2 C6:2'
    riff2 = 'r:2 A5:2 G5:2 A5:2 r:4 E5:2 G5:2'
    VERSO = bars(' | '.join([riff1, riff2] * 4))
    # refrão: (passo, duração do "lê", nota do "ô", nota do "lê"); o "ô" é a semicolcheia antes
    CHANT = [
        [(1, 3, 'C5', 'F5'), (5, 1, 'F5', 'F5'), (7, 1, 'F5', 'G5'), (9, 6, 'G5', 'A5')],
        [(1, 3, 'A5', 'G5'), (5, 1, 'G5', 'F5'), (7, 1, 'F5', 'E5'), (9, 6, 'E5', 'D5')],
        [(1, 3, 'C5', 'F5'), (5, 1, 'F5', 'F5'), (7, 1, 'F5', 'G5'), (9, 6, 'G5', 'C6')],
        [(1, 3, 'B5', 'A5'), (5, 3, 'G5', 'E5')],
    ]
    PONTE = bars('C6:4 A5:4 C6:2 D6:2 C6:4 | B5:4 G5:4 B5:2 D6:2 B5:4 | A5:2 C6:2 E6:4 D6:2 C6:2 A5:4 | G5:2 B5:2 E6:4 D6:2 B5:2 G5:4 |'
                 'C6:4 A5:4 C6:2 D6:2 C6:4 | D6:4 B5:4 D6:2 E6:2 D6:4 | E6:2 D6:2 C6:2 A5:2 C6:4 E6:4 | r:16')
    P_CH = ['F', 'G', 'Am', 'Em', 'F', 'G', 'Am', 'Em']
    # verso 8, refrão 4 (o coro canta a frase UMA vez), verso 8, ponte instrumental 8, refrão 4 = 32 compassos
    # um refrão só por volta; termina na ponte, com virada no último compasso puxando de volta para o verso (loop)
    FORMA = [('V', 0, 8), ('R', 0, 4), ('V', 1, 8), ('P', 0, 8), ('T', 0, 4)]
    T_CH = ['Am', 'F', 'G', 'Em']
    KICK = [0, 3, 6, 10]; TOMS = [(7, 0.6), (14, 0.8), (15, 0.7)]
    bi = 0
    for sec, var, nb in FORMA:
        for b in range(nb):
            base = bi * 16; bi += 1
            ch = {'V': V_CH, 'R': R_CH, 'P': P_CH, 'T': T_CH}[sec][b]
            if sec == 'T':                              # volta: só riff curto, baixo e batida; virada no fim
                last = b == nb - 1
                for s in range(16 if not last else 8):
                    S.add(hat(S, 0.7 if s % 2 else 0.4), base + s, pan=0.4, gain=0.20)
                for s in (KICK if not last else [0, 3, 6]): S.add(kick(S, 1.0, low=45, decay=0.15), base + s, gain=0.32)
                S.add(clap(S), base + 4, pan=-0.1, gain=0.30)
                if not last: S.add(clap(S), base + 12, pan=-0.1, gain=0.30)
                S.add(bass808(S, BS[ch], 3), base + 0, gain=0.26); S.add(bass808(S, BS[ch], 3), base + 10 if not last else base + 6, gain=0.26)
                for st, du, nt in VERSO[b % 2]:
                    if not last or st < 8:
                        S.add(lead(S, nt, du, duty=0.25, vib=0.0, legato=0.7, sustain=0.5), base + st, pan=0.15, gain=0.10)
                if last:
                    for k, st in enumerate((8, 9, 10, 11, 12, 13, 14, 15)):
                        S.add(tom(S, 0.6 + 0.05 * k, 200 - 12 * k), base + st, pan=-0.3 + 0.08 * k, gain=0.30)
                    S.add(tuim(S), base + 12, pan=0.3, gain=0.06)
                continue
            for s in range(16):
                S.add(hat(S, 0.7 if s % 2 else 0.4), base + s, pan=0.4, gain=0.20)
            for s in KICK: S.add(kick(S, 1.0, low=45, decay=0.15), base + s, gain=0.32)
            for s in (4, 12): S.add(clap(S), base + s, pan=-0.1, gain=0.30)
            for s, a in TOMS: S.add(tom(S, a, 160 if s == 7 else 120), base + s, pan=0.2, gain=0.28)
            S.add(bass808(S, BS[ch], 3), base + 0, gain=0.26)
            S.add(bass808(S, BS[ch], 2), base + 6, gain=0.26)
            S.add(bass808(S, BS[ch], 3), base + 10, gain=0.28)
            if b % 4 == 3: S.add(tuim(S), base + 14, pan=0.3, gain=0.05)
            if sec == 'V':
                for s in (2, 9, 13):
                    for k, x in enumerate(chord_notes(STAB[ch])):
                        S.add(pluck(S, x, 0.12, duty=0.5, lp=0.3), base + s + 0.02 * k, pan=-0.4, gain=0.09)
                for st, du, nt in VERSO[b]:
                    S.add(lead(S, nt, du, duty=0.25 if var == 0 else 0.125, vib=0.0, legato=0.7, sustain=0.5), base + st, pan=0.15, gain=0.12)
                    if var == 1: S.add(lead(S, nt, du, duty=0.5, octave=-1, vib=0.0, legato=0.7, sustain=0.5), base + st, pan=-0.15, gain=0.05)
            elif sec == 'P':
                for s in (2, 9, 13):
                    for k, x in enumerate(chord_notes(STAB[ch])):
                        S.add(pluck(S, x, 0.12, duty=0.5, lp=0.3), base + s + 0.02 * k, pan=-0.4, gain=0.09)
                for st, du, nt in PONTE[b]:
                    S.add(lead(S, nt, du, duty=0.5, vib=0.008, legato=0.85), base + st, pan=0.15, gain=0.11)
                    S.add(lead(S, nt, du, duty=0.125, octave=-1, vib=0.0, legato=0.85), base + st, pan=-0.15, gain=0.05)
            else:
                for st, du, n_o, n_le in CHANT[b % 4]:
                    # coro: várias vozes (graves e agudas, levemente desafinadas e fora de tempo) = arquibancada
                    vozes = [(0, 0.0, 0.0, 0.0), (0, 0.006, 0.12, 0.3), (0, -0.005, 0.18, -0.3), (-1, 0.003, 0.08, 0.15)]
                    if var == 1: vozes += [(0, 0.01, 0.25, 0.5), (-1, -0.007, 0.2, -0.45)]
                    for octv, det, lag, pan in vozes:
                        S.add(voice(S, n_o, 1, 'o', 'o', octave=octv, detune=det, legato=1.0), base + st - 1 + lag * 0.3, pan=pan, gain=0.10)
                        S.add(voice(S, n_le, du, 'o', 'e', glide=0.25, octave=octv, detune=det), base + st + lag * 0.3, pan=pan, gain=0.115)
                if b % 4 == 3:
                    for s in (12, 13, 14):            # palmas da galera
                        S.add(clap(S, 1.0), base + s, pan=-0.3, gain=0.22); S.add(clap(S, 0.9), base + s + 0.08, pan=0.3, gain=0.2)
                    for octv, det, pan in ((0, 0.0, 0.0), (0, 0.008, 0.4), (-1, -0.006, -0.4)):
                        S.add(voice(S, 'A5', 2, 'e', 'e', octave=octv, detune=det), base + 15, pan=pan, gain=0.09)   # "Ê!"
    S.write(path, echo=0.2, echo_steps=3)

# ============================= SERTANEJO =============================
D_MAJOR = [2, 4, 6, 7, 9, 11, 13]                  # D E F# G A B C# (em semitons, a partir de C)
def third_below(note, scale=D_MAJOR):
    x = m(note); pc = x % 12
    pcs = [p % 12 for p in scale]
    if pc not in pcs: return x - 3
    i = pcs.index(pc); j = (i - 2) % 7
    return x - ((pc - pcs[j]) % 12)
def midi_name(x):
    names = ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B']
    return names[x % 12] + str(x // 12 - 1)

def third_above(note, scale=D_MAJOR):
    x = m(note); pcs = [p % 12 for p in scale]; pc = x % 12
    if pc not in pcs: return x + 4
    i = pcs.index(pc); j = (i + 2) % 7
    return x + ((pcs[j] - pc) % 12)
def sertanejo(path):
    """Sertanejo raiz (toada): viola caipira ponteando em terças, violão de dedo (baixo + acorde), dupla aguda em terças, sem bateria."""
    S = Song(88, 16, 41)
    VOI = {'D': ['D3', 'F#3', 'A3', 'D4'], 'A7': ['C#3', 'E3', 'G3', 'A3'], 'G': ['B2', 'D3', 'G3', 'B3']}
    BS = {'D': ('D2', 'A2'), 'A7': ('A1', 'E2'), 'G': ('G1', 'D2')}
    I_CH = ['G', 'D', 'A7', 'D']
    V_CH = ['D', 'D', 'A7', 'A7', 'G', 'D', 'A7', 'D']
    R_CH = ['G', 'D', 'A7', 'D', 'G', 'D', 'A7', 'D']
    # dupla: melodia (1ª voz) + terça acima (2ª voz, a aguda), frases silábicas que terminam em nota longa
    VERSO = bars('r:4 F#4:2 G4:2 A4:4 A4:2 B4:2 | A4:4 F#4:2 E4:2 D4:8 |'
                 'r:4 E4:2 F#4:2 G4:4 G4:2 A4:2 | G4:4 E4:2 F#4:2 E4:8 |'
                 'r:4 B4:2 B4:2 B4:4 A4:2 G4:2 | A4:4 A4:2 G4:2 F#4:8 |'
                 'r:4 E4:2 F#4:2 G4:4 F#4:2 E4:2 | D4:12 r:4')
    REFRAO = bars('D5:8 B4:2 A4:2 B4:4 | A4:8 F#4:2 G4:2 A4:4 | G4:2 A4:2 B4:2 C#5:2 E5:8 | D5:12 r:4 |'
                  'D5:8 E5:2 D5:2 B4:4 | A4:8 B4:2 A4:2 F#4:4 | E4:4 G4:4 B4:2 A4:2 G4:2 E4:2 | D4:12 r:4')
    # respostas da viola nas pausas da dupla
    RESP = {1: 'r:12 A5:1 G5:1 F#5:1 E5:1', 3: 'r:12 G5:1 F#5:1 E5:1 C#5:1', 5: 'r:12 B5:1 A5:1 G5:1 F#5:1',
            7: 'r:12 F#5:1 G5:1 A5:1 B5:1'}
    RESP = {k: bars(v)[0] for k, v in RESP.items()}
    FORMA = [('V', 8), ('R', 8)]   # começa direto na dupla (introdução de viola saiu a pedido)
    def viola(nt, du, at, g=1.0):
        S.add(pluck(S, m(nt), 0.12 + 0.035 * du, duty=0.25, lp=0.5), at, pan=0.3, gain=0.14 * g)
        S.add(pluck(S, third_below(nt), 0.12 + 0.035 * du, duty=0.25, lp=0.5), at + 0.03, pan=0.3, gain=0.10 * g)
    bi = 0
    for sec, nb in FORMA:
        for b in range(nb):
            base = bi * 16; bi += 1
            ch = {'I': I_CH, 'V': V_CH, 'R': R_CH}[sec][b]
            notes = chord_notes(VOI[ch]); r1, r5 = BS[ch]
            # violão de toada: polegar no baixo (1 e 3), dedos no acorde ("bum, tchá-tchá")
            for h in (0, 8):
                S.add(pluck(S, m(r1 if h == 0 else r5), 0.35, duty=0.5, lp=0.25), base + h, pan=-0.2, gain=0.16)
                for s, a in ((2, 1.0), (3, 0.6), (6, 0.8), (7, 0.5)):
                    for k, x in enumerate(notes[1:]):
                        S.add(pluck(S, x, 0.14, duty=0.25, lp=0.45), base + h + s + 0.04 * k, pan=-0.4, gain=0.05 * a)
            if sec == 'I':
                for st, du, nt in REFRAO[4 + b]: viola(midi_name(m(nt) + 12), du, base + st)   # introdução = viola tocando o fim do refrão
                continue
            mel = (VERSO if sec == 'V' else REFRAO)[b]
            for st, du, nt in mel:
                hi = midi_name(third_above(nt))
                S.add(lead(S, nt, du, duty=0.5, vib=0.010, legato=0.95, lp=0.18), base + st, pan=-0.15, gain=0.055)
                S.add(lead(S, hi, du, duty=0.5, vib=0.012, legato=0.95, lp=0.15), base + st, pan=0.15, gain=0.04)
                S.add(voice(S, nt, du, 'a', 'o', glide=0.6, legato=0.95), base + st, pan=-0.15, gain=0.045)
                S.add(voice(S, hi, du, 'a', 'o', glide=0.6, detune=0.003, legato=0.95), base + st + 0.04, pan=0.15, gain=0.035)
            if b in RESP:
                for st, du, nt in RESP[b]: viola(nt, du, base + st, 0.8)
    S.write(path, echo=0.10, echo_steps=4)

# =============================== FREVO ===============================
def frevo(path):
    """Frevo de rua: caixa em semicolcheias, surdo no 2, metais em contratempo, melodia rápida e trombones respondendo."""
    S = Song(148, 32, 24)
    A_CH = ['F', 'C7', 'C7', 'F', 'F7', 'Bb', 'C7', 'F']
    B_CH = ['Bb', 'Bbm', 'F', 'D7', 'Gm', 'C7', 'F', 'C7']
    VOI = {'F': ['F4', 'A4', 'C5'], 'C7': ['E4', 'G4', 'Bb4'], 'F7': ['Eb4', 'F4', 'A4'], 'Bb': ['D4', 'F4', 'Bb4'],
           'Bbm': ['Db4', 'F4', 'Bb4'], 'D7': ['C4', 'F#4', 'A4'], 'Gm': ['D4', 'G4', 'Bb4']}
    BS = {'F': ('F1', 'C2'), 'C7': ('C2', 'G1'), 'F7': ('F1', 'C2'), 'Bb': ('Bb1', 'F1'), 'Bbm': ('Bb1', 'F1'), 'D7': ('D2', 'A1'), 'Gm': ('G1', 'D2')}
    A = bars('C6:1 D6:1 C6:1 A5:1 F5:2 A5:2 C6:2 F6:2 E6:2 C6:2 | Bb5:1 C6:1 Bb5:1 G5:1 E5:2 G5:2 Bb5:2 C6:2 Bb5:2 G5:2 |'
             'E5:2 G5:2 Bb5:2 C6:2 D6:2 E6:2 C6:4 | A5:1 C6:1 A5:1 F5:1 C5:4 r:4 C6:2 C6:2 |'
             'A5:1 Bb5:1 A5:1 F5:1 Eb5:2 F5:2 A5:2 C6:2 Eb6:2 C6:2 | D6:2 Bb5:2 F5:2 D5:2 F5:2 Bb5:2 D6:4 |'
             'E6:2 C6:2 G5:2 E5:2 Bb5:2 G5:2 E5:2 C5:2 | F5:4 A5:2 C6:2 F6:4 r:4')
    B = bars('F5:2 Bb5:2 D6:2 F6:4 D6:2 Bb5:2 F5:2 | F5:2 Bb5:2 Db6:2 F6:4 Db6:2 Bb5:2 F5:2 | A5:2 C6:2 F6:4 E6:2 D6:2 C6:4 |'
             'F#5:2 A5:2 C6:2 D6:2 F#6:4 D6:4 | G5:1 A5:1 Bb5:1 C6:1 D6:4 Bb5:2 G5:2 D5:4 | C6:1 D6:1 E6:1 F6:1 G6:4 E6:2 C6:2 Bb5:4 |'
             'A5:4 C6:2 A5:2 F5:4 r:4 | E5:2 G5:2 Bb5:2 C6:2 E6:2 G6:2 E6:2 C6:2')
    STABS = [0, 3, 6, 8, 11, 14]
    TROMB = [bars('r:8 C4:2 D4:2 E4:2 G4:2')[0], bars('r:12 C4:1 E4:1 G4:1 Bb4:1')[0]]
    def bar(base, sec, var, b, mel):
        ch = (A_CH if sec == 'A' else B_CH)[b]
        for s in range(16):                           # caixa do frevo: semicolcheias com acento
            S.add(snare(S, 1.0 if s % 4 == 0 else (0.7 if s % 4 == 2 else 0.4), tone=260, decay=0.03), base + s, pan=0.25, gain=0.18)
        for s in (0, 8): S.add(kick(S, 0.8, low=60), base + s, gain=0.20)
        for s in (4, 12): S.add(kick(S, 1.0, low=55), base + s, gain=0.27)
        r1, r5 = BS[ch]
        for s in (0, 4, 8, 12): S.add(bass_tri(S, r1 if s % 8 == 0 else r5, 3), base + s, gain=0.15)
        for s in STABS:                               # metais em contratempo (acordes curtos)
            for k, x in enumerate(chord_notes(VOI[ch])):
                S.add(pluck(S, x + 12, 0.09, duty=0.5, lp=0.35), base + s + 0.01 * k, pan=-0.4, gain=0.07)
        for st, du, nt in mel:
            S.add(brass(S, nt, du), base + st, pan=0.15, gain=0.16)
            if var != 0: S.add(brass(S, nt, du, octave=-1), base + st, pan=-0.15, gain=0.07)
        if sec == 'A' and b in (3, 7):                 # trombones respondendo no buraco da frase
            for st, du, nt in TROMB[b == 7]:
                S.add(brass(S, nt, du), base + st, pan=-0.3, gain=0.13)
    form_loop(S, FORM, A, B, bar); S.write(path, echo=0.08, echo_steps=4)

# =============================== FORRÓ ===============================
def forro(path):
    """Forró pé-de-serra (baião): zabumba, bacalhau, triângulo e sanfona solando em Sol mixolídio."""
    S = Song(116, 32, 25)
    A_CH = ['G', 'F', 'G', 'D7', 'G', 'F', 'C', 'D7']
    B_CH = ['C', 'C', 'G', 'G', 'D7', 'D7', 'G', 'G']
    VOI = {'G': ['D4', 'G4', 'B4'], 'F': ['C4', 'F4', 'A4'], 'C': ['C4', 'E4', 'G4'], 'D7': ['C4', 'F#4', 'A4']}
    BS = {'G': ('G1', 'D2'), 'F': ('F1', 'C2'), 'C': ('C2', 'G1'), 'D7': ('D2', 'A1')}
    A = bars('B5:2 D6:2 D6:2 B5:2 G5:4 A5:4 | C6:2 A5:2 F5:2 A5:2 C6:4 A5:4 | B5:2 D6:2 G6:4 F6:2 D6:2 B5:4 | A5:4 F#5:2 A5:2 C6:4 B5:2 A5:2 |'
             'G5:2 B5:2 D6:2 B5:2 G5:2 B5:2 D6:4 | F6:4 D6:2 C6:2 A5:4 F5:4 | E5:2 G5:2 C6:2 E6:2 D6:2 C6:2 G5:4 | A5:4 C6:2 B5:2 A5:4 F#5:2 D5:2')
    B = bars('E6:4 D6:2 C6:2 D6:4 E6:4 | G6:8 E6:4 C6:4 | D6:4 C6:2 B5:2 C6:4 D6:4 | B5:8 G5:4 r:4 |'
             'C6:4 B5:2 A5:2 B5:4 C6:4 | D6:8 F#5:4 A5:4 | G5:2 B5:2 D6:2 G6:2 F6:2 D6:2 B5:4 | G5:8 r:8')
    TRI = [0.5, 0.35, 1.0, 0.35]                     # triângulo: abafado / aberto no "e"
    def bar(base, sec, var, b, mel):
        ch = (A_CH if sec == 'A' else B_CH)[b]
        for s in range(16):
            S.add(triangle_inst(S, TRI[s % 4], open_=(s % 4 == 2)), base + s, pan=0.45, gain=0.09)
        for half in (0, 8):                           # baião: zabumba no 1 e no "a" do 1, bacalhau nos contratempos
            S.add(zabumba(S, 1.0), base + half, gain=0.30)
            S.add(zabumba(S, 0.8), base + half + 3, gain=0.30)
            S.add(zabumba(S, 0.5), base + half + 6, gain=0.22)
            for s in (2, 6): S.add(bacalhau(S, 0.8), base + half + s, pan=-0.2, gain=0.16)
        r1, r5 = BS[ch]
        for half in (0, 8):
            S.add(bass_tri(S, r1, 3, legato=0.8), base + half, gain=0.16)       # baião: tônica pontuada + quinta
            S.add(bass_tri(S, r5, 4, legato=0.8), base + half + 3, gain=0.14)
        for s in (2, 6, 10, 14):                      # sanfona de base no contratempo
            for x in chord_notes(VOI[ch]):
                S.add(pluck(S, x + 12, 0.1, duty=0.35, lp=0.3), base + s, pan=-0.4, gain=0.05)
        for st, du, nt in mel:
            S.add(accordion(S, nt, du, octave=-1), base + st, pan=0.15, gain=0.15)   # sanfona uma oitava abaixo (estava estridente)
            if var == 1 or sec == 'B': S.add(accordion(S, nt, du, octave=-2), base + st, pan=-0.15, gain=0.05)
    form_loop(S, FORM, A, B, bar); S.write(path, echo=0.08, echo_steps=4)

# =============================== AXÉ ===============================
def axe(path):
    """Axé (galope baiano): surdo, timbal na clave 3-3-2, guitarra baiana solando e coro "ê-ô" grave no refrão."""
    S = Song(132, 32, 52)
    V_CH = ['A', 'D', 'E', 'A', 'A', 'D', 'E', 'A']
    R_CH = ['D', 'E', 'C#m', 'F#m', 'D', 'E', 'A', 'A']
    VOI = {'A': ['C#4', 'E4', 'A4'], 'D': ['D4', 'F#4', 'A4'], 'E': ['B3', 'E4', 'G#4'], 'C#m': ['C#4', 'E4', 'G#4'], 'F#m': ['C#4', 'F#4', 'A4']}
    BS = {'A': 'A1', 'D': 'D2', 'E': 'E2', 'C#m': 'C#2', 'F#m': 'F#1'}
    VERSO = bars('E5:2 A5:2 C#6:2 B5:2 A5:2 E5:2 F#5:4 | F#5:2 A5:2 D6:2 C#6:2 B5:2 A5:2 F#5:4 |'
                 'G#5:2 B5:2 E6:2 D6:2 C#6:2 B5:2 G#5:4 | A5:4 C#6:4 E6:4 r:4 |'
                 'E5:2 A5:2 C#6:2 B5:2 A5:2 E5:2 F#5:4 | F#5:2 A5:2 D6:2 C#6:2 B5:2 A5:2 F#5:2 A5:2 |'
                 'B5:2 C#6:2 B5:2 G#5:2 E5:2 F#5:2 G#5:4 | A5:12 r:4')
    # coro: "ê-ô" duas vezes por compasso, grave (uma oitava abaixo do escrito), metais respondem no fim de cada chamada
    CORO = [('A4', 'F#4'), ('B4', 'G#4'), ('G#4', 'E4'), ('A4', 'F#4'), ('A4', 'F#4'), ('B4', 'G#4'), ('C#5', 'A4'), None]
    CLAVE = [0, 3, 6, 8, 11, 14]                      # timbal: 3-3-2 baiano
    FORMA = [('V', 0), ('R', 0), ('V', 1), ('R', 1)]
    bi = 0
    for sec, var in FORMA:
        for b in range(8):
            base = bi * 16; bi += 1
            ch = (V_CH if sec == 'V' else R_CH)[b]; notes = chord_notes(VOI[ch])
            roll = sec == 'R' and var == 1 and b == 7     # virada de timbal puxando o loop
            for s in range(16): S.add(shaker(S, 0.8 if s % 2 == 0 else 0.45), base + s, pan=0.35, gain=0.14)
            for s in (0, 4, 8, 12): S.add(kick(S, 1.0 if s in (4, 12) else 0.7, low=50, decay=0.16), base + s, gain=0.24)
            for s in ((4, 12) if not roll else (4,)): S.add(snare(S, 0.9, tone=230, decay=0.06), base + s, pan=-0.05, gain=0.26)
            for s in (7, 15): S.add(snare(S, 0.35, tone=230, decay=0.03), base + s, pan=-0.05, gain=0.20)
            for s in (CLAVE if not roll else [0, 3, 6]):
                S.add(tom(S, 0.8, 330 if s in (0, 8) else 260), base + s, pan=0.2, gain=0.22)
            if roll:
                for k, s in enumerate(range(8, 16)): S.add(tom(S, 0.6 + 0.05 * k, 300 - 15 * k), base + s, pan=-0.3 + 0.08 * k, gain=0.26)
            # baixo curtinho e sincopado (1, "a" do 1, 3)
            for s, d in ((0, 2), (3, 2), (8, 3)): S.add(bass_tri(S, BS[ch], d, legato=0.75), base + s, gain=0.15)
            # teclado em contratempo
            for s in (2, 6, 10, 14):
                for k, x in enumerate(notes): S.add(pluck(S, x + 12, 0.08, duty=0.5, lp=0.3), base + s + 0.01 * k, pan=-0.4, gain=0.05)
            if sec == 'V':                                 # guitarra baiana (aguda, mas filtrada)
                for st, du, nt in VERSO[b]:
                    S.add(lead(S, nt, du, duty=0.25, vib=0.006, legato=0.8, lp=0.22, sustain=0.6), base + st, pan=0.15, gain=0.10)
                    if var == 1: S.add(lead(S, nt, du, duty=0.5, octave=-1, vib=0.0, legato=0.8, lp=0.2, sustain=0.6), base + st, pan=-0.15, gain=0.06)
            else:
                pair = CORO[b]
                calls = [(0, 2, 4), (8, 2, 4)] if pair else []
                if pair is None:                           # último: "ê-ôôô" comprido (só na primeira vez; na segunda vem a virada)
                    pair = ('C#5', 'A4'); calls = [(0, 2, 8)] if not roll else [(0, 2, 4)]
                for c0, d1, d2 in calls:
                    for octv, det, lag, pan in [(-1, 0.0, 0.0, 0.0), (-1, 0.005, 0.15, 0.3), (-1, -0.004, 0.2, -0.3)]:
                        S.add(voice(S, pair[0], d1, 'e', 'e', octave=octv, detune=det, legato=1.0), base + c0 + lag * 0.3, pan=pan, gain=0.07)
                        S.add(voice(S, pair[1], d2, 'e', 'o', glide=0.2, octave=octv, detune=det), base + c0 + d1 + lag * 0.3, pan=pan, gain=0.075)
                    for k, x in enumerate(notes):          # metais respondem
                        S.add(brass(S, midi_name(x + 12), 1), base + c0 + 6, pan=-0.2, gain=0.07)
    S.write(path, echo=0.08, echo_steps=4)

# =============================== CARIMBÓ ===============================
def carimbo(path):
    """Carimbó do Pará: curimbós (tambor grave + tapa), maracas, banjo picotando no contratempo, flauta na melodia e sax respondendo."""
    S = Song(124, 32, 61)
    A_CH = ['G', 'G', 'C', 'D', 'G', 'C', 'D', 'G']
    B_CH = ['C', 'C', 'G', 'G', 'D', 'D', 'G', 'G']
    VOI = {'G': ['D4', 'G4', 'B4'], 'C': ['C4', 'E4', 'G4'], 'D': ['D4', 'F#4', 'A4']}
    A = bars('D5:3 B4:3 G4:2 A4:2 B4:2 D5:4 | E5:3 D5:3 B4:2 D5:8 | E5:3 C5:3 G4:2 A4:2 C5:2 E5:4 | F#5:3 E5:3 D5:2 A4:8 |'
             'D5:3 B4:3 G4:2 A4:2 B4:2 D5:4 | E5:2 G5:2 E5:2 C5:2 D5:4 E5:4 | D5:3 C5:3 A4:2 F#4:2 A4:2 C5:4 | B4:4 G4:8 r:4')
    B = bars('G5:4 E5:2 G5:2 E5:4 C5:4 | D5:2 E5:2 D5:2 C5:2 A4:8 | D5:4 B4:2 D5:2 B4:4 G4:4 | A4:2 B4:2 A4:2 G4:2 D4:8 |'
             'F#5:4 D5:2 F#5:2 A5:4 F#5:4 | E5:2 F#5:2 E5:2 D5:2 C5:8 | B4:2 D5:2 G5:4 F#5:2 E5:2 D5:4 | G4:8 r:8')
    # sax responde nas notas longas da flauta
    RESP = {('A', 1): 'r:8 G4:2 A4:2 B4:2 D5:2', ('A', 3): 'r:8 A4:2 B4:2 C5:2 C#5:2', ('A', 7): 'r:12 D4:1 E4:1 F#4:1 A4:1',
            ('B', 1): 'r:8 C5:2 B4:2 A4:2 G4:2', ('B', 3): 'r:8 D4:2 F#4:2 A4:2 C5:2', ('B', 5): 'r:8 A4:2 B4:2 C5:2 D5:2',
            ('B', 7): 'r:8 G4:2 A4:2 B4:2 D5:2'}
    RESP = {k: bars(v)[0] for k, v in RESP.items()}
    def bar(base, sec, var, b, mel):
        ch = (A_CH if sec == 'A' else B_CH)[b]; notes = chord_notes(VOI[ch])
        for s in range(16): S.add(shaker(S, 0.9 if s % 4 == 2 else (0.6 if s % 2 == 0 else 0.35)), base + s, pan=0.4, gain=0.14)
        for h in (0, 8):                              # curimbó: grave no 1 e no "a", tapa no 2 e no "e"
            S.add(tom(S, 1.0, 85), base + h, pan=-0.1, gain=0.34)
            S.add(tom(S, 0.8, 85), base + h + 3, pan=-0.1, gain=0.30)
            S.add(tom(S, 0.8, 170), base + h + 4, pan=0.15, gain=0.22)
            S.add(tom(S, 0.6, 170), base + h + 6, pan=0.15, gain=0.18)
            S.add(tom(S, 0.4, 170), base + h + 7, pan=0.15, gain=0.14)
        for s in (2, 6, 10, 14):                      # banjo: acorde curtinho e repicado no contratempo
            for rep in (0, 0.5):
                for k, x in enumerate(notes):
                    S.add(pluck(S, x + 12, 0.07, duty=0.125, lp=0.5), base + s + rep + 0.015 * k, pan=-0.35, gain=0.05 if rep == 0 else 0.03)
        for st, du, nt in mel:                        # flauta (som redondo, sem agudo estridente)
            S.add(lead(S, nt, du, duty=0.5, vib=0.010, legato=0.9, lp=0.16, attack=0.015), base + st, pan=0.15, gain=0.10)
            if var == 1: S.add(lead(S, nt, du, duty=0.25, octave=-1, vib=0.006, legato=0.9, lp=0.18), base + st, pan=-0.15, gain=0.05)
        for st, du, nt in RESP.get((sec, b), []):     # sax
            S.add(lead(S, nt, du, duty=0.25, octave=-1, vib=0.006, legato=0.85, lp=0.22, sustain=0.65), base + st, pan=-0.25, gain=0.10)
    form_loop(S, FORM, A, B, bar); S.write(path, echo=0.08, echo_steps=4)

# =============================== TOADA DE BOI-BUMBÁ ===============================
def boi(path):
    """Toada de boi-bumbá (Parintins): marcação grave em todo tempo, palminhas e rocar, teclado de fundo, metais épicos e coro "ô"."""
    S = Song(140, 32, 71)
    V_CH = ['Am', 'F', 'G', 'C', 'Am', 'F', 'E7', 'Am']
    R_CH = ['F', 'G', 'Em', 'Am', 'F', 'G', 'E7', 'Am']
    VOI = {'Am': ['A3', 'C4', 'E4'], 'F': ['A3', 'C4', 'F4'], 'G': ['B3', 'D4', 'G4'], 'C': ['G3', 'C4', 'E4'],
           'E7': ['G#3', 'D4', 'E4'], 'Em': ['G3', 'B3', 'E4']}
    BS = {'Am': 'A1', 'F': 'F1', 'G': 'G1', 'C': 'C2', 'E7': 'E1', 'Em': 'E1'}
    # escrito uma oitava acima; toca uma oitava abaixo (região média, sem estridência)
    VERSO = bars('E5:4 A5:4 C6:4 B5:2 A5:2 | C6:6 A5:2 F5:8 | D5:2 E5:2 G5:4 B5:4 A5:2 G5:2 | E5:12 r:4 |'
                 'E5:4 A5:4 C6:4 D6:2 C6:2 | A5:6 C6:2 F6:4 E6:2 D6:2 | B5:4 G#5:4 E5:4 D6:2 B5:2 | A5:12 r:4')
    REFRAO = bars('A5:4 C6:4 F6:6 E6:2 | D6:4 B5:4 G5:8 | G5:2 A5:2 B5:4 E6:4 D6:2 B5:2 | C6:12 r:4 |'
                  'A5:4 C6:4 F6:4 E6:2 D6:2 | D6:4 E6:4 D6:4 B5:4 | B5:4 D6:4 C6:2 B5:2 G#5:4 | A5:12 r:4')
    FORMA = [('V', 0), ('R', 0), ('V', 1), ('R', 1)]
    bi = 0
    for sec, var in FORMA:
        for b in range(8):
            base = bi * 16; bi += 1
            ch = (V_CH if sec == 'V' else R_CH)[b]; notes = chord_notes(VOI[ch])
            fill = sec == 'R' and var == 1 and b == 7
            for s in (range(16) if not fill else range(8)):   # palminhas (madeira) nos contratempos + rocar
                if s % 2: S.add(bacalhau(S, 0.8 if s % 4 == 3 else 0.5), base + s, pan=-0.35, gain=0.14)
                S.add(shaker(S, 0.7 if s % 4 == 2 else 0.4), base + s, pan=0.4, gain=0.15)
            for s in ((0, 4, 8, 12) if not fill else (0, 4)):   # marcação: tambor grave em todo tempo
                S.add(kick(S, 1.0 if s % 8 == 0 else 0.85, low=42, punch=70, decay=0.22), base + s, gain=0.26)
            for s in ((4, 12) if not fill else (4,)): S.add(snare(S, 0.5, tone=200, decay=0.05), base + s, gain=0.22)
            if fill:
                for k, s in enumerate(range(8, 16)):
                    S.add(tom(S, 0.6 + 0.05 * k, 150 - 8 * k), base + s, pan=-0.3 + 0.08 * k, gain=0.30)
            S.add(bass_tri(S, BS[ch], 4, legato=0.8), base + 0, gain=0.13)
            S.add(bass_tri(S, BS[ch], 4, legato=0.8), base + 8, gain=0.12)
            for x in notes:                                    # teclado de fundo (acorde segurado, bem filtrado)
                S.add(lead(S, midi_name(x + 12), 16, duty=0.5, vib=0.0, legato=0.98, lp=0.08, attack=0.08, sustain=0.9), base, pan=-0.2, gain=0.035)
            mel = (VERSO if sec == 'V' else REFRAO)[b]
            for st, du, nt in mel:
                S.add(brass(S, nt, du, octave=-1), base + st, pan=0.15, gain=0.15)
                if var == 1:                                   # coro "ô" suave dobrando a melodia
                    S.add(voice(S, nt, du, 'o', 'o', octave=-1, legato=0.95), base + st, pan=-0.2, gain=0.05)
                    S.add(voice(S, nt, du, 'o', 'o', octave=-1, detune=0.005, legato=0.95), base + st + 0.05, pan=0.25, gain=0.045)
    S.write(path, echo=0.08, echo_steps=4)

SONGS = {'rock': rock, 'funk': funk, 'sertanejo': sertanejo, 'frevo': frevo, 'forro': forro, 'axe': axe, 'carimbo': carimbo, 'boi': boi}
if __name__ == '__main__':
    which = sys.argv[1] if len(sys.argv) > 1 else 'todas'
    folder = sys.argv[2] if len(sys.argv) > 2 else '.'
    for name, fn in SONGS.items():
        if which in ('todas', name):
            p = os.path.join(folder, name + '.wav'); fn(p); print(p)
