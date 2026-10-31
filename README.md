# Pixel Kingdom

**Vom kleinen Dorf zum Königreich** – ein minimalistisches, nett gezeichnetes 3D-Städtebau- und Strategiespiel im Mittelalter für Unity.

Ihr startet mit zwei Hütten und einem Acker. Ihr sammelt Ressourcen, kümmert euch um eure Bürger, baut, bildet Truppen aus und expandiert. Ihr trefft auf Fürsten, freie Dörfer und das Kaiserreich – verbündet euch mit Gold oder erobert sie. Schritt für Schritt steigt ihr auf: **Dorf → Städtegemeinde → Grafschaft → Fürstentum → Königreich → Kaiserreich**. Am Ende stürzt ihr den tyrannischen **Kaiser Malrik** und segelt zu anderen Kontinenten.

Der Stil orientiert sich am Vorbild: warme Terrakotta- und Sandtöne, Pixel-Icons, Spielkarten zum Bauen und Eck-Klammern um die Auswahl. Die Welt selbst ist 3D, aber bewusst flach schattiert und reduziert statt fotorealistisch.

---

## Starten

1. **Unity 2022.3 LTS** oder **Unity 6** installieren. Getestet wird gegen die API von Unity 2021.3+.
2. Im Unity Hub: *Add → Add project from disk* und diesen Ordner wählen. Falls der Hub nach der Version fragt, eine installierte 2022.3- oder 6000.x-Version auswählen.
3. Eine beliebige Szene öffnen (z.B. die leere Standardszene) und **Play** drücken.

Mehr braucht es nicht. Das Spiel startet sich selbst (`RuntimeInitializeOnLoadMethod`) und erzeugt Kamera, Welt und Oberfläche komplett aus Code. Es gibt keine Prefabs, keine Texturen und keine Szenen-Einrichtung.

- Läuft in der **Built-in Render Pipeline** und in **URP**. Der Shader hat keinen LightMode-Tag und wird deshalb überall gezeichnet.
- Funktioniert mit dem **alten Input Manager** und mit dem **neuen Input System** (Unity-6-Standard).
- Für einen Build: *File → Build Settings → Add Open Scenes → Build*.

## Steuerung

| Aktion | Eingabe |
|---|---|
| Kamera bewegen | WASD / Pfeiltasten / rechte Maustaste ziehen |
| Zoomen | Mausrad |
| Drehen | Q / E oder mittlere Maustaste ziehen |
| Auswählen | Linksklick auf Siedlung oder Heer |
| Heer befehligen | Rechtsklick auf Gelände, eigene Stadt (Garnison) oder Feind (Angriff) |
| Überfall statt Eroberung | Shift + Rechtsklick auf eine feindliche Stadt |
| Pause / Tempo | Leertaste / 1, 2, 3 |
| Reich / Diplomatie / Chronik / Hilfe | R / G / L / F1 |
| Nächste Siedlung / Hauptstadt | Tab / H |
| Farbpalette wechseln | P |
| Speichern | F5 (zusätzlich automatisch alle 90 s und beim Beenden) |

## Was alles drin ist

### Welt
- **Prozedural und groß**: 320 × 224 Felder, aus einem Seed erzeugt. Dazu gehören:
  - der Heimatkontinent **Mittland**, die kalten **Nordlande**, das heiße **Südreich** und der Archipel **Inselbund**,
  - **Gebirgszüge** (Ridged Noise) mit Schneegipfeln,
  - **Flüsse**, die bergab ins Meer fließen, sowie **Seen**, **Inseln**, Strände und Flachwasser,
  - **Klimazonen** mit Laub- und Nadelwald, Steppe, Wüste und fruchtbarem Schwemmland an Flüssen.
- **Low-Poly-3D**: Die Höhenfelder sind flach schattiert, das Licht ist in die Vertexfarben „gebacken“. Bäume, Steine, Grenzlinien und Gebietstönung sind ebenfalls 3D.
- **Nebel des Krieges als Pergament**: Unerforschtes Land ist flaches Papier. Beim Erkunden wächst die 3D-Welt daraus hervor.
- **Drei Farbpaletten**: Terrakotta (wie das Vorbild), Pastell und Abenddämmerung.

