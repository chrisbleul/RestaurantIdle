# Restaurant Idle Game

Projektplan: siehe `PLAN.md`. Stand: Phase 1 (Balancing), Phase 5
(Backend-Grundgerüst, live auf `cgo-app.de`) und Phase 2/3 im Client (alle
sieben Stationen, Manager, Marketing/Gästestrom, lokaler Save) sind
umgesetzt. WebGL-CI ist eingerichtet, aber noch nicht grün -- siehe
"Bekannte offene Punkte" unten.

## Struktur

- `game/BalancingCore` -- reines C#-Modul (kein Unity) für Kostenkurven,
  Meilensteine, Prestige und Offline-Ertrag, auf `BigDouble`
  (vendored von [Razenpok/BreakInfinity.cs](https://github.com/Razenpok/BreakInfinity.cs)).
  Unit-Tests in `game/BalancingCore.Tests` (`dotnet test`).
- `client` -- Unity-Projekt (2D URP). `Assets/Scripts/BalancingCore` ist eine
  1:1-Kopie von `game/BalancingCore` (bewusst dupliziert statt verlinkt --
  Unity kompiliert alles unter `Assets/` selbst, ein Projektverweis über
  Ordnergrenzen hinweg wäre nur Fragilität) -- inklusive der neuen
  `StationCatalog.cs` (alle sieben Stationen aus PLAN.md Abschnitt 1) und
  `GuestFlow.cs` (Marketing/Gästestrom-Deckel), beide mit Unit-Tests in
  `game/BalancingCore.Tests`. `Assets/Scripts/Game`: `Station.cs` (Zustand
  einer Instanz, bekommt ihre `StationDefinition` vom Aufrufer statt sie
  selbst zu halten), `GameManager.cs` (Tick-Loop über alle Stationen,
  Manager-Gating -- ohne Manager produziert eine Station nicht automatisch,
  nur per Klick, siehe PLAN.md Abschnitt 1 --, Gästestrom-Deckel, baut sein
  UI weiterhin zur Laufzeit selbst -- kein Art-Pass vor Phase 4),
  `SaveSystem.cs` (lokaler JSON-Save). `Assets/Editor/CIBuild.cs` baut Szene
  und WebGL-Player programmatisch für CI (robuster als eine handgepflegte
  `.unity`-Datei).
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

### Bekannte offene Punkte

- **WebGL-CI schlägt aktuell im `webgl-build`-Job ab** (Stand 2026-08-20):
  `UnityConnectLoginRequest: Failed to login ... HTTP error code 401`, obwohl
  `UNITY_LICENSE` (echtes, gültiges `.ulf`, per Hub-GUI-Login aktiviert),
  `UNITY_EMAIL` und `UNITY_PASSWORD` (frisch bei id.unity.com gesetzt, kein
  2FA) alle gesetzt sind. Noch nicht diagnostiziert: ob dasselbe
  Zugangsdaten-Paar über Unity Hubs eigenen GUI-Login (nicht die alte
  `core.cloud.unity3d.com/api/login`-API, die game-ci intern nutzt)
  funktioniert -- das würde zeigen, ob es ein Account-Problem oder ein
  API-spezifisches ist. Nächster Schritt bei Gelegenheit: diesen Vergleich
  in einem frischen Codespace nachholen.
- `client/` wurde nie in einem echten Unity-Editor geöffnet (nur als Dateien
  geschrieben, ohne visuelle Prüfung) -- funktionierender Code nach bestem
  Wissen, aber ungetestet im Editor. `game/BalancingCore` (die eigentliche
  Spiellogik dahinter) ist vollständig unit-getestet (`dotnet test`).

## Entwicklung

```bash
cd apps/api && npm install && npm run dev   # braucht DATABASE_URL, siehe .env.example
cd game && dotnet test
```

Unity-Projekt lokal öffnen: Unity Hub → "Add" → `client`-Ordner (Editor-Version
6000.0.32f1, siehe `client/ProjectSettings/ProjectVersion.txt`).
