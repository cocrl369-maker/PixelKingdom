using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    public static class NameGen
    {
        static readonly string[][] Pre =
        {
            new[] { "Brenn", "Eich", "Wolf", "Stein", "Hohen", "Linden", "Bern", "Rosen", "Falken", "Kalt", "Grün", "Moor", "Hirsch", "Erlen", "Weiß", "Roth", "Asch", "Birk", "Dorn", "Hasel", "Kirch", "Mühl", "Oster", "Sonn", "Tann", "Wald", "Wester", "Lerchen", "Fuchs", "Bären", "Gold", "Silber", "Schwan", "Raben" },
            new[] { "Skag", "Hrafn", "Thor", "Ulf", "Sig", "Dal", "Fjell", "Is", "Kol", "Rand", "Snø", "Vind", "Bjør", "Grim", "Hald", "Jarl", "Stav" },
            new[] { "Qas", "Sah", "Mar", "Zar", "Kha", "Nur", "Tal", "Am", "Baz", "Dar", "Fer", "Jas", "Ras", "Sul", "Yaz", "Ham" },
            new[] { "Kor", "Mel", "San", "Ilo", "Pera", "Tir", "Vel", "Coral", "Del", "Aru", "Lumi" },
            new[] { "Ael", "Vas", "Tirr", "Oru", "Sael", "Imm", "Qir", "Lys", "Esh", "Nau", "Vey", "Ory", "Ith", "Saer" },
        };
        static readonly string[][] Suf =
        {
            new[] { "dorf", "burg", "heim", "au", "feld", "bach", "hausen", "stein", "tal", "furt", "wald", "berg", "ingen", "rode", "hof", "brück", "stadt", "see", "hagen", "weiler" },
            new[] { "vik", "heim", "gard", "fjord", "stad", "holm", "dal", "by", "nes", "berg" },
            new[] { "abad", "ir", "an", "esh", "ara", "un", "iya", "ad", "oum", "ar" },
            new[] { "a", "os", "ia", "ana", "ea", "is", "or", "ella" },
            new[] { "anor", "ith", "ume", "avar", "esse", "ora", "iel", "enn", "ul", "aris", "ond", "aleth" },
        };
        static readonly string[][] First =
        {
            new[] { "Konrad", "Adalbert", "Gisela", "Heinrich", "Mechthild", "Ludwig", "Irmgard", "Ottokar", "Kunigunde", "Bertold", "Hedwig", "Rupert", "Agnes", "Eberhard", "Walburga", "Dietrich", "Ysolde", "Friedrich", "Margarethe", "Lothar", "Richardis", "Gerold" },
            new[] { "Ragnhild", "Sigurd", "Astrid", "Harald", "Ingrid", "Bjarne", "Sigrun", "Leif", "Thyra", "Erik" },
            new[] { "Zahir", "Amira", "Karim", "Layla", "Rashid", "Samira", "Tariq", "Yasmin", "Malik", "Nadia" },
            new[] { "Melis", "Taro", "Ilana", "Koa", "Sera", "Davo", "Lumi", "Pelan" },
            new[] { "Aelis", "Vorin", "Saeth", "Iolan", "Meriva", "Taunis", "Orvan", "Lysa", "Kaelon", "Yseth", "Nerith", "Ossia" },
        };
        static readonly string[][] Titles =
        {
            new[] { "Graf", "Gräfin", "Herzog", "Herzogin", "Fürst", "Fürstin", "Markgraf", "Baronin" },
            new[] { "Jarl", "Jarl", "Königin", "König" },
            new[] { "Emir", "Emira", "Sultan", "Sultana" },
            new[] { "Kapitän", "Admiralin", "Ratsherr", "Ratsfrau" },
            new[] { "Hochherr", "Hochherrin", "Sternrat", "Seefürstin" },
        };

        public static string Place(Random r, int culture, HashSet<string> used)
        {
            culture = Math.Max(0, Math.Min(4, culture));
            for (int t = 0; t < 50; t++)
            {
                string n = Pre[culture][r.Next(Pre[culture].Length)] + Suf[culture][r.Next(Suf[culture].Length)];
                if (culture == 0 && r.NextDouble() < 0.12) n = (r.NextDouble() < 0.5 ? "Alt" : "Neu") + n.ToLowerInvariant();
                if (used == null || used.Add(n)) return n;
            }
            return "Ort " + r.Next(1000);
        }

        public static string Leader(Random r, int culture)
        {
            culture = Math.Max(0, Math.Min(4, culture));
            int ti = r.Next(Titles[culture].Length);
            string title = Titles[culture][ti];
            return title + " " + First[culture][r.Next(First[culture].Length)];
        }

        public static string RealmName(int culture, string capital)
        {
            switch (culture)
            {
                case 1: return "Jarltum " + capital;
                case 2: return "Emirat " + capital;
                case 3: return "Bund von " + capital;
                case 4: return Loc.T("Hohes Haus von ") + capital;
                default: return "Haus " + capital;
            }
        }
    }
}
