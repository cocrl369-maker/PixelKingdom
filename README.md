# Pixel Kingdom

**Vom kleinen Dorf zum Königreich** – ein Städtebau- und Strategiespiel im Mittelalter für Unity, im Retro-Pixel-Stil alter Rollenspiele.

Ihr startet mit zwei Hütten und einem Acker. Ihr sammelt Ressourcen, kümmert euch um eure Bürger, baut, bildet Truppen aus und expandiert. Ihr trefft auf Fürsten, freie Dörfer und das Kaiserreich – verbündet euch mit Gold oder erobert sie. Schritt für Schritt steigt ihr auf: **Dorf → Städtegemeinde → Grafschaft → Fürstentum → Königreich → Kaiserreich**. Am Ende stürzt ihr den tyrannischen **Kaiser Malrik** und segelt zu anderen Kontinenten.

Die Karte ist 2D in Schrägansicht wie in alten Spielen. Räumlich wirkt sie durch Hangschattierung, Klippenkanten, Brandung an den Küsten und Schlagschatten. Jedes Gebäude ist eine eigene Pixelgrafik mit Ziegeldach, Fachwerk, Fenstern und Türen, sodass man die Gebäudearten auf einen Blick unterscheidet. Alle Grafiken werden beim Start aus Code erzeugt, es gibt keine Bilddateien.

---

## Starten

1. **Unity 6** (empfohlen, z. B. 6000.6.5f1) oder **Unity 2022.3 LTS** installieren.
2. Im Unity Hub: *Add → Add project from disk* und diesen Ordner wählen. Falls der Hub nach der Version fragt, eine installierte 2022.3- oder 6000.x-Version auswählen.
3. Eine beliebige Szene öffnen (z.B. die leere Standardszene) und **Play** drücken.

Mehr braucht es nicht. Das Spiel startet sich selbst (`RuntimeInitializeOnLoadMethod`) und erzeugt Kamera, Welt und Oberfläche komplett aus Code. Es gibt keine Prefabs, keine Texturen und keine Szenen-Einrichtung.

- Läuft in der **Built-in Render Pipeline** und in **URP**. Die Shader haben keinen LightMode-Tag und werden deshalb überall gezeichnet.
- Funktioniert mit dem **alten Input Manager** und mit dem **neuen Input System** (Unity-6-Standard).
- Für einen Build: *File → Build Settings → Add Open Scenes → Build*.

## Android (Tablet & Handy)

Das Spiel ist für Android ausgelegt und bleibt am PC mit Tastatur und Maus spielbar.

**Einmalig vorbereiten**
1. Im Unity Hub bei deiner Unity-Version unter *Installs → Zahnrad → Add modules* das Modul **Android Build Support** installieren, mit **OpenJDK** und **Android SDK & NDK Tools**.
2. Projekt öffnen, oben im Menü **Pixel Kingdom → 1. Android einrichten** wählen. Das übernimmt alle Einstellungen:
   - Paketname `com.pixelkingdom.game`
   - Querformat
   - Android 7 oder neuer
   - 64 Bit mit IL2CPP (Pflicht für Google Play)
   - App-Icon aus der Pixelkunst
   - Startszene
   - Umschalten auf die Android-Plattform

**Auf dem Tablet testen**
1. **Pixel Kingdom → 2. APK bauen** wählen. Die Datei landet in `Builds/PixelKingdom.apk`.
2. Die APK aufs Tablet kopieren (USB, Google Drive, …) und dort antippen. Beim ersten Mal muss das „Installieren aus unbekannten Quellen“ erlaubt werden.
3. Alternativ das Tablet per USB mit aktiviertem Entwicklermodus anschließen und in Unity *File → Build And Run* nutzen.

**Für Google Play**
- **Pixel Kingdom → 3. AAB bauen** erzeugt `Builds/PixelKingdom.aab`.
- Vorher unter *Edit → Project Settings → Player → Publishing Settings* einen eigenen Keystore anlegen. Er ist für Updates im Play Store nötig, also gut aufbewahren!
- Bei jedem Update die *Bundle Version Code* erhöhen.

