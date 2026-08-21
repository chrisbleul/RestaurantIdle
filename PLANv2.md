# Restaurant Idle Game — Plan v2

**Referenz:** Eatventure (Lessmore UG)
**Ausgangslage:** Phasen 1–3, 5–6 fertig, Phase 4 offen
**Stand:** 21.08.2026

---

## 1. Was Eatventure strukturell anders macht

Fünf Dinge, die den Unterschied zu einem Zahlen-Idle ausmachen:

### 1.1 Location-Progression statt abstraktem Prestige

Der Einstieg ist ein Limonadenstand. Ist alles voll ausgebaut, wird das Geschäft **renoviert** — Food Truck, Café, Diner, Drive-Thru. Der Reset ist damit kein Menüpunkt, sondern eine sichtbare Belohnung: neuer Ort, neue Optik, neue Gerichte.

> **Das ist der wichtigste Unterschied zu deinem aktuellen Stand.** Michelin-Sterne sind mathematisch identisch, aber emotional leer. Dieselbe `√(Lifetime)`-Formel als *Umzug* verpackt trägt das ganze Spiel.

### 1.2 Die Szene *ist* das UI

Stationen sind physische Objekte im Raum. Gäste betreten das Lokal, stellen sich an, Personal läuft zwischen Küche und Theke. Der Spieler liest seinen Fortschritt an der Szene ab, nicht an einer Liste. Ein Marketing-Upgrade ist erst dann befriedigend, wenn danach *sichtbar* mehr Leute reinkommen.

### 1.3 Zwei Eingabe-Ebenen

Idle-Ertrag läuft immer. Darüber liegt eine aktive Schicht: antippen, um einzusammeln oder zu beschleunigen. Wer zuschaut, wird belohnt — wer weglegt, verliert nichts.

### 1.4 Gestaffelte Freischaltung

Neue Stationstypen tröpfeln einzeln herein, jede optisch klar unterscheidbar. Es gibt immer ein sichtbares nächstes Ziel, das nah genug ist.

### 1.5 Meta-Layer

Items/Perks mit globaler, dauerhafter Wirkung, dazu Gems und wöchentlich rotierende Events.

---

## 2. Was übernommen wird — und was nicht

| Element | Entscheidung | Begründung |
|---|---|---|
| Location-Progression | ✅ Kern | Größter Effekt, geringster Aufwand — Mathematik existiert |
| Isometrische Szene | ✅ Kern | Ohne das bleibt es ein Zahlen-Idle |
| Gäste-/Personal-Simulation | ✅ Kern | Macht Upgrades lesbar |
| Tap-Layer | ✅ Kern | Wenige Zeilen, hoher Gamefeel-Gewinn |
| Gestaffelte Freischaltung | ✅ Kern | Reines Balancing/Content-Gating |
| Items/Perks | 🟡 Später | Sinnvoll, aber erst nach Phase 10 |
| Gems / Zweitwährung | ⬜ Nein | Existiert nur wegen Monetarisierung |
| LTEs, Clubs, Vault | ⬜ Nein | Braucht Live-Ops und Spielerbasis |
| Werbung / IAP | ⬜ Nein | Kein App Store geplant |

**Realistisches Ziel:** eine Location-Kette mit 5 Stufen, 8 Stationstypen, sauber poliert. Eatventure ist ein Studio-Produkt mit Jahren an Iteration — Feature-Parität ist nicht die Messlatte.

---

## 3. Was am bestehenden Code bleibt

| Baustein | Status |
|---|---|
| `BalancingCore` + 48 Tests | **Unverändert.** Kostenkurven, Meilensteine, Offline-Ertrag bleiben identisch |
| BreakInfinity.cs | Unverändert |
| Backend, Postgres, `cgo-auth` | Unverändert |
| Serverseitiger Offline-Progress | Unverändert |
| Prestige-Formel | **Umbenannt, nicht umgerechnet** — Michelin-Sterne → Renovierung |
| Stationen-UI | **Wird ersetzt** durch Szenen-Objekte |

Der Umbau betrifft ausschließlich die Präsentationsschicht. Das war die Belohnung dafür, die Mathematik in Phase 1 als eigenständiges Modul zu bauen.

---

## 4. Look: Low-Poly 3D unter Ortho-Kamera

Eatventure ist kein 2D-Spiel. Der Look entsteht aus:

- **Low-Poly-3D-Modelle** mit flachen Farbflächen, ohne Texturen
- **Orthografische Kamera** in isometrischem Winkel (ca. 30°/45°)
- **Weiches Licht** — ein Directional Light plus Ambient, weiche Schatten
- **Kräftige, gesättigte Palette** mit klarer Trennung Innen/Außen

Das ist mit CC0-Assets erreichbar, weil Low-Poly-Kits im Gegensatz zu handgemalten 2D-Sprites frei kombinierbar sind, solange die Polygondichte zusammenpasst.

### Renderer

URP beibehalten, aber auf 3D umstellen. Für ein Idle-Spiel mit statischer Kamera ist das performancetechnisch unkritisch — auch im WebGL-Build.

---

## 5. Asset-Plan (alles kostenlos)

### Primärquelle: Kenney (CC0, keine Namensnennung nötig)

| Pack | Verwendung |
|---|---|
| Food Kit | Gerichte, Zutaten, Theken-Deko |
| Furniture Kit | Tische, Stühle, Regale |
| Modular Buildings / City Kit | Gebäudehülle je Location |
| Toon Characters | Gäste und Personal (bereits im Einsatz) |
| Particle Pack | Dampf, Münz-Bursts, Meilenstein-Effekte |
| UI Pack | Buttons, Panels, Rahmen |
| Interface Sounds | Klicks, Kauf-Bestätigungen |
| Music Jingles | Meilenstein- und Renovierungs-Fanfaren |

