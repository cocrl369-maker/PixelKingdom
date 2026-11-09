using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    /// <summary>A* auf dem Kachelraster (8-Nachbarschaft), mit Geländekosten.</summary>
    public static class Pathfinder
    {
        static float[] g;
        static int[] parent;
        static int[] stamp;
        static int curStamp;
        static readonly List<(float f, int i)> heap = new List<(float, int)>();

        public static List<int> Find(WorldMap map, int sx, int sy, int tx, int ty, bool canSail, int maxNodes = 400000)
        {
            int N = WorldMap.W * WorldMap.H;
            if (g == null) { g = new float[N]; parent = new int[N]; stamp = new int[N]; }
            curStamp++;
            if (!WorldMap.In(sx, sy) || !WorldMap.In(tx, ty)) return null;
            int start = WorldMap.Idx(sx, sy), goal = WorldMap.Idx(tx, ty);
            if (map.MoveCost(goal, canSail) == float.MaxValue) return null;

            heap.Clear();
            g[start] = 0; parent[start] = -1; stamp[start] = curStamp;
            hScale = canSail ? 0.45f : 0.9f;
            Push(H(sx, sy, tx, ty), start);
            int expanded = 0;
            while (heap.Count > 0)
            {
                var (_, cur) = Pop();
                if (cur == goal) break;
                if (++expanded > maxNodes) return null;
                int cx = WorldMap.X(cur), cy = WorldMap.Y(cur);
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + WorldMap.DX8[k], ny = cy + WorldMap.DY8[k];
                    if (!WorldMap.In(nx, ny)) continue;
                    int ni = WorldMap.Idx(nx, ny);
                    float c = map.MoveCost(ni, canSail);
                    if (c == float.MaxValue) continue;
                    if (k >= 4)
                    {
                        // keine Diagonale durch Hindernisse
                        if (map.MoveCost(WorldMap.Idx(nx, cy), canSail) == float.MaxValue || map.MoveCost(WorldMap.Idx(cx, ny), canSail) == float.MaxValue) continue;
                        c *= 1.4142f;
                    }
                    float ng = g[cur] + c;
                    if (stamp[ni] == curStamp && ng >= g[ni]) continue;
                    stamp[ni] = curStamp; g[ni] = ng; parent[ni] = cur;
                    Push(ng + H(nx, ny, tx, ty), ni);
                }
            }
            if (stamp[goal] != curStamp) return null;
            var path = new List<int>();
            for (int c = goal; c != -1; c = parent[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        public static float PathCost(WorldMap map, List<int> path, bool canSail)
        {
            float c = 0;
            for (int i = 1; i < path.Count; i++) c += map.MoveCost(path[i], canSail);
            return c;
        }

        static float hScale = 0.9f;
        static float H(int x, int y, int tx, int ty)
        {
            int dx = Math.Abs(x - tx), dy = Math.Abs(y - ty);
            return hScale * (Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy)); // unterschätzend (Seewege sind billig)
        }

        static void Push(float f, int i)
        {
            heap.Add((f, i));
            int c = heap.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) / 2;
                if (heap[p].f <= heap[c].f) break;
                (heap[p], heap[c]) = (heap[c], heap[p]);
                c = p;
            }
        }

        static (float, int) Pop()
        {
            var top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int c = 0;
            while (true)
            {
                int l = c * 2 + 1, r = l + 1, m = c;
                if (l < heap.Count && heap[l].f < heap[m].f) m = l;
                if (r < heap.Count && heap[r].f < heap[m].f) m = r;
                if (m == c) break;
                (heap[m], heap[c]) = (heap[c], heap[m]);
                c = m;
            }
            return top;
        }
    }
}
