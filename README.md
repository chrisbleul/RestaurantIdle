# Restaurant Idle Game

Projektplan: siehe `PLAN.md`. Stand: Phase 1 (Balancing), Phase 5
(Backend-Grundgerüst, live auf `cgo-app.de`) und ein erster Phase-2-Prototyp
(eine Station, Tick-Loop, lokaler Save) sind umgesetzt.

## Struktur

- `game/BalancingCore` -- reines C#-Modul (kein Unity) für Kostenkurven,
  Meilensteine, Prestige und Offline-Ertrag, auf `BigDouble`
  (vendored von [Razenpok/BreakInfinity.cs](https://github.com/Razenpok/BreakInfinity.cs)).
  Unit-Tests in `game/BalancingCore.Tests` (`dotnet test`).
- `client` -- Unity-Projekt (2D URP). `Assets/Scripts/BalancingCore` ist eine
  1:1-Kopie von `game/BalancingCore` (bewusst dupliziert statt verlinkt --
  Unity kompiliert alles unter `Assets/` selbst, ein Projektverweis über
  Ordnergrenzen hinweg wäre nur Fragilität). `Assets/Scripts/Game` ist der
  Phase-2-Prototyp: `Station.cs`/`StationDefinition.cs` (eine Produktions-
  Station, Rezept-Upgrade), `GameManager.cs` (Tick-Loop, baut sein UI zur
  Laufzeit selbst -- kein Art-Pass vor Phase 4), `SaveSystem.cs` (lokaler
  JSON-Save). `Assets/Editor/CIBuild.cs` baut Szene und WebGL-Player
  programmatisch für CI (robuster als eine handgepflegte `.unity`-Datei).
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
ein lokaler Schritt ist jetzt unvermeidbar. Einmalig nötig, auf einem
beliebigen Windows/Mac/Linux-Rechner mit Unity Hub:

1. [Unity Hub](https://unity.com/download) installieren, mit einem
   (kostenlosen) Unity-Account einloggen.
2. Preferences → Licenses → "Add" → "Get a free personal license".
3. Die dabei erzeugte Lizenzdatei öffnen:
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
4. Drei Secrets im Repo hinterlegen (Settings → Secrets and variables →
   Actions → "New repository secret"): `UNITY_LICENSE` (kompletter Inhalt
   der `.ulf`-Datei), `UNITY_EMAIL`, `UNITY_PASSWORD` (dieselben
   Zugangsdaten wie der Unity-Account).

Danach baut `ci.yml` bei jedem Push auf `main` automatisch WebGL, deployt es
auf `cgo-app.de/restaurant/` -- kein weiterer manueller Schritt. Alles
andere (Repo, Deploy-Key, Backend, Registrierung) ist bereits eingerichtet.

## Entwicklung

```bash
cd apps/api && npm install && npm run dev   # braucht DATABASE_URL, siehe .env.example
cd game && dotnet test
```

Unity-Projekt lokal öffnen: Unity Hub → "Add" → `client`-Ordner (Editor-Version
6000.0.32f1, siehe `client/ProjectSettings/ProjectVersion.txt`).
