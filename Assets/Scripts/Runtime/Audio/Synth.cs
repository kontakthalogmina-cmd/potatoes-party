using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Mały syntezator: generuje w locie radosną muzykę i efekty dźwiękowe,
    /// więc gra nie potrzebuje żadnych plików audio.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 22050;

        // Stopnie gamy durowej w półtonach
        static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };

        public struct SongStyle
        {
            public string Name;
            public int Bpm;
            public int RootMidi;
            public int[] Progression; // stopnie akordów (0 = I, 4 = V, 5 = vi...)
            public int Seed;
            public float Swing;
            public float LeadDuty;
            public bool Bells;
        }

        public static readonly SongStyle MenuSong = new SongStyle
        {
            Name = "Menu",
            Bpm = 112,
            RootMidi = 60,
            Progression = new[] { 0, 4, 5, 3, 0, 4, 5, 3, 3, 4, 2, 5, 1, 4, 0, 0 },
            Seed = 7,
            Swing = 0.12f,
            LeadDuty = 0.3f,
            Bells = true,
        };

        public static readonly SongStyle GameSong = new SongStyle
        {
            Name = "Gra",
            Bpm = 136,
            RootMidi = 65,
            Progression = new[] { 0, 3, 4, 0, 0, 3, 4, 4, 5, 3, 0, 4, 5, 3, 4, 0 },
            Seed = 21,
            Swing = 0.0f,
            LeadDuty = 0.22f,
            Bells = false,
        };

        public static readonly SongStyle ResultsSong = new SongStyle
        {
            Name = "Wyniki",
            Bpm = 100,
            RootMidi = 62,
            Progression = new[] { 0, 5, 3, 4, 0, 5, 3, 4 },
            Seed = 3,
            Swing = 0.15f,
            LeadDuty = 0.4f,
            Bells = true,
        };

        static float Freq(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        static int ScaleMidi(int root, int degree)
        {
            int oct = Mathf.FloorToInt(degree / 7f);
            int d = degree - oct * 7;
            return root + oct * 12 + Major[d];
        }

        struct Note
        {
            public float Start, Length, Freq, Volume;
            public int Voice; // 0 lead, 1 bass, 2 pluck, 3 bell, 4 kick, 5 snare, 6 hat
        }

        public static AudioClip MakeSong(SongStyle style)
        {
            var rng = new System.Random(style.Seed);
            float beat = 60f / style.Bpm;
            float bar = beat * 4f;
            int bars = style.Progression.Length;
            float total = bar * bars;
            var notes = new List<Note>();

            // --- Melodia: motyw A (4 takty) powtarzany, środek B, powrót A z kadencją ---
            int[][] rhythms =
            {
                new[] { 2, 1, 1, 2, 2 },
                new[] { 1, 1, 1, 1, 2, 2 },
                new[] { 3, 1, 2, 2 },
                new[] { 2, 2, 1, 1, 2 },
                new[] { 1, 1, 2, 1, 1, 2 },
                new[] { 2, 1, 1, 1, 1, 2 },
            };
            var motifA = new List<int[]>();
            var motifB = new List<int[]>();
            for (int i = 0; i < 4; i++) motifA.Add(rhythms[rng.Next(rhythms.Length)]);
            for (int i = 0; i < 4; i++) motifB.Add(rhythms[rng.Next(rhythms.Length)]);

            int prevDeg = 7; // tonika oktawę wyżej
            var melodyCache = new Dictionary<int, List<int>>();
            for (int b = 0; b < bars; b++)
            {
                int section = (b / 4) % 4; // 0 A, 1 A', 2 B, 3 A
                bool isB = bars >= 12 && section == 2;
                int[] rhythm = isB ? motifB[b % 4] : motifA[b % 4];
                int chord = style.Progression[b];
                int cacheKey = (isB ? 100 : 0) + (b % 4);
                bool reuse = !isB && melodyCache.ContainsKey(cacheKey) && b != bars - 1 && b != 7;
                List<int> degs = reuse ? melodyCache[cacheKey] : new List<int>();
                float t = 0;
                for (int n = 0; n < rhythm.Length; n++)
                {
                    int deg;
                    if (reuse) deg = degs[n];
                    else
                    {
                        if (n == 0 || rhythm[n] >= 2)
                        {
                            // Mocna część taktu: dźwięk akordu najbliżej poprzedniego
                            int best = prevDeg, bestDist = 99;
                            for (int o = 0; o < 3; o++)
                            {
                                int cand = chord + o * 2 + 7;
                                for (int sh = -7; sh <= 7; sh += 7)
                                {
                                    int c = cand + sh;
                                    int dist = Mathf.Abs(c - prevDeg) + rng.Next(2);
                                    if (c >= 4 && c <= 13 && dist < bestDist) { bestDist = dist; best = c; }
                                }
                            }
                            deg = best;
                        }
                        else
                        {
                            int step = rng.Next(5) - 2;
                            if (step == 0) step = 1;
                            deg = Mathf.Clamp(prevDeg + step, 3, 13);
                        }
                        if (b == bars - 1 && n == rhythm.Length - 1) deg = 7; // zakończenie na tonice
                        degs.Add(deg);
                    }
                    prevDeg = deg;
                    float len = rhythm[n] * beat * 0.5f;
                    float swing = (Mathf.RoundToInt(t / (beat * 0.5f)) % 2 == 1) ? style.Swing * beat * 0.5f : 0f;
                    notes.Add(new Note { Start = b * bar + t + swing, Length = len * 0.9f, Freq = Freq(ScaleMidi(style.RootMidi, deg)), Volume = 0.22f, Voice = 0 });
                    if (style.Bells && rhythm[n] >= 2 && rng.Next(3) == 0)
                        notes.Add(new Note { Start = b * bar + t + swing, Length = 0.6f, Freq = Freq(ScaleMidi(style.RootMidi, deg) + 12), Volume = 0.07f, Voice = 3 });
                    t += len;
                }
                if (!reuse && !isB && !melodyCache.ContainsKey(cacheKey)) melodyCache[cacheKey] = degs;

                // --- Bas "um-pa": pryma, oktawa, kwinta, oktawa ---
                int rootMidi = ScaleMidi(style.RootMidi - 24, chord);
                int[] bassPattern = { 0, 12, 7, 12 };
                for (int q = 0; q < 4; q++)
                    notes.Add(new Note { Start = b * bar + q * beat, Length = beat * 0.55f, Freq = Freq(rootMidi + bassPattern[q]), Volume = 0.3f, Voice = 1 });

                // --- Akordy grane na "i" (off-beat), bardzo skoczne ---
                for (int q = 0; q < 4; q++)
                {
                    float st = b * bar + q * beat + beat * 0.5f + style.Swing * beat * 0.5f;
                    for (int k = 0; k < 3; k++)
                        notes.Add(new Note { Start = st, Length = beat * 0.3f, Freq = Freq(ScaleMidi(style.RootMidi - 12, chord + k * 2)), Volume = 0.07f, Voice = 2 });
                }

                // --- Perkusja ---
                for (int q = 0; q < 4; q++)
                {
                    float st = b * bar + q * beat;
                    if (q == 0 || q == 2) notes.Add(new Note { Start = st, Length = 0.25f, Volume = 0.55f, Voice = 4 });
                    if (q == 1 || q == 3) notes.Add(new Note { Start = st, Length = 0.18f, Volume = 0.22f, Voice = 5 });
                    notes.Add(new Note { Start = st, Length = 0.04f, Volume = 0.05f, Voice = 6 });
                    notes.Add(new Note { Start = st + beat * 0.5f + style.Swing * beat * 0.5f, Length = 0.06f, Volume = 0.08f, Voice = 6 });
                }
                if (b % 4 == 3) notes.Add(new Note { Start = b * bar + beat * 3.5f, Length = 0.15f, Volume = 0.18f, Voice = 5 });
            }

            int len2 = Mathf.CeilToInt(total * SampleRate);
            var buf = new float[len2];
            uint noise = 22222;
            foreach (var n in notes) Render(n, buf, ref noise);

            // Łagodny limiter
            for (int i = 0; i < buf.Length; i++) buf[i] = Soft(buf[i] * 1.6f);

            var clip = AudioClip.Create("Muzyka " + style.Name, buf.Length, 1, SampleRate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        static float Soft(float x) { return x / (1f + Mathf.Abs(x)); }

        static float Noise(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFF) / 32768f - 1f;
        }

        static void Render(Note n, float[] buf, ref uint noise)
        {
            int start = Mathf.FloorToInt(n.Start * SampleRate);
            float release = n.Voice == 0 ? 0.06f : 0.03f;
            int count = Mathf.CeilToInt((n.Length + release) * SampleRate);
            float phase = 0f;
            float dt = 1f / SampleRate;
            for (int i = 0; i < count; i++)
            {
                float t = i * dt;
                float env;
                float s;
                switch (n.Voice)
                {
                    case 0: // prowadzenie: kwadrat z wibrato + łagodny sinus
                    {
                        env = Adsr(t, n.Length, 0.008f, 0.08f, 0.65f, release);
                        float vib = t > 0.15f ? Mathf.Sin(t * 33f) * 0.006f : 0f;
                        phase += n.Freq * (1f + vib) * dt;
                        float p = phase - Mathf.Floor(phase);
                        s = (p < 0.3f ? 0.6f : -0.6f) * 0.55f + Mathf.Sin(phase * 6.2831853f) * 0.6f;
                        break;
                    }
                    case 1: // bas: trójkąt
                    {
                        env = Adsr(t, n.Length, 0.004f, 0.1f, 0.7f, release);
                        phase += n.Freq * dt;
                        float p = phase - Mathf.Floor(phase);
                        s = (p < 0.5f ? p * 4f - 1f : 3f - p * 4f);
                        break;
                    }
                    case 2: // szarpane akordy
                    {
                        env = Mathf.Exp(-t * 18f) * (t < n.Length + release ? 1f : 0f);
                        phase += n.Freq * dt;
                        float p = phase - Mathf.Floor(phase);
                        s = Mathf.Sin(phase * 6.2831853f) * 0.7f + (p * 2f - 1f) * 0.3f;
                        break;
                    }
                    case 3: // dzwoneczki
                    {
                        env = Mathf.Exp(-t * 6f);
                        phase += n.Freq * dt;
                        s = Mathf.Sin(phase * 6.2831853f) + Mathf.Sin(phase * 6.2831853f * 2.76f) * 0.3f;
                        break;
                    }
                    case 4: // stopa
                    {
                        env = Mathf.Exp(-t * 14f);
                        float f = 50f + 110f * Mathf.Exp(-t * 30f);
                        phase += f * dt;
                        s = Mathf.Sin(phase * 6.2831853f);
                        break;
                    }
                    case 5: // werbel
                    {
                        env = Mathf.Exp(-t * 22f);
                        phase += 190f * dt;
                        s = Noise(ref noise) * 0.75f + Mathf.Sin(phase * 6.2831853f) * 0.35f;
                        break;
                    }
                    default: // talerzyk
                    {
                        env = Mathf.Exp(-t * 70f);
                        float a = Noise(ref noise);
                        float b = Noise(ref noise);
                        s = a - b * 0.9f;
                        break;
                    }
                }
                int idx = (start + i) % buf.Length; // zawijanie: pętla bez "szwu"
                buf[idx] += s * env * n.Volume;
            }
        }

        static float Adsr(float t, float len, float a, float d, float s, float r)
        {
            if (t < a) return t / a;
            if (t < a + d) return 1f - (1f - s) * ((t - a) / d);
            if (t < len) return s;
            float k = 1f - (t - len) / r;
            return k > 0 ? s * k : 0f;
        }

        // ---------------- Efekty dźwiękowe ----------------

        public delegate float Wave(float t, ref uint noise);

        static AudioClip Make(string name, float seconds, Wave w)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            uint noise = 1234;
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(w(i / (float)SampleRate, ref noise), -1f, 1f);
            // krótkie wyciszenie na końcu, żeby nie trzaskało
            int fade = Mathf.Min(n, 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sq(float phase) { return phase - Mathf.Floor(phase) < 0.5f ? 1f : -1f; }

        public static Dictionary<Sfx, AudioClip> MakeSfx()
        {
            var d = new Dictionary<Sfx, AudioClip>();
            d[Sfx.Jump] = Make("skok", 0.18f, (float t, ref uint s) =>
            {
                return Mathf.Sin(6.2831853f * (300f * t + 450f * t * t)) * Mathf.Exp(-t * 12f) * 0.5f;
            });
            d[Sfx.Dash] = Make("zryw", 0.22f, (float t, ref uint s) =>
            {
                float n = Noise(ref s);
                return n * Mathf.Sin(Mathf.PI * t / 0.22f) * 0.35f;
            });
            d[Sfx.Bump] = Make("bum", 0.25f, (float t, ref uint s) =>
            {
                float ph = 120f * t - 150f * t * t;
                return (Mathf.Sin(6.2831853f * ph) * 0.8f + Noise(ref s) * 0.25f) * Mathf.Exp(-t * 16f);
            });
            d[Sfx.Coin] = Make("moneta", 0.3f, (float t, ref uint s) =>
            {
                float f = t < 0.07f ? 988f : 1319f;
                return Sq(f * t) * 0.18f * Mathf.Exp(-t * 7f);
            });
            d[Sfx.Explosion] = Make("wybuch", 0.9f, (float t, ref uint s) =>
            {
                float n = Noise(ref s);
                return (n * 0.8f + Mathf.Sin(6.2831853f * 55f * t) * 0.5f) * Mathf.Exp(-t * 4.5f);
            });
            d[Sfx.Fall] = Make("spadanie", 0.6f, (float t, ref uint s) =>
            {
                float ph = 800f * t - 550f * t * t;
                return Mathf.Sin(6.2831853f * ph) * 0.35f * (1f - t / 0.6f);
            });
            d[Sfx.Beep] = Make("bip", 0.15f, (float t, ref uint s) => Sq(660f * t) * 0.18f * Mathf.Exp(-t * 10f));
            d[Sfx.Go] = Make("start", 0.45f, (float t, ref uint s) => (Sq(880f * t) * 0.12f + Mathf.Sin(6.2831853f * 1320f * t) * 0.15f) * Mathf.Exp(-t * 4f));
            d[Sfx.Click] = Make("klik", 0.06f, (float t, ref uint s) => Mathf.Sin(6.2831853f * 1500f * t) * 0.3f * Mathf.Exp(-t * 60f));
            d[Sfx.Join] = Make("dolacz", 0.35f, (float t, ref uint s) =>
            {
                float f = t < 0.1f ? 523f : (t < 0.2f ? 659f : 784f);
                return Sq(f * t) * 0.14f * Mathf.Exp(-t * 4f);
            });
            d[Sfx.Fanfare] = Make("fanfara", 1.6f, (float t, ref uint s) =>
            {
                float[] seq = { 523f, 659f, 784f, 1047f };
                int i = Mathf.Min(3, Mathf.FloorToInt(t / 0.16f));
                float f = seq[i];
                float local = t - i * 0.16f;
                float env = i < 3 ? Mathf.Exp(-local * 6f) : Mathf.Exp(-local * 1.8f);
                return (Sq(f * t) * 0.12f + Mathf.Sin(6.2831853f * f * 0.5f * t) * 0.18f) * env;
            });
            d[Sfx.Tick] = Make("tik", 0.05f, (float t, ref uint s) => Mathf.Sin(6.2831853f * 2200f * t) * 0.2f * Mathf.Exp(-t * 80f));
            d[Sfx.Crack] = Make("trzask", 0.3f, (float t, ref uint s) => Noise(ref s) * 0.4f * Mathf.Exp(-t * 15f) * (Mathf.Sin(t * 300f) > 0 ? 1f : 0.3f));
            d[Sfx.Whoosh] = Make("koło", 0.08f, (float t, ref uint s) => Noise(ref s) * 0.15f * Mathf.Exp(-t * 40f));
            return d;
        }
    }

    public enum Sfx { Jump, Dash, Bump, Coin, Explosion, Fall, Beep, Go, Click, Join, Fanfare, Tick, Crack, Whoosh }
}
