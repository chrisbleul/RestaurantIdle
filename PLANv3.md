# Restaurant Idle — Gap-Analyse & Plan v3

**Codestand:** `RestaurantIdle-code.zip`, 21.08.2026
**Referenzen:** Eatventure (Lessmore), Cat Snack Bar (Treeplla), Idle Restaurant Tycoon
**Umfang:** ~1.410 Zeilen Unity-Gameplay, ~500 Zeilen BalancingCore, Fastify/Postgres-Backend

---

## 1. Befund in einem Satz

Die Fundamente sind besser als bei den meisten Hobbyprojekten in diesem Genre — getestete Mathematik, funktionierendes Backend, CI, saubere Lizenzführung. Was fehlt, ist aber **keine Politur**: es sind drei strukturelle Konflikte, die verhindern, dass aus den vorhandenen Teilen ein Spiel wird. Alles Weitere ist danach Fleißarbeit.

---

## 2. Die drei strukturellen Konflikte

### K1 — Die Ökonomie ist AdVenture Capitalist, die Szene ist Eatventure

`Station.OwnedCount` plus `Milestones` bei 25/50/100/200 bedeutet wörtlich: *kaufe 200 Kaffeemaschinen*. Die Szene zeigt aber **eine** Kaffeemaschine auf einer Theke. Für `OwnedCount = 87` gibt es keinen visuellen Ausdruck und kann es keinen geben.

Eatventure kennt kein `OwnedCount`. Dort existiert pro Station **eine Instanz mit einem Level**, und es gibt zwei Upgrade-Achsen:

- **Preis** → mehr Umsatz pro Verkauf
- **Ausstattung** → schneller / höhere Kapazität

Das ist kein Detail, sondern der Grund, warum die Szene dort das UI sein *kann*.

**Empfehlung — umstellen:**

| Heute | Künftig |
|---|---|
| `OwnedCount` (0–200+) | `PriceLevel` + `EquipmentLevel` |
| Meilenstein bei Stückzahl | Meilenstein bei Level (alle 10/25/50) |
| Meilenstein = unsichtbarer ×2 | Meilenstein = **sichtbarer Modellwechsel** der Maschine |
| `YieldPerCycle × OwnedCount` | `YieldPerSale`, Durchsatz aus `EquipmentLevel` |

**Was das an BalancingCore kostet:** `CostCurve`, `OfflineEarnings`, `Prestige` bleiben unverändert. `Milestones` bleibt, bekommt nur andere Schwellen. Betroffen sind `StationCatalog` und `Station` — schätzungsweise 15 der 48 Tests müssen angepasst werden, der Rest bleibt gültig. Das ist die Auszahlung dafür, die Mathematik als eigenständiges Modul gebaut zu haben.

### K2 — Geld entsteht aus Timern, nicht aus Gästen

Heute läuft das so:

```
Station.Tick()  →  Geld
GuestFlow.CapacityFactor()  →  skaliert das Geld nachträglich runter
SpawnGuest()  →  Kapsel läuft von A nach B, berührt nichts
```

Der Gast in der Szene ist ein **Diagramm neben der Simulation**, nicht ihr Teil. Marketing kaufen erhöht eine Zahl, und als Nebeneffekt spawnen Kapseln häufiger — aber kein einziger Euro im Spiel stammt von einem bedienten Gast.

Bei Eatventure ist der Gast die Transaktion: betreten → anstellen → bestellen → Personal kocht an der Station → servieren → Geld. Deshalb ist dort jedes Upgrade sofort *sichtbar*: die Schlange wird kürzer, der Koch schneller, mehr Leute passen rein.

**Empfehlung — echte Auftragskette:**

```
Gast:    Enter → Queue → Order → Wait → Served → Leave
Station: Auftragswarteschlange, Kapazität, Zykluszeit
Geld:    entsteht ausschließlich bei "Served"
```

`GuestFlow.CapacityFactor` entfällt ersatzlos. Die Begrenzung wird **emergent**: zu wenig Gäste → Stationen laufen leer; zu wenig Kapazität → Schlange wächst, Gäste gehen unbedient. Beides ist auf einen Blick lesbar, ohne dass irgendwo „Auslastung 62 %" stehen muss.

