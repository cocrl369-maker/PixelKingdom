using System;

namespace PixelKingdom
{
    public enum Biome : byte { Ocean, Shallow, Lake, Beach, Grass, Fertile, Forest, Pine, Hills, Mountain, Snow, Desert, Steppe }

    /// <summary>Statische Weltkarte (aus dem Seed erzeugt) + veränderliche Laufzeitdaten (Besitz, Erkundung).</summary>
    public class WorldMap
    {
        public const int W = 320, H = 224;
        public const float Sea = 0.40f;
        public static readonly string[] ContinentNames = { "Mittland", "Nordlande", "Südreich", "Inselbund" };

        public readonly float[] h = new float[W * H];
        public readonly Biome[] biome = new Biome[W * H];
        public readonly bool[] river = new bool[W * H];
        public readonly bool[] coast = new bool[W * H];
        public readonly byte[] cont = new byte[W * H];
        public readonly byte[] trees = new byte[W * H];
        public readonly int[] owner = new int[W * H];      // Fraktions-Id oder -1
        public readonly int[] ownerSettlement = new int[W * H];
        public readonly bool[] disc = new bool[W * H];

        public static readonly int[] DX4 = { 1, -1, 0, 0 };
        public static readonly int[] DY4 = { 0, 0, 1, -1 };
        public static readonly int[] DX8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        public static readonly int[] DY8 = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public WorldMap()
        {
            for (int i = 0; i < owner.Length; i++) { owner[i] = -1; ownerSettlement[i] = -1; }
        }

        public static int Idx(int x, int y) => y * W + x;
        public static bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public static int X(int i) => i % W;
        public static int Y(int i) => i / W;

        public bool IsWater(int i) { var b = biome[i]; return b == Biome.Ocean || b == Biome.Shallow || b == Biome.Lake; }
        public bool IsSea(int i) { var b = biome[i]; return b == Biome.Ocean || b == Biome.Shallow; }
        public bool IsLand(int i) => !IsWater(i);

        public bool Buildable(int i)
        {
            if (IsWater(i) || river[i]) return false;
            var b = biome[i];
            return b != Biome.Mountain && b != Biome.Snow;
        }

        /// <summary>Bewegungskosten pro Feld (in "Feldern"). float.MaxValue = unpassierbar.</summary>
        public float MoveCost(int i, bool canSail)
        {
            var b = biome[i];
            if (b == Biome.Ocean || b == Biome.Shallow) return canSail ? 0.5f : float.MaxValue;
            if (b == Biome.Lake) return float.MaxValue;
            float c;
            switch (b)
            {
                case Biome.Forest: case Biome.Pine: c = 1.6f; break;
                case Biome.Hills: c = 1.8f; break;
                case Biome.Mountain: c = 3.5f; break;
                case Biome.Snow: return float.MaxValue;
                case Biome.Desert: c = 1.5f; break;
                default: c = 1f; break;
            }
            if (river[i]) c += 1.0f; // Furt
            return c;
        }

        /// <summary>Weltkoordinate Y der Oberfläche (Wasseroberfläche bei Wasser).</summary>
        public float SurfaceY(int i)
        {
            if (IsSea(i)) return 0f;
            if (biome[i] == Biome.Lake) return LandY(h[i]) - 0.1f;
            if (river[i]) return LandY(h[i]) - 0.12f;
            return LandY(h[i]);
        }

        public static float LandY(float hv)
        {
            float t = (hv - Sea) / (1f - Sea);
            if (t < 0) return -0.8f + t * 2f;
            return 0.08f + (float)Math.Pow(t, 1.55) * 9f;
        }

        public float SurfaceAt(float wx, float wz)
        {
            int x = (int)Math.Floor(wx), y = (int)Math.Floor(wz);
            if (!In(x, y)) return 0f;
            return Math.Max(0f, SurfaceY(Idx(x, y)));
        }

        public int CountAround(int cx, int cy, int r, Func<int, bool> pred)
        {
            int n = 0;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!In(x, y)) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r + 1) continue;
                    if (pred(Idx(x, y))) n++;
                }
            return n;
        }

        public string DiscoveredToString()
        {
            var bytes = new byte[(disc.Length + 7) / 8];
            for (int i = 0; i < disc.Length; i++) if (disc[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            return Convert.ToBase64String(bytes);
        }

        public void DiscoveredFromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(s); } catch { return; }
            for (int i = 0; i < disc.Length && (i >> 3) < bytes.Length; i++) disc[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        }

        public string BiomeName(int i)
        {
            if (river[i]) return "Fluss";
            switch (biome[i])
            {
                case Biome.Ocean: return "Meer";
                case Biome.Shallow: return "Küstengewässer";
                case Biome.Lake: return "See";
                case Biome.Beach: return "Strand";
                case Biome.Grass: return "Wiese";
                case Biome.Fertile: return "Fruchtbares Land";
                case Biome.Forest: return "Laubwald";
                case Biome.Pine: return "Nadelwald";
                case Biome.Hills: return "Hügel";
                case Biome.Mountain: return "Gebirge";
                case Biome.Snow: return "Gipfel";
                case Biome.Desert: return "Wüste";
                case Biome.Steppe: return "Steppe";
            }
            return "?";
        }
    }
}
