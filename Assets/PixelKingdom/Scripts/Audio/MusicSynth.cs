using System;

namespace PixelKingdom
{
    /// <summary>
    /// Komponiert und synthetisiert ruhige mittelalterliche Musik vollständig im Code:
    /// gezupfte Laute (Karplus-Strong), Flöte mit Vibrato, Bordun (Drehleier-artig) und eine leise Rahmentrommel,
    /// im dorischen Modus auf D. Dazu der dumpfe Klick-Ton für Knöpfe. Unabhängig von Unity (Threads möglich).
    /// </summary>
    public static class MusicSynth
    {
        public const int SR = 22050;
        static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        const float D2 = 73.42f;

        static float Freq(int degree)
        {
            int oct = (int)Math.Floor(degree / 7.0);
            int idx = degree - oct * 7;
            return D2 * (float)Math.Pow(2, (Dorian[idx] + 12 * oct) / 12.0);
        }

        // ------------------------------------------------------------------
        public static float[] Piece(int seed)
        {
            var r = new Random(seed * 7919 + 13);
            float bpm = 62 + r.Next(14);
            int meter = r.NextDouble() < 0.6 ? 3 : 4;
            float beat = 60f / bpm;
            int[][] progs =
            {
                new[] { 0, 6, 0, 4 }, new[] { 0, 2, 6, 0 }, new[] { 0, 5, 6, 0 }, new[] { 0, 3, 6, 4 }, new[] { 0, 6, 5, 4 },
            };
            var progA = progs[r.Next(progs.Length)];
            var progB = progs[r.Next(progs.Length)];
            int bars = 24;
            float barLen = beat * meter;
            int total = (int)((bars * barLen + 4f) * SR);
            var buf = new float[total];

            // Bordun (leise, ganze Länge)
            Drone(buf, 0, total, 0.045f, r);

            // Melodie vorbereiten (Phrasen)
            int melodyDeg = 7 + r.Next(3);
            for (int bar = 0; bar < bars; bar++)
            {
                var prog = (bar / 8) % 2 == 0 ? progA : progB;
                int root = prog[bar % 4];
                float barStart = bar * barLen + 0.5f;
                bool intro = bar < 2, outro = bar >= bars - 1;

                // Laute: Arpeggio in Achteln
                int[] pattern = meter == 3 ? new[] { 0, 2, 4, 2, 4, 2 } : new[] { 0, 2, 4, 2, 7, 4, 2, 4 };
                for (int k = 0; k < pattern.Length; k++)
                {
                    float t = barStart + k * beat / 2f;
                    int deg = 7 + root + pattern[k];
                    float amp = (k == 0 ? 0.24f : 0.15f) * (intro ? 0.8f : 1f);
                    if (outro && k > 0) break;
                    Pluck(buf, (int)(t * SR), Freq(deg), amp, outro ? 3.2f : 1.6f, r);
                }
                // tiefer Grundton auf der Eins
                Pluck(buf, (int)(barStart * SR), Freq(root), 0.2f, 2.4f, r);

                // Rahmentrommel, sanft
                if (bar >= 4 && !outro)
                {
                    Drum(buf, (int)(barStart * SR), 0.12f, r);
                    if (meter == 4) Drum(buf, (int)((barStart + 2 * beat) * SR), 0.06f, r);
                }

                // Flöte: in der Mitte jedes Achttakt-Abschnitts
                int section = bar % 8;
                if (bar >= 4 && section >= 2 && section <= 6 && !outro)
                {
                    float[][] rhythms = meter == 3
                        ? new[] { new[] { 1f, 1f, 1f }, new[] { 2f, 1f }, new[] { 1.5f, 0.5f, 1f }, new[] { 3f } }
                        : new[] { new[] { 1f, 1f, 1f, 1f }, new[] { 2f, 2f }, new[] { 1.5f, 0.5f, 2f }, new[] { 1f, 1f, 2f } };
                    var rhythm = section == 6 ? new[] { (float)meter } : rhythms[r.Next(rhythms.Length)];
                    float tt = barStart;
                    for (int n = 0; n < rhythm.Length; n++)
                    {
                        if (section == 6)
                        {
                            // Phrasenende: auf einen Akkordton
                            int[] tones = { root + 7, root + 9, root + 11, root + 14 };
                            int bestT = tones[0];
                            foreach (var tn in tones) if (Math.Abs(tn - melodyDeg) < Math.Abs(bestT - melodyDeg)) bestT = tn;
                            melodyDeg = bestT;
                        }
                        else
                        {
                            int step = r.Next(5) - 2;
                            if (step == 0 && r.NextDouble() < 0.5) step = r.NextDouble() < 0.5 ? 1 : -1;
                            melodyDeg = Math.Max(7, Math.Min(16, melodyDeg + step));
                        }
                        float dur = rhythm[n] * beat;
                        Flute(buf, (int)(tt * SR), Freq(melodyDeg + 7), dur * 0.95f, 0.11f, r);
                        tt += dur;
                    }
                }
            }

            Reverb(buf);
            // Ein-/Ausblenden und normalisieren
            float peak = 0.0001f;
            for (int i = 0; i < total; i++) peak = Math.Max(peak, Math.Abs(buf[i]));
            float g = 0.5f / peak;
            int fade = 2 * SR;
            for (int i = 0; i < total; i++)
            {
                float e = 1f;
                if (i < fade) e = i / (float)fade;
                if (i > total - fade) e = Math.Max(0, (total - i) / (float)fade);
                buf[i] *= g * e;
            }
            return buf;
        }