Das ist der teuerste Umbau in diesem Plan — und der, der am meisten bringt. Ohne ihn bleibt es ein Zahlenspiel mit Kulisse.

### K3 — Prestige hat keine Wirkung

`prestigeStars` wird berechnet, angezeigt, gespeichert — und **nirgends als Multiplikator verwendet**. Ein Grep über `Assets/Scripts` zeigt keinen einzigen Treffer in `YieldPerCycle`, `YieldPerSecond` oder irgendeiner Ertragsberechnung.

Konkret heißt das: „Renovieren" löscht den gesamten Fortschritt und gibt dafür eine Zahl zurück, die nichts tut. Der Reset ist aktuell ein reiner Verlust.

**Fix:** globaler Multiplikator in die Ertragskette, z. B. `1 + stars × f`. Wenige Zeilen — aber solange er fehlt, ist Phase 6 faktisch nicht fertig, unabhängig davon, was der Statusbericht sagt.

---

## 3. Konkrete Code-Befunde

| Schwere | Datei | Befund |
|---|---|---|
| 🔴 | `GameManager` | Prestige-Sterne ohne jede Wirkung (K3) |
| 🔴 | `GameManager.HandleStationTap` | `EventSystem.current.IsPointerOverGameObject()` **ohne `fingerId`** — auf Touch-Geräten liefert das den Maus-Status. Auf iOS schlagen UI-Taps zusätzlich als 3D-Raycast durch. Fix: `IsPointerOverGameObject(Input.GetTouch(0).fingerId)` |
| 🔴 | `GameState` | Keine Schema-Version. Jede spätere Änderung an `Station` bricht bestehende Saves stillschweigend — und der Umbau aus K1 ist genau so eine Änderung |
| 🔴 | `client/Packages/manifest.json` | Enthält `modules.physics2d`, aber **nicht `modules.physics`** (3D) — `Physics.Raycast` und die Collider aus `CreatePrimitive` hängen daran. Prüfen, ob Unity das implizit auflöst |
| 🟡 | `ProjectVersion.txt` | Sagt `6000.0.32f1`; laut Statusbericht läuft die Dev-Instanz auf `6000.0.82f1`, weil `.32` deterministisch crasht. Repo und Instanz driften auseinander |
| 🟡 | `GameManager.Update` | `RefreshUi()` bei jedem einkommenden Frame → ~10 interpolierte Strings pro Frame. GC-Druck auf Mobile/WebGL. Auf ~10 Hz drosseln |
| 🟡 | `SpawnGuest`, `CoinBurst`, `SpawnStaffWorker` | `Shader.Find()` + `new Material()` **pro Instanz**. `Shader.Find` ist teuer, jede Instanz bekommt ein eigenes Material. Pooling + geteilte Materialien |
| 🟡 | `PlaySfx` | `AudioSource.PlayClipAtPoint` erzeugt pro Sound ein GameObject. Bei schnellem Tappen spürbar. Außerdem: **kein Mute, keine Lautstärke** |
| 🟡 | `GameManager.CreateLabel` | Legacy `UnityEngine.UI.Text` statt TextMeshPro — obwohl TMP im Manifest liegt. Fredoka wird bei Skalierung matschig |
| 🟡 | `GuestWalker.cs` | **Toter Code.** Die animierten Kenney-Sprites werden nie instanziiert; `SpawnGuest()` erzeugt Kapsel-Primitive |
| 🟡 | `ApplyOfflineEarnings` | Offline-Ertrag nur als `Debug.Log`. Der wichtigste Wiederkehr-Moment des Genres ist für den Spieler unsichtbar |
| 🔵 | `apps/api/routes/save.ts` | `state` ist `z.record(z.string(), z.unknown())`, Umsatz wird clientseitig geführt. Der Server ist autoritativ über *Zeit*, nicht über *Geld* |
| 🔵 | `StationCatalog` | Manager-Kosten sind eine Konstante pro Station, keine Kurve |
| 🔵 | `BuildUi` | Alle sieben Stationen ab Sekunde 0 in der Liste — kein Gating, kein sichtbares nächstes Ziel |
| 🔵 | Projekt | Nur `Main.unity`. Kein Menü, keine Einstellungen, kein Pause-Zustand |

---

## 4. Was gameplay-seitig fehlt

### Erste Sitzung (0–15 Minuten)