**Touch-Steuerung**
| Aktion | Geste |
|---|---|
| Auswählen | Tippen auf Siedlung oder Heer |
| Heer befehligen | Heer gewählt → auf ein Ziel tippen → **Befehlsmenü** mit Weg, Marschdauer und allen Befehlen (Erobern, Überfall, Einrücken, Hinziehen …) |
| Listen und Bauleiste | seitlich bzw. hoch/runter wischen |
| Sprache | Hauptmenü oben rechts (Deutsch/English) oder Menü → „Sprache / Language“ |
| Karte verschieben | mit einem Finger ziehen |
| Zoomen | zwei Finger (Pinch) |
| Geländeinfo (Vorkommen, Bäume) | lange drücken |
| Bauen, Diplomatie, Aufsteigen | einmal tippen = Info, zweimal tippen = ausführen |
| Zurück / Menü | Android-Zurück-Taste |

**Auf dem Gerät**
- Die Oberfläche skaliert nach Bildschirmgröße und berücksichtigt Notch und Kameraaussparungen.
- Beim Wechsel in eine andere App wird gespeichert. Beim Zurückkehren wird die verpasste Zeit nachgeholt, mit Zusammenfassung.
- Auf Mobilgeräten wird nur der sichtbare Kartenbereich im Speicher gehalten.

**Am PC die Touch-Steuerung testen**: **F9** schaltet die Touch-Simulation um. Die Maus verhält sich dann wie ein Finger, und die Oberfläche wird wie auf dem Tablet skaliert.

## Steuerung

| Aktion | Eingabe |
|---|---|
| Karte bewegen | WASD / Pfeiltasten / rechte oder mittlere Maustaste ziehen |
| Zoomen (pixelgenaue Stufen) | Mausrad oder Q / E |
| Auswählen | Linksklick auf Siedlung oder Heer |
| Heer befehligen | Rechtsklick auf ein Ziel: bei Gelände marschiert das Heer sofort, bei Städten und Orten öffnet sich das Befehlsmenü |
| Überfall statt Eroberung | Shift + Rechtsklick auf eine feindliche Stadt (oder im Befehlsmenü wählen) |
| Pause / Tempo | Leertaste / 1, 2, 3 |
| Reich / Heerführung / Diplomatie / Chronik / Hilfe | R / M / G / L / F1 |
| Nächste Siedlung / Hauptstadt | Tab / H |
| Gebäude platzieren | Bau-Karte anklicken, dann Linksklick auf ein grünes Feld (Shift: mehrere, Esc/Rechtsklick: abbrechen); am Tablet zweimal tippen |
| Speichern | F5 (zusätzlich automatisch alle 90 s und beim Beenden) |
| Touch-Simulation (Android testen) | F9 |

## Was alles drin ist

### Grafik
- **Pixelkunst aus Code**: über 70 Sprites, darunter 20 unterscheidbare Gebäude, Ortszentren vom Dorfhaus bis zum Kaiserpalast, Palisaden und Steinmauern mit Türmen und Toren, Felder, Bäume, Berge, Erzvorkommen und Truppen. Alle haben Kontur und Schlagschatten.
- **Kartenboden pixelweise gemalt**:
  - weiche Küsten mit Brandung und Stränden
  - geschwungene Flüsse und Teiche
  - Hangschattierung mit Licht von Nordosten
  - Klippen an Geländestufen
  - Gras, Blumen, Sand und Fels als Bodentexturen