### Sekundär

- **Quaternius** (CC0) — modulare Charaktere und Props, stilistisch kompatibel
- **Poly Pizza** — Aggregator; Lizenz pro Modell prüfen
- **itch.io / OpenGameArt** — größere Auswahl, wechselnde Lizenzen. CC-BY braucht einen Credits-Screen
- **Google Fonts** — Baloo 2, Fredoka oder Nunito

### Regel

**Ein Kernpack pro Kategorie.** Der klassische Fehler bei kostenlosen Assets ist das Mischen aus fünf Quellen — das Ergebnis sieht immer zusammengeklaubt aus, egal wie gut die Einzelteile sind. Kenney-Packs teilen Proportionen, Palette und Formensprache; das ist der eigentliche Wert.

### Lizenz-Hygiene

`ASSETS.md` im Repo: Pack, Quelle, Lizenz, Datum. Kostet fünf Minuten und erspart später Rekonstruktionsarbeit.

---

## 6. Location-Design

Fünf Stufen, je 3–4 freischaltbare Stationen:

| # | Location | Stationen |
|---|---|---|
| 1 | Limonadenstand | Limonade, Kaffee, Kekse |
| 2 | Food Truck | + Fritteuse, Hotdog |
| 3 | Café | + Backwaren, Espresso |
| 4 | Diner | + Grill, Milchshake |
| 5 | Restaurant | + Pizzaofen, Dessert |

Beim Umzug: Lifetime-Umsatz → Sterne (bestehende Formel), Stationen zurückgesetzt, Sterne wirken als globaler Multiplikator. Jede Location bekommt eine eigene Gebäudehülle und Palette — das ist die eigentliche Belohnung.

**Wichtig:** Spätere Locations dürfen sich nicht nur durch größere Zahlen unterscheiden. Mindestens eine neue Mechanik pro Stufe (Warteschlange, zweiter Tresen, Lieferfenster).

---

## 7. Phasen

### Phase 7 — Struktur-Pivot *(Design, kein Code)*
- [ ] Location-Kette festlegen, Stationen zuordnen
- [ ] Prestige-Wording und -UI auf Renovierung umstellen
- [ ] Freischalt-Reihenfolge und Zeitpunkte definieren

### Phase 8 — Isometrische Szene
- [ ] URP auf 3D, orthografische Kamera, Licht-Setup
- [ ] Location 1 aus Kenney-Kits bauen
- [ ] Stationen als Szenen-Objekte statt Listenzeilen
- [ ] Bestehende Balancing-Anbindung auf neue Objekte umhängen

### Phase 9 — Simulation
- [ ] Gast-Spawner, Rate **direkt aus dem Gästestrom-Wert**
- [ ] Wegfindung Eingang → Warteschlange → Theke → Ausgang
- [ ] Personal an Stationen, Animation an Zykluszeit gekoppelt
- [ ] Tap-Layer: Antippen sammelt ein / beschleunigt

> Gast-Spawn-Rate an den echten Wert koppeln, nicht als Deko animieren. Nur so wird die Szene zum Fortschrittsanzeiger.

### Phase 10 — Location-Progression
- [ ] Locations 2–5 als Prefabs
- [ ] Renovierungs-Sequenz mit Übergang
- [ ] Balancing über alle fünf Stufen

### Phase 11 — Polish
- [ ] Audio: Klick, Kauf, Münze, Jingle, Ambience-Loop
- [ ] Partikel: Dampf, Münz-Bursts, Renovierungs-Effekt
- [ ] Zahlenformat: Schwelle für wissenschaftliche Notation festlegen
- [ ] Schrift und UI-Abstände vereinheitlichen

### Phase 12 — Meta *(optional)*
- [ ] Perks mit globaler, dauerhafter Wirkung
- [ ] Sammelfortschritt über Locations hinweg

---

## 8. Reihenfolge-Hinweise

**Audio vor Partikeln.** Ein Kauf-Click, ein Münz-Sound und ein Meilenstein-Jingle sind etwa zwei Stunden Arbeit und verändern die Wahrnehmung stärker als jeder Grafikeffekt. Idle-Games leben von der Bestätigungsschleife — die ist aktuell stumm.

**Location 1 fertig vor Location 2.** Ein ordentlich beleuchteter Limonadenstand mit drei Stationen wirkt besser als fünf halbfertige Läden.

**Nach jeder Phase zehn Minuten am Stück spielen.** Der `k`-Wert für Prestige lässt sich nur so kalibrieren, und die Renovierung muss sich nach 20–40 Minuten das erste Mal lohnen.

---

## 9. Risiken

| Risiko | Gegenmaßnahme |
|---|---|
| Asset-Mix wirkt zusammengewürfelt | Ein Kernpack pro Kategorie, Kenney als Basis |
| 3D-Umbau blockiert wochenlang | Phase 8 auf Location 1 begrenzen |
| Simulation als reine Deko | Spawn-Rate an echten Gästestrom koppeln |
| Späte Locations nur größere Zahlen | Pro Stufe mindestens eine neue Mechanik |
| Feature-Vergleich mit Eatventure | Zielbild sind 5 Locations, nicht Parität |
| Unity-Dev-Instanz läuft weiter | Auto-Stop-Timer nach jeder Sitzung reaktivieren |
