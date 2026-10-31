using System;

namespace PixelKingdom
{
    /// <summary>Deterministisches, seedbares Perlin-Rauschen (unabhängig von Unity).</summary>
    public class Noise
    {
        readonly int[] p = new int[512];

        public Noise(int seed)
        {
            var rnd = new Random(seed);
            var perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--) { int j = rnd.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
            for (int i = 0; i < 512; i++) p[i] = perm[i & 255];
        }

        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Rauschen im Bereich ca. 0..1</summary>
        public float Perlin(float x, float y)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float xf = x - xi, yf = y - yi;
            xi &= 255; yi &= 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = p[p[xi] + yi], ab = p[p[xi] + yi + 1], ba = p[p[xi + 1] + yi], bb = p[p[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Lerp(x1, x2, v) * 0.5f + 0.5f;
        }

        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0, f = 1;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(x * f + i * 17.3f, y * f - i * 9.1f) * amp;
                norm += amp; amp *= gain; f *= lacunarity;
            }
            return sum / norm;
        }

        public float Ridged(float x, float y, int octaves)
        {
            float sum = 0, amp = 1, norm = 0, f = 1;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Math.Abs(Perlin(x * f + 31.7f * i, y * f + 7.7f * i) * 2f - 1f);
                sum += n * n * amp;
                norm += amp; amp *= 0.5f; f *= 2f;
            }
            return sum / norm;
        }
    }
}
