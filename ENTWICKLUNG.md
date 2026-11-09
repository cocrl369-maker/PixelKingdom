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
- Gebirge: große, langgezogene, natürlich geformte Ketten (braune Felsen, Schneegipfel, Licht von links) wie im Vorbild des Entwicklers; sie füllen genau das Gebirgsgebiet und überschreiten es nicht; unpassierbar.
- Eingabe: Ein Druck auf Knöpfe/Fenster darf nie die Karte darunter auslösen (`GameUI.LastUIPress`, Ebenen in `OnGUI`).
- Farben wechseln **automatisch mit der Jahreszeit** (kein Paletten-Knopf).
- Ideen aus großen Strategiespielen sind willkommen, aber **nichts kopieren** (keine Namen, Grafiken, Musik, Texte).

## Regeln für neuen Code

- **Der Ordner `Assets/PixelKingdom` muss in jedes neue Unity-Projekt passen**: keine Abhängigkeit von Paketen, keine Projekteinstellungen ändern. Das neue Input System wird nur per Reflection angesprochen (`Core/PKInput.cs`), `link.xml` schützt es beim Android-Build. Im Editor-Skript nur stabile APIs (`NamedBuildTarget`), nichts Versionsabhängiges.
- **Texte immer übersetzbar schreiben**: `Loc.T("Deutscher Text")` für feste Texte, `Loc.F("Text mit {0} und {1}", a, b)` für Texte mit Werten (keine `$"..."`-Strings für sichtbare Texte). Die englische Übersetzung kommt in `Scripts/Loc/Loc.En.cs` (Schlüssel = deutscher Text). Fehlt sie, erscheint der deutsche Text.
- Feste Daten (Gebäude, Einheiten, Ränge, Kapitel) übersetzen sich selbst über ihre Eigenschaften (`Name`, `Desc` …); feste Listen nutzen `LArr`.
- **Eingabe der Oberfläche**: Nur `Clicked(rect)`, `Hover(rect)`, `Scroll(...)` aus `UI/GameUI.Input.cs` benutzen – nie `Event.current` für Maus/Finger (auf Android kommen Unitys Mausereignisse mit falscher Position an). Gruppen mit `BeginGroup`/`EndGroup` (nicht `GUI.BeginGroup`).

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
| Pixelkunst | `Scripts/Art/*.cs` | `SpriteArt` (Sprites), `GroundPainter` (Boden **und Gebirge**: Höhenfeld + Schrägansicht in `MountH`/`MountainPixel`), `MapLayout` (Objekte, Siedlungsbild), `ArtPalette` (Jahreszeiten) |
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

- Welt 720 × 450: Eisenfels und Smaragdinsel umschließen Mittland als Bögen (Meeresarm wird erzwungen). Spielstand-Version 6.
- Problem-Symbole über Gebäuden (`Sim/Problems.cs`, Anzeige in `GameUI.DrawProblemIcons`, abschaltbar).
- Mythen (`Sim/Myth.cs`): Steintafeln mit Rätseln, Drachenhort nach 3 Rätseln, Heilige Quellen, Irrlichter, Komet.
- Eingabe ohne Paketabhängigkeit (Reflection), Editor-Skript für Unity 6 bereinigt.
- Seefahrt (`Sim/Navy.cs`): Handels- und Kriegsschiffe, Klassen Kogge/Holk/Karavelle (Wissen × Werft), Seehandel, Transportplätze, Piraten, Beschuss von Küstenstädten.
- Garnisonsplätze je Kaserne (`GarrisonCap`), Feldlager für Heere (Erholung, +30 % Verteidigung, Sicht).
- Welt 600 × 336 mit zwei großen Inseln im Osten (Kontinente 4 „Eisenfels“, 5 „Smaragdinsel“). Spielstand-Version 5.
- Eigene Zeiger-Eingabe: Klicks gehen nie mehr „durch“ Fenster; Wischen scrollt Listen und die Bauleiste.
- Befehlsmenü für Heere (`Sim/Orders.cs`): Ziel antippen → Weg, Marschdauer, mögliche Befehle.
- Verständliche Ertragsangaben beim Bauen (Vergleich mit einem normalen Gebäude, z. B. „+12 %“).
- Musik: 12 ruhige Stücke, wechselnde Tonarten und Stile.
- Englische Sprache (`Scripts/Loc/`), umschaltbar im Menü.
- Gebirge neu: in den Boden gemaltes Höhenfeld (Hauptgrat, Seitengrate, Schnee, Almflecken), Schrägansicht, bleibt im Gebirgsgebiet; unpassierbar.
- Touch: Knöpfe in Dialogen/Fenstern lösen nicht mehr zusätzlich die Karte aus.
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
