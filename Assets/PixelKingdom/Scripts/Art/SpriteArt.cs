using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    public class ArtSprite
    {
        public string Name;
        public PixelCanvas C;
        public int PivotX, PivotY;     // Bodenpunkt (PivotY von unten gezählt)
        public bool Flat;              // liegt flach auf dem Boden (Felder)
        public PixelCanvas Shadow;     // Schlagschatten
        public int ShadowPivotX, ShadowPivotY;
        public PixelCanvas Overlay;    // einfärbbare Teile (Fahnen, Wappenröcke) in Weiß
    }

    /// <summary>
    /// Prozedurale Retro-Pixelkunst: Gebäude in Schrägansicht mit Ziegeldächern, Fachwerk, Fenstern;
    /// Bäume, Berge, Bodenschätze, Mauern, Felder und Truppen. Alles mit Kontur und Schlagschatten.
    /// </summary>
    public static class SpriteArt
    {
        // ---------------- Farben -------------------------------------------------
        static Px H(string s) => Px.Hex(s);
        public static readonly Px K = H("#2A1D1A");
        static readonly Px Plaster = H("#EFE2C2"), PlasterD = H("#D3C29C");
        static readonly Px Beam = H("#6B4226"), BeamL = H("#8A5A34");
        static readonly Px Plank = H("#B07444"), PlankL = H("#C98B55"), PlankD = H("#8A5530"), PlankDD = H("#6B3F22");
        static readonly Px RedPlank = H("#A8402F"), RedPlankL = H("#C25640"), RedPlankD = H("#7E2E22");
        static readonly Px Log = H("#9B6236"), LogL = H("#B97C48"), LogD = H("#74451F"), LogEnd = H("#D8A86A");
        static readonly Px Stone = H("#B5ADA2"), StoneL = H("#CFC8BE"), StoneD = H("#8E867C"), Mortar = H("#7C746B");
        static readonly Px White = H("#E6E1D6"), WhiteL = H("#F6F2EA"), WhiteD = H("#BDB6A8");
        static readonly Px DStone = H("#6F6A7A"), DStoneL = H("#8C879A"), DStoneD = H("#524E5C");
        static readonly Px Glass = H("#8CC8E8"), GlassL = H("#DDF2FF"), Lit = H("#F6D365"), LitL = H("#FFF0B0"), WinFrame = H("#5A3A24");
        static readonly Px Door = H("#6B3F22"), DoorD = H("#4A2A16");
        static readonly Px Iron = H("#8D97A3"), IronL = H("#C2CAD3"), IronD = H("#5C636D");
        static readonly Px Gold = H("#F2C94C"), GoldL = H("#FFF2A0"), GoldD = H("#B88A22");
        static readonly Px Rust = H("#B4552E"), RustL = H("#D9783C");
        static readonly Px Skin = H("#F0C29A"), SkinD = H("#C98B60"), Hair = H("#5A3A24");
        static readonly Px Hay = H("#E8C35A"), HayD = H("#B8902E");
        static readonly Px Fire = H("#E8892F"), FireL = H("#FFD15A");
        static readonly Px Smoke = H("#C9C4C0"), SmokeL = H("#E8E4E0");
        static readonly Px Red = H("#C8473B"), Blue = H("#4E7BC0"), Cloth = H("#F2EBDD"), ClothD = H("#CFC4AE");
        static readonly Px Soil = H("#8A6236"), SoilD = H("#6E4A28");

        struct Roof { public Px Mid, Light, Dark, Ridge; public Roof(string m, string l, string d, string r) { Mid = H(m); Light = H(l); Dark = H(d); Ridge = H(r); } }
        static readonly Roof RoofRed = new Roof("#C8473B", "#E06A54", "#92302A", "#F08A6E");
        static readonly Roof RoofBlue = new Roof("#4E7BC0", "#6E9AD8", "#345A92", "#9CC0EC");
        static readonly Roof RoofGreen = new Roof("#4E9A55", "#6DBA6A", "#356E3C", "#93D489");
        static readonly Roof RoofBrown = new Roof("#9A5E3A", "#B87A4E", "#6E3F24", "#D49A6A");
        static readonly Roof RoofSlate = new Roof("#6C7686", "#8A94A4", "#4C5462", "#AEB8C6");
        static readonly Roof RoofThatch = new Roof("#D7B25A", "#EBCB78", "#A8843A", "#F4DE9C");
        static readonly Roof RoofGold = new Roof("#E0B040", "#F4D070", "#A87C22", "#FFF0A8");

        enum WallStyle { Plaster, Wood, RedWood, Log, Stone, White, Dark }

        // ==================================================================
        //  Grundbausteine
        // ==================================================================
        static void Wall(PixelCanvas c, int x0, int y0, int w, int h, WallStyle st)
        {
            switch (st)
            {
                case WallStyle.Plaster:
                    c.Rect(x0, y0, w, h, Plaster);
                    c.HLine(x0, x0 + w - 1, y0, Beam);
                    if (h >= 7) c.HLine(x0, x0 + w - 1, y0 + h / 2, Beam);
                    for (int x = x0; x < x0 + w; x += 5) c.VLine(x, y0, y0 + h - 1, Beam);
                    c.VLine(x0 + w - 1, y0, y0 + h - 1, Beam);
                    if (w >= 10 && h >= 7) { c.Line(x0 + 1, y0 + h - 2, x0 + 4, y0 + h / 2 + 1, Beam); }
                    break;
                case WallStyle.Wood:
                case WallStyle.RedWood:
                    {
                        bool red = st == WallStyle.RedWood;
                        c.Rect(x0, y0, w, h, red ? RedPlank : Plank);
                        for (int y = y0 + 1; y < y0 + h; y += 2) c.HLine(x0, x0 + w - 1, y, red ? RedPlankD : PlankD);
                        for (int y = y0; y < y0 + h; y += 2)
                            for (int x = x0; x < x0 + w; x++)
                                if (PixelCanvas.Rnd(x, y, 3) < 0.12f) c.Set(x, y, red ? RedPlankL : PlankL);
                        c.VLine(x0, y0, y0 + h - 1, red ? RedPlankD : PlankDD);
                        break;
                    }
                case WallStyle.Log:
                    for (int y = y0; y < y0 + h; y++)
                    {
                        Px col = (y - y0) % 2 == 0 ? LogL : LogD;
                        c.HLine(x0, x0 + w - 1, y, col);
                        if ((y - y0) % 2 == 0) { c.Set(x0, y, LogEnd); c.Set(x0 + w - 1, y, LogEnd); }
                    }
                    break;
                default:
                    {
                        Px b = st == WallStyle.White ? White : st == WallStyle.Dark ? DStone : Stone;
                        Px l = st == WallStyle.White ? WhiteL : st == WallStyle.Dark ? DStoneL : StoneL;
                        Px d = st == WallStyle.White ? WhiteD : st == WallStyle.Dark ? DStoneD : StoneD;
                        c.Rect(x0, y0, w, h, b);
                        for (int y = y0; y < y0 + h; y++)
                        {
                            int r = y - y0;
                            if (r % 3 == 2) { c.HLine(x0, x0 + w - 1, y, d); continue; }
                            int off = (r / 3) % 2 * 3;
                            for (int x = x0; x < x0 + w; x++)
                            {
                                if ((x - x0 + off) % 6 == 0) c.Set(x, y, d);
                                else if (r % 3 == 0 && PixelCanvas.Rnd(x / 6, y / 3, 7) < 0.3f) c.Set(x, y, l);
                            }
                        }
                        break;
                    }
            }
            c.Recolor(x0 + w - 2, y0, 2, h, p => p.Mul(0.85f));
        }

        /// <summary>Sichtbare Seitenwand rechts (gibt die räumliche Tiefe).</summary>
        static void SideWall(PixelCanvas c, int x, int wy, int h, WallStyle ws)
        {
            Px col = ws == WallStyle.Plaster || ws == WallStyle.White ? PlasterD.Mul(0.82f) : ws == WallStyle.Stone ? StoneD : ws == WallStyle.Log ? LogD : PlankDD;
            for (int i = 0; i < 3; i++)
                for (int y = wy - i; y <= wy + h - 1; y++)
                    c.Set(x + i, y, (y - wy) % 3 == 2 && ws != WallStyle.Plaster ? col.Mul(0.85f) : col);
        }

        /// <summary>Dachkante an der Seite: dunkler, nach hinten geneigt.</summary>
        static void RoofSide(PixelCanvas c, int x, int y0, int h, Roof r)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < 3; i++)
                    if (j >= i) c.Set(x + i, y0 + j, j == h - 1 ? r.Dark.Mul(0.8f) : r.Dark.Mul(0.9f));
        }

        static void RoofArea(PixelCanvas c, int x0, int y0, int w, int h, Roof r, int pattern = 0)
        {
            for (int j = 0; j < h; j++)
            {
                int inset = j == 0 ? 1 : 0;
                for (int x = x0 + inset; x < x0 + w - inset; x++)
                {
                    Px col;
                    int band = (j - 1) % 3;
                    if (j == 0) col = r.Ridge;
                    else if (j == h - 1) col = r.Dark;
                    else if (pattern == 1) // Stroh
                    {
                        float n = PixelCanvas.Rnd(x, j, 11);
                        col = band == 2 ? r.Dark : n < 0.25f ? r.Light : n > 0.8f ? r.Dark : r.Mid;
                    }
                    else
                    {
                        if (band == 2) col = r.Dark;
                        else if (pattern == 0 && band == 1 && ((x + ((j - 1) / 3) * 2) % 4 == 0)) col = r.Dark;
                        else if (band == 0) col = r.Light;
                        else col = r.Mid;
                    }
                    if (x > x0 + w * 0.64f) col = col.Mul(0.88f);
                    c.Set(x, y0 + j, col);
                }
            }
        }

        /// <summary>Giebel, der zum Betrachter zeigt (Dreieck mit Dachkanten und Giebelwand).</summary>
        static void Gable(PixelCanvas c, int cx, int baseY, int halfW, int h, Roof r, Px wall)
        {
            for (int j = 0; j < h; j++)
            {
                int y = baseY - j;
                float half = halfW * (1f - j / (float)h);
                for (int x = (int)Math.Round(cx - half); x <= (int)Math.Round(cx + half); x++)
                {
                    float edge = half - Math.Abs(x - cx);
                    Px col = edge < 2.2f ? (x < cx ? r.Light : r.Dark) : wall;
                    if (edge < 2.2f && j == 0) col = r.Dark;
                    c.Set(x, y, col);
                }
            }
        }

        static void Window(PixelCanvas c, int x, int y, int w = 2, int h = 2, bool lit = false)
        {
            c.Rect(x - 1, y - 1, w + 2, h + 2, WinFrame);
            c.Rect(x, y, w, h, lit ? Lit : Glass);
            c.Set(x, y, lit ? LitL : GlassL);
            c.HLine(x - 1, x + w, y + h + 1, Plaster);
        }

        static void DoorAt(PixelCanvas c, int x, int y, int w = 3, int h = 4, bool arch = false)
        {
            c.Rect(x - 1, y - 1, w + 2, h + 1, WinFrame);
            c.Rect(x, y, w, h, Door);
            c.VLine(x + w - 1, y, y + h - 1, DoorD);
            if (w >= 3) c.Set(x + w - 2, y + h / 2, Gold);
            if (arch) { c.Set(x - 1, y - 1, Px.Clear); c.Set(x + w, y - 1, Px.Clear); }
        }

        static void Chimney(PixelCanvas c, int x, int y, int h = 4)
        {
            c.Rect(x, y, 3, h, StoneD);
            c.HLine(x, x + 2, y, Stone);
            c.Set(x + 1, y, K);
        }

        static void Flag(PixelCanvas c, int x, int baseY, int poleH = 9)
        {
            c.VLine(x, baseY - poleH, baseY, Beam);
            c.Set(x, baseY - poleH - 1, Gold);
            for (int j = 0; j < 4; j++)
                for (int i = 1; i <= 5; i++)
                {
                    int wave = (i == 3 || i == 4) ? 1 : 0;
                    c.Set(x + i, baseY - poleH + j + wave, (j == 3 || i == 5) ? Px.TintDark : Px.Tint);
                }
        }

        static void Battlements(PixelCanvas c, int x0, int y, int w, Px col, Px dark)
        {
            for (int x = x0; x < x0 + w; x++)
            {
                bool merlon = ((x - x0) / 2) % 2 == 0;
                if (merlon) { c.Set(x, y - 1, col); c.Set(x, y - 2, (x - x0) % 2 == 0 ? col.Light(0.15f) : col); }
                c.Set(x, y, dark);
            }
        }

        /// <summary>Standardhaus: Wand + Dach + Tür + Fenster + Schornstein.</summary>
        static void House(PixelCanvas c, int cx, int gy, int w, int wallH, int roofH, WallStyle ws, Roof r, int pattern = 0,
                          bool chimney = true, int windows = 2, bool lit = false, bool doorLeft = true)
        {
            int x0 = cx - w / 2;
            int wy = gy - wallH + 1;
            roofH += 2;
            if (chimney) Chimney(c, x0 + w - 6, wy - roofH - 2, 5);
            Wall(c, x0, wy, w, wallH, ws);
            c.HLine(x0, x0 + w - 1, gy, StoneD);
            SideWall(c, x0 + w, wy, wallH, ws);
            RoofArea(c, x0 - 1, wy - roofH, w + 5, roofH, r, pattern);
            RoofSide(c, x0 + w + 1, wy - roofH, roofH, r);
            c.HLine(x0, x0 + w - 1, wy, ws == WallStyle.Plaster ? Beam : PlankDD); // Traufschatten
            int dx = doorLeft ? x0 + 2 : x0 + w - 5;
            DoorAt(c, dx, gy - 4, 3, 4);
            int wx = doorLeft ? x0 + 7 : x0 + 2;
            for (int k = 0; k < windows; k++)
            {
                int xx = wx + k * 5;
                if (xx + 2 > x0 + w - 2) break;
                Window(c, xx, gy - wallH + 3, 2, 2, lit);
            }
        }

        // ==================================================================
        //  Gebäude
        // ==================================================================
        static void Wohnhaus(PixelCanvas c, int cx, int gy, int v)
        {
            switch (v % 3)
            {
                case 0: House(c, cx, gy, 15, 7, 8, WallStyle.Plaster, RoofRed); break;
                case 1: House(c, cx, gy, 15, 7, 7, WallStyle.Wood, RoofBrown, 0, true, 2, false, false); break;
                default:
                    House(c, cx, gy, 13, 7, 7, WallStyle.Plaster, RoofBlue, 0, false, 1);
                    Gable(c, cx + 2, gy - 7, 4, 5, RoofBlue, Plaster);
                    Window(c, cx + 1, gy - 9, 2, 1);
                    break;
            }
        }

        static void Bauernhof(PixelCanvas c, int cx, int gy)
        {
            int w = 20, wallH = 10, x0 = cx - w / 2, wy = gy - wallH + 1;
            Wall(c, x0, wy, w, wallH, WallStyle.RedWood);
            c.HLine(x0, x0 + w - 1, gy, StoneD);
            RoofArea(c, x0 - 1, wy - 8, w + 2, 8, RoofThatch, 1);
            // Tor mit Kreuzstreben
            int dx = cx - 3;
            c.Rect(dx - 1, gy - 7, 8, 8, RedPlankD);
            c.Rect(dx, gy - 6, 6, 7, PlankL);
            c.Line(dx, gy - 6, dx + 5, gy, Beam); c.Line(dx + 5, gy - 6, dx, gy, Beam);
            c.VLine(dx + 3, gy - 6, gy, Beam);
            Window(c, cx - 1, wy + 1, 2, 1);
            // Heuballen
            c.Ellipse(x0 - 2, gy - 1.5f, 3, 2.5f, Hay);
            c.HLine(x0 - 4, x0, gy - 2, HayD);
        }

        static void Fischerhuette(PixelCanvas c, int cx, int gy)
        {
            House(c, cx - 4, gy, 12, 6, 6, WallStyle.Wood, RoofBlue, 0, false, 1);
            int px = cx + 4;
            c.VLine(px, gy - 9, gy, Beam); c.VLine(px + 7, gy - 9, gy, Beam);
            c.HLine(px, px + 7, gy - 9, BeamL);
            for (int y = gy - 8; y <= gy - 3; y++)
                for (int x = px + 1; x < px + 7; x++)
                    if ((x + y) % 2 == 0) c.Set(x, y, ClothD);
            c.Set(px + 2, gy - 4, IronL); c.Set(px + 3, gy - 4, Iron); c.Set(px + 5, gy - 5, IronL);
        }

        static void Holzfaeller(PixelCanvas c, int cx, int gy)
        {
            House(c, cx - 3, gy, 13, 7, 7, WallStyle.Log, RoofGreen, 0, true, 1);
            // Holzstapel
            int lx = cx + 5;
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 3 - row; k++)
                {
                    float x = lx + k * 3 + row * 1.5f, y = gy - 1.5f - row * 2.6f;
                    c.Ellipse(x + 1.5f, y, 1.6f, 1.4f, LogEnd);
                    c.Set((int)(x + 1.5f), (int)y, LogD);
                }
            // Hackklotz mit Axt
            c.Rect(cx - 12, gy - 2, 3, 3, Log); c.HLine(cx - 12, cx - 10, gy - 2, LogEnd);
            c.VLine(cx - 10, gy - 6, gy - 3, BeamL); c.Rect(cx - 11, gy - 6, 2, 2, IronL);
        }

        static void Steinbruch(PixelCanvas c, int cx, int gy)
        {
            // Felswand
            c.Tri(cx - 14, gy, cx - 4, gy - 13, cx + 4, gy, StoneD);
            c.Tri(cx - 9, gy, cx - 4, gy - 13, cx + 2, gy, Stone);
            c.Rect(cx - 7, gy - 6, 6, 3, StoneL); c.Rect(cx - 10, gy - 2, 7, 2, StoneL);
            c.HLine(cx - 7, cx - 2, gy - 3, Mortar);
            // Steinblöcke
            Block(c, cx + 3, gy, 5); Block(c, cx + 8, gy, 5); Block(c, cx + 5, gy - 4, 5);
            // Kran
            c.Line(cx + 12, gy, cx + 14, gy - 13, Beam); c.Line(cx + 16, gy, cx + 14, gy - 13, Beam);
            c.HLine(cx + 7, cx + 15, gy - 13, BeamL);
            c.VLine(cx + 8, gy - 12, gy - 9, Cloth); c.Rect(cx + 7, gy - 9, 3, 2, StoneL);
        }

        static void Block(PixelCanvas c, int x, int gy, int s)
        {
            c.Rect(x, gy - s + 1, s, s, Stone);
            c.HLine(x, x + s - 1, gy - s + 1, StoneL);
            c.VLine(x + s - 1, gy - s + 1, gy, StoneD);
        }

        static void Mine(PixelCanvas c, int cx, int gy, bool gold)
        {
            // Hügel
            c.Ellipse(cx, gy + 2, 14, 13, StoneD);
            c.Ellipse(cx + 2, gy + 1, 11, 11, Stone);
            for (int k = 0; k < 14; k++)
            {
                int x = cx - 10 + (int)(PixelCanvas.Rnd(k, 1, 3) * 22), y = gy - 9 + (int)(PixelCanvas.Rnd(k, 2, 3) * 8);
                if (c.Opaque(x, y)) c.Set(x, y, PixelCanvas.Rnd(k, 3, 3) < 0.5f ? StoneL : Mortar);
            }
            c.Rect(cx - 14, gy - 1, 29, 3, Px.Clear);
            // Stollen
            c.Rect(cx - 4, gy - 6, 7, 7, K);
            c.Ellipse(cx - 0.5f, gy - 6, 3.5f, 2, K);
            c.VLine(cx - 5, gy - 7, gy, Beam); c.VLine(cx + 3, gy - 7, gy, Beam);
            c.HLine(cx - 6, cx + 4, gy - 8, BeamL);
            c.Set(cx + 4, gy - 6, Lit);
            // Schienen und Lore
            c.HLine(cx + 3, cx + 13, gy, IronD);
            c.Rect(cx + 6, gy - 4, 6, 3, IronD); c.HLine(cx + 6, cx + 11, gy - 4, Iron);
            Px ore = gold ? Gold : Rust, oreL = gold ? GoldL : RustL;
            c.Rect(cx + 7, gy - 6, 4, 2, ore); c.Set(cx + 8, gy - 6, oreL); c.Set(cx + 10, gy - 5, oreL);
            c.Set(cx + 7, gy - 1, K); c.Set(cx + 10, gy - 1, K);
        }

        static void Lagerhaus(PixelCanvas c, int cx, int gy)
        {
            int w = 22, wallH = 9, x0 = cx - w / 2, wy = gy - wallH + 1;
            Wall(c, x0, wy, w, wallH, WallStyle.Wood);
            c.HLine(x0, x0 + w - 1, gy, StoneD);
            RoofArea(c, x0 - 1, wy - 7, w + 2, 7, RoofBrown);
            c.Rect(cx - 6, gy - 6, 5, 7, DoorD); c.Rect(cx + 1, gy - 6, 5, 7, DoorD);
            c.VLine(cx - 4, gy - 6, gy, Door); c.VLine(cx + 3, gy - 6, gy, Door);
            Crate(c, x0 - 3, gy); Barrel(c, x0 + w + 1, gy);
        }

        static void Crate(PixelCanvas c, int x, int gy)
        {
            c.Rect(x, gy - 4, 5, 5, PlankL); c.Frame(x, gy - 4, 5, 5, PlankD);
            c.Line(x, gy - 4, x + 4, gy, PlankD);
        }

        static void Barrel(PixelCanvas c, int x, int gy)
        {
            c.Rect(x, gy - 5, 4, 6, Plank); c.VLine(x + 3, gy - 5, gy, PlankD);
            c.HLine(x, x + 3, gy - 4, IronD); c.HLine(x, x + 3, gy - 1, IronD);
        }

        static void Markt(PixelCanvas c, int cx, int gy)
        {
            Stall(c, cx - 1, gy - 4, Red, Cloth, new[] { Red, Gold, Red });
            Stall(c, cx - 11, gy, Blue, Cloth, new[] { Hay, Hay, PlankL });
            Stall(c, cx + 9, gy, Gold, Cloth, new[] { H("#7FBF4A"), Red, H("#7FBF4A") });
        }

        static void Stall(PixelCanvas c, int cx, int gy, Px a, Px b, Px[] goods)
        {
            int x0 = cx - 4;
            c.VLine(x0, gy - 8, gy, Beam); c.VLine(x0 + 8, gy - 8, gy, Beam);
            c.Rect(x0, gy - 3, 9, 4, Plank); c.HLine(x0, x0 + 8, gy - 3, PlankL);
            for (int i = 0; i < goods.Length; i++) c.Rect(x0 + 1 + i * 3, gy - 4, 2, 1, goods[i]);
            for (int y = gy - 10; y <= gy - 8; y++)
                for (int x = x0 - 1; x <= x0 + 9; x++)
                    c.Set(x, y, ((x - x0 + 1) / 2) % 2 == 0 ? a : b);
            for (int x = x0 - 1; x <= x0 + 9; x += 2) c.Set(x, gy - 7, ((x - x0 + 1) / 2) % 2 == 0 ? a : b);
        }

        static void Taverne(PixelCanvas c, int cx, int gy)
        {
            int w = 18, wallH = 13, x0 = cx - w / 2, wy = gy - wallH + 1;
            Chimney(c, x0 + 3, wy - 11, 5);
            Wall(c, x0, wy, w, 6, WallStyle.Wood);
            Wall(c, x0, wy + 6, w, wallH - 6, WallStyle.Plaster);
            c.HLine(x0 - 1, x0 + w, wy + 6, PlankDD);
            c.HLine(x0, x0 + w - 1, gy, StoneD);
            RoofArea(c, x0 - 1, wy - 9, w + 2, 9, RoofRed);
            c.HLine(x0, x0 + w - 1, wy, PlankDD);
            Window(c, x0 + 3, wy + 2, 2, 2, true); Window(c, x0 + 8, wy + 2, 2, 2, true); Window(c, x0 + 13, wy + 2, 2, 2, true);
            DoorAt(c, x0 + 3, gy - 4, 3, 4);
            Window(c, x0 + 9, gy - 4, 3, 2, true);
            c.HLine(x0 + 8, x0 + 12, gy - 1, PlankD); c.Set(x0 + 9, gy - 2, Red); c.Set(x0 + 11, gy - 2, Gold);
            // Schild mit Krug
            c.HLine(x0 + w, x0 + w + 4, wy + 7, Beam);
            c.Rect(x0 + w + 1, wy + 8, 5, 4, PlankL); c.Frame(x0 + w + 1, wy + 8, 5, 4, PlankDD);
            c.Rect(x0 + w + 2, wy + 9, 2, 2, Lit); c.Set(x0 + w + 2, wy + 9, Cloth); c.Set(x0 + w + 4, wy + 10, Lit);
        }

        static void Kapelle(PixelCanvas c, int cx, int gy)
        {
            int w = 14, wallH = 9, x0 = cx - 4, wy = gy - wallH + 1;
            Wall(c, x0, wy, w, wallH, WallStyle.White);
            RoofArea(c, x0 - 1, wy - 6, w + 2, 6, RoofSlate, 2);
            c.Ellipse(x0 + 8, wy + 3.5f, 2.2f, 2.2f, WinFrame);
            c.Set(x0 + 7, wy + 3, Red); c.Set(x0 + 8, wy + 3, Gold); c.Set(x0 + 7, wy + 4, Blue); c.Set(x0 + 8, wy + 4, Red);
            // Turm
            int tx = cx - 10, tw = 7, th = wallH + 9;
            Wall(c, tx, gy - th + 1, tw, th, WallStyle.White);
            c.Tri(tx - 1, gy - th + 1, tx + tw / 2f, gy - th - 10, tx + tw, gy - th + 1, RoofSlate.Mid);
            c.Tri(tx + tw / 2f, gy - th + 1, tx + tw / 2f, gy - th - 10, tx + tw, gy - th + 1, RoofSlate.Dark);
            c.VLine(tx + 3, gy - th - 14, gy - th - 10, Gold); c.HLine(tx + 2, tx + 4, gy - th - 13, Gold);
            c.Rect(tx + 2, gy - th + 3, 3, 3, K); c.Set(tx + 3, gy - th + 5, Gold);
            DoorAt(c, tx + 2, gy - 4, 3, 4, true);
        }

        static void Brunnen(PixelCanvas c, int cx, int gy)
        {
            c.Ellipse(cx, gy - 2, 6, 3, StoneD);
            c.Ellipse(cx, gy - 3, 6, 2.5f, Stone);
            c.Ellipse(cx, gy - 3, 4, 1.5f, H("#2F66A8"));
            c.HLine(cx - 2, cx, gy - 3, H("#58A8D8"));
            c.VLine(cx - 5, gy - 11, gy - 3, Beam); c.VLine(cx + 5, gy - 11, gy - 3, Beam);
            Gable(c, cx, gy - 11, 7, 4, RoofRed, RoofRed.Mid);
            c.VLine(cx, gy - 10, gy - 7, Cloth);
            c.Rect(cx - 1, gy - 7, 3, 2, Plank);
        }

        static void Schmiede(PixelCanvas c, int cx, int gy)
        {
            int w = 15, wallH = 8, x0 = cx - w / 2 - 2, wy = gy - wallH + 1;
            // Schornstein mit Rauch
            c.Rect(x0 + w - 5, wy - 11, 4, 10, StoneD); c.HLine(x0 + w - 5, x0 + w - 2, wy - 11, Stone);
            c.Ellipse(x0 + w - 2, wy - 14, 2.5f, 2, Smoke); c.Ellipse(x0 + w + 1, wy - 17, 2, 1.6f, SmokeL);
            Wall(c, x0, wy, w, wallH, WallStyle.Stone);
            RoofArea(c, x0 - 1, wy - 6, w + 2, 6, RoofSlate, 2);
            // offene Esse
            c.Rect(x0 + 2, gy - 5, 6, 6, K);
            c.Rect(x0 + 3, gy - 3, 4, 3, Fire); c.HLine(x0 + 4, x0 + 5, gy - 2, FireL);
            Window(c, x0 + 11, gy - 5, 2, 2, true);
            // Amboss
            int ax = x0 + w + 2;
            c.Rect(ax, gy - 4, 6, 2, IronD); c.HLine(ax, ax + 5, gy - 4, Iron); c.Rect(ax + 2, gy - 2, 2, 3, IronD);
        }

        static void Kaserne(PixelCanvas c, int cx, int gy)
        {
            int w = 24, wallH = 9, x0 = cx - w / 2, wy = gy - wallH + 1;
            Wall(c, x0, wy, w, wallH, WallStyle.Stone);
            RoofArea(c, x0 - 1, wy - 7, w + 2, 7, RoofRed);
            c.HLine(x0, x0 + w - 1, wy, StoneD);
            for (int k = 0; k < 4; k++) if (k != 2) Window(c, x0 + 3 + k * 5, wy + 2, 2, 2);
            DoorAt(c, cx - 1, gy - 5, 4, 5);
            Flag(c, x0 + 3, wy - 7, 8);
            // Waffenständer
            int rx = x0 + w + 2;
            for (int k = 0; k < 3; k++) { c.VLine(rx + k * 2, gy - 9, gy, BeamL); c.Set(rx + k * 2, gy - 10, IronL); }
            c.HLine(rx - 1, rx + 5, gy - 4, Beam);
        }

        static void Archerie(PixelCanvas c, int cx, int gy)
        {
            int x0 = cx - 8;
            c.VLine(x0, gy - 11, gy, Beam); c.VLine(x0 + 8, gy - 11, gy, Beam);
            c.Line(x0, gy, x0 + 8, gy - 8, BeamL); c.Line(x0 + 8, gy, x0, gy - 8, BeamL);
            c.Rect(x0 - 1, gy - 13, 11, 3, Plank); c.HLine(x0 - 1, x0 + 9, gy - 13, PlankL);
            Wall(c, x0, gy - 18, 9, 5, WallStyle.Wood);
            c.Rect(x0 + 3, gy - 17, 3, 2, K);
            c.Tri(x0 - 2, gy - 18, x0 + 4.5f, gy - 25, x0 + 11, gy - 18, RoofThatch.Mid);
            c.Tri(x0 + 4.5f, gy - 18, x0 + 4.5f, gy - 25, x0 + 11, gy - 18, RoofThatch.Dark);
            // Zielscheibe
            int tx = cx + 7;
            c.Line(tx - 2, gy, tx, gy - 5, Beam); c.Line(tx + 3, gy, tx + 1, gy - 5, Beam);
            c.Ellipse(tx + 0.5f, gy - 7, 4, 4, Cloth);
            c.Ellipse(tx + 0.5f, gy - 7, 3, 3, Red);
            c.Ellipse(tx + 0.5f, gy - 7, 1.8f, 1.8f, Cloth);
            c.Set(tx, gy - 8, Red);
        }

        static void StallBuilding(PixelCanvas c, int cx, int gy)
        {
            int w = 22, wallH = 8, x0 = cx - w / 2, wy = gy - wallH + 1;
            Wall(c, x0, wy, w, wallH, WallStyle.Wood);
            c.HLine(x0, x0 + w - 1, gy, StoneD);
            RoofArea(c, x0 - 1, wy - 7, w + 2, 7, RoofThatch, 1);
            for (int k = 0; k < 3; k++)
            {
                int dx = x0 + 2 + k * 7;
                c.Rect(dx, gy - 6, 5, 7, DoorD);
                c.HLine(dx, dx + 4, gy - 3, PlankL);
                if (k == 1) { c.Rect(dx + 1, gy - 6, 3, 3, H("#8A5A34")); c.Set(dx + 2, gy - 6, Cloth); c.Set(dx + 1, gy - 5, K); }
            }
            // Zaun
            for (int x = x0 - 6; x <= x0 - 1; x += 2) c.VLine(x, gy - 4, gy, LogL);
            c.HLine(x0 - 6, x0 - 1, gy - 3, LogD); c.HLine(x0 - 6, x0 - 1, gy - 1, LogD);
        }

        static void Hafen(PixelCanvas c, int cx, int gy)
        {
            // Steg
            int x0 = cx - 14;
            c.Rect(x0, gy - 4, 28, 4, Plank);
            for (int x = x0; x < x0 + 28; x += 3) c.VLine(x, gy - 4, gy - 1, PlankD);
            for (int x = x0 + 1; x < x0 + 28; x += 6) c.VLine(x, gy, gy + 2, PlankDD);
            c.HLine(x0, x0 + 27, gy - 4, PlankL);
            Crate(c, x0 + 2, gy - 4); Barrel(c, x0 + 8, gy - 4);
            // Kran
            c.VLine(x0 + 15, gy - 16, gy - 4, Beam); c.HLine(x0 + 15, x0 + 21, gy - 16, Beam);
            c.Line(x0 + 15, gy - 12, x0 + 19, gy - 16, Beam);
            c.VLine(x0 + 21, gy - 15, gy - 10, Cloth); c.Rect(x0 + 20, gy - 10, 3, 2, Hay);
            // Boot
            int bx = cx + 6;
            c.Rect(bx, gy + 3, 12, 3, Plank); c.HLine(bx + 1, bx + 10, gy + 6, PlankD); c.HLine(bx, bx + 11, gy + 3, PlankL);
            c.VLine(bx + 6, gy - 7, gy + 2, Beam);
            c.Tri(bx + 7, gy - 7, bx + 7, gy + 1, bx + 12, gy + 1, Cloth);
        }

        static void Kathedrale(PixelCanvas c, int cx, int gy)
        {
            int w = 22, wallH = 16, x0 = cx - w / 2, wy = gy - wallH + 1;
            Wall(c, x0, wy, w, wallH, WallStyle.White);
            RoofArea(c, x0 - 1, wy - 9, w + 2, 9, RoofSlate, 2);
            Gable(c, cx, wy, 8, 7, RoofSlate, White);
            // Türme
            foreach (int tx in new[] { x0 - 7, x0 + w })
            {
                int th = wallH + 12;
                Wall(c, tx, gy - th + 1, 8, th, WallStyle.White);
                c.Tri(tx - 1, gy - th + 1, tx + 4, gy - th - 12, tx + 9, gy - th + 1, RoofSlate.Mid);
                c.Tri(tx + 4, gy - th + 1, tx + 4, gy - th - 12, tx + 9, gy - th + 1, RoofSlate.Dark);
                c.Set(tx + 4, gy - th - 13, Gold);
                c.Rect(tx + 3, gy - th + 4, 2, 5, K);
                c.Rect(tx + 3, gy - th + 13, 2, 4, Glass);
            }
            // Rosette und Portal
            c.Ellipse(cx, wy + 3, 3.4f, 3.4f, WinFrame);
            c.Ellipse(cx, wy + 3, 2.4f, 2.4f, Blue);
            c.Set(cx - 1, wy + 2, Red); c.Set(cx, wy + 3, Gold); c.Set(cx, wy + 2, Red); c.Set(cx - 1, wy + 3, Gold);
            DoorAt(c, cx - 3, gy - 7, 6, 7, true);
            c.VLine(cx, gy - 7, gy - 1, DoorD);
            Window(c, x0 + 3, wy + 7, 2, 4); Window(c, x0 + w - 5, wy + 7, 2, 4);
        }

        static void Werkstatt(PixelCanvas c, int cx, int gy)
        {
            int x0 = cx - 12;
            for (int k = 0; k < 3; k++) c.VLine(x0 + k * 12, gy - 10, gy, Beam);
            RoofArea(c, x0 - 2, gy - 15, 28, 5, RoofThatch, 1);
            // Ramme
            c.Rect(x0 + 2, gy - 6, 18, 3, Log); c.HLine(x0 + 2, x0 + 19, gy - 6, LogL);
            c.Rect(x0 + 19, gy - 7, 4, 5, IronD); c.Set(x0 + 22, gy - 5, IronL);
            c.Ellipse(x0 + 6, gy - 1, 2, 2, PlankDD); c.Ellipse(x0 + 16, gy - 1, 2, 2, PlankDD);
            c.Set(x0 + 6, gy - 1, IronL); c.Set(x0 + 16, gy - 1, IronL);
        }

        static void Windmuehle(PixelCanvas c, int cx, int gy)
        {
            // runder Turm, kegelförmig
            for (int y = gy - 16; y <= gy; y++)
            {
                int half = 4 + (y - (gy - 16)) / 6;
                for (int x = cx - half; x <= cx + half; x++) c.Set(x, y, x > cx + half / 2 ? PlasterD : Plaster);
            }
            c.HLine(cx - 6, cx + 6, gy, StoneD);
            DoorAt(c, cx - 1, gy - 4, 3, 4, true);
            Window(c, cx, gy - 11, 1, 2);
            c.Tri(cx - 6, gy - 16, cx, gy - 23, cx + 6, gy - 16, RoofBrown.Mid);
            c.Tri(cx, gy - 16, cx, gy - 23, cx + 6, gy - 16, RoofBrown.Dark);
            // Flügel (X)
            int hx = cx, hy = gy - 18;
            for (int k = -11; k <= 11; k++)
            {
                c.Set(hx + k, hy + k, Beam); c.Set(hx + k, hy - k, Beam);
                if (System.Math.Abs(k) > 3)
                {
                    int sgn = k > 0 ? 1 : -1;
                    c.Set(hx + k, hy + k - sgn, Cloth); c.Set(hx + k + sgn, hy + k, Cloth);
                    c.Set(hx + k, hy - k + sgn, Cloth); c.Set(hx + k - sgn, hy - k, Cloth);
                }
            }
            c.Rect(hx - 1, hy - 1, 3, 3, Beam);
        }

        static void Kloster(PixelCanvas c, int cx, int gy)
        {
            // Kreuzgang-Mauer mit Klosterkirche
            int w = 30, x0 = cx - w / 2;
            Wall(c, x0, gy - 6, w, 7, WallStyle.Stone);
            for (int k = 0; k < 6; k++) { c.Rect(x0 + 2 + k * 5, gy - 4, 3, 5, K); c.Set(x0 + 2 + k * 5, gy - 4, Px.Clear); c.Set(x0 + 4 + k * 5, gy - 4, Px.Clear); }
            RoofArea(c, x0 - 1, gy - 10, w + 2, 4, RoofRed);
            // Kirche dahinter
            int kx = cx - 8;
            Wall(c, kx, gy - 20, 16, 10, WallStyle.White);
            RoofArea(c, kx - 1, gy - 26, 18, 6, RoofSlate, 2);
            Wall(c, kx + 13, gy - 30, 6, 20, WallStyle.White);
            c.Tri(kx + 12, gy - 30, kx + 16, gy - 38, kx + 20, gy - 30, RoofSlate.Mid);
            c.Tri(kx + 16, gy - 30, kx + 16, gy - 38, kx + 20, gy - 30, RoofSlate.Dark);
            c.VLine(kx + 16, gy - 42, gy - 38, Gold); c.HLine(kx + 15, kx + 17, gy - 41, Gold);
            c.Ellipse(kx + 7, gy - 16, 2, 2, WinFrame); c.Set(kx + 7, gy - 16, Blue);
            // Klostergarten
            c.Rect(x0 - 6, gy - 2, 5, 3, H("#4E8A34")); c.Set(x0 - 5, gy - 2, Red); c.Set(x0 - 3, gy - 1, Gold);
        }

        static void Turnierplatz(PixelCanvas c, int cx, int gy)
        {
            // Schranken
            c.HLine(cx - 18, cx + 18, gy - 3, LogL); c.HLine(cx - 18, cx + 18, gy - 2, LogD);
            for (int x = cx - 18; x <= cx + 18; x += 6) c.VLine(x, gy - 5, gy, Log);
            // Zelte mit Wimpeln
            foreach (int tx in new[] { cx - 13, cx + 13 })
            {
                bool left = tx < cx;
                c.Tri(tx - 6, gy - 4, tx, gy - 14, tx + 6, gy - 4, left ? Red : Blue);
                c.Tri(tx, gy - 14, tx + 6, gy - 4, tx + 1, gy - 4, left ? Red.Mul(0.75f) : Blue.Mul(0.75f));
                for (int y = gy - 12; y <= gy - 4; y += 3) c.HLine(tx - 5 + (gy - 4 - y) / 2, tx + 5 - (gy - 4 - y) / 2, y, Cloth);
                c.Rect(tx - 1, gy - 8, 3, 4, K);
                c.VLine(tx, gy - 19, gy - 14, Beam); c.Rect(tx + 1, gy - 19, 3, 2, Px.Tint);
            }
            // Tribüne in der Mitte
            c.Rect(cx - 6, gy - 10, 13, 6, Plank); c.HLine(cx - 6, cx + 6, gy - 10, PlankL);
            c.Rect(cx - 7, gy - 13, 15, 3, Px.Tint); c.HLine(cx - 7, cx + 7, gy - 11, Px.TintDark);
            c.Set(cx - 4, gy - 9, Skin); c.Set(cx, gy - 9, Skin); c.Set(cx + 4, gy - 9, Skin);
        }

        static void Ruine(PixelCanvas c, int cx, int gy)
        {
            var moss = H("#5E8C45");
            // zerfallene Mauerreste
            Wall(c, cx - 14, gy - 9, 6, 10, WallStyle.Stone);
            c.Rect(cx - 14, gy - 9, 2, 2, Px.Clear); c.Set(cx - 10, gy - 9, Px.Clear);
            Wall(c, cx + 6, gy - 13, 7, 14, WallStyle.Stone);
            c.Rect(cx + 10, gy - 13, 3, 3, Px.Clear); c.Set(cx + 6, gy - 13, Px.Clear);
            // Bogen in der Mitte
            Wall(c, cx - 6, gy - 16, 3, 17, WallStyle.Stone);
            Wall(c, cx + 1, gy - 16, 3, 17, WallStyle.Stone);
            c.Rect(cx - 6, gy - 18, 10, 3, Stone); c.HLine(cx - 6, cx + 3, gy - 18, StoneL);
            c.Rect(cx - 3, gy - 15, 4, 16, Px.Clear);
            // Geröll und Moos
            Block(c, cx - 4, gy, 3); Block(c, cx + 2, gy, 2); Block(c, cx - 9, gy, 3);
            for (int k = 0; k < 14; k++)
            {
                int x = cx - 14 + (int)(PixelCanvas.Rnd(k, 3, 41) * 27), y = gy - 17 + (int)(PixelCanvas.Rnd(k, 4, 41) * 17);
                if (c.Opaque(x, y)) c.Set(x, y, moss);
            }
        }

        static void Raeuberlager(PixelCanvas c, int cx, int gy)
        {
            var tent = H("#7A6A55"); var tentD = H("#5A4C3C");
            // Palisade hinten
            for (int x = cx - 16; x <= cx + 16; x += 2) { c.VLine(x, gy - 12, gy - 6, LogD); c.Set(x, gy - 13, LogL); }
            // Zelte
            foreach (int tx in new[] { cx - 9, cx + 8 })
            {
                c.Tri(tx - 7, gy, tx, gy - 11, tx + 7, gy, tent);
                c.Tri(tx, gy - 11, tx + 7, gy, tx + 1, gy, tentD);
                c.Tri(tx - 2, gy, tx, gy - 5, tx + 2, gy, K);
            }
            // Lagerfeuer
            c.Rect(cx - 2, gy - 1, 5, 2, LogD);
            c.Set(cx, gy - 3, FireL); c.Set(cx - 1, gy - 2, Fire); c.Set(cx + 1, gy - 2, Fire); c.Set(cx, gy - 2, FireL); c.Set(cx, gy - 4, Fire);
            // Totenkopf-Fahne
            c.VLine(cx + 16, gy - 20, gy, Beam);
            c.Rect(cx + 17, gy - 20, 6, 5, H("#2A2A2A"));
            c.Set(cx + 19, gy - 19, WhiteL); c.Set(cx + 20, gy - 19, WhiteL); c.Set(cx + 19, gy - 18, WhiteL); c.Set(cx + 20, gy - 18, WhiteL);
            // Beutekiste
            Crate(c, cx - 3, gy - 6);
        }

        // ------------------------------------------------------------------
        //  Ortszentren
        // ------------------------------------------------------------------
        static void Dorfhaus(PixelCanvas c, int cx, int gy)
        {
            House(c, cx - 2, gy, 18, 9, 10, WallStyle.Plaster, RoofRed, 0, true, 2);
            Gable(c, cx + 2, gy - 9, 5, 6, RoofRed, Plaster);
            Window(c, cx + 1, gy - 12, 2, 2);
            Flag(c, cx + 10, gy, 14);
        }

        static void Rathaus(PixelCanvas c, int cx, int gy)
        {
            int w = 24, x0 = cx - w / 2;
            Wall(c, x0, gy - 5, w, 6, WallStyle.Stone);
            Wall(c, x0, gy - 13, w, 8, WallStyle.Plaster);
            RoofArea(c, x0 - 1, gy - 22, w + 2, 9, RoofSlate, 2);
            c.HLine(x0, x0 + w - 1, gy - 13, Beam);
            for (int k = 0; k < 4; k++) Window(c, x0 + 3 + k * 6, gy - 10, 2, 2);
            for (int k = 0; k < 4; k++) if (k != 1 && k != 2) Window(c, x0 + 3 + k * 6, gy - 3, 2, 2);
            DoorAt(c, cx - 2, gy - 4, 4, 4, true);
            // Uhrturm
            int tx = cx - 4;
            Wall(c, tx, gy - 33, 8, 12, WallStyle.Plaster);
            c.Ellipse(tx + 4, gy - 28, 2.6f, 2.6f, WhiteL); c.Set(tx + 4, gy - 28, K); c.Set(tx + 4, gy - 29, K); c.Set(tx + 5, gy - 28, K);
            c.Tri(tx - 1, gy - 33, tx + 4, gy - 42, tx + 9, gy - 33, RoofSlate.Mid);
            c.Tri(tx + 4, gy - 33, tx + 4, gy - 42, tx + 9, gy - 33, RoofSlate.Dark);
            Flag(c, tx + 4, gy - 42, 5);
        }

        static void Bergfried(PixelCanvas c, int cx, int gy)
        {
            int w = 16, h = 26, x0 = cx - w / 2;
            Wall(c, x0, gy - h + 1, w, h, WallStyle.Stone);
            Battlements(c, x0 - 1, gy - h, w + 2, Stone, StoneD);
            Window(c, x0 + 4, gy - h + 6, 2, 3); Window(c, x0 + 10, gy - h + 6, 2, 3);
            Window(c, x0 + 7, gy - h + 14, 2, 3);
            DoorAt(c, cx - 2, gy - 6, 4, 6, true);
            Flag(c, cx, gy - h - 2, 8);
            // Anbau
            House(c, cx + 14, gy, 11, 6, 6, WallStyle.Plaster, RoofRed, 0, false, 1);
        }

        static void Palast(PixelCanvas c, int cx, int gy)
        {
            int w = 30, x0 = cx - w / 2;
            Wall(c, x0, gy - 15, w, 16, WallStyle.Dark);
            RoofArea(c, x0 - 1, gy - 24, w + 2, 9, RoofGold);
            Gable(c, cx, gy - 15, 9, 9, RoofGold, DStone);
            c.Ellipse(cx, gy - 19, 2, 2, Px.Tint);
            foreach (int tx in new[] { x0 - 8, x0 + w })
            {
                Wall(c, tx, gy - 27, 9, 28, WallStyle.Dark);
                c.Tri(tx - 1, gy - 27, tx + 4.5f, gy - 39, tx + 10, gy - 27, RoofGold.Mid);
                c.Tri(tx + 4.5f, gy - 27, tx + 4.5f, gy - 39, tx + 10, gy - 27, RoofGold.Dark);
                Flag(c, tx + 4, gy - 39, 4);
                Window(c, tx + 3, gy - 20, 2, 3, true);
            }
            for (int k = 0; k < 4; k++) Window(c, x0 + 3 + k * 7, gy - 11, 2, 3, true);
            DoorAt(c, cx - 3, gy - 7, 6, 7, true);
            c.Rect(cx - 8, gy - 1, 16, 2, Red);
        }

        static void Burg(PixelCanvas c, int cx, int gy)
        {
            // Bergfried hinten
            int kw = 14, kh = 30, kx = cx - kw / 2;
            Wall(c, kx, gy - kh - 6, kw, kh, WallStyle.Stone);
            Battlements(c, kx - 1, gy - kh - 7, kw + 2, Stone, StoneD);
            Window(c, kx + 6, gy - kh + 1, 2, 3);
            Flag(c, cx, gy - kh - 9, 8);
            // Ringmauer
            int w = 40, x0 = cx - w / 2;
            Wall(c, x0, gy - 11, w, 12, WallStyle.Stone);
            Battlements(c, x0, gy - 12, w, Stone, StoneD);
            // Tor
            c.Rect(cx - 4, gy - 8, 8, 9, K);
            c.Ellipse(cx, gy - 8, 4, 2.5f, K);
            for (int x = cx - 3; x <= cx + 3; x += 2) c.VLine(x, gy - 8, gy, IronD);
            c.HLine(cx - 3, cx + 3, gy - 4, IronD);
            // Türme vorne
            foreach (int tx in new[] { x0 - 5, x0 + w - 4 })
            {
                Wall(c, tx, gy - 19, 9, 20, WallStyle.Stone);
                c.Tri(tx - 2, gy - 19, tx + 4.5f, gy - 29, tx + 11, gy - 19, RoofBlue.Mid);
                c.Tri(tx + 4.5f, gy - 19, tx + 4.5f, gy - 29, tx + 11, gy - 19, RoofBlue.Dark);
                c.Rect(tx + 3, gy - 14, 3, 4, K);
            }
        }

        // ------------------------------------------------------------------
        //  Befestigung
        // ------------------------------------------------------------------
        static void WallH(PixelCanvas c, int cx, int gy, bool stone)
        {
            int x0 = cx - 8;
            if (stone)
            {
                Wall(c, x0, gy - 8, 16, 9, WallStyle.Stone);
                Battlements(c, x0, gy - 9, 16, Stone, StoneD);
            }
            else
            {
                for (int x = x0; x < x0 + 16; x += 2)
                {
                    c.Rect(x, gy - 9, 2, 10, Log);
                    c.Set(x, gy - 9, LogL); c.VLine(x + 1, gy - 9, gy, LogD);
                    c.Set(x, gy - 10, LogL);
                }
                c.HLine(x0, x0 + 15, gy - 6, Beam); c.HLine(x0, x0 + 15, gy - 2, Beam);
            }
        }

        static void WallV(PixelCanvas c, int cx, int gy, bool stone)
        {
            int x0 = cx - 2;
            for (int y = gy - 17; y <= gy; y++)
            {
                c.Set(x0, y, stone ? StoneL : LogL);
                c.Set(x0 + 1, y, stone ? Stone : Log);
                c.Set(x0 + 2, y, stone ? Stone : Log);
                c.Set(x0 + 3, y, stone ? StoneD : LogD);
                if (stone && (y - gy) % 4 == 0) c.HLine(x0, x0 + 3, y, Mortar);
                if (!stone && (y - gy) % 3 == 0) c.Set(x0 + 1, y, LogEnd);
            }
            if (stone) { c.Set(x0 - 1, gy - 17, Stone); c.Set(x0 + 4, gy - 17, Stone); }
        }

        static void Tower(PixelCanvas c, int cx, int gy, bool stone)
        {
            int x0 = cx - 5;
            Wall(c, x0, gy - 17, 10, 18, stone ? WallStyle.Stone : WallStyle.Log);
            var r = stone ? RoofRed : RoofThatch;
            c.Tri(x0 - 2, gy - 17, cx, gy - 26, x0 + 12, gy - 17, r.Mid);
            c.Tri(cx, gy - 17, cx, gy - 26, x0 + 12, gy - 17, r.Dark);
            c.Rect(cx - 1, gy - 13, 2, 3, K);
        }

        static void Gate(PixelCanvas c, int cx, int gy, bool stone)
        {
            WallH(c, cx, gy, stone);
            c.Rect(cx - 3, gy - 6, 6, 7, K);
            c.Ellipse(cx, gy - 6, 3, 2, K);
            if (stone) for (int x = cx - 2; x <= cx + 2; x += 2) c.VLine(x, gy - 6, gy, IronD);
            else c.Rect(cx - 2, gy - 5, 4, 6, Door);
        }

        // ------------------------------------------------------------------
        //  Natur
        // ------------------------------------------------------------------
        static void Oak(PixelCanvas c, int cx, int gy, int size, ArtPalette p, int seed)
        {
            float r = size;
            c.Rect(cx - 1, gy - 4, 2, 5, p.Trunk);
            c.Set(cx, gy - 4, p.Trunk.Dark(0.3f));
            float ccy = gy - 4 - r * 0.85f;
            c.Ellipse(cx, ccy, r, r * 0.9f, p.LeafDark);
            c.Ellipse(cx + r * 0.15f, ccy - r * 0.12f, r * 0.82f, r * 0.72f, p.Leaf);
            // Lichtflecken oben rechts
            for (int k = 0; k < 10; k++)
            {
                float a = (float)(PixelCanvas.Rnd(seed, k, 1) * Math.PI * 2);
                float d = PixelCanvas.Rnd(seed, k, 2) * r * 0.6f;
                int x = (int)(cx + r * 0.25f + Math.Cos(a) * d), y = (int)(ccy - r * 0.3f + Math.Sin(a) * d * 0.8f);
                if (c.Opaque(x, y)) { c.Set(x, y, p.LeafLight); if (PixelCanvas.Rnd(seed, k, 3) < 0.5f) c.Set(x + 1, y, p.LeafLight); }
            }
            for (int k = 0; k < 6; k++)
            {
                int x = (int)(cx - r * 0.6f + PixelCanvas.Rnd(seed, k, 4) * r), y = (int)(ccy + PixelCanvas.Rnd(seed, k, 5) * r * 0.6f);
                if (c.Opaque(x, y)) c.Set(x, y, p.LeafDark);
            }
        }

        static void PineTree(PixelCanvas c, int cx, int gy, int size, ArtPalette p, bool snow)
        {
            c.Rect(cx - 1, gy - 2, 2, 3, p.Trunk);
            int tiers = 3;
            float top = gy - 3 - size * 2.6f;
            for (int t = 0; t < tiers; t++)
            {
                float ty = top + t * size * 0.75f;
                float by = ty + size * 1.25f;
                float hw = size * (0.55f + t * 0.28f);
                c.Tri(cx - hw, by, cx, ty, cx + hw, by, p.PineDark);
                c.Tri(cx, by, cx, ty + 1, cx + hw - 1, by - 1, p.Pine);
                c.Line((int)cx + 1, (int)ty + 2, (int)(cx + hw - 2), (int)by - 1, p.PineLight);
                if (snow)
                {
                    c.Line((int)(cx - hw * 0.5f), (int)(ty + (by - ty) * 0.5f), (int)cx, (int)ty + 1, H("#EEF4F8"));
                    c.HLine((int)(cx - hw + 1), (int)(cx - 1), (int)by - 1, H("#DCE6EE"));
                }
            }
        }

        /// <summary>
        /// Ein Gipfel im Pixel-Stil: links die beleuchtete Flanke, rechts die Schattenflanke,
        /// oben eine Schneekappe (links grau, rechts weiß), dazu Felsrinnen. Gestufte Kanten.
        /// </summary>
        static void Mountain(PixelCanvas c, int cx, int gy, int w, int h, bool snowy, ArtPalette p, int seed)
        {
            int top = gy - h;
            int peakX = cx + (int)((PixelCanvas.Rnd(seed, 1, 9) - 0.5f) * w * 0.12f);
            float capFrac = snowy ? 0.42f : 0.26f;
            // Grat (Trennlinie zwischen Licht- und Schattenflanke) schlängelt sich vom Gipfel nach unten
            for (int y = top; y <= gy; y++)
            {
                float t = (y - top) / (float)h;
                int half = (int)Math.Round(t * w / 2f);
                // gestufte, leicht unregelmäßige Kanten wie bei Pixel-Gebirgen
                int jagL = (int)(PixelCanvas.Rnd(y / 3, seed, 2) * 3) - 1;
                int jagR = (int)(PixelCanvas.Rnd(y / 3, seed, 3) * 3) - 1;
                int x0 = peakX - half + (t > 0.15f ? jagL : 0), x1 = peakX + half + (t > 0.15f ? jagR : 0);
                int ridge = peakX + (int)Math.Round(Math.Sin(t * 5 + seed) * w * 0.05f * t + t * w * 0.08f);
                int capLine = top + (int)(h * capFrac) + (int)((PixelCanvas.Rnd((y + seed) / 2, 7, seed) - 0.5f) * 4);
                for (int x = x0; x <= x1; x++)
                {
                    bool lit = x < ridge;
                    Px col = lit ? p.MountA : p.MountB;
                    if (y < capLine - Math.Abs(x - peakX) / 3) col = lit ? p.CapGrey : p.CapSnow;
                    else if (y < capLine + 2 - Math.Abs(x - peakX) / 4 && ((x + y) & 1) == 0) col = lit ? p.CapGrey : p.CapSnow;
                    c.Set(x, y, col);
                }
                // dunkle Kante direkt am Grat auf der Schattenseite
                if (y > capLine) { c.Set(ridge, y, p.MountD); }
            }
            // Felsrinnen: auf der Lichtseite dunkle, auf der Schattenseite helle Striche
            int gullies = Math.Max(3, w / 9);
            for (int k = 0; k < gullies; k++)
            {
                bool litSide = k % 2 == 0;
                float fx = (PixelCanvas.Rnd(seed, k, 31) * 0.7f + 0.15f);
                int sx = litSide ? peakX - (int)(fx * w * 0.4f) : peakX + (int)(fx * w * 0.4f);
                int sy = top + (int)(h * (capFrac + 0.05f + PixelCanvas.Rnd(seed, k, 32) * 0.25f));
                int len = (int)(h * (0.2f + PixelCanvas.Rnd(seed, k, 33) * 0.3f));
                for (int d = 0; d < len; d++)
                {
                    int x = sx + (litSide ? -d / 4 : d / 4), y = sy + d;
                    if (!c.Opaque(x, y) || y >= gy) continue;
                    Px cur = c.Get(x, y);
                    if (cur.Same(p.CapGrey) || cur.Same(p.CapSnow)) continue;
                    c.Set(x, y, litSide ? p.MountB : p.MountC);
                    if (d % 3 == 0 && c.Opaque(x + 1, y)) c.Set(x + 1, y, litSide ? p.MountD : p.MountA);
                }
            }
            // Fuß etwas dunkler
            for (int x = cx - w / 2 - 2; x <= cx + w / 2 + 2; x++)
                for (int y = gy - 2; y <= gy; y++)
                    if (c.Opaque(x, y)) c.Set(x, y, c.Get(x, y).Mul(0.85f));
        }

        /// <summary>Gebirgsmassiv: mehrere Gipfel, hintere zuerst – wirkt wie eine echte Gebirgskette.</summary>
