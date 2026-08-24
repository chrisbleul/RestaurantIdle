# Restaurant Idle Game

Projektplan: **`PLANv4.md`** ist der aktuelle Stand und die einzige gültige
Quelle für offene Arbeit. `PLAN.md`, `PLANv2.md` und `PLANv3.md` sind
abgeschlossen und dokumentieren die jeweilige Analyse -- ihre Checklisten
sind überholt.

Kurzstand (24.08.2026): Ökonomie (zwei Upgrade-Achsen), Auftragskette (Geld
entsteht nur beim Servieren eines echten Gastes), Warteschlange, Ruf,
Trinkgeld, Rush Hour, VIP-Gäste und der Art-Pass im Hochformat sind umgesetzt
und im laufenden Editor visuell geprüft. Die WebGL-CI ist grün: jeder Push auf
`main` ist nach 15--20 Minuten live unter <https://cgo-app.de/restaurant/>
(hinter dem Plattform-Login). Offen sind vor allem Retention-Inhalte
(Tagesziele) und echtes Balancing mit Daten, siehe PLANv4 Abschnitt 5.

## Entwicklungsstand

Ausführlich in `PLANv4.md` Abschnitt 4; hier die Kurzfassung, damit klar ist,
worauf man aufsetzt.

- **Simulation.** Geld entsteht ausschließlich in einem einzigen Pfad
  (`GameManager.ServeGuest`) beim Bedienen eines echten Gastes. Daran hängen
  Trinkgeld (bis +50 % bei sofortiger Bedienung), Ruf (0--100, wirkt als
  Multiplikator auf den Gästestrom), VIP-Faktor und Prestige-Multiplikator.
  Warteschlange mit vier Plätzen und eigener Geduld, Rush Hour alle 150 s.
  Die reine Rechnerei liegt Unity-frei in `BalancingCore` (`Service.cs` für
  Trinkgeld und Ruf) und ist unit-getestet.
- **Bedienung.** Jede Stationsaktion läuft über Antippen der Station und den
  daraufhin geöffneten Dialog. Es gibt bewusst keine Dauerbuttons pro Station;
  am unteren Rand stehen nur die beiden globalen Aktionen.
- **Darstellung.** Hochformat 1080x1920. Kopfleiste, Ziel-Fortschrittsbalken,
  Schilder über den Stationen mit Geduldsbalken, aufsteigende Beträge am
  Verkaufsort, Toasts. Gastraum aus dem Kenney Furniture Kit, Modelle nach
  gemessener Zielgröße platziert (die Kits sind untereinander *nicht*
  maßstabsgetreu), prozeduraler Fliesenboden, weiche Schatten,
  Bodenschatten-Flecken unter den Figuren, Post-Processing.
- **Technik.** Der Grundriss steht an genau einer Stelle
  (`Game/RestaurantLayout.cs`) und wird von Editor-Szenenbau *und* Laufzeit
  gemeinsam benutzt. Die Kamera rahmt selbsttätig den freien Streifen
  zwischen den HUD-Leisten. Gemeinsames Partikel-Material, dauerhafte
  AudioSource und Asset-Cache (`Game/GameAssets.cs`), 60 fps gesetzt.
  57 Balancing-Tests grün.

## Struktur