Die kritischste Phase, und aktuell die schwächste.

- Kein Tutorial, kein geführter erster Kauf
- Kein sichtbares nächstes Ziel — der Spieler sieht sieben Listeneinträge und weiß nicht, welcher zuerst
- Alle Stationen sofort sichtbar → keine Entdeckung, keine Belohnung fürs Weiterspielen
- Der Zeitpunkt der ersten Renovierung ist nicht terminiert (Zielwert: 20–40 Minuten)

**Vergleich:** Eatventure startet mit *einem* Produkt am Limonadenstand. Die zweite Station ist ein Ereignis. Das ist kein Content-Vorteil, sondern eine Reihenfolgenentscheidung.

### Wiederkehr (Tag 2+)

- **Kein Offline-Dialog.** In jedem Genre-Vertreter der zentrale Moment beim Öffnen: „Während du weg warst: X" mit Einsammeln-Button
- Keine Tagesbelohnung
- Keine Ziele/Quests
- Zwischen zwei Sitzungen passiert nichts außer, dass eine Zahl größer wird

### Langfrist

- Prestige ohne Wirkung (K3)
- Locations sind reine Farbwechsel — `LocationTheme` färbt Boden und Wand um, mehr nicht
- Keine Sammel- oder Meta-Ebene

**Vergleich:** Eatventure hat Items mit dauerhafter, globaler Wirkung über alle Locations hinweg — die eigentliche Langzeitmotivation neben dem Umzug. Das ist die einzige Meta-Mechanik der Referenz, die den Aufwand für ein Soloprojekt wert ist.

### Boosts

Ein kurzfristiger ×2-Multiplikator ist in jedem Vertreter des Genres vorhanden. Kommerziell ist er an Werbung gekoppelt; ohne Monetarisierung wird er zur Belohnung für erreichte Ziele oder Tap-Serien. Er kostet wenig und gibt der aktiven Spielschicht einen Sinn.

---

## 5. Design & Art

### Was heute im Bild ist

Kapsel-Primitive als Gäste und Personal, sieben Küchengeräte in einer Reihe auf einer Ebene, Farbflächen als Wände, graue Rechteck-Buttons in Legacy-UI.

### Was fehlt — nach Wirkung sortiert

1. **Charaktere statt Kapseln.** Kenney Toon Characters liegen bereits im Projekt, `GuestWalker` ist bereits geschrieben — es fehlt nur die Verdrahtung. Höchste Wirkung pro Aufwand im gesamten Plan.
2. **Stationszustand sichtbar machen.** Kein Fortschrittsring, kein Fertig-Signal, kein Level-Badge. Der Spieler kann einer Station nicht ansehen, ob sie arbeitet. Das ist der zweite Grund, warum die Szene aktuell wie Deko wirkt.
3. **UI-Skin.** Das Kenney UI Pack liegt nicht im Projekt. Buttons sind graue Rechtecke mit Legacy-Text.
4. **Raumlayout statt Reihe.** Eingang, Warteschlange, Theke, Sitzbereich, Küche. Die Kamera soll Ordnung zeigen, nicht Aufstellung — und das Layout muss die Auftragskette aus K2 abbilden.
5. **Renovierungs-Übergang.** Der visuell wichtigste Moment im Spiel läuft aktuell als stiller Farbwechsel ab.
6. **Partikel über den Münz-Burst hinaus** — Dampf an aktiven Stationen, Meilenstein-Effekt.

---

## 6. Phasenplan

### Phase A — Reparatur *(1 Session)*
- [ ] Prestige-Multiplikator in die Ertragskette (K3)
- [ ] `fingerId` beim `IsPointerOverGameObject`-Check
- [ ] `SchemaVersion` in `GameState` + Migrationspfad
- [ ] Physics-Modul im Manifest prüfen, `ProjectVersion.txt` angleichen
- [ ] `RefreshUi()` auf ~10 Hz drosseln

> Vor allem anderen. Die Schema-Version muss stehen, **bevor** Phase B das Save-Format ändert.

### Phase B — Ökonomie-Umbau *(2–3 Sessions)*
- [ ] `Station`: `OwnedCount` → `PriceLevel` + `EquipmentLevel`
- [ ] `StationCatalog` auf zwei Upgrade-Achsen umstellen
- [ ] `Milestones` auf Level-Schwellen
- [ ] Tests anpassen, Save-Migration schreiben