- Der Boden wird im Hintergrund-Thread gemalt, so ruckelt das Spiel nicht.
- **Nebel des Krieges**: Unerforschtes Land ist Pergament mit Tintenrand.
- **Fraktionsfarben** auf Fahnen, Wappenröcken, Segeln und gestrichelten Grenzen.
- **Pixelgenauer Zoom** in sechs Stufen, von der Weltkarte bis zur Nahansicht.
- **Jahreszeiten färben die Welt**: Frühling/Sommer leuchtend grün, Herbst in Orange und Gold, Winter verschneit. Die Farben wechseln automatisch.
- **Baustellen** sind an ihrem Holzgerüst zu erkennen.
- Die Kamera endet am Kartenrand – kein leerer Raum dahinter.

### Welt
- **Prozedural und groß**: 600 × 336 Felder, aus einem Seed erzeugt. Dazu gehören:
  - der Heimatkontinent **Mittland**, die kalten **Nordlande**, das heiße **Südreich** und der Archipel **Inselbund**,
  - zwei große Inseln weit im Osten, nur mit Schiffen erreichbar: das raue, erzreiche **Eisenfels** und die grüne, fruchtbare **Smaragdinsel** – jeweils mit eigenen Herrschern und freien Dörfern,
  - lange, natürlich geformte **Gebirgsketten** mit Hauptgrat, Seitengraten, Schluchten, Almflecken und Schneegipfeln. Sie werden pixelgenau in den Boden gemalt, ragen in Schrägansicht auf und füllen genau das Gebirgsgebiet. Gebirge ist **unpassierbar**, auch für Späher – nur Pässe und Täler führen hindurch,
  - **Flüsse**, die bergab ins Meer fließen, sowie **Seen**, **Inseln**, Strände und Flachwasser,
  - **Klimazonen** mit Laub- und Nadelwald, Steppe, Wüste und fruchtbarem Schwemmland an Flüssen.
- **Bodenschätze**: Eisen-, Gold- und Steinvorkommen liegen als Felsen auf der Karte.

### Entdecken & Herrschen
- **Ruinen**: Mit einem Heer oder Späher erkunden. Dort warten Gold, alte Waffen, heilige Relikte oder Landkarten – manchmal auch eine Falle.
- **Räuberlager**: Sie überfallen Höfe in der Nähe und machen den Leuten Angst. Hebt sie mit einem Heer aus, dann gibt es Beute und Ansehen.
- **Erlasse** (Reich-Übersicht): Wehrpflicht, Marktrecht, Erntedank, Freie Bauern, Pilgerwege. Jeder Rang erlaubt einen Erlass mehr.
- **Fahrendes Volk**: Gaukler und Händler ohne festen Wohnsitz ziehen durchs Niemandsland. Besucht sie mit einem Heer: handeln (Waren und eine Karte), sie zum Bleiben einladen (+Bewohner) oder vertreiben. Nach einigen Wochen ziehen sie weiter.
- **Wachtürme**: dürfen weit draußen stehen, decken die Umgebung auf und warnen vor Räubern.
- **Wissen** (Reich-Übersicht, Reiter „Wissen“): Doppelaxt, Schwerer Pflug, Steinmetzkunst, Kräuterkunde, Handelsgilde, Kartografie, Tiefbau, Schmiedekunst, Kettenhemden, Fruchtfolge. Immer eine Forschung zugleich, Klöster beschleunigen.
- **Reliquien**: In Ruinen gefundene Relikte bringen im Kloster (2 je Kloster) täglich Gold und Ansehen.
- **Ritterturniere** (mit Turnierplatz): Sie bringen Ansehen und Zufriedenheit, die Fürsten werden euch gewogener, und zwei fahrende Ritter bleiben bei euch.
- **Meister Aldo, der Chronist**, erklärt im Dialogfeld am unteren Bildschirmrand jede Neuerung, sobald sie zum ersten Mal vorkommt: erstes Heer, Winter, Hunger, Markt, Ruinen, Räuber, Erlasse, Druck des Kaisers und mehr. Die Tipps lassen sich im Menü abschalten.