- `game/BalancingCore` -- reines C#-Modul (kein Unity) für Kostenkurven,
  Meilensteine, Prestige und Offline-Ertrag, auf `BigDouble`
  (vendored von [Razenpok/BreakInfinity.cs](https://github.com/Razenpok/BreakInfinity.cs)).
  Unit-Tests in `game/BalancingCore.Tests` (`dotnet test`).
- `client` -- Unity-Projekt (URP, orthografisch-isometrisch, Hochformat).
  `Assets/Scripts/BalancingCore` ist eine 1:1-Kopie von `game/BalancingCore`
  (bewusst dupliziert statt verlinkt -- Unity kompiliert alles unter
  `Assets/` selbst, ein Projektverweis über Ordnergrenzen hinweg wäre nur
  Fragilität); **Änderungen dort müssen von Hand in beide Ordner**, sonst
  testet `dotnet test` etwas anderes als das Spiel ausführt.
  `Assets/Scripts/Game`:
  - `GameManager.cs` -- Tick-Loop, Warteschlange, Rush Hour, Ruf, HUD-Aufbau
    zur Laufzeit, Kamera-Rahmung. Der eine Geldpfad ist `ServeGuest`.
  - `RestaurantLayout.cs` -- Grundriss (Stationsabstände, Gastplätze,
    Warteschlange, Eingang/Ausgang) als gemeinsame Wahrheit für
    `Assets/Editor/CIBuild.cs` und die Laufzeit.
  - `GameAssets.cs` -- Cache für Kamera, Font, Sprites, Partikel-Material und
    eine dauerhafte 2D-AudioSource (statt pro Sound ein GameObject).
  - `GroundShadow.cs`, `SurfaceTexture.cs` -- Bodenschatten unter den Figuren
    (versetzt entlang der Lichtrichtung, sonst verdeckt das Billboard sie
    selbst) und prozedurale Fliesen-/Korn-Texturen.
  - `Station.cs`, `StationBadge.cs`, `StationHotspot.cs`, `GuestMover.cs`,
    `GuestSpriteAnimator.cs`, `StaffWorker.cs`, `FloatingText.cs`,
    `CoinBurst.cs`, `Toast.cs`, `SteamEffect.cs`, `LocationTheme.cs`,
    `SaveSystem.cs`, `BackendClient.cs`.

  `Assets/Editor`: `CIBuild.cs` baut Szene und WebGL-Player programmatisch
  (robuster als eine handgepflegte `.unity`-Datei; die Szene wird nur über
  den Menüpunkt `RestaurantIdle/Szene fuer Editor erzeugen` oder bei
  fehlender Datei neu erzeugt), `UrpSetup.cs` setzt Pipeline-Qualität und
  Post-Processing-Profil, `GameViewPortrait.cs` stellt den Game-View aufs
  Hochformat.
- `apps/api` -- Fastify-Backend (Node/TS): Save-Endpunkte, serverautoritativer
  Offline-Progress, Auth über `@cgo/platform-auth`. Liefert unter `apps/api/public/webgl`
  zusätzlich den WebGL-Build aus (kein eigener "web"-Service nötig, nginx
  routet `/restaurant/` bereits auf denselben Port wie `/restaurant/api/`).
- `infra` -- `docker-compose.yml`, `deploy.sh`, `.env.example` für den Deploy
  auf der EC2, analog zu den anderen Apps auf `cgo-app.de`.
- `.github/workflows/ci.yml` -- Tests (Node + .NET), WebGL-Build (nur bei
  main-Push, game-ci/unity-builder), Docker-Image nach GHCR, Deploy per SSH
  (wiederverwendet `chrisbleul/CGOPlattform`s `verify-node`/`deploy`-Bausteine,
  Muster aus dem Arbeitsauftrag AP4.1).

## Unity-CI einrichten (einmaliger Schritt, nicht automatisierbar)

Unity Editor läuft nicht auf ARM/aarch64 (die EC2 ist Graviton), der
WebGL-Build läuft deshalb in CI auf einem x86_64-GitHub-Runner. Die
Lizenzaktivierung dafür hängt zwingend an einem echten, interaktiven
Unity-Account-Login -- game-ci hat den früheren rein-CI-basierten
Aktivierungsweg (Actions-Artifact-Datei hochladen) mittlerweile abgeschaltet,
ein lokaler Schritt mit echter GUI ist jetzt unvermeidbar.

**Ohne eigenen Windows/Mac/Linux-Rechner** (z. B. nur Handy): dieses Repo hat
einen fertigen [GitHub Codespace](https://docs.github.com/codespaces) mit
Browser-Desktop, extra dafür gebaut.

1. Auf github.com im Repo: "Code" → Tab "Codespaces" → "Create codespace on main".
   Läuft komplett im Browser, auch auf dem Handy. Der Codespace installiert
   beim Start automatisch Unity Hub (`.devcontainer/install-unity-hub.sh`).
2. Sobald fertig: Tab "PORTS" → Port 6080 öffnen (Symbol für "im Browser
   öffnen") → Passwort `unity`. Das ist ein Linux-Desktop im Browser.
3. Dort per Rechtsklick ein Terminal öffnen, `unityhub` eingeben, mit
   (kostenlosem) Unity-Account einloggen, Preferences → Licenses → "Add" →
   "Get a free personal license".
4. Zurück im normalen Codespaces-Datei-Explorer (nicht im Desktop-Tab):
   `~/.local/share/unity3d/Unity/Unity_lic.ulf` öffnen, Inhalt kopieren.
5. Drei Secrets im Repo hinterlegen (Settings → Secrets and variables →
   Actions → "New repository secret"): `UNITY_LICENSE` (Inhalt der
   `.ulf`-Datei), `UNITY_EMAIL`, `UNITY_PASSWORD` (dieselben Zugangsdaten
   wie der Unity-Account).
6. Codespace danach löschen (Codespaces-Übersicht → "..." → Delete) -- wird
   nur für diesen einen Schritt gebraucht.

**Mit eigenem Rechner:** [Unity Hub](https://unity.com/download) lokal
installieren, Schritte 3-5 dort genauso durchführen (Lizenzdatei-Pfad unter
Windows: `C:\ProgramData\Unity\Unity_lic.ulf`, Mac:
`/Library/Application Support/Unity/Unity_lic.ulf`).

Danach baut `ci.yml` bei jedem Push auf `main` automatisch WebGL, deployt es
auf `cgo-app.de/restaurant/` -- kein weiterer manueller Schritt. Alles
andere (Repo, Deploy-Key, Backend, Registrierung) ist bereits eingerichtet.

Der frühere 401-Fehler im `webgl-build`-Job (`UnityConnectLoginRequest:
Failed to login`) ist erledigt; die Pipeline läuft seit dem Lizenz-Setup
durch.

### Bekannte offene Punkte

- Balancing ist gesetzt, aber nicht gemessen -- `PrestigeK`, die Kostenkurven
  und der Ruf-Verlust je verlorenem Gast wurden nach Testläufen nach Gefühl
  korrigiert (z. B. `LossPerLostGuest` von 3.0 auf 1.5 halbiert, weil der Ruf
  ohne Manager binnen zwei Minuten auf 0 fiel). Ereignis-Logging als Datenbasis
  fehlt (PLANv4 R2).
- Das Laufzeit-UI benutzt noch `UnityEngine.UI.Text` statt TextMeshPro, es gibt
  kein Object Pooling und keine Unity-Play-Mode-Tests (PLANv4 R4).
- iOS ist bewusst noch nicht angefasst (PLANv4 R5) -- siehe PLAN.md Abschnitt 4
  fuer den vorgesehenen Weg (TestFlight statt App Store, Codemagic/macOS-Runner
  statt eigener EC2-Mac-Instance, Bearer-Token-Auth statt Cookie-Login).
- Kamera-Pan (Ziehen/Wischen, siehe unten) ist per Code fertig, aber noch nicht
  auf einem echten Touch-Geraet bestaetigt -- synthetische Maus-Drags per
  `xdotool` unter Xvfb sind dafuer nicht zuverlaessig genug.
- Personal-Kochmuetze (siehe unten) ist prozedural erzeugt, aber noch nicht mit
  einem tatsaechlich gekauften Manager im Spiel visuell geprueft.

## Eatventure-Umbau (24.08.2026): Kueche unten, Tresen, Gastraum dahinter

Nutzer-Feedback wollte drei Dinge auf einmal: Kamera per Ziehen bewegbar,
Charaktergroesse an die Geraete gekoppelt statt fest, und ein Layout wie
Eatventure -- Kuechenstationen in einer Reihe unten (nah an der Kamera), EIN
durchgehender Tresen, Gaeste dahinter (weiter weg). Betraf `RestaurantLayout.cs`
(komplett neu), `CIBuild.cs` (Szenenaufbau neu) und `GameManager.cs`
(Charaktergroesse, Kamera-Pan, Personal-Silhouette).

**Warum das mehrere Anlaeufe brauchte:** Bei einer isometrischen Kamera
(Euler 55/45/0) laesst sich aus der Vektor-Algebra allein nicht ablesen, ob
eine Weltrichtung im Bild nach oben/unten oder vorne/hinten faellt -- das
muss man am Screenshot pruefen, jedes Mal neu. Drei falsche Vorzeichen-Annahmen
hintereinander, jede erst nach einem Screenshot sichtbar:

1. `RowRotation` war nur von `CounterRotation` umbenannt, nicht neu berechnet
   -- die neue Reihen-Achse steht 90 Grad zur alten, Modelle standen quer.
2. `DepthDirection` zeigte zunaechst so, dass Gaeste NAH an der Kamera und die
   Kuechenwand WEIT weg lag -- genau umgekehrt zum Ziel. Nach dem Umdrehen
   stand die (hohe) Wand dann NAH an der Kamera und verdeckte die (kuerzeren)
   Kuechengeraete dahinter komplett -- eine Wand direkt hinter der Kueche
   funktioniert bei diesem Kamerawinkel nicht. Endgueltige Loesung: Wand als
   reiner Hintergrund hinter dem GESAMTEN Gastraum (`WallFarDepthOffset`,
   nicht `WallDepthOffset`), nicht als Kulisse direkt hinter dem Personal.
3. Die Warteschlange war am letzten Stationsplatz (Index 6) verankert, nicht
   am ersten -- im Fruehspiel (nur Station 0 sichtbar) zog das die
   Kamera-Rahmung (`RecomputeCameraTarget`) trotzdem ueber die volle
   9-Einheiten-Reihenbreite. Jetzt an Station 0 verankert (immer als erste
   freigeschaltet).

**Geraete schwebten sichtbar ueber der Theke:** `CounterHeight` als
Geraete-Y-Position angenommen ging davon aus, dass die Theke ihren Pivot an
der Basis hat. Die Kenney-Module haben ihn aber an einer ECKE (siehe
`InstantiateModel`-Kommentar) -- die tatsaechliche Oberkante lag nicht exakt
bei `CounterHeight`. Fix: `MeasureScaledTopY()` misst die echte
Modell-Oberkante bei Zielgroesse, statt sie anzunehmen.

**Charaktergroesse:** `GuestSpriteScale` war ein freistehender fester Wert
(0.55), jetzt ein Anteil von `RestaurantLayout.CounterHeight`
(`GuestHeightRatio = 0.67`) -- Personen skalieren automatisch mit, falls die
Geraetegroesse sich je aendert.

**Kamera-Pan:** `HandleStationTap()` (nur Press-Erkennung) wurde zu
`HandlePointerInput()` (echter Press/Drag/Release-Automat). Bewegung ueber
`DragThresholdPixels` (18px) wird als Kamera-Ziehen gewertet und unterdrueckt
den Tap; `PanCamera()` rechnet Bildschirm-Pixel in Weltversatz um
(`unitsPerPixel = 2 * orthographicSize / Screen.height`, fuer eine
orthografische Kamera unabhaengig von Breite/Hoehe) und klemmt auf
`PanClampRow`/`PanClampDepth`, damit man nicht ins Leere ziehen kann. Der
Versatz addiert sich in `ApplyCameraFraming` auf das automatische
Framing-Ziel drauf -- die Auto-Kamera bleibt im Hintergrund aktiv.

**Personal-Silhouette:** Personal nutzte bisher exakt dasselbe Sprite wie
Gaeste, nur eingefaerbt (Kenney Toon Characters liefert nur die vier
Gast-Bilder im Projekt, kein zweites Set, und ein Nachladen aus dem Netz war
ohne genaue Quelle zu riskant). Stattdessen `GameAssets.ChefHatSprite`:
prozedural erzeugte Kochmuetze (zwei ueberlappende Ellipsen, gleiches
Alpha-Verlauf-Verfahren wie `BlobShadowSprite`), als Kind-GameObject ueber
dem Kopf -- echte Silhouetten-Unterscheidung ohne neuen Asset-Import.

## Arbeiten mit Claude Code an diesem Projekt

Regeln, die sich in mehreren Sessions bewaehrt haben:

- **Immer Rueckfragen stellen, ob alles richtig verstanden wurde, bevor
  groessere Aenderungen umgesetzt werden** -- und zwar als anklickbare Auswahl
  (Claude Codes `AskUserQuestion`-Tool), nicht als reiner Fliesstext, den man
  selbst beantworten muss.
- **Jede Aenderung zusaetzlich mit einem lokalen WebGL-Build validieren**,
  nicht nur im Editor-Play-Mode -- der Editor strippt nie Shader, ein echter
  Build kann sich sichtbar unterscheiden (siehe Pink-Bug oben).
- Bei jeder UI-/Grafik-Aenderung fuer **Smartphone-Aufloesung** pruefen (siehe
  `RestaurantIdle/Game-View auf iPhone 16 Pro Max (1290x2796)`), nicht nur
  die grobe 1080x1920-Naeherung -- ein iPhone 16 Pro Max ist mit 0.4614
  spuerbar schmaler als 0.5625.
- Kommunikationssprache in diesem Projekt ist Deutsch.
- Bei laengeren Hintergrund-Laeufen (Build, Editor-Boot) zwischendurch kurz
  Bescheid geben statt schweigend zu warten.

### Stolpersteine bei der Editor-Fernsteuerung per SSH/Xvfb (fuer naechste Runden)

- **CPU-Auslastung ist kein verlaessliches "Editor fertig geladen"-Signal.**
  Mit laufendem mcp-unity pollt der Editor dauerhaft mit 20--35 % CPU, auch im
  Leerlauf. Bereitschaft stattdessen per Screenshot pruefen, nicht per
  CPU-Schwelle.
- **`pkill -f "<muster>"` kann sich selbst treffen**, wenn das Suchmuster im
  eigenen Bash-Aufruf als Text vorkommt (`pkill -f "http.server"` matcht die
  eigene `bash -c 'pkill -f "http.server"'`-Zeile) -- die SSH-Sitzung bricht
  dann mit Exit 255 ab. Fuer Port-basiertes Beenden stattdessen
  `fuser -k <port>/tcp` nutzen.
- **Play Mode ueberlebt Skript-Neukompilierungen** (Domain-Reload uebernimmt
  neuen Code, aber nicht die Szene) -- nach groesseren Struktur-Aenderungen
  fuehrt das zu verwirrenden, veralteten Zwischenstaenden. Play Mode nach
  so einer Aenderung explizit stoppen und die Szene ueber
  `RestaurantIdle/Szene fuer Editor erzeugen` neu bauen, nicht nur neu
  kompilieren lassen.
- Lokale WebGL-Builds brauchen fuer sichtbare Gast-Sprites unter
  Software-Rendering (Swiftshader, wie es dieser Xvfb-Aufbau nutzt) deutlich
  laenger zum "Aufwaermen" als der native Editor (~90s statt ~25s) -- kein
  Bug, nur ein langsameres erstes Frame.

## Entwicklung

```bash
cd apps/api && npm install && npm run dev   # braucht DATABASE_URL, siehe .env.example
cd game && dotnet test
```

Unity-Projekt lokal öffnen: Unity Hub → "Add" → `client`-Ordner (Editor-Version
6000.0.82f1, siehe `client/ProjectSettings/ProjectVersion.txt`).

## Arbeitsumgebung: visuelle Prüfung im Editor

Entwickelt wird auf zwei EC2-Instanzen: `uiFlow` (Graviton, hostet
`cgo-app.de`, hier liegt das Repo und läuft der Deploy) und eine
x86-`unity-instance` mit echtem Unity-Editor. Der Editor ist der einzige Weg,
Änderungen an der Optik *zu sehen* -- der WebGL-Build in CI dauert 15--20
Minuten und eignet sich nicht als Rückkopplung.

Nach jedem Stop/Start der `unity-instance`:

1. Neue Public IP in `~/.ssh/config` unter `Host unity-instance` eintragen.
2. X-Server selbst starten (es ist niemand per RDP eingeloggt):
   `nohup Xvfb :11 -screen 0 1600x1000x24 &`, dann `DISPLAY=:11 nohup xfwm4 &`.
   Damit funktionieren `xwd`-Screenshots und `xdotool`-Klicks.
3. `client/Packages/manifest.json` prüfen -- ein `git pull` überschreibt den
   MCP-Unity-Eintrag. Er muss als Git-URL dastehen, nicht als `file:`-Pfad:
   `"com.gamelovers.mcp-unity": "https://github.com/CoderGamester/mcp-unity.git"`.
4. `DISPLAY=:11 ~/Unity/Hub/Editor/6000.0.82f1/Editor/Unity -projectPath ~/RestaurantIdle/client`
5. Menüpunkt `RestaurantIdle/Game-View auf Portrait (1080x1920)` ausführen --
   sonst prüft man das Handyspiel im Querformat und sieht die falschen Ränder.

Gesteuert wird der Editor über die MCP-Unity-WebSocket-Brücke (Port 8090) mit
`~/mcp_call.py` auf der `unity-instance`.

**Zwei Fallen, die schon Zeit gekostet haben:**

- `get_console_logs` mit `logType: "error"` meldete *keine* Fehler, obwohl die
  Kompilierung fehlgeschlagen war. Verlässlich ist stattdessen, ob
  `set_play_mode_status` `isPlaying: true` zurückgibt -- Unity geht bei
  Compile-Fehlern nicht in den Play-Modus.
- Bei 1080x1920 zeigt der Game-View auf 0,25 skaliert an; ein Zoom in diesen
  Screenshot zeigt Treppenstufen, die im echten Render nicht existieren. Für
  Kantenglättungs-Prüfungen den Menüpunkt
  `RestaurantIdle/Game-View auf Portrait klein (405x720)` benutzen (1:1).

Unity benutzt hier **C# 9** -- kein `record struct`, keine `with`-Ausdrücke.
