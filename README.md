# Restaurant Idle Game

Projektplan: siehe `PLAN.md`. Stand: Phase 1 (Balancing) und ein Teil von
Phase 5 (Backend-Grundgerüst) sind umgesetzt; alles andere folgt in der in
`PLAN.md` Abschnitt 7 festgelegten Reihenfolge.

## Struktur

- `game/BalancingCore` -- reines C#-Modul (kein Unity) für Kostenkurven,
  Meilensteine, Prestige und Offline-Ertrag, auf `BigDouble`
  (vendored von [Razenpok/BreakInfinity.cs](https://github.com/Razenpok/BreakInfinity.cs)).
  Unit-Tests in `game/BalancingCore.Tests` (`dotnet test`).
- `apps/api` -- Fastify-Backend (Node/TS): Save-Endpunkte, serverautoritativer
  Offline-Progress, Auth über `@cgo/platform-auth`.
- `infra` -- `docker-compose.yml` + `.env.example` für den Deploy auf der EC2,
  analog zu den anderen Apps auf `cgo-app.de`.

## Offene Schritte vor dem produktiven Deploy

Diese drei Schritte greifen in geteilte Infrastruktur ein und sind bewusst
noch nicht ausgeführt:

1. **Datenbank anlegen**: Rolle + DB `restaurant_idle` im laufenden
   `platform-db`-Container (das Init-Skript läuft nur beim allerersten
   Start mit leerem Datenverzeichnis, siehe
   `cgo-platform/apps/platform-db/infra/init/01-create-databases.sh` -- neu
   hinzukommen muss händisch per `CREATE ROLE`/`CREATE DATABASE`).
2. **In `cgo-platform` registrieren**: `./bin/app-add.sh restaurant 8098 "Restaurant Idle" chrisbleul/RestaurantIdle`
   erzeugt den Registry-Eintrag und das nginx-Snippet, dann
   `./bin/platform-sync.sh` zum Ausrollen.
3. **GitHub-Repo anlegen und pushen** -- lokal bislang nur `git init`.

## Entwicklung

```bash
cd apps/api && npm install && npm run dev   # braucht DATABASE_URL, siehe .env.example
cd game && dotnet test
```
