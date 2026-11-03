# Pixel Kingdom – Entwicklungsnotizen (Übergabe für neue Sitzungen)

Diese Datei ist für die Weiterarbeit gedacht – von dir oder von Claude Code in einer neuen Sitzung.
Eine neue Sitzung sollte **zuerst diese Datei und die README lesen**, dann weitermachen.

## Wünsche des Entwicklers (gelten weiter)

- Sprache im Spiel und in der Kommunikation: **Deutsch**.
- Ziel: **Android** (Tablet/Handy), am PC mit Tastatur und Maus spielbar (F9 simuliert Touch).
- Stil: **Retro-2D-Pixelkunst** in Schrägansicht, alles im Code erzeugt (keine Bilddateien, keine Assets).
- Mittelalter-Aufbau + Strategie: Dorf → Städtegemeinde → Grafschaft → Fürstentum → Königreich → Kaiserreich.
- Gut für **gelegentliches Spielen**, aber nicht langweilig über lange Zeit. Ruhephasen + wachsender Druck des Kaisers.
- **Übersichtlich bleiben**: Neues erst zeigen, wenn es freigeschaltet ist; Meister Aldo (Chronist) erklärt Neues unten am Bildschirm (`Game.Hint(key, text)`).
- Gebirge: wenige große, überlappende Berge als geschlossene Wand – unpassierbar.
- Farben wechseln **automatisch mit der Jahreszeit** (kein Paletten-Knopf).
- Ideen aus großen Strategiespielen sind willkommen, aber **nichts kopieren** (keine Namen, Grafiken, Musik, Texte).

## Aufbau in Kürze

| Bereich | Ort | Hinweis |
|---|---|---|
| Einstieg, Spielschleife, Eingabe, Modi | `Scripts/Core/GameManager.cs` | bootet selbst per `RuntimeInitializeOnLoadMethod`, keine Szene nötig |
| Speicherdaten | `Scripts/Core/GameState.cs` | `CurrentVersion` erhöhen, wenn sich das Format grundlegend ändert |
| Werte & Balance | `Scripts/Data/Defs.cs` | Gebäude, Einheiten, Ränge, Erlasse, Wissen (`Techs`) |
| Simulation | `Scripts/Sim/*.cs` | `partial class Game`, **ohne Unity** testbar |
| Bauplätze | `Scripts/Sim/Placement.cs` | `CanPlace`, `SiteBonus`, `DistanceFactor`, `FindSite`, `EnsurePlaced` |
| Grenzland / Forschung | `Scripts/Sim/Frontier.cs` | Grenzstreit, fahrendes Volk, Wachtürme, Reliquien, Wissen |
| Karte | `Scripts/World/*.cs` | 480×336 Felder; `MoveCost` (Gebirge/Schnee = unpassierbar) |
| Pixelkunst | `Scripts/Art/*.cs` | `SpriteArt` (Sprites), `GroundPainter` (Boden), `MapLayout` (Objekte, Siedlungsbild), `ArtPalette` (Jahreszeiten) |
| Darstellung | `Scripts/Render/*.cs` | Chunks, Atlas, Kamera (Kartenrand-Begrenzung in `CameraController.LateUpdate`) |
| Oberfläche | `Scripts/UI/*.cs` | IMGUI mit virtueller Auflösung, eigene Pixelschrift; `__text__` = unterstrichen |
| Musik | `Scripts/Audio/*.cs` | im Code synthetisiert |

Wichtige Mechaniken:
- Jedes Gebäude (außer Mauer/Burg) hat einen **Bauplatz** (`SettlementData.placed`). Baustellen stehen als `QueueItem` mit `x/y` in der Bauschlange und werden als Gerüst (`b_scaffold`) gezeichnet.
- Ertrag = Summe über alle Gebäude aus Standortbonus × Entfernungsfaktor (`SiteSum`) × Personal × Zufriedenheit × Wissen (`TechProd`).
- Die KI sucht Bauplätze mit `FindSite`; der Spieler wählt sie im Modus `GameManager.Mode.PlaceBuilding`.
- `RecomputeTerritory` baut auch die Belegung (`RebuildOccupancy`) neu auf und vergibt fehlende Bauplätze.

## Testen ohne Unity

Die Simulation und die Pixelkunst sind reines C#. Mit dem .NET SDK (8) lassen sich kleine Konsolenprojekte bauen,
die die Ordner `World/`, `Sim/`, `Data/` und `Core/GameState.cs` (für Grafik zusätzlich `Art/`) einbinden:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><LangVersion>9.0</LangVersion></PropertyGroup>
  <ItemGroup>
    <Compile Include="PFAD/Assets/PixelKingdom/Scripts/World/*.cs" />
    <Compile Include="PFAD/Assets/PixelKingdom/Scripts/Sim/*.cs" />
    <Compile Include="PFAD/Assets/PixelKingdom/Scripts/Data/*.cs" />
    <Compile Include="PFAD/Assets/PixelKingdom/Scripts/Core/GameState.cs" />
    <Compile Include="Program.cs" />
  </ItemGroup>
</Project>
```

Ein Bot-Spieler (`Game.NewGame(seed)`, `g.Headless = true`, dann `g.Advance(1)` in einer Schleife, Verwalter an,
Ereignisse mit `ResolveEvent(0)`) läuft 1500 Tage in wenigen Sekunden. So prüft man Balance, Abstürze und Story.
Die Unity-Teile kompilieren gegen das NuGet-Paket `UnityEngine.Modules` (Ziel `netstandard2.1`).

## Stand (zuletzt erledigt)

- Neue Gebirgsketten (wenige große, überlappende Berge), Berge unpassierbar (auch für Späher).
- Kamera endet am Kartenrand, Zoom-Grenze passend zur Bildschirmgröße.
- Jahreszeiten-Paletten (Frühling/Sommer, Herbst, Winter) wechseln automatisch.
- Freie Gebäudeplatzierung mit Standortregeln, Gerüste für Baustellen, Außenposten, Grenzstreit.
- Fahrendes Volk, Wachturm, Wissen (10 Kenntnisse), Reliquien im Kloster.
- Gesperrte Gebäude/Truppen/Reiter/Fenster werden ausgeblendet.

## Offene Ideen (Vorschläge für später)

- Straßen zwischen Gebäuden/Siedlungen, die Wege verkürzen (Entfernungsfaktor verbessern).
- Gebäude abreißen / verlegen (Knopf beim Antippen eines Gebäudes).
- Handelskarren zwischen eigenen Städten und Verbündeten (sichtbare Einheiten).
- Formationen bzw. Haltung von Heeren (offensiv/defensiv).
- Mehr Ereignisse rund ums fahrende Volk und Grenzland (Hirtenstreit, Wilderer, Pilgerzüge).
- Erfolge/Chroniken-Seite, Schwierigkeitsgrade, zweiter Spielstand-Platz.
- Echte Geräte-Tests auf Android: UI-Größen, Akku/Leistung (Chunk-Anzahl in `GroundRenderer`).

## So geht es in einer neuen Claude-Code-Sitzung weiter

1. Neue Sitzung auf diesem Repository starten (gleicher Branch oder neuer Branch vom aktuellen Stand).
2. Erste Nachricht z. B.: *„Lies ENTWICKLUNG.md und README.md. Dann: &lt;dein Wunsch&gt;.“*
3. Größere Wünsche lieber in mehreren kleineren Schritten geben und zwischendurch in Unity testen.
4. Am Ende jeder Sitzung diese Datei im Abschnitt „Stand“ aktualisieren lassen.
