using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Kleine, freundliche Low-Poly-Modelle für Gebäude, Truppen und Schiffe – alles aus Code.</summary>
    public static class Models
    {
        // Alle Modelle werden um (0,0,0) gebaut; der Aufrufer setzt per mb.SetTransform Position & Drehung.

        public static void House(MeshBuilder mb, Palette p, float w = 0.26f, float d = 0.2f, float h = 0.16f, bool alt = false)
        {
            mb.Box(new Vector3(0, -0.15f, 0), w, h + 0.15f, d, p.Wall);
            mb.GableRoof(new Vector3(0, h, 0), w, d, d * 0.75f, alt ? p.RoofAlt : p.Roof);
            mb.Box(new Vector3(0, 0, -d / 2 - 0.002f), w * 0.22f, h * 0.55f, 0.01f, p.Dark); // Tür
        }

        public static void Building(MeshBuilder mb, Palette p, BType t, int variant)
        {
            switch (t)
            {
                case BType.Wohnhaus:
                    House(mb, p, 0.24f + (variant % 3) * 0.03f, 0.2f, 0.14f + (variant % 2) * 0.04f, variant % 4 == 0);
                    break;
                case BType.Bauernhof:
                    // Felder + Scheune
                    Field(mb, p, new Vector3(-0.12f, 0, 0.1f), variant);
                    mb.Box(new Vector3(0.14f, -0.15f, -0.12f), 0.22f, 0.32f, 0.16f, p.Wood);
                    mb.GableRoof(new Vector3(0.14f, 0.17f, -0.12f), 0.22f, 0.16f, 0.14f, p.RoofAlt);
                    break;
                case BType.Fischerhuette:
                    House(mb, p, 0.18f, 0.16f, 0.12f, true);
                    mb.Box(new Vector3(0, -0.05f, -0.22f), 0.06f, 0.06f, 0.24f, p.Wood); // Steg
                    break;
                case BType.Holzfaeller:
                    House(mb, p, 0.2f, 0.16f, 0.12f, true);
                    for (int i = 0; i < 3; i++)
                        mb.Box(new Vector3(0.2f, i * 0.04f, -0.06f + i * 0.0f), 0.05f, 0.04f, 0.18f, p.Trunk);
                    break;
                case BType.Steinbruch:
                    mb.Box(new Vector3(-0.05f, -0.1f, 0), 0.14f, 0.18f, 0.14f, p.Stone);
                    mb.Box(new Vector3(0.12f, -0.1f, 0.08f), 0.1f, 0.14f, 0.1f, p.Stone);
                    mb.Box(new Vector3(0.08f, -0.1f, -0.12f), 0.08f, 0.12f, 0.08f, p.Stone);
                    mb.Box(new Vector3(-0.16f, 0, -0.1f), 0.02f, 0.2f, 0.02f, p.Wood);
                    break;
                case BType.Mine:
                    mb.Box(new Vector3(0, -0.1f, 0), 0.26f, 0.22f, 0.18f, p.Stone);
                    mb.Box(new Vector3(0, 0, -0.092f), 0.1f, 0.1f, 0.01f, p.Dark);
                    mb.Box(new Vector3(0.09f, 0.12f, 0.02f), 0.02f, 0.18f, 0.02f, p.Wood);
                    mb.Box(new Vector3(-0.09f, 0.12f, 0.02f), 0.02f, 0.18f, 0.02f, p.Wood);
                    mb.Box(new Vector3(0, 0.29f, 0.02f), 0.22f, 0.02f, 0.03f, p.Wood);
                    break;
                case BType.Lagerhaus:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.34f, 0.3f, 0.2f, p.Wood);
                    mb.GableRoof(new Vector3(0, 0.15f, 0), 0.34f, 0.2f, 0.08f, p.RoofAlt);
                    break;
                case BType.Markt:
                    for (int i = 0; i < 3; i++)
                    {
                        var c = new Vector3(-0.14f + i * 0.14f, 0, (i % 2) * 0.08f);
                        mb.Box(c, 0.1f, 0.06f, 0.08f, p.Wood);
                        mb.Pyramid(c + new Vector3(0, 0.12f, 0), 0.13f, 0.11f, 0.06f, i % 2 == 0 ? p.Roof : p.Wall);
                        mb.Box(c + new Vector3(0, 0.06f, 0), 0.012f, 0.06f, 0.012f, p.Wood);
                    }
                    break;
                case BType.Taverne:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.28f, 0.42f, 0.22f, p.Wall);
                    mb.GableRoof(new Vector3(0, 0.27f, 0), 0.28f, 0.22f, 0.16f, p.Roof);
                    mb.Box(new Vector3(0.17f, 0.1f, -0.06f), 0.06f, 0.05f, 0.01f, p.Wood); // Schild
                    mb.Box(new Vector3(0, 0, -0.112f), 0.06f, 0.1f, 0.01f, p.Dark);
                    break;
                case BType.Kapelle:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.3f, 0.35f, 0.18f, p.Wall);
                    mb.GableRoof(new Vector3(0, 0.2f, 0), 0.3f, 0.18f, 0.14f, p.Roof);
                    mb.Box(new Vector3(-0.17f, -0.15f, 0), 0.1f, 0.55f, 0.1f, p.Wall);
                    mb.Pyramid(new Vector3(-0.17f, 0.4f, 0), 0.12f, 0.12f, 0.2f, p.Roof);
                    break;
                case BType.Brunnen:
                    mb.Cylinder(new Vector3(0, -0.05f, 0), 0.07f, 0.1f, 6, p.Stone);
                    mb.Box(new Vector3(0.06f, 0.05f, 0), 0.015f, 0.14f, 0.015f, p.Wood);
                    mb.Box(new Vector3(-0.06f, 0.05f, 0), 0.015f, 0.14f, 0.015f, p.Wood);
                    mb.GableRoof(new Vector3(0, 0.19f, 0), 0.16f, 0.12f, 0.06f, p.Roof);
                    break;
                case BType.Schmiede:
                    House(mb, p, 0.24f, 0.2f, 0.14f, true);
                    mb.Box(new Vector3(0.08f, 0.1f, 0.05f), 0.05f, 0.18f, 0.05f, p.Stone);
                    break;
                case BType.Kaserne:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.4f, 0.32f, 0.2f, p.Stone);
                    mb.GableRoof(new Vector3(0, 0.17f, 0), 0.4f, 0.2f, 0.1f, p.RoofAlt);
                    Flag(mb, p, new Vector3(0.2f, 0.2f, 0), p.Roof);
                    break;
                case BType.Archerie:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.14f, 0.55f, 0.14f, p.Wood);
                    mb.Pyramid(new Vector3(0, 0.4f, 0), 0.2f, 0.2f, 0.14f, p.Roof);
                    mb.Cylinder(new Vector3(0.2f, 0, 0), 0.05f, 0.02f, 6, p.Wall);
                    break;
                case BType.Stall:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.38f, 0.27f, 0.18f, p.Wood);
                    mb.GableRoof(new Vector3(0, 0.12f, 0), 0.38f, 0.18f, 0.1f, p.RoofAlt);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.18f + i * 0.12f, 0, -0.18f), 0.015f, 0.07f, 0.015f, p.Wood);
                    mb.Box(new Vector3(0, 0.05f, -0.18f), 0.38f, 0.015f, 0.012f, p.Wood);
                    break;
                case BType.Hafen:
                    mb.Box(new Vector3(0, -0.05f, -0.15f), 0.12f, 0.06f, 0.5f, p.Wood);
                    mb.Box(new Vector3(0.12f, -0.15f, 0.15f), 0.2f, 0.3f, 0.16f, p.Wall);
                    mb.GableRoof(new Vector3(0.12f, 0.15f, 0.15f), 0.2f, 0.16f, 0.1f, p.Roof);
                    mb.Box(new Vector3(-0.16f, -0.04f, -0.32f), 0.1f, 0.06f, 0.22f, p.Wood); // Boot
                    mb.Box(new Vector3(-0.16f, 0.02f, -0.32f), 0.01f, 0.18f, 0.01f, p.Dark);
                    break;
                case BType.Kathedrale:
                    mb.Box(new Vector3(0, -0.15f, 0.05f), 0.5f, 0.55f, 0.26f, p.Wall);
                    mb.GableRoof(new Vector3(0, 0.4f, 0.05f), 0.5f, 0.26f, 0.2f, p.Roof);
                    mb.Box(new Vector3(-0.2f, -0.15f, -0.16f), 0.14f, 0.9f, 0.14f, p.Wall);
                    mb.Box(new Vector3(0.2f, -0.15f, -0.16f), 0.14f, 0.9f, 0.14f, p.Wall);
                    mb.Pyramid(new Vector3(-0.2f, 0.75f, -0.16f), 0.16f, 0.16f, 0.3f, p.Roof);
                    mb.Pyramid(new Vector3(0.2f, 0.75f, -0.16f), 0.16f, 0.16f, 0.3f, p.Roof);
                    break;
                case BType.Werkstatt:
                    mb.Box(new Vector3(0, 0.12f, 0), 0.34f, 0.02f, 0.22f, p.Wood);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(i < 2 ? -0.15f : 0.15f, -0.1f, i % 2 == 0 ? -0.09f : 0.09f), 0.02f, 0.22f, 0.02f, p.Wood);
                    mb.Box(new Vector3(0, 0, 0), 0.26f, 0.06f, 0.06f, p.Trunk);
                    break;
                default:
                    House(mb, p);
                    break;
            }
        }

        public static void Field(MeshBuilder mb, Palette p, Vector3 c, int variant)
        {
            var col = variant % 2 == 0 ? p.Field : Color.Lerp(p.Field, p.Fertile, 0.4f);
            for (int i = 0; i < 4; i++)
            {
                var a = c + new Vector3(-0.17f + i * 0.09f, 0.012f, -0.15f);
                mb.Box(a, 0.065f, 0.025f, 0.3f, i % 2 == 0 ? col : Color.Lerp(col, p.Dark, 0.15f));
            }
        }

        public static void Flag(MeshBuilder mb, Palette p, Vector3 b, Color c, float h = 0.35f)
        {
            mb.Box(b, 0.015f, h, 0.015f, p.Dark);
            var top = b + new Vector3(0.008f, h - 0.11f, 0);
            mb.Box(top + new Vector3(0.07f, 0, 0), 0.14f, 0.1f, 0.008f, c);
        }

        /// <summary>Zentrum einer Siedlung je nach Größe/Rang: Dorfhaus, Rathaus mit Turm oder Bergfried.</summary>
        public static void Center(MeshBuilder mb, Palette p, int level, Color faction)
        {
            switch (level)
            {
                case 0:
                    House(mb, p, 0.32f, 0.24f, 0.18f);
                    Flag(mb, p, new Vector3(0.22f, 0, 0.1f), faction, 0.4f);
                    break;
                case 1:
                    mb.Box(new Vector3(0, -0.15f, 0), 0.36f, 0.42f, 0.26f, p.Wall);
                    mb.GableRoof(new Vector3(0, 0.27f, 0), 0.36f, 0.26f, 0.18f, p.Roof);
                    mb.Box(new Vector3(0, -0.15f, -0.05f), 0.12f, 0.75f, 0.12f, p.Wall);
                    mb.Pyramid(new Vector3(0, 0.6f, -0.05f), 0.16f, 0.16f, 0.2f, p.Roof);
                    Flag(mb, p, new Vector3(0, 0.8f, -0.05f), faction, 0.25f);
                    break;
                default:
                    float s = level >= 4 ? 1.25f : 1f;
                    mb.Box(new Vector3(0, -0.15f, 0), 0.34f * s, 0.95f * s, 0.34f * s, p.Stone);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0 ? -1 : 1) * 0.15f * s, z = (i < 2 ? -1 : 1) * 0.15f * s;
                        mb.Box(new Vector3(x, 0.8f * s - 0.15f, z), 0.07f * s, 0.07f, 0.07f * s, p.Stone);
                    }
                    mb.Pyramid(new Vector3(0, 0.8f * s - 0.15f, 0), 0.24f * s, 0.24f * s, 0.25f * s, p.Roof);
                    Flag(mb, p, new Vector3(0, 1.0f * s, 0), faction, 0.3f);
                    break;
            }
        }

        public static void Tower(MeshBuilder mb, Palette p, Vector3 b, float h, float r)
        {
            mb.Cylinder(b + Vector3.down * 0.15f, r, h + 0.15f, 6, p.Stone);
            mb.Cone(b + Vector3.up * h, r * 1.3f, r * 2.2f, 6, p.Roof);
        }

        public static void Castle(MeshBuilder mb, Palette p, Color faction)
        {
            float d = 0.42f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * d, z = (i < 2 ? -1 : 1) * d;
                Tower(mb, p, new Vector3(x, 0, z), 0.55f, 0.09f);
            }
            mb.Box(new Vector3(0, -0.15f, -d), d * 2, 0.5f, 0.05f, p.Stone);
            mb.Box(new Vector3(0, -0.15f, d), d * 2, 0.5f, 0.05f, p.Stone);
            mb.Box(new Vector3(-d, -0.15f, 0), 0.05f, 0.5f, d * 2, p.Stone);
            mb.Box(new Vector3(d, -0.15f, 0), 0.05f, 0.5f, d * 2, p.Stone);
            Flag(mb, p, new Vector3(d, 0.7f, -d), faction, 0.3f);
        }

        public static void Soldier(MeshBuilder mb, Palette p, Vector3 b, Color body, int kind)
        {
            mb.Box(b, 0.05f, 0.09f, 0.04f, body);
            mb.Box(b + new Vector3(0, 0.09f, 0), 0.035f, 0.035f, 0.035f, p.Wall);
            switch (kind)
            {
                case 1: mb.Box(b + new Vector3(0.035f, 0.0f, 0), 0.008f, 0.22f, 0.008f, p.Wood); break;      // Speer
                case 2: mb.Box(b + new Vector3(0.035f, 0.03f, 0), 0.01f, 0.1f, 0.01f, p.Wood); break;       // Bogen
                case 3: mb.Box(b + new Vector3(0, -0.05f, 0), 0.06f, 0.06f, 0.14f, p.Trunk); break;          // Pferd
                case 4: mb.Box(b + new Vector3(0, 0, 0), 0.08f, 0.06f, 0.18f, p.Wood); break;               // Ramme
            }
        }

        public static void Ship(MeshBuilder mb, Palette p, Color sail)
        {
            mb.Box(new Vector3(0, -0.04f, 0), 0.16f, 0.08f, 0.42f, p.Wood);
            mb.Pyramid(new Vector3(0, -0.04f, 0.25f), 0.16f, 0.1f, 0.08f, p.Wood);
            mb.Box(new Vector3(0, 0.04f, 0), 0.015f, 0.38f, 0.015f, p.Dark);
            mb.Box(new Vector3(0, 0.14f, 0.01f), 0.24f, 0.2f, 0.01f, sail);
        }

        public static void Cart(MeshBuilder mb, Palette p)
        {
            mb.Box(new Vector3(0, 0.04f, 0), 0.14f, 0.06f, 0.24f, p.Wood);
            mb.GableRoof(new Vector3(0, 0.1f, 0), 0.14f, 0.24f, 0.08f, p.Wall, 0.01f);
            mb.Box(new Vector3(0.075f, 0f, 0.07f), 0.012f, 0.07f, 0.07f, p.Dark);
            mb.Box(new Vector3(-0.075f, 0f, 0.07f), 0.012f, 0.07f, 0.07f, p.Dark);
            mb.Box(new Vector3(0.075f, 0f, -0.07f), 0.012f, 0.07f, 0.07f, p.Dark);
            mb.Box(new Vector3(-0.075f, 0f, -0.07f), 0.012f, 0.07f, 0.07f, p.Dark);
        }
    }
}
