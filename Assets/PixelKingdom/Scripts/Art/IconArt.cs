using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    /// <summary>Farbige 16x16-Icons und 24x24-Porträts für die Oberfläche.</summary>
    public static class IconArt
    {
        static Px H(string s) => Px.Hex(s);
        static readonly Px K = H("#2A1D1A");

        public static Dictionary<string, PixelCanvas> Build()
        {
            var d = new Dictionary<string, PixelCanvas>();
            PixelCanvas N(int s = 16) => new PixelCanvas(s, s);
            void Done(string name, PixelCanvas c) { c.Outline(K); d["i_" + name] = c; }

            // Nahrung: Brotlaib mit Ähre
            { var c = N(); var b = H("#D99A4E"); c.Ellipse(8, 10, 6.5f, 3.6f, b.Dark(0.2f)); c.Ellipse(8, 9.5f, 6, 3, b); c.Ellipse(9, 8.5f, 3.5f, 1.4f, b.Light(0.3f));
              for (int k = 0; k < 3; k++) c.Line(4 + k * 3, 8, 5 + k * 3, 11, b.Dark(0.3f));
              var w = H("#F2C94C"); c.Line(12, 2, 12, 7, H("#B88A22")); c.Set(11, 3, w); c.Set(13, 3, w); c.Set(11, 5, w); c.Set(13, 5, w); c.Set(12, 1, w); Done("food", c); }
            // Holz: Stämme
            { var c = N(); var l = H("#9B6236"); var e = H("#D8A86A");
              foreach (var (x, y) in new[] { (5f, 11f), (11f, 11f), (8f, 6f) }) { c.Ellipse(x, y, 3.2f, 3.2f, l.Dark(0.2f)); c.Ellipse(x, y, 2.4f, 2.4f, e); c.Set((int)x, (int)y, l); }
              Done("wood", c); }
            // Stein
            { var c = N(); var s = H("#B5ADA2"); c.Ellipse(6, 10, 4.5f, 3.5f, s.Dark(0.25f)); c.Ellipse(6.5f, 9.5f, 3.6f, 2.6f, s); c.Set(7, 8, s.Light(0.4f));
              c.Ellipse(11, 7, 3.5f, 3, s.Dark(0.25f)); c.Ellipse(11.4f, 6.6f, 2.6f, 2.2f, s.Light(0.15f)); c.Set(12, 5, s.Light(0.5f)); Done("stone", c); }
            // Eisen: Barren
            { var c = N(); var i = H("#8D97A3"); c.Tri(2, 12, 5, 6, 14, 12, i.Dark(0.2f)); c.Rect(5, 6, 8, 6, i); c.Tri(13, 6, 14, 12, 12.5f, 12, i.Dark(0.3f)); c.HLine(5, 12, 6, i.Light(0.5f)); c.Set(6, 8, i.Light(0.6f)); Done("iron", c); }
            // Gold: Münzstapel
            { var c = N(); var g = H("#F2C94C"); var gd = H("#B88A22");
              for (int k = 0; k < 4; k++) { c.Ellipse(6, 12 - k * 2, 4, 1.6f, gd); c.Ellipse(6, 11.5f - k * 2, 4, 1.4f, g); }
              c.Ellipse(11.5f, 9, 3.5f, 3.5f, gd); c.Ellipse(11.5f, 8.7f, 2.8f, 2.8f, g); c.Set(11, 8, H("#FFF2A0")); c.VLine(12, 7, 10, gd); Done("gold", c); }
            // Waffen: Schwert & Schild
            { var c = N(); var s = H("#C2CAD3"); c.Line(3, 13, 12, 4, s); c.Line(4, 13, 13, 4, H("#8D97A3")); c.Line(4, 9, 8, 13, H("#B88A22")); c.Set(2, 14, H("#6B4226"));
              c.Ellipse(11.5f, 11, 3.2f, 3.6f, H("#C8473B")); c.VLine(11, 8, 14, H("#F2C94C")); Done("weapons", c); }
            // Leute
            { var c = N(); var sk = H("#F0C29A");
              c.Ellipse(5, 5, 2, 2, sk); c.Rect(3, 8, 5, 6, H("#4E7BC0")); c.HLine(3, 7, 3, H("#5A3A24"));
              c.Ellipse(11, 6, 2, 2, sk); c.Rect(9, 9, 5, 5, H("#C8473B")); c.HLine(9, 13, 4, H("#E8C35A")); Done("people", c); }
            // Gesichter
            Px Face(PixelCanvas c, string col) { var f = H(col); c.Ellipse(8, 8, 6.4f, 6.4f, f.Dark(0.2f)); c.Ellipse(7.6f, 7.6f, 5.6f, 5.6f, f); c.Set(5, 5, f.Light(0.5f)); c.Set(6, 4, f.Light(0.5f)); return f; }
            { var c = N(); Face(c, "#F2C94C"); c.Set(6, 6, K); c.Set(6, 7, K); c.Set(10, 6, K); c.Set(10, 7, K); c.HLine(6, 10, 11, K); c.Set(5, 10, K); c.Set(11, 10, K); Done("smile", c); }
            { var c = N(); Face(c, "#E8892F"); c.Set(6, 7, K); c.Set(10, 7, K); c.Line(5, 5, 7, 6, K); c.Line(11, 5, 9, 6, K); c.HLine(6, 10, 10, K); c.Set(5, 11, K); c.Set(11, 11, K); Done("angry", c); }
            // Ansehen: Stern
            { var c = N(); var g = H("#F2C94C");
              c.Tri(8, 1, 5.5f, 7, 10.5f, 7, g); c.Tri(1, 6, 15, 6, 8, 10, g); c.Tri(8, 9, 3, 15, 5.5f, 7, g); c.Tri(8, 9, 13, 15, 10.5f, 7, g);
              c.Set(7, 5, H("#FFF2A0")); c.Set(7, 6, H("#FFF2A0")); Done("prestige", c); }
            // Krone
            { var c = N(); var g = H("#F2C94C"); var gd = H("#B88A22");
              c.Rect(2, 8, 12, 5, g); c.Tri(2, 9, 2, 3, 6, 9, g); c.Tri(5, 9, 8, 2, 11, 9, g); c.Tri(10, 9, 14, 3, 14, 9, g);
              c.HLine(2, 13, 12, gd); c.Set(5, 10, H("#C8473B")); c.Set(8, 10, H("#4E7BC0")); c.Set(11, 10, H("#C8473B")); c.Set(8, 2, H("#FFF2A0")); Done("crown", c); }

            // ---- Porträts 24x24 --------------------------------------------------
            PixelCanvas Portrait(string skin, string robe, Action<PixelCanvas> extra)
            {
                var c = N(24);
                var r = H(robe); var s = H(skin);
                c.Ellipse(12, 23, 10, 7, r.Dark(0.15f)); c.Ellipse(12, 22.5f, 9, 6, r);
                c.Ellipse(12, 11, 6, 7, s.Dark(0.15f)); c.Ellipse(11.6f, 10.6f, 5.4f, 6.4f, s);
                c.Set(9, 11, K); c.Set(14, 11, K); c.Set(9, 10, s.Dark(0.4f)); c.Set(14, 10, s.Dark(0.4f));
                c.HLine(11, 13, 15, s.Dark(0.45f));
                extra(c);
                c.Outline(K);
                return c;
            }
            PixelCanvas Plain(Action<PixelCanvas> draw) { var c = N(24); draw(c); c.Outline(K); return c; }
            d["i_advisor"] = Portrait("#F0C29A", "#4E7A3A", c =>
            {
                var h = H("#C9C4C0"); // graues Haar, Kapuze
                c.Ellipse(12, 7, 7, 5, H("#3E6A30")); c.Rect(5, 7, 3, 9, H("#3E6A30")); c.Rect(16, 7, 3, 9, H("#3E6A30"));
                c.HLine(8, 15, 7, h); c.HLine(7, 16, 8, h);
                c.Set(10, 14, H("#D98A70")); c.Set(13, 14, H("#D98A70"));
                c.HLine(10, 14, 19, H("#F2C94C")); c.Set(12, 20, H("#F2C94C"));
            });
            d["i_emperor"] = Portrait("#E6C0A0", "#2E2A34", c =>
            {
                var g = H("#F2C94C");
                c.Rect(6, 3, 12, 4, g); c.Set(6, 2, g); c.Set(9, 1, g); c.Set(12, 1, g); c.Set(15, 1, g); c.Set(17, 2, g);
                c.Set(9, 4, H("#C8473B")); c.Set(12, 4, H("#4E7BC0")); c.Set(15, 4, H("#C8473B"));
                c.Rect(7, 15, 10, 4, H("#3A3A3A")); c.HLine(11, 13, 15, K);
                c.Line(8, 9, 10, 10, K); c.Line(16, 9, 14, 10, K);
                c.HLine(4, 19, 19, H("#C8473B"));
            });
            d["i_knight"] = Portrait("#F0C29A", "#4E7BC0", c =>
            {
                var m = H("#A8B0BA"); var md = H("#6E7682");
                c.Ellipse(12, 9, 7, 7.5f, md); c.Ellipse(11.6f, 8.6f, 6.4f, 6.8f, m);
                c.Rect(7, 10, 10, 2, K); c.VLine(12, 4, 15, md); c.Set(9, 11, H("#F0C29A")); c.Set(14, 11, H("#F0C29A"));
                c.Rect(9, 1, 6, 3, H("#C8473B"));
                c.Rect(10, 19, 4, 3, H("#F2C94C"));
            });
            d["i_scroll"] = Plain(c =>
            {
                c.Rect(4, 3, 16, 18, H("#EAD8A0")); c.Ellipse(4, 3, 2, 2, H("#C9A86A")); c.Ellipse(20, 21, 2, 2, H("#C9A86A"));
                for (int y = 6; y < 18; y += 3) c.HLine(7, 17, y, H("#8A6A44"));
                c.Ellipse(16, 18, 2.5f, 2.5f, H("#C8473B"));
            });
            d["i_book"] = Plain(c =>
            {
                c.Rect(3, 4, 18, 16, H("#8A2F2A")); c.Rect(5, 5, 14, 14, H("#A83A30")); c.VLine(12, 4, 19, H("#5A1E18"));
                c.Rect(7, 8, 4, 3, H("#F2C94C")); c.Rect(14, 8, 3, 3, H("#F2C94C"));
            });
            return d;
        }
    }
}