### Wirtschaft & Bürger
- **6 Ressourcen**: Nahrung, Holz, Stein, Eisen, Gold und Waffen. Dazu kommen **Ansehen** und **Bevölkerung**.
- **20 Gebäude** mit Standortbedingungen:
  - Bauernhöfe brauchen fruchtbares Land, Fischer brauchen Wasser.
  - Holzfäller brauchen Wald, Steinbrüche Hügel und Minen Gebirge.
  - Der Ertrag hängt vom Gelände im Stadtgebiet ab.
- **Realistische Bevölkerung**: Es arbeiten 60 % der Bewohner, Nahrungsbetriebe werden zuerst besetzt. Wachstum braucht Nahrung, Wohnraum und Zufriedenheit. Rekruten werden aus der Bevölkerung genommen.
- **Zufriedenheit**: Steuern, Rationen, Überfüllung, Hunger, Krieg, Winter, frisch eroberte Städte und Verwaltungslast wirken sich aus. Den Zielwert seht ihr samt Gründen im Tooltip.
- **Jahreszeiten**: Im Winter wachsen kaum Feldfrüchte, deshalb lohnt sich Vorratshaltung.
- **Markt** mit schwankenden Preisen, dazu Lagerkapazität und Handelsabkommen.
- **Unruhen und Aufstände**: Unzufriedene Städte können sich lossagen.

### Militär
- **6 Einheiten**: Miliz, Speerträger, Bogenschützen, Ritter, Rammen und Späher.
- **Konter-System**: Speer schlägt Ritter, Ritter schlagen Bogen, Bogen schlagen Speer. Bogenschützen schießen zuerst, Rammen brechen Mauern.
- **Heere ziehen über die Karte**: Die Wegsuche (A*) berücksichtigt Wald, Hügel, Furten und Gebirge.
- **Kampfarten**: Feldschlachten, Belagerungen mit Mauern, Burg, Geländebonus und Bürgeraufgebot sowie **Überfälle** zum Plündern.
- Moral, Sold, Desertion bei leeren Kassen und Schlachtberichte.

### Diplomatie & lebendige Welt
- Jeder Herrscher hat **Charakterzüge**: kriegerisch, habgierig, ehrenhaft, ehrgeizig, kaisertreu oder kaiserfeindlich.
- **Interessenkonflikte**:
  - Gebietsansprüche auf fremde Städte
  - Reibereien an Grenzen
  - Treue oder Feindschaft gegenüber dem Kaiser
  - Neid auf euren Aufstieg
  - Gemeinsame Feinde, die verbinden
- **Aktionen**: Geschenk, Handel, Nichtangriffspakt, **Bündnis gegen Mitgift**, **Anschluss kaufen** (mit Geld vereinen), Tribut fordern, Frieden und Krieg. Vorab werden Aussicht und Gründe angezeigt.
- Bündnisse werden eingehalten: Verbündete treten in Kriege ein. Vertragsbruch kostet Ansehen bei allen.
- Die **KI-Fürsten** bauen, rekrutieren, siedeln, erklären einander Krieg, schließen Frieden, stellen Gebietsforderungen, bieten Bündnisse an, bitten um Hilfe und verheiraten ihre Häuser.
- **Über 25 Ereignisse mit Entscheidungen**, zum Beispiel Flüchtlinge, Räuber, Seuchen, Feste, der kaiserliche Zehnt, Kartographen, Aufstände im Kaiserreich und Heiratsangebote.

### Geschichte (9 Kapitel)
1. **Ein bescheidener Anfang**: die ersten Gebäude
2. **Vom Dorf zur Gemeinde**: Wachstum und erster Aufstieg
3. **Fremde Banner**: Nachbarn kennenlernen, zweite Siedlung, erster Vertrag
4. **Das Joch des Kaisers**: Grafschaft, Burg, erste Siege
5. **Der Bund der Freien**: Fürstentum, Verbündete und ein Heer. Danach erklärt der Kaiser den Krieg.
6. **Krone des Volkes**: Königreich werden und kaiserliche Städte befreien
7. **Sturm auf Valdoran**: die Hauptstadt des Tyrannen erobern
8. **Neue Ufer**: Hafen bauen, über das Meer segeln, in Übersee Fuß fassen
9. **Herrscher der Welt**: alle Kontinente durch Bündnis oder Eroberung einen