### Musik & Klang
- Ruhige, sanfte mittelalterliche Musik, **komplett im Code komponiert und synthetisiert**: Harfe/Laute, Flöte, weiche Flächen und Bordun in wechselnden Kirchentonarten. **12 Stücke** (zusammen etwa 26 Minuten) in vier Stilen wechseln sich ab, mit Pausen dazwischen. Immer nur das nächste Stück wird vorbereitet, das spart Speicher.
- Dumpfer Holz-Klick bei jedem Knopfdruck.
- Musik und Klänge lassen sich im Menü einzeln abschalten.

### Wirtschaft & Bürger
- **6 Ressourcen**: Nahrung, Holz, Stein, Eisen, Gold und Waffen. Dazu kommen **Ansehen** und **Bevölkerung**.
- **25 Gebäude, frei platzierbar**: Ihr wählt für jedes Gebäude den Bauplatz auf der Karte.
  - Holzfäller brauchen Bäume in der Nähe, Bauernhöfe Wiese oder fruchtbares Land, Fischer Wasser, Häfen die Meeresküste, Minen ein passendes Vorkommen, Steinbrüche Hügel oder Fels.
  - Der **Standort** bestimmt den Ertrag (z. B. wie viele Bäume rund um den Holzfäller stehen).
  - Stadtgebäude (Markt, Taverne, Kaserne …) müssen in der Stadt stehen. **Produktionsgebäude** (Höfe, Minen, Fischer, Mühlen, Wachtürme) dürfen bis zu 14 Felder weit draußen stehen – lange Wege kosten aber Ertrag und Bauzeit.
  - **Fremdes Gebiet** ist tabu. Im **Niemandsland** sind Außenposten erlaubt, doch Nachbarn beschweren sich (**Grenzstreit**: zahlen, auf dem Recht bestehen oder räumen). Wächst ein Nachbar über euren Posten, bringt er kaum noch etwas.
- **Übersichtlicher Anfang**: Gebäude, Truppen, Reiter und Fenster erscheinen erst, wenn sie freigeschaltet sind (Rang, nötige Gebäude, erste Nachbarn).
- **Holzfällerei**: Holzfäller fällen sichtbar Bäume, an ihrer Stelle bleiben Stümpfe. Der Wald wächst langsam nach. Ein kahler Wald liefert wenig Holz.
- **Erzabbau**: Eisenminen und Goldminen stehen direkt an einem passenden Erzvorkommen. Je mehr Vorkommen, desto höher der Ertrag. Jeder Spieler hat ein Eisen- und ein Steinvorkommen in Reichweite seines Startdorfs.
- **Realistische Bevölkerung**: Es arbeiten 60 % der Bewohner, Nahrungsbetriebe werden zuerst besetzt. Wachstum braucht Nahrung, Wohnraum und Zufriedenheit. Rekruten werden aus der Bevölkerung genommen.
- **Zufriedenheit**: Steuern, Rationen, Überfüllung, Hunger, Krieg, Winter, frisch eroberte Städte und Verwaltungslast wirken sich aus. Den Zielwert seht ihr samt Gründen im Tooltip.
- **Jahreszeiten**: Im Winter wachsen kaum Feldfrüchte, deshalb lohnt sich Vorratshaltung.
- **Markt** mit schwankenden Preisen, dazu Lagerkapazität und Handelsabkommen.
- **Unruhen und Aufstände**: Unzufriedene Städte können sich lossagen.

### Seefahrt
- **Hafen und Wissen „Schiffbau“** (ab Grafschaft) schalten die Seefahrt frei – vor oder nach dem Sturz des Kaisers.
- **Handelsschiffe** bringen täglich Gold; Handelsabkommen und eigene Häfen auf anderen Kontinenten erhöhen den Ertrag.
- **Kriegsschiffe** tragen Heere übers Meer (genug Plätze nötig), schützen vor **Piraten** und beschießen Küstenstädte bei Belagerungen.
- **Schiffsklassen** Kogge → Holk → Karavelle: Das Wissen der Schiffbauer (Schiffbau, Kielbau, Seekarten & Kompass) und die Größe der **Werft** bestimmen, wie groß, schnell und wehrhaft die Flotte ist. Auf See segeln Heere deutlich schneller als zu Fuß.
- Der Reiter **„Hafen“** erscheint nur in Hafenstädten.