        static void Pluck(float[] buf, int start, float freq, float amp, float dur, Random r)
        {
            int n = Math.Max(2, (int)(SR / freq));
            var ring = new float[n];
            for (int i = 0; i < n; i++) ring[i] = (float)(r.NextDouble() * 2 - 1);
            // weicher Anschlag: Rauschen vorfiltern
            for (int pass = 0; pass < 2; pass++) for (int i = 1; i < n; i++) ring[i] = (ring[i] + ring[i - 1]) * 0.5f;
            int len = Math.Min((int)(dur * SR), buf.Length - start);
            float decay = 0.9965f;
            int idx = 0;
            for (int i = 0; i < len; i++)
            {
                float v = ring[idx];
                int nxt = idx + 1 == n ? 0 : idx + 1;
                ring[idx] = decay * 0.5f * (ring[idx] + ring[nxt]);
                idx = nxt;
                float env = i < 30 ? i / 30f : 1f;
                if (i > len - 400) env *= (len - i) / 400f;
                buf[start + i] += v * amp * env;
            }
        }

        static void Flute(float[] buf, int start, float freq, float dur, float amp, Random r)
        {
            int len = Math.Min((int)(dur * SR), buf.Length - start);
            double phase = 0;
            float noiseLp = 0;
            int att = (int)(0.07f * SR), rel = (int)(0.12f * SR);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float vib = 1f + 0.0045f * (float)Math.Sin(2 * Math.PI * 5.2 * t) * Math.Min(1f, t * 3f);
                phase += 2 * Math.PI * freq * vib / SR;
                float s = (float)(Math.Sin(phase) + 0.22 * Math.Sin(2 * phase) + 0.07 * Math.Sin(3 * phase));
                noiseLp += ((float)(r.NextDouble() * 2 - 1) - noiseLp) * 0.08f;
                float env = i < att ? i / (float)att : i > len - rel ? Math.Max(0, (len - i) / (float)rel) : 1f;
                buf[start + i] += (s * 0.85f + noiseLp * 0.35f) * env * amp;
            }
        }

        static void Drone(float[] buf, int start, int len, float amp, Random r)
        {
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                p1 += 2 * Math.PI * D2 / SR;
                p2 += 2 * Math.PI * D2 * 1.5f / SR;
                p3 += 2 * Math.PI * D2 * 2f * 1.002f / SR;
                float swell = 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 0.07 * t);
                float s = (float)(Math.Sin(p1) + 0.6 * Math.Sin(p2) + 0.3 * Math.Sin(p3));
                buf[start + i] += s * amp * swell;
            }
        }

        static void Drum(float[] buf, int start, float amp, Random r)
        {
            int len = Math.Min((int)(0.35f * SR), buf.Length - start);
            double phase = 0;
            float lp = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float f = 95f - 35f * Math.Min(1f, t * 8f);
                phase += 2 * Math.PI * f / SR;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.15f;
                float env = (float)Math.Exp(-t * 14);
                buf[start + i] += ((float)Math.Sin(phase) * 0.9f + lp * 0.4f * (float)Math.Exp(-t * 60)) * env * amp;
            }
        }

        static void Reverb(float[] buf)
        {
            int[] delays = { (int)(0.037f * SR), (int)(0.053f * SR), (int)(0.079f * SR), (int)(0.113f * SR) };
            float[] gains = { 0.3f, 0.25f, 0.2f, 0.15f };
            var outb = (float[])buf.Clone();
            for (int k = 0; k < delays.Length; k++)
            {
                int d = delays[k]; float gk = gains[k];
                var comb = new float[buf.Length];
                for (int i = 0; i < buf.Length; i++)
                {
                    float fb = i >= d ? comb[i - d] : 0f;
                    comb[i] = buf[i] + fb * 0.55f;
                    outb[i] += (i >= d ? comb[i - d] : 0f) * gk;
                }
            }
            Array.Copy(outb, buf, buf.Length);
        }

        // ------------------------------------------------------------------
        /// <summary>Dumpfer, weicher Klick (wie ein Klopfen auf Holz).</summary>
        public static float[] Click()
        {
            int len = (int)(0.12f * SR);
            var b = new float[len];
            var r = new Random(5);
            double phase = 0;
            float lp = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float f = 170f - 90f * Math.Min(1f, t * 14f);
                phase += 2 * Math.PI * f / SR;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.12f;
                float env = (float)Math.Exp(-t * 38);
                b[i] = ((float)Math.Sin(phase) * 0.8f + lp * 0.5f * (float)Math.Exp(-t * 120)) * env * 0.7f;
            }
            return b;
        }
    }
}