static void Massif(PixelCanvas c, int cx, int gy, int w, int h, bool snowy, ArtPalette p, int seed, int peaks)
        {
            // Seitliche, kleinere Gipfel hinten – der Hauptgipfel vorne überlappt sie zu einer geschlossenen Wand
            if (peaks >= 2)
            {
                Mountain(c, cx - (int)(w * 0.3f), gy - 2, (int)(w * 0.6f), (int)(h * 0.66f), snowy, p, seed * 7 + 1);
                Mountain(c, cx + (int)(w * 0.32f), gy - 1, (int)(w * 0.56f), (int)(h * 0.6f), snowy, p, seed * 7 + 2);
            }
            Mountain(c, cx, gy, (int)(w * 0.78f), h, snowy, p, seed * 13 + 5);
        }

                static void Hill(PixelCanvas c, int cx, int gy, ArtPalette p, int seed)
        {
            c.Ellipse(cx, gy, 10, 6, p.Hills.Mul(0.82f));
            c.Ellipse(cx + 1.5f, gy - 0.8f, 8, 4.8f, p.Hills);
            c.Ellipse(cx + 3, gy - 2.5f, 4, 2, p.Hills.Light(0.15f));
            c.Rect(cx - 11, gy + 1, 23, 7, Px.Clear);
            for (int k = 0; k < 5; k++)
            {
                int x = cx - 7 + (int)(PixelCanvas.Rnd(seed, k, 1) * 14), y = gy - 4 + (int)(PixelCanvas.Rnd(seed, k, 2) * 4);
                if (c.Opaque(x, y)) { c.Set(x, y, p.Hills.Mul(0.75f)); c.Set(x, y - 1, p.Hills.Light(0.2f)); }
            }
        }

        static void Rocks(PixelCanvas c, int cx, int gy, Px main, Px light, Px dark, Px speck, Px speck2, int seed)
        {
            void Boulder(float x, float y, float rx, float ry)
            {
                c.Ellipse(x, y, rx, ry, dark);
                c.Ellipse(x + 0.6f, y - 0.6f, rx - 0.8f, ry - 0.8f, main);
                c.Set((int)(x + rx * 0.3f), (int)(y - ry * 0.5f), light);
            }
            Boulder(cx - 3, gy - 2.5f, 4, 3); Boulder(cx + 3, gy - 2, 3.5f, 2.6f); Boulder(cx, gy - 5, 3.2f, 2.8f);
            for (int k = 0; k < 8; k++)
            {
                int x = cx - 6 + (int)(PixelCanvas.Rnd(seed, k, 1) * 12), y = gy - 7 + (int)(PixelCanvas.Rnd(seed, k, 2) * 7);
                if (c.Opaque(x, y) && !c.Get(x, y).Same(K)) c.Set(x, y, k % 3 == 0 ? speck2 : speck);
            }
        }

        static void FieldSprite(PixelCanvas c, int cx, int gy, Px a, Px b, Px soil)
        {
            int x0 = cx - 7, y0 = gy - 13;
            c.Rect(x0, y0, 15, 14, soil);
            for (int y = y0 + 1; y < y0 + 13; y += 3)
                for (int x = x0 + 1; x < x0 + 14; x++)
                {
                    c.Set(x, y, a); c.Set(x, y + 1, (x % 2 == 0) ? b : a);
                }
        }

        // ------------------------------------------------------------------
        //  Truppen
        // ------------------------------------------------------------------
        static void Person(PixelCanvas c, int cx, int gy, Px body, Px head, bool helmet)
        {
            c.Set(cx - 1, gy, K); c.Set(cx + 1, gy, K);
            c.VLine(cx - 1, gy - 2, gy - 1, SoilD); c.VLine(cx + 1, gy - 2, gy - 1, SoilD);
            c.Rect(cx - 2, gy - 7, 5, 5, body);
            c.VLine(cx + 2, gy - 7, gy - 3, body.Equals(Px.Tint) ? Px.TintDark : body.Mul(0.8f));
            c.Rect(cx - 1, gy - 10, 3, 3, Skin); c.Set(cx + 1, gy - 9, SkinD);
            if (helmet) { c.HLine(cx - 2, cx + 2, gy - 10, IronL); c.HLine(cx - 1, cx + 1, gy - 11, Iron); }
            else { c.HLine(cx - 1, cx + 1, gy - 11, head); c.Set(cx - 2, gy - 10, head); }
        }

        static void Horse(PixelCanvas c, int cx, int gy, Px col)
        {
            var d = col.Mul(0.75f);
            c.Rect(cx - 5, gy - 6, 10, 4, col);
            c.HLine(cx - 5, cx + 4, gy - 3, d);
            c.VLine(cx - 4, gy - 2, gy, d); c.VLine(cx - 2, gy - 2, gy, col); c.VLine(cx + 2, gy - 2, gy, d); c.VLine(cx + 4, gy - 2, gy, col);
            c.Rect(cx + 4, gy - 9, 3, 4, col); c.Rect(cx + 6, gy - 9, 2, 2, col); c.Set(cx + 7, gy - 8, d);
            c.VLine(cx + 4, gy - 9, gy - 6, Hair); c.Set(cx - 6, gy - 5, Hair); c.Set(cx - 6, gy - 4, Hair);
        }

        static void Unit(PixelCanvas c, int cx, int gy, UType t)
        {
            switch (t)
            {
                case UType.Miliz:
                    Person(c, cx, gy, H("#8A6A44"), Hair, false);
                    c.HLine(cx - 2, cx + 2, gy - 4, Px.Tint);
                    c.VLine(cx + 4, gy - 12, gy, BeamL); c.HLine(cx + 3, cx + 5, gy - 12, IronL); c.Set(cx + 3, gy - 13, IronL); c.Set(cx + 5, gy - 13, IronL);
                    break;
                case UType.Speer:
                    Person(c, cx, gy, Px.Tint, Hair, true);
                    c.VLine(cx + 4, gy - 15, gy, BeamL); c.VLine(cx + 4, gy - 17, gy - 16, IronL);
                    c.Rect(cx - 4, gy - 7, 3, 4, Px.TintDark); c.Set(cx - 3, gy - 6, Gold);
                    break;
                case UType.Bogen:
                    Person(c, cx, gy, Px.Tint, H("#4E7A3A"), false);
                    c.Set(cx - 2, gy - 9, H("#4E7A3A"));
                    c.Line(cx + 3, gy - 11, cx + 5, gy - 7, Beam); c.Line(cx + 5, gy - 7, cx + 3, gy - 3, Beam);
                    c.VLine(cx + 3, gy - 10, gy - 4, Cloth);
                    break;
                case UType.Ritter:
                    Horse(c, cx, gy, H("#8A5A34"));
                    c.Rect(cx - 2, gy - 11, 4, 5, Px.Tint); c.VLine(cx + 1, gy - 11, gy - 7, Px.TintDark);
                    c.Rect(cx - 2, gy - 14, 3, 3, IronL); c.Set(cx, gy - 13, K);
                    c.Line(cx + 2, gy - 9, cx + 10, gy - 15, BeamL); c.Set(cx + 10, gy - 16, IronL);
                    break;
                case UType.Ramme:
                    c.Rect(cx - 7, gy - 5, 14, 3, Log); c.HLine(cx - 7, cx + 6, gy - 5, LogL);
                    c.Rect(cx + 6, gy - 6, 3, 5, IronD);
                    c.Tri(cx - 8, gy - 6, cx, gy - 11, cx + 8, gy - 6, RoofThatch.Mid);
                    c.Ellipse(cx - 4, gy - 1, 1.6f, 1.6f, PlankDD); c.Ellipse(cx + 4, gy - 1, 1.6f, 1.6f, PlankDD);
                    c.Set(cx - 1, gy - 8, Px.Tint); c.Set(cx, gy - 8, Px.Tint);
                    break;
                case UType.Spaeher:
                    Horse(c, cx, gy, H("#C9A26A"));
                    c.Rect(cx - 2, gy - 11, 4, 5, Px.Tint);
                    c.Rect(cx - 1, gy - 13, 3, 2, Skin); c.HLine(cx - 2, cx + 1, gy - 14, H("#4E7A3A"));
                    c.Line(cx - 3, gy - 10, cx - 5, gy - 7, Px.TintDark);
                    break;
                default: // Siedlerwagen
                    c.Rect(cx - 5, gy - 6, 11, 4, Plank); c.HLine(cx - 5, cx + 5, gy - 6, PlankL);
                    c.Ellipse(cx, gy - 8, 5.5f, 4, Cloth); c.Rect(cx - 6, gy - 7, 13, 2, Px.Clear);
                    c.Ellipse(cx, gy - 9, 5, 3.5f, Cloth); c.HLine(cx - 4, cx + 4, gy - 7, ClothD);
                    c.Ellipse(cx - 3, gy - 1, 2, 2, PlankDD); c.Ellipse(cx + 3, gy - 1, 2, 2, PlankDD);
                    c.Set(cx - 3, gy - 1, IronL); c.Set(cx + 3, gy - 1, IronL);
                    c.Rect(cx + 7, gy - 5, 4, 3, H("#8A5A34")); c.Rect(cx + 10, gy - 6, 2, 2, H("#8A5A34"));
                    c.VLine(cx + 7, gy - 2, gy, H("#6E4628")); c.VLine(cx + 10, gy - 2, gy, H("#6E4628"));
                    c.Set(cx - 2, gy - 9, Px.Tint); c.Set(cx - 1, gy - 9, Px.Tint);
                    break;
            }
        }

        static void Ship(PixelCanvas c, int cx, int gy)
        {
            c.Rect(cx - 9, gy - 4, 18, 4, Plank);
            c.HLine(cx - 9, cx + 8, gy - 4, PlankL);
            c.HLine(cx - 7, cx + 6, gy, PlankD);
            c.Set(cx - 10, gy - 5, Plank); c.Set(cx + 9, gy - 5, Plank); c.Set(cx + 10, gy - 6, PlankD);
            c.VLine(cx, gy - 20, gy - 5, Beam);
            for (int y = gy - 18; y <= gy - 7; y++)
            {
                int half = 5 + (y - (gy - 18)) / 3;
                for (int x = cx - half; x <= cx + half; x++)
                    if (x != cx) c.Set(x, y, ((y - gy) / 3) % 2 == 0 ? Px.Tint : Cloth);
            }
            Flag(c, cx, gy - 20, 1);
        }

        static void Geruest(PixelCanvas c, int cx, int gy)
        {
            // halb hochgezogene Mauern mit Holzgerüst, Bretterboden und Baumaterial
            Wall(c, cx - 9, gy - 5, 18, 6, WallStyle.Stone);
            c.Rect(cx - 9, gy - 6, 18, 1, Mortar);
            for (int x = cx - 9; x <= cx + 9; x += 6)
            {
                c.VLine(x, gy - 17, gy, Log); c.VLine(x + 1, gy - 17, gy, LogD);
            }
            for (int yy = gy - 16; yy <= gy - 6; yy += 5) { c.HLine(cx - 10, cx + 10, yy, Plank); c.HLine(cx - 10, cx + 10, yy + 1, PlankD); }
            // Querstreben
            for (int k = 0; k < 5; k++) { c.Set(cx - 8 + k, gy - 11 + k, LogL); c.Set(cx + 4 + k, gy - 15 + k, LogL); }
            // Kran mit Seil
            c.VLine(cx + 10, gy - 22, gy - 16, Log); c.HLine(cx + 3, cx + 11, gy - 22, Log);
            c.VLine(cx + 4, gy - 21, gy - 18, ClothD); Block(c, cx + 3, gy - 16, 2);
            // Material am Boden
            c.Rect(cx - 15, gy - 2, 5, 3, LogD); c.HLine(cx - 15, cx - 11, gy - 2, LogEnd);
            Block(c, cx + 12, gy, 3);
            c.Set(cx - 3, gy - 18, Skin); c.Set(cx - 3, gy - 17, Red);
        }

        static void Wachturm(PixelCanvas c, int cx, int gy)
        {
            // Steinsockel, hölzerner Turm mit Plattform und Dach
            Wall(c, cx - 5, gy - 6, 11, 7, WallStyle.Stone);
            c.Rect(cx - 4, gy - 20, 9, 14, Plank); c.VLine(cx - 4, gy - 20, gy - 7, PlankL); c.VLine(cx + 4, gy - 20, gy - 7, PlankD);
            for (int y = gy - 18; y < gy - 7; y += 3) c.HLine(cx - 4, cx + 4, y, PlankDD);
            Window(c, cx - 1, gy - 15, 2, 2, true);
            c.Rect(cx - 7, gy - 23, 15, 3, Log); c.HLine(cx - 7, cx + 7, gy - 23, LogL);
            for (int x = cx - 7; x <= cx + 7; x += 2) c.Set(x, gy - 24, LogD);
            Gable(c, cx, gy - 25, 7, 6, RoofBrown, Plank);
            Flag(c, cx + 6, gy - 25, 6);
        }

        static void FahrendesVolk(PixelCanvas c, int cx, int gy)
        {
            // zwei Planwagen und ein bunt geflicktes Zelt mit Feuer
            foreach (int wx in new[] { cx - 12, cx + 9 })
            {
                c.Rect(wx - 6, gy - 5, 12, 3, Plank); c.HLine(wx - 6, wx + 5, gy - 5, PlankL);
                c.Ellipse(wx, gy - 8, 6, 4, wx < cx ? Cloth : H("#C8A060"));
                c.HLine(wx - 5, wx + 5, gy - 8, ClothD);
                c.Ellipse(wx - 4, gy - 1, 1.6f, 1.6f, LogD); c.Ellipse(wx + 4, gy - 1, 1.6f, 1.6f, LogD);
            }
            c.Tri(cx - 6, gy, cx - 1, gy - 10, cx + 4, gy, Red);
            c.Tri(cx - 1, gy - 10, cx + 4, gy, cx, gy, H("#8E3028"));
            c.VLine(cx - 1, gy - 9, gy - 1, Gold);
            c.Set(cx + 6, gy - 2, FireL); c.Set(cx + 5, gy - 1, Fire); c.Set(cx + 7, gy - 1, Fire);
            c.Set(cx - 4, gy - 3, Skin); c.Set(cx - 4, gy - 2, Blue);
        }

        // ==================================================================
        //  Zusammenstellung
        // ==================================================================
        public static Dictionary<string, ArtSprite> Build(ArtPalette pal)
        {
            var d = new Dictionary<string, ArtSprite>();
            void Add(string name, int size, Action<PixelCanvas, int, int> draw, bool flat = false, bool outline = true, Px? outlineCol = null)
            {
                var c = new PixelCanvas(size, size);
                int cx = size / 2, gy = size - 6;
                draw(c, cx, gy);
                if (outline) c.Outline(outlineCol ?? K);
                var t = c.Trim(out int ox, out int oy);
                var s = new ArtSprite { Name = name, C = t, PivotX = cx - ox, PivotY = (t.H - 1) - (gy - oy), Flat = flat };
                if (!flat) MakeShadow(s);
                SplitTint(s);
                d[name] = s;
            }

            // Gebäude
            for (int v = 0; v < 3; v++) { int vv = v; Add("b_Wohnhaus" + v, 40, (c, x, y) => Wohnhaus(c, x, y, vv)); }
            Add("b_Bauernhof", 48, Bauernhof);
            Add("b_Fischerhuette", 48, Fischerhuette);
            Add("b_Holzfaeller", 48, Holzfaeller);
            Add("b_Steinbruch", 48, Steinbruch);
            Add("b_Mine", 48, (c, x, y) => Mine(c, x, y, false));
            Add("b_Goldmine", 48, (c, x, y) => Mine(c, x, y, true));
            Add("b_Lagerhaus", 48, Lagerhaus);
            Add("b_Markt", 48, Markt);
            Add("b_Taverne", 48, Taverne);
            Add("b_Kapelle", 56, Kapelle);
            Add("b_Brunnen", 32, Brunnen);
            Add("b_Schmiede", 48, Schmiede);
            Add("b_Kaserne", 48, Kaserne);
            Add("b_Archerie", 48, Archerie);
            Add("b_Stall", 48, StallBuilding);
            Add("b_Hafen", 48, Hafen);
            Add("b_Kathedrale", 72, Kathedrale);
            Add("b_Werkstatt", 48, Werkstatt);
            Add("b_Burg", 80, Burg);
            Add("b_Windmuehle", 48, Windmuehle);
            Add("p_ruin", 48, Ruine);
            Add("p_camp", 56, Raeuberlager);
            Add("b_Kloster", 64, Kloster);
            Add("b_Turnierplatz", 64, Turnierplatz);
            Add("b_scaffold", 48, Geruest);
            Add("b_Wachturm", 48, Wachturm);
            Add("p_nomads", 56, FahrendesVolk);
            Add("b_Mauer", 32, (c, x, y) => Tower(c, x, y, true));
            // Zentren
            Add("c_dorf", 48, Dorfhaus);
            Add("c_rathaus", 64, Rathaus);
            Add("c_keep", 64, Bergfried);
            Add("c_palace", 80, Palast);
            // Befestigung
            foreach (bool st in new[] { false, true })
            {
                string k = st ? "stone" : "pal"; bool s2 = st;
                Add("w_" + k + "_h", 32, (c, x, y) => WallH(c, x, y, s2));
                Add("w_" + k + "_v", 32, (c, x, y) => WallV(c, x, y, s2));
                Add("w_" + k + "_tower", 40, (c, x, y) => Tower(c, x, y, s2));
                Add("w_" + k + "_gate", 32, (c, x, y) => Gate(c, x, y, s2));
            }
            // Felder (flach)
            Add("f_wheat", 24, (c, x, y) => FieldSprite(c, x, y, Hay, HayD, Soil), true, false);
            Add("f_green", 24, (c, x, y) => FieldSprite(c, x, y, H("#7FBF4A"), H("#4E8A34"), Soil), true, false);
            Add("f_snow", 24, (c, x, y) => FieldSprite(c, x, y, H("#EEF2F4"), H("#C9D2DA"), SoilD), true, false);
            // Natur
            Px leafOutline = pal.LeafDark.Dark(0.55f);
            Px pineOutline = pal.PineDark.Dark(0.55f);
            Add("t_oak0", 32, (c, x, y) => Oak(c, x, y, 6, pal, 1), false, true, leafOutline);
            Add("t_oak1", 32, (c, x, y) => Oak(c, x, y, 7, pal, 2), false, true, leafOutline);
            Add("t_oak2", 32, (c, x, y) => Oak(c, x, y, 5, pal, 3), false, true, leafOutline);
            Add("t_pine0", 32, (c, x, y) => PineTree(c, x, y, 4, pal, false), false, true, pineOutline);
            Add("t_pine1", 32, (c, x, y) => PineTree(c, x, y, 5, pal, false), false, true, pineOutline);
            Add("t_snowpine0", 32, (c, x, y) => PineTree(c, x, y, 4, pal, true), false, true, pineOutline);
            Add("t_snowpine1", 32, (c, x, y) => PineTree(c, x, y, 5, pal, true), false, true, pineOutline);
            Add("t_bush", 24, (c, x, y) => { c.Ellipse(x, y - 2.5f, 4, 3, pal.LeafDark); c.Ellipse(x + 0.7f, y - 3, 3, 2, pal.Leaf); c.Set(x + 1, y - 4, pal.LeafLight); }, false, true, leafOutline);
            Add("t_cactus", 24, (c, x, y) =>
            {
                var g = H("#5E9A4A"); var gl = H("#86BE5E");
                c.Rect(x - 1, y - 10, 3, 11, g); c.VLine(x + 1, y - 10, y, gl.Mul(0.8f)); c.VLine(x - 1, y - 9, y, gl);
                c.Rect(x - 4, y - 6, 3, 2, g); c.Rect(x - 4, y - 9, 2, 3, g); c.Rect(x + 2, y - 5, 3, 2, g); c.Rect(x + 3, y - 8, 2, 3, g);
            });
            Add("t_stump", 16, (c, x, y) => { c.Ellipse(x, y - 1, 2.5f, 1.8f, pal.Trunk); c.Set(x, y - 2, LogEnd); c.Set(x - 1, y - 2, LogEnd); });
            Add("m_rock0", 48, (c, x, y) => Mountain(c, x, y, 30, 22, false, pal, 3));
            Add("m_rock1", 48, (c, x, y) => Mountain(c, x, y, 24, 17, false, pal, 7));
            Add("m_snow0", 48, (c, x, y) => Mountain(c, x, y, 32, 26, true, pal, 5));
            Add("m_snow1", 48, (c, x, y) => Mountain(c, x, y, 26, 20, true, pal, 9));
            // Große Massive für Gebirgszüge
            Add("m_mid", 112, (c, x, y) => Massif(c, x, y, 78, 54, false, pal, 11, 2));
            Add("m_big", 144, (c, x, y) => Massif(c, x, y, 110, 76, false, pal, 12, 3));
            Add("m_huge", 160, (c, x, y) => Massif(c, x, y, 128, 92, true, pal, 13, 3));
            Add("m_mid_s", 112, (c, x, y) => Massif(c, x, y, 78, 58, true, pal, 14, 2));
            Add("m_big_s", 144, (c, x, y) => Massif(c, x, y, 112, 82, true, pal, 15, 3));
            Add("h_hill0", 32, (c, x, y) => Hill(c, x, y, pal, 1), false, true, pal.Hills.Dark(0.5f));
            Add("r_rock", 24, (c, x, y) => Rocks(c, x, y, pal.Rock, pal.RockLight, pal.RockDark, pal.RockLight, pal.Rock, 4));
            Add("d_iron", 24, (c, x, y) => Rocks(c, x, y, H("#8C7F74"), H("#B0A396"), H("#5E544C"), Rust, RustL, 5));
            Add("d_gold", 24, (c, x, y) => Rocks(c, x, y, H("#9C9488"), H("#C6BEB2"), H("#6A6258"), Gold, GoldL, 6));
            Add("d_stone", 24, (c, x, y) => Rocks(c, x, y, StoneL, WhiteL, StoneD, WhiteL, StoneL, 7));
            Add("t_reeds", 16, (c, x, y) =>
            {
                for (int k = 0; k < 4; k++) { int xx = x - 3 + k * 2; c.VLine(xx, y - 5 - k % 2, y, H("#6E9A3A")); c.Set(xx, y - 6 - k % 2, Plank); }
            }, false, false);
            // Truppen
            foreach (UType u in Enum.GetValues(typeof(UType))) { var uu = u; Add("u_" + u, 32, (c, x, y) => Unit(c, x, y, uu)); }
            Add("u_cart", 32, (c, x, y) => Unit(c, x, y, (UType)99));
            Add("u_ship", 40, Ship);
            Add("u_banner", 24, (c, x, y) =>
            {
                c.VLine(x, y - 16, y, Beam); c.Set(x, y - 17, Gold);
                for (int j = 0; j < 7; j++) for (int i = 1; i <= 6; i++) c.Set(x + i, y - 16 + j + (i > 3 && j == 6 ? -1 : 0), i == 6 || j == 6 ? Px.TintDark : Px.Tint);
            });
            Add("flag", 24, (c, x, y) => Flag(c, x, y, 9));
            // Weißes Pixel (für Linien/Punkte)
            var px = new PixelCanvas(2, 2); px.Rect(0, 0, 2, 2, new Px(255, 255, 255));
            d["white"] = new ArtSprite { Name = "white", C = px, PivotX = 1, PivotY = 1, Flat = true };
            return d;
        }

        /// <summary>Schlagschatten: Silhouette, schräg nach links unten auf den Boden projiziert.</summary>
        static void MakeShadow(ArtSprite s)
        {
            var c = s.C;
            int groundRow = c.H - 1 - s.PivotY;
            int shift = (int)Math.Ceiling(c.H * 0.6f) + 2;
            var sh = new PixelCanvas(c.W + shift, c.H + 2);
            var dark = new Px(0, 0, 0, 255);
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    if (c.P[y * c.W + x].a == 0) continue;
                    int hgt = Math.Max(0, groundRow - y);
                    int sx = x - (int)Math.Round(hgt * 0.6f) + shift;
                    int sy = groundRow - (int)Math.Round(hgt * 0.38f) + 1;
                    sh.Set(sx, sy, dark); sh.Set(sx + 1, sy, dark); sh.Set(sx, sy - 1, dark);
                }
            var t = sh.Trim(out int ox, out int oy);
            s.Shadow = t;
            s.ShadowPivotX = s.PivotX + shift - ox;
            int shGround = groundRow + 1;
            s.ShadowPivotY = (t.H - 1) - (shGround - oy) + 1;
        }

        static void SplitTint(ArtSprite s)
        {
            bool any = false;
            var c = s.C;
            var ov = new PixelCanvas(c.W, c.H);
            for (int i = 0; i < c.P.Length; i++)
            {
                var p = c.P[i];
                if (p.a == 255 && p.g == 0 && p.r == p.b && (p.r == 255 || p.r == 200))
                {
                    byte v = p.r == 255 ? (byte)255 : (byte)170;
                    ov.P[i] = new Px(v, v, v);
                    c.P[i] = new Px(v, v, v);
                    any = true;
                }
            }
            if (any) s.Overlay = ov;
        }
    }
}