### Militär
- **Garnisonsplätze**: Jede Kaserne bietet 40 Plätze (Archerie, Stall und Burg weitere). Wer keinen Platz hat, lagert als Heer vor der Stadt.
- **Feldlager**: frei platzierbare Lager auch weit draußen. Heere dort erholen sich, verteidigen sich um 30 % besser (bis 60 Mann) und sehen weit ins Land.
- **6 Einheiten**: Miliz, Speerträger, Bogenschützen, Ritter, Rammen und Späher.
- **Konter-System**: Speer schlägt Ritter, Ritter schlagen Bogen, Bogen schlagen Speer. Bogenschützen schießen zuerst, Rammen brechen Mauern.
- **Heere ziehen über die Karte**: Die Wegsuche (A*) berücksichtigt Wald, Hügel, Furten und Gebirge.
- **Heerführung (M)**:
  - alle Heere und Garnisonen auf einen Blick
  - Ziele aus einer Liste wählen, mit Entfernung und Status: Erobern, Überfall, Hinziehen, Einrücken
  - Heere teilen und vereinen, Truppen mit der Garnison tauschen
- **Befehlsvorschau**: Ist ein Heer gewählt, steht am Mauszeiger, was ein Rechtsklick bewirkt.
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

### Geschichte (13 Kapitel) – ihr bestimmt das Tempo
1. **Ein bescheidener Anfang**: die ersten Gebäude
2. **Vom Dorf zur Gemeinde**: Wachstum und erster Aufstieg
3. **Vorräte für den Winter**: Lagerhaus, Brunnen, volle Speicher
4. **Fremde Banner**: Nachbarn kennenlernen, zweite Siedlung, erster Vertrag
5. **Gilden und Handwerk**: Mine, Schmiede, Markt und die ersten Waffen
6. **Das Joch des Kaisers**: Grafschaft, Burg, erste Siege
7. **Der Bund der Freien**: Fürstentum, Verbündete, ein Heer – und euer Aufstand gegen den Kaiser
8. **Krone des Volkes**: Königreich werden und kaiserliche Städte befreien
9. **Sturm auf Valdoran**: die Hauptstadt des Tyrannen erobern
10. **Der Thronstreit**: General Kael und „Malriks Getreue“ – besiegen oder Frieden schließen
11. **Neue Ufer**: Hafen bauen, über das Meer segeln, in Übersee Fuß fassen
12. **Der Kaiser im Exil**: Malriks Festung im Südreich einnehmen
13. **Herrscher der Welt**: alle Kontinente durch Bündnis oder Eroberung einen

**Tempo und Druck**:
- **Friedenszeit** zu Spielbeginn (150 Tage) und nach jedem Kapitel (45 Tage, im Krieg gegen den Kaiser 20). In dieser Zeit greift euch niemand an, ihr könnt euch ganz um eure Dörfer kümmern.
- **Druck des Kaiserreichs** (Leiste unter den Aufgaben): Er steigt langsam mit eurem Wachstum und wenn ihr den Zehnt verweigert. Zahlen und Geschenke senken ihn.
  - Ab 50 drohen Strafexpeditionen. Ihr könnt Buße zahlen, plündern lassen oder kämpfen.
  - Ab Kapitel 7 bestimmt ihr selbst, wann ihr euch erhebt (Knopf „Gegen den Kaiser erheben“). Wartet ihr zu lange, erklärt der Kaiser bei 100 von sich aus den Krieg.

### Sprachen
- **Deutsch und Englisch**. Beim ersten Start gilt die Systemsprache; umschalten im Hauptmenü oder im Spielmenü, jederzeit und ohne Neustart.

