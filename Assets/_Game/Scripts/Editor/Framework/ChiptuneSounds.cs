using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Gera os sons provisórios (chiptune) em WAV mono 16 bits, 22050 Hz. Só escreve os que ainda não existem,
    /// então um som de verdade com o mesmo nome não é sobrescrito.
    /// </summary>
    public static class ChiptuneSounds
    {
        private const int Rate = 22050;
        private static System.Random random;

        /// <summary>Escreve em <paramref name="folder"/> os sons que faltam. Devolve quantos criou.</summary>
        public static int WriteMissing(string folder)
        {
            Directory.CreateDirectory(folder);
            random = new System.Random(7);
            int created = 0;

            void Write(string name, Func<List<float>> make, float peak = 0.9f)
            {
                string path = Path.Combine(folder, name + ".wav");
                if (File.Exists(path)) return;
                WriteWav(path, make(), peak);
                created++;
            }

            Write("peteleco", Flick);
            Write("batida_fraca", () => Clack(1900f, 0.05f, 0.25f), 0.45f);
            Write("batida_forte", () => Clack(1500f, 0.08f, 0.35f));
            Write("bola_parede", Thud, 0.7f);
            Write("apito", () => Whistle(0.45f));
            Write("apito_final", () => Concat(Whistle(0.35f), Silence(0.12f), Whistle(0.35f), Silence(0.12f), Whistle(0.9f)));
            Write("cartao", Card, 0.6f);
            Write("gol", Goal);
            Write("musica_menu", () => Music(112f, MenuMelody, MenuBass), 0.6f);
            Write("musica_partida", () => Music(132f, MatchMelody, MatchBass), 0.55f);
            return created;
        }

        // ---- Sons ----

        // Peteleco: estalo curto do dedo no botão.
        private static List<float> Flick()
        {
            int total = N(0.07f);
            var s = new List<float>(total);
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Max(900f - 6000f * t, 200f);
                s.Add(Env(i, total, 0.001f, 4f) * (0.6f * Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Noise()));
            }
            return s;
        }

        // Batida de acrílico: "toc" agudo.
        private static List<float> Clack(float freq, float length, float noiseAmount)
        {
            int total = N(length);
            var s = new List<float>(total);
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)Rate;
                s.Add(Env(i, total, 0.0005f, 5f) *
                      (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2.7f * t) + noiseAmount * Noise()));
            }
            return s;
        }

        // Bola na parede: baque surdo.
        private static List<float> Thud()
        {
            int total = N(0.12f);
            var s = new List<float>(total);
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Max(260f - 900f * t, 90f);
                s.Add(Env(i, total, 0.001f, 3f) * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Noise()));
            }
            return LowPass(s, 0.35f);
        }

        // Apito: tom agudo com vibrato.
        private static List<float> Whistle(float length)
        {
            int total = N(length);
            var s = new List<float>(total);
            double phase = 0;
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)Rate;
                float f = 2900f + 120f * Mathf.Sin(2f * Mathf.PI * 28f * t);
                phase += f / Rate;
                float a = Mathf.Min(1f, t / 0.02f) * Mathf.Min(1f, (length - t) / 0.04f);
                s.Add(a * (0.8f * Mathf.Sin(2f * Mathf.PI * (float)(phase % 1.0)) + 0.15f * Noise()));
            }
            return s;
        }

        // Cartão: dois bipes quadrados descendo.
        private static List<float> Card()
        {
            var s = new List<float>();
            foreach (int midi in new[] { 84, 77 })
            {
                int total = N(0.12f);
                float f = NoteFrequency(midi);
                for (int i = 0; i < total; i++) s.Add(Env(i, total, 0.002f, 1.5f) * Square(f * i / Rate, 0.25f));
                s.AddRange(Silence(0.03f));
            }
            return s;
        }

        // Gol: torcida (ruído filtrado que cresce e some) com fanfarra chiptune.
        private static List<float> Goal()
        {
            int total = N(2.2f);
            var crowdNoise = new List<float>(total);
            for (int i = 0; i < total; i++) crowdNoise.Add(Noise());
            var crowd = LowPass(crowdNoise, 0.12f);

            var s = new List<float>(total);
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)Rate;
                float swell = Mathf.Min(1f, t / 0.3f) * Mathf.Max(0f, 1f - Mathf.Max(0f, t - 1.2f) / 1f);
                s.Add(0.9f * crowd[i] * swell);
            }

            int[] fanfare = { 72, 76, 79, 84, 79, 84 };
            int pos = 0;
            for (int k = 0; k < fanfare.Length; k++)
            {
                int count = N(k < fanfare.Length - 1 ? 0.11f : 0.5f);
                float f = NoteFrequency(fanfare[k]);
                for (int i = 0; i < count && pos + i < total; i++)
                    s[pos + i] += 0.45f * Env(i, count, 0.002f, 0.8f) * Square(f * i / Rate, 0.5f);
                pos += count;
            }
            return s;
        }

        // ---- Música em loop: melodia quadrada, baixo triangular e chimbal de ruído (colcheias) ----

        private const int C = 60, D = 62, E = 64, F = 65, G = 67, A = 69, B = 71, R = -1; // R = pausa

        private static readonly int[] MenuMelody =
        {
            E + 12, R, G + 12, E + 12, D + 12, R, C + 12, R, D + 12, E + 12, R, C + 12, A, R, R, R,
            F + 12, R, A + 12, F + 12, E + 12, R, D + 12, R, E + 12, D + 12, C + 12, B, C + 12, R, R, R
        };

        private static readonly int[] MenuBass =
        {
            C - 12, R, C - 12, R, A - 24, R, A - 24, R, F - 24, R, F - 24, R, G - 24, R, G - 24, R,
            F - 24, R, F - 24, R, C - 12, R, C - 12, R, G - 24, R, G - 24, R, C - 12, R, C - 12, R
        };

        private static readonly int[] MatchMelody =
        {
            G, A, B, D + 12, B, A, G, R, E, G, A, G, E, D, R, R,
            G, A, B, D + 12, E + 12, D + 12, B, R, A, B, A, G, A, R, R, R
        };

        private static readonly int[] MatchBass =
        {
            G - 24, R, G - 12, R, G - 24, R, G - 12, R, C - 12, R, C, R, C - 12, R, C, R,
            G - 24, R, G - 12, R, E - 24, R, E - 12, R, D - 24, R, D - 12, R, D - 24, R, D - 12, R
        };

        private static List<float> Music(float bpm, int[] melody, int[] bass, int repeat = 2)
        {
            float step = 60f / bpm / 2f;
            int per = N(step);
            int steps = melody.Length * repeat;
            int total = per * steps;
            var s = new List<float>(new float[total]);

            for (int k = 0; k < steps; k++)
            {
                int start = k * per;
                int mel = melody[k % melody.Length];
                if (mel >= 0)
                {
                    float f = NoteFrequency(mel);
                    for (int i = 0; i < per; i++) s[start + i] += 0.28f * Env(i, per, 0.003f, 0.6f) * Square(f * i / Rate, 0.25f);
                }

                int low = bass[k % bass.Length];
                if (low >= 0)
                {
                    float f = NoteFrequency(low);
                    for (int i = 0; i < per; i++) s[start + i] += 0.35f * Mathf.Min(1f, (per - i) / 200f) * Triangle(f * i / Rate);
                }

                if (k % 2 == 1)
                {
                    int hat = N(0.03f);
                    for (int i = 0; i < hat && start + i < total; i++)
                    {
                        float fade = 1f - i / (float)hat;
                        s[start + i] += 0.08f * fade * fade * Noise();
                    }
                }
            }
            return s;
        }

        // ---- Ferramentas ----

        private static int N(float seconds) => (int)(seconds * Rate);

        private static float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);

        private static float Env(int i, int total, float attack, float decayPower)
        {
            float t = i / (float)Rate;
            float a = attack > 0f ? Mathf.Min(1f, t / attack) : 1f;
            return a * Mathf.Pow(1f - i / (float)total, decayPower);
        }

        private static float Square(float phase, float duty) => Mathf.Repeat(phase, 1f) < duty ? 1f : -1f;

        private static float Triangle(float phase)
        {
            float p = Mathf.Repeat(phase, 1f);
            return p < 0.5f ? 4f * p - 1f : 3f - 4f * p;
        }

        private static float NoteFrequency(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private static List<float> Silence(float seconds) => new(new float[N(seconds)]);

        private static List<float> Concat(params List<float>[] parts)
        {
            var s = new List<float>();
            foreach (var part in parts) s.AddRange(part);
            return s;
        }

        private static List<float> LowPass(List<float> samples, float alpha)
        {
            var s = new List<float>(samples.Count);
            float y = 0f;
            foreach (float x in samples)
            {
                y += alpha * (x - y);
                s.Add(y);
            }
            return s;
        }

        private static void WriteWav(string path, List<float> samples, float peak)
        {
            float max = 1e-9f;
            foreach (float x in samples) max = Mathf.Max(max, Mathf.Abs(x));
            float scale = peak / max;

            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Count * 2;
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataBytes);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(dataBytes);
            foreach (float x in samples) writer.Write((short)(Mathf.Clamp(x * scale, -1f, 1f) * 32767f));
        }
    }
}