Wer dem Kaiser den Zehnt dreimal verweigert, löst den Krieg schon früher aus.

### Für gelegentliches Spielen gemacht
- **Automatisches Speichern** alle 90 s, beim Beenden und beim Pausieren der App.
- **Fortschritt während der Abwesenheit**: Pro 3 Minuten Pause vergeht ein Spieltag, maximal 100. Wirtschaft, Bau und Wachstum laufen weiter, Kriegserklärungen und Ereignisse nicht. Beim Zurückkehren gibt es eine Zusammenfassung.
- **Verwalter** pro Siedlung: baut automatisch, was gebraucht wird.
- **Anfängerschutz**: Solange ihr nur eine Siedlung habt und unter Grafschaft-Rang seid, wird euer Dorf höchstens geplündert, nicht ausgelöscht.
- **Für Langzeitspieler**:
  - Ränge mit harten Bedingungen
  - mehrere Fronten und wechselnde Allianzen
  - Belagerungen
  - Aufstände in eroberten Städten
  - Verwaltungslast bei zu vielen Städten
  - Übersee-Expansion

## Projektstruktur

```
Assets/PixelKingdom/
  Resources/PKFlat.shader      Unbeleuchteter Vertexfarben-Shader (Built-in & URP)
  Scripts/
    Core/      GameManager (Einstieg, Schleife, Eingabe), GameState (Speicherdaten), SaveSystem, PKInput
    Data/      Defs: Ressourcen, Ränge, Gebäude, Einheiten, Kalender – hier wird balanciert
    World/     WorldMap, WorldGenerator (Kontinente/Flüsse/Klima), Noise, Pathfinder (A*)
    Sim/       Game (Kern), Economy, Military, Diplomacy, AI, Events, Story, WorldSetup, NameGen
    Render/    TerrainRenderer (Chunks), EntityRenderer (Städte/Heere), Models, MeshBuilder, Palette, CameraController
    UI/        GameUI (+Panels, +Windows), PixelFont (eigene Bitmapschrift), PixelIcons (12x12-Icons)
```

Die Simulation (`World/`, `Sim/`, `Data/`) ist **komplett unabhängig von Unity**. Sie lässt sich ohne Editor testen und über tausende Spieltage laufen lassen.

## Erweitern

- **Neues Gebäude**: Wert in `BType` ergänzen, in `Buildings` definieren (Kosten, Produktion, Bedingungen) und ein Modell in `Models.Building` hinzufügen. Die Bau-Karte erscheint automatisch.
- **Neue Einheit**: `UType` und `Units.All` erweitern, dann `Units.Count` anpassen.
- **Neues Ereignis**: In `Events.cs` eine ID in den Pool aufnehmen, dann einen `Describe`- und einen `ResolveEvent`-Fall schreiben.
- **Neues Kapitel**: In `Story.cs` einen Eintrag in `Chapters` mit Intro, Zielen, Outro und Belohnung anlegen.
- **Neue Farbpalette**: In `Palette.All` hinzufügen.
- **Neue Icons**: 12×12 ASCII-Art in `PixelIcons` eintragen (`#` voll, `+` halbtransparent).

## Hinweise

- Die Unity-Teile wurden gegen die Unity-Referenzassemblies kompiliert. Die Simulation wurde headless mit Bot-Spielern über tausende Tage getestet, darunter Story, Kaiser-Sturz und Seefahrt. Optik, Kamera und UI-Größen sollten beim ersten Start im Editor noch einmal angeschaut und nach Geschmack nachjustiert werden.
- Der Spielstand liegt unter `Application.persistentDataPath/pixelkingdom_save.json`, eine Sicherung unter `.bak`.
