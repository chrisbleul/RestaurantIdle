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

Unity Editor läuft nicht auf ARM/aarch64 (die EC2 ist Graviton) und braucht
für CI eine Lizenzaktivierung, die zwingend an einen Unity-Account hängt --
das kann kein Agent für dich erledigen. Einmalig nötig:

1. Falls noch nicht vorhanden: kostenlosen Account auf [unity.com](https://unity.com) anlegen.
2. `.github/workflows/unity-activation.yml` manuell auslösen (Actions-Tab →
   "unity-activation" → "Run workflow").
3. Die erzeugte `.alf`-Datei aus dem Artifact herunterladen, auf
   [license.unity3d.com/manual](https://license.unity3d.com/manual) hochladen
   (Personal-Lizenz) → man bekommt eine `.ulf`-Datei zurück.
4. Drei Secrets im Repo hinterlegen (Settings → Secrets and variables →
   Actions): `UNITY_LICENSE` (Inhalt der `.ulf`-Datei), `UNITY_EMAIL`,
   `UNITY_PASSWORD` (dieselben Zugangsdaten wie der Unity-Account).

Danach baut `ci.yml` bei jedem Push auf `main` automatisch WebGL, deployt es
auf `cgo-app.de/restaurant/` -- kein weiterer manueller Schritt.

## Entwicklung

```bash
cd apps/api && npm install && npm run dev   # braucht DATABASE_URL, siehe .env.example
cd game && dotnet test
```

Unity-Projekt lokal öffnen: Unity Hub → "Add" → `client`-Ordner (Editor-Version
6000.0.32f1, siehe `client/ProjectSettings/ProjectVersion.txt`).
