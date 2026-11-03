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
        // Kirchentonarten (mittelalterlich): Dorisch, Äolisch, Mixolydisch, Hypolydisch (selten)
        static readonly int[][] Modes = { new[] { 0, 2, 3, 5, 7, 9, 10 }, new[] { 0, 2, 3, 5, 7, 8, 10 }, new[] { 0, 2, 4, 5, 7, 9, 10 }, new[] { 0, 2, 4, 6, 7, 9, 11 } };
        static int[] mode = Modes[0];
        static float root = 73.42f;

        static float Freq(int degree)
        {
            int oct = (int)Math.Floor(degree / 7.0);
            int idx = degree - oct * 7;
            return root * (float)Math.Pow(2, (mode[idx] + 12 * oct) / 12.0);
        }

        /// <summary>Anzahl verschiedener Stücke (Stil, Tonart und Tempo wechseln).</summary>
        public const int PieceCount = 12;

        // ------------------------------------------------------------------
        /// <summary>
        /// Ein ruhiges Stück. Stile wechseln: 0 Harfe & Flöte, 1 Choral mit sanften Flächen, 2 Laute allein,
        /// 3 Hirtenweise (Flöte über Bordun). Langsam, leise, ohne harte Schläge.
        /// </summary>
        public static float[] Piece(int seed)
        {
            lock (typeof(MusicSynth))
            {
                var r = new Random(seed * 7919 + 13);
                int style = seed % 4;
                mode = Modes[r.NextDouble() < 0.12 ? 3 : r.Next(3)];
                root = new[] { 73.42f, 65.41f, 82.41f, 98.00f, 55.0f * 1.25f }[r.Next(5)];
                float bpm = 50 + r.Next(10) - (style == 1 ? 6 : 0);
                int meter = r.NextDouble() < 0.6 ? 3 : 4;
                float beat = 60f / bpm;
                int[][] progs =
                {
                    new[] { 0, 6, 0, 4 }, new[] { 0, 2, 6, 0 }, new[] { 0, 5, 6, 0 }, new[] { 0, 3, 6, 4 }, new[] { 0, 6, 5, 4 }, new[] { 0, 3, 4, 0 },
                };
                var progA = progs[r.Next(progs.Length)];
                var progB = progs[r.Next(progs.Length)];
                int bars = 28 + r.Next(9);
                float barLen = beat * meter;
                int total = (int)((bars * barLen + 6f) * SR);
                var buf = new float[total];

                Drone(buf, 0, total, style == 3 ? 0.04f : 0.025f, r);
                int melodyDeg = 7 + r.Next(3);
                for (int bar = 0; bar < bars; bar++)
                {
                    var prog = (bar / 8) % 2 == 0 ? progA : progB;
                    int ch = prog[bar % 4];
                    float barStart = bar * barLen + 1.0f;
                    bool intro = bar < 2, outro = bar >= bars - 1;

                    // Begleitung
                    if (style == 1)
                    {
                        // Choral: weiche Akkordflächen, zwei Takte lang ausklingend
                        if (bar % 2 == 0 || outro)
                            foreach (int iv in new[] { 0, 2, 4 }) Pad(buf, (int)(barStart * SR), Freq(7 + ch + iv), barLen * 2.1f, 0.05f);
                        Pluck(buf, (int)(barStart * SR), Freq(ch), 0.12f, 3.5f, r, 4);
                    }
                    else
                    {
                        int[] pattern = style == 2
                            ? (meter == 3 ? new[] { 0, 4, 2, 4, 7, 4 } : new[] { 0, 4, 2, 4, 7, 4, 2, 4 })
                            : (meter == 3 ? new[] { 0, 2, 4, 7, 4, 2 } : new[] { 0, 2, 4, 2, 7, 4, 2, 4 });
                        bool sparse = style == 3;
                        for (int k = 0; k < pattern.Length; k++)
                        {
                            if (sparse && k % 2 == 1) continue;
                            if (outro && k > 0) break;
                            float t = barStart + k * beat / 2f + (float)(r.NextDouble() * 0.012);
                            float amp = (k == 0 ? 0.16f : 0.1f) * (intro ? 0.7f : 1f);
                            Pluck(buf, (int)(t * SR), Freq(7 + ch + pattern[k]), amp, outro ? 4f : 2.2f, r, style == 0 ? 3 : 4);
                        }
                        Pluck(buf, (int)(barStart * SR), Freq(ch), 0.14f, 3f, r, 4);
                    }

                    // sehr leise Rahmentrommel nur in manchen Stücken (wie entferntes Klopfen)
                    if (style == 0 && bar >= 6 && !outro && bar % 2 == 0) Drum(buf, (int)(barStart * SR), 0.035f, r);

                    // Melodie (Flöte), mit Atempausen
                    int section = bar % 8;
                    bool melody = style != 2 ? (bar >= 4 && section >= 1 && section <= 6) : (bar >= 8 && section >= 2 && section <= 5);
                    if (melody && !outro)
                    {
                        float[][] rhythms = meter == 3
                            ? new[] { new[] { 2f, 1f }, new[] { 1f, 1f, 1f }, new[] { 3f }, new[] { 1.5f, 0.5f, 1f } }
                            : new[] { new[] { 2f, 2f }, new[] { 1f, 1f, 2f }, new[] { 4f }, new[] { 1.5f, 0.5f, 2f } };
                        var rhythm = section == 6 || section == 3 ? new[] { (float)meter } : rhythms[r.Next(rhythms.Length)];
                        float tt = barStart;
                        for (int n = 0; n < rhythm.Length; n++)
                        {
                            if (section == 6)
                            {
                                int[] tones = { ch + 7, ch + 9, ch + 11, ch + 14 };
                                int bestT = tones[0];
                                foreach (var tn in tones) if (Math.Abs(tn - melodyDeg) < Math.Abs(bestT - melodyDeg)) bestT = tn;
                                melodyDeg = bestT;
                            }
                            else
                            {
                                int step = r.Next(3) - 1;
                                if (step == 0 && r.NextDouble() < 0.6) step = r.NextDouble() < 0.5 ? 1 : -1;
                                if (r.NextDouble() < 0.12) step *= 2;
                                melodyDeg = Math.Max(7, Math.Min(15, melodyDeg + step));
                            }
                            float dur = rhythm[n] * beat;
                            if (style == 2) Pluck(buf, (int)(tt * SR), Freq(melodyDeg + 7), 0.12f, dur * 1.6f, r, 2);
                            else Flute(buf, (int)(tt * SR), Freq(melodyDeg + 7), dur * 0.92f, style == 1 ? 0.06f : 0.075f, r);
                            tt += dur;
                        }
                    }
                }

                LowPass(buf, 0.35f);
                Reverb(buf);
                float peak = 0.0001f;
                for (int i = 0; i < total; i++) peak = Math.Max(peak, Math.Abs(buf[i]));
                float g = 0.38f / peak;
                int fade = 3 * SR;
                for (int i = 0; i < total; i++)
                {
                    float e = 1f;
                    if (i < fade) e = i / (float)fade;
                    if (i > total - fade) e = Math.Max(0, (total - i) / (float)fade);
                    buf[i] *= g * e;
                }
                return buf;
            }
        }

        /// <summary>Weiche Fläche (wie leise Streicher/Orgelpfeifen) mit langsamem Ein- und Ausschwingen.</summary>
        static void Pad(float[] buf, int start, float freq, float dur, float amp)
        {
            int len = Math.Min((int)(dur * SR), buf.Length - start);
            double p1 = 0, p2 = 0;
            int att = (int)(0.8f * SR), rel = (int)(1.2f * SR);
            for (int i = 0; i < len; i++)
            {
                p1 += 2 * Math.PI * freq / SR; p2 += 2 * Math.PI * freq * 1.003 / SR;
                float env = i < att ? i / (float)att : i > len - rel ? Math.Max(0, (len - i) / (float)rel) : 1f;
                float s = (float)(Math.Sin(p1) + Math.Sin(p2) * 0.7 + Math.Sin(2 * p1) * 0.12);
                buf[start + i] += s * amp * env * env;
            }
        }

        static void LowPass(float[] buf, float k)
        {
            float y = 0;
            for (int i = 0; i < buf.Length; i++) { y += (buf[i] - y) * k; buf[i] = y; }
        }

        static void Pluck(float[] buf, int start, float freq, float amp, float dur, Random r, int soft = 2)
        {
            int n = Math.Max(2, (int)(SR / freq));
            var ring = new float[n];
            for (int i = 0; i < n; i++) ring[i] = (float)(r.NextDouble() * 2 - 1);
            // weicher Anschlag: Rauschen vorfiltern
            for (int pass = 0; pass < soft; pass++) for (int i = 1; i < n; i++) ring[i] = (ring[i] + ring[i - 1]) * 0.5f;
            int len = Math.Min((int)(dur * SR), buf.Length - start);
            float decay = 0.9958f;
            int idx = 0;
            for (int i = 0; i < len; i++)
            {
                float v = ring[idx];
                int nxt = idx + 1 == n ? 0 : idx + 1;
                ring[idx] = decay * 0.5f * (ring[idx] + ring[nxt]);
                idx = nxt;
                float env = i < 90 ? i / 90f : 1f;   // weicher Anschlag
                if (i > len - 400) env *= (len - i) / 400f;
                buf[start + i] += v * amp * env;
            }
        }

        static void Flute(float[] buf, int start, float freq, float dur, float amp, Random r)
        {
            int len = Math.Min((int)(dur * SR), buf.Length - start);
            double phase = 0;
            float noiseLp = 0;
            int att = (int)(0.16f * SR), rel = (int)(0.25f * SR);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float vib = 1f + 0.0045f * (float)Math.Sin(2 * Math.PI * 5.2 * t) * Math.Min(1f, t * 3f);
                phase += 2 * Math.PI * freq * vib / SR;
                float s = (float)(Math.Sin(phase) + 0.22 * Math.Sin(2 * phase) + 0.07 * Math.Sin(3 * phase));
                noiseLp += ((float)(r.NextDouble() * 2 - 1) - noiseLp) * 0.08f;
                float env = i < att ? i / (float)att : i > len - rel ? Math.Max(0, (len - i) / (float)rel) : 1f;
                buf[start + i] += (s * 0.9f + noiseLp * 0.22f) * env * amp;
            }
        }

        static void Drone(float[] buf, int start, int len, float amp, Random r)
        {
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                p1 += 2 * Math.PI * root / SR;
                p2 += 2 * Math.PI * root * 1.5f / SR;
                p3 += 2 * Math.PI * root * 2f * 1.002f / SR;
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