### Einstieg
- **Einführung beim ersten Spiel**: Die Oberfläche wird Schritt für Schritt hervorgehoben und kurz erklärt – Vorräte, Rang, Tempo, Schnellzugriff, Aufgaben, Weltkarte, Dorf und Bauen. Sie lässt sich überspringen und über die Hilfe (F1) erneut aufrufen.
- **Gut lesbare Beschreibungen**: Die Überpunkte in den Infotexten (Kosten, Bauzeit, Wirkung, Werte, Sold, Aussicht, …) sind unterstrichen. Gebäude zeigen ihre Wirkung, Truppen ihre Stärken und Voraussetzungen.

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
    Sim/       Game (Kern), Economy, Military, Diplomacy, AI, Events, Story, WorldSetup, NameGen,
               Placement (Bauplätze), Realm (Ruinen/Lager/Erlasse/Turniere), Frontier (Grenzland, fahrendes Volk, Wissen)
    Art/       Pixelkunst ohne Unity: PixelCanvas, SpriteArt (Gebäude/Natur/Truppen), IconArt, GroundPainter (Kartenboden),
               MapLayout (Natur-Objekte je Kachel, Anordnung der Gebäude in Siedlungen), ArtPalette
    Render/    GroundRenderer, ObjectRenderer, EntityRenderer, SpriteLib (Atlas), QuadBatch, CameraController, Palette
    UI/        GameUI (+Panels, +Windows), PixelFont (eigene Bitmapschrift), PixelIcons
```

Die Simulation (`World/`, `Sim/`, `Data/`) und die Pixelkunst (`Art/`) sind **komplett unabhängig von Unity**. Sie lässt sich ohne Editor testen und über tausende Spieltage laufen lassen.

## Erweitern

- **Neues Gebäude**: Wert am Ende von `BType` ergänzen und in `Buildings` definieren (Kosten, Produktion, Bedingungen). Dann eine Zeichenfunktion in `SpriteArt` schreiben und sie als `"b_" + Name` registrieren. Wo es stehen darf, regelt `CanPlace` in `Sim/Placement.cs` (ggf. `IsRemote` und `SiteBonus` ergänzen). Die Bau-Karte erscheint automatisch.
- **Neue Einheit**: `UType` und `Units.All` erweitern, dann `Units.Count` anpassen.
- **Neues Ereignis**: In `Events.cs` eine ID in den Pool aufnehmen, dann einen `Describe`- und einen `ResolveEvent`-Fall schreiben.
- **Neues Kapitel**: In `Story.cs` einen Eintrag in `Chapters` mit Intro, Zielen, Outro und Belohnung anlegen.
- **Neues Wissen**: In `Techs.All` (Data/Defs.cs) ergänzen und die Wirkung in `Sim/Frontier.cs` (`TechProd`, `CombatMult` …) einbauen.
- **Jahreszeiten-Farben**: In `ArtPalette.All` (0 = Frühling/Sommer, 1 = Herbst, 2 = Winter).
- **Neue Icons**: farbig in `IconArt` (16×16) oder schlicht in `PixelIcons` (12×12 ASCII-Art).

## Hinweise

- Die Unity-Teile wurden gegen die Unity-Referenzassemblies kompiliert. Die Simulation wurde ohne Unity mit Bot-Spielern über tausende Tage getestet, darunter Story, Kaiser-Sturz und Seefahrt. Gebäude, Icons und Kartenausschnitte wurden außerhalb von Unity als Vorschaubilder gerendert und geprüft.
- Spielstände aus älteren Versionen (kleinere Welt) werden nicht mehr geladen. Sie werden als `.alt-vN` gesichert, und es beginnt ein neues Spiel.
- **Weiterentwickeln mit Claude Code**: siehe `ENTWICKLUNG.md` – dort stehen Aufbau, Stand und offene Ideen, damit eine neue Sitzung sofort weitermachen kann.
- Der Spielstand liegt unter `Application.persistentDataPath/pixelkingdom_save.json`, eine Sicherung unter `.bak`.