### Phase C — Auftragskette *(3–4 Sessions)*
- [ ] Gast-Zustandsmaschine: Enter → Queue → Order → Wait → Served → Leave
- [ ] Auftragswarteschlange pro Station
- [ ] Geld entsteht ausschließlich beim Servieren
- [ ] `GuestFlow.CapacityFactor` entfernen
- [ ] Raumlayout: Eingang, Schlange, Theke, Küche
- [ ] Gäste/Personal als Kenney-Charaktere (`GuestWalker` anschließen), Pooling statt `CreatePrimitive`

### Phase D — Fortschritt & Gating *(2 Sessions)*
- [ ] Stationen gestaffelt freischalten statt alle sofort
- [ ] Sichtbares nächstes Ziel im UI
- [ ] Fortschrittsringe und Level-Badges an den Stationen
- [ ] Geführter erster Kauf

### Phase E — Art-Pass *(2–3 Sessions)*
- [ ] Kenney UI Pack einbinden, Buttons und Panels skinnen
- [ ] Legacy `Text` → TextMeshPro
- [ ] Location-Prefabs statt reiner Farbwechsel
- [ ] Renovierungs-Übergang mit Kamerafahrt
- [ ] Dampf an aktiven Stationen, Meilenstein-Effekt

### Phase F — Retention *(2 Sessions)*
- [ ] Offline-Dialog beim Start mit Einsammeln-Button
- [ ] Ziele/Quests mit ×2-Boost als Belohnung
- [ ] Einstellungen: Ton, Mute, Save zurücksetzen
- [ ] Optional: Items mit globaler Dauerwirkung

### Phase G — iOS *(1–2 Sessions)*
- [ ] Apple Developer Program
- [ ] CI-Runner mit iOS-Target neben `BuildWebGl`
- [ ] TestFlight-Build
- [ ] Touch-Eingabe auf echtem Gerät prüfen

### Phase H — Balancing *(laufend)*
- [ ] `PrestigeK` kalibrieren — erste Renovierung nach 20–40 Minuten
- [ ] Kostenkurven über alle fünf Locations
- [ ] Ereignis-Logging für datenbasiertes Tuning

---

## 7. Was bewusst nicht gebaut wird

| Element | Grund |
|---|---|
| Werbung, IAP, Zweitwährung | Kein App Store, keine Monetarisierung |
| Wöchentliche Events (LTE) | Braucht Live-Ops und Spielerbasis |
| Clubs, Ranglisten, Social | Braucht Spielerbasis |
| Gacha-/Lootbox-Systeme | Ohne Monetarisierung ohne Funktion |
| Serverautoritative Ökonomie | Bei einem privaten Spiel unverhältnismäßig — der Server bleibt autoritativ über Zeit |

---

## 8. Ehrliche Einordnung

Eatventure ist ein Studioprodukt mit mehreren Jahren Live-Ops und zweistelligen Millionen-Downloads. Cat Snack Bar ist der erfolgreichste Nachbau davon und stammt von einem Team, dessen Mitglieder aus einem etablierten Studio kamen. Feature-Parität ist kein sinnvolles Ziel.

Was erreichbar ist: **ein Spiel, das im Genre nicht als Prototyp auffällt.** Konkret heißt das:

- eine erste Sitzung, die 20–30 Minuten trägt
- ein Grund, am nächsten Tag zurückzukommen
- fünf Locations, die sich sichtbar unterscheiden
- eine Szene, an der man den eigenen Fortschritt abliest

Die Analysen der Referenztitel kommen übereinstimmend zu dem Schluss, dass es kein einzelnes Feature gibt, das Bindung erzeugt — die Wirkung entsteht aus der Summe sauber umgesetzter Standards. Das ist eine gute Nachricht für ein Soloprojekt: nichts auf dieser Liste ist besonders schwer, es ist nur viel.

**Der kritische Pfad sind K1 und K2.** Phase E ohne Phase C ergibt ein hübsches Zahlenspiel mit Kulisse. Phase C ohne Phase E ergibt ein hässliches Spiel, das funktioniert — und das ist die deutlich bessere Ausgangslage.
