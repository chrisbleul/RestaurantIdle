# Restaurant Idle — Plan v4 (Abschluss von v3)

> Nachfolger von [PLANv3.md](PLANv3.md). v3 war eine Gap-Analyse mit drei
> strukturellen Konflikten (K1–K3) und acht Phasen. Dieses Dokument zieht die
> Bilanz und ist ab hier die einzige gültige Quelle für den Stand. v3 bleibt
> als Begründung der Analyse erhalten, seine Checklisten sind überholt.

---

## 1. Stand in einem Satz

Die drei strukturellen Konflikte aus v3 sind behoben, die Simulation trägt
sich selbst, und das Spiel sieht im Zielformat nicht mehr nach Prototyp aus —
offen sind Retention-Inhalte, der iOS-Weg und echtes Balancing.

---

## 2. Die drei Konflikte aus v3 — erledigt

| | Befund v3 | Auflösung |
|---|---|---|
| **K1** | Ökonomie war Stückzahl-basiert (`OwnedCount`), die Szene zeigt aber eine Instanz pro Station | Zwei Upgrade-Achsen: `PriceLevel` (Ertrag/Verkauf) und `EquipmentLevel` (Zyklusdauer). Save-Migration auf einem echten Alt-Spielstand geprüft. |
| **K2** | Geld entstand aus Timern, Gäste waren Deko | Geld entsteht ausschließlich beim Servieren eines konkreten wartenden Gastes. Ein Gast belegt einen Platz, hat Geduld und geht unbedient wieder. |
| **K3** | Renovierungspunkte wurden angezeigt, wirkten aber nirgends | Globaler Ertragsmultiplikator; `PrestigeK` von 1.0 auf 0.035 plus Mindestschwelle — vorher war der Button ab dem ersten Verkauf grün und damit als Signal wertlos. |

---

## 3. Was v3 nicht vorhergesehen hat

Drei Befunde, die erst im Spielbetrieb sichtbar wurden und mehr Wirkung
hatten als einzelne Punkte der v3-Liste:

**Das Hochformat war nie geprüft.** Der Game-View stand auf „Free Aspect",
also querformatig — jede visuelle Beurteilung war für ein Portrait-Handyspiel
wertlos. Auf 1080×1920 umgestellt zeigte sich: Die Kamera steht isometrisch,
eine Stationsreihe entlang der Welt-X-Achse läuft im Bild diagonal und
bestimmt die Bildbreite, oben und unten blieb zwangsläufig Leerfläche. Der
Grundriss liegt jetzt auf der Weltdiagonalen (1,0,1), die im Bild senkrecht
verläuft. Das war die eigentliche Ursache des mehrfachen Feedbacks
„das Design gefällt mir nicht" — nicht die Skalierung einzelner Modelle, an
der zuvor mehrfach vergeblich nachjustiert wurde.

**Die Render-Einstellungen standen auf Unity-Standard.** Das URP-Asset wurde
einmalig erzeugt und nie angefasst: Kantenglättung aus, weiche Schatten in
der Pipeline abgeschaltet (`LightShadows.Soft` am Licht lief damit ins
Leere), Schattenreichweite 50 bei einem 12 Einheiten großen Lokal,
Schattenstärke implizit 1.0. Vier Werte, alle auf dem jeweils schlechtesten
Stand. Sie stehen jetzt im Code (`UrpSetup`), weil das Asset nicht unter
Versionskontrolle liegt und auf jedem Rechner neu entsteht.

**Die Simulation war korrekt, aber stumm.** Ein abgewanderter Gast war
folgenlos, ein schnell bedienter brachte dasselbe wie ein spät bedienter.
Ruf, Trinkgeld, Rush Hour und VIP-Gäste schließen diese Lücken — sie sind
der Grund, überhaupt aktiv hinzuschauen, statt nur zuzusehen.

---

## 4. Was steht

**Ökonomie & Simulation**
Zwei Upgrade-Achsen, Manager-Automatisierung, Meilensteine, Offline-Ertrag
(mit Ruf-Faktor), Renovierung mit Ortswechsel. Warteschlange mit vier
Plätzen, Geduld pro Gast, Trinkgeld bis +50 % bei sofortiger Bedienung,
Ruf 0–100 als Multiplikator auf den Gästestrom, Rush Hour alle 150 s,
VIP-Gäste mit sechsfachem Ertrag.

**Bedienung**
Alle Stationsaktionen laufen über Antippen der Station und den daraufhin
geöffneten Dialog — keine Dauerbuttons für einzelne Stationen. Am unteren
Rand nur die beiden globalen Aktionen.

**Darstellung**
Kopfleiste mit Umsatz, Ziel-Fortschrittsbalken, Schilder über den Stationen
mit Geduldsbalken und Ertrag, aufsteigende Beträge am Verkaufsort, Toasts
für Ereignisse. Gastraum aus dem Kenney Furniture Kit mit gemessenen
Zielgrößen je Modell, Fliesenboden, weiche Schatten, Post-Processing.

**Technik**
Mitwachsende Kamera, die den freien Streifen zwischen den HUD-Leisten rahmt.
Gemeinsames Partikel-Material, dauerhafte AudioSource, Asset-Zwischenspeicher,
60 fps gesetzt. 57 Balancing-Tests grün.

---

## 5. Offen — nach Wirkung sortiert

### R1 — Gründe zurückzukommen *(2 Sessions)*
- [ ] Tagesziele/Quests mit ×2-Boost als Belohnung *(aus v3 Phase F, der einzige unerledigte Retention-Punkt)*
- [ ] Ereignis-Logging als Grundlage für Balancing

> Das Spiel trägt eine erste Sitzung. Was fehlt, ist ein konkreter Grund für
> Tag 2, der über den bloßen Offline-Ertrag hinausgeht.

### R2 — Balancing mit Daten *(laufend)*
- [ ] `PrestigeK` und `PrestigeMultiplierPerStar` an echten Spielzeiten kalibrieren — beide sind weiterhin Platzhalter
- [ ] Kostenkurven über alle fünf Locations prüfen
- [ ] Ruf-Verlust nachjustieren: aktuell 1.5 pro ignoriertem Gast, im Testlauf bereits einmal von 3.0 halbiert

### R3 — Restliche Optik *(2 Sessions)*
- [ ] Fliesengröße und Fugenstärke am Screenshot nachjustieren (`SurfaceTexture.TileWorldSize`)
- [ ] Gastbereich links liegt außerhalb des Hochformat-Ausschnitts — näher heranrücken oder streichen
- [ ] Location-Prefabs statt reinem Farb-/Texturwechsel *(aus v3 Phase E)*
- [ ] Renovierungs-Übergang mit Kamerafahrt *(aus v3 Phase E)*

### R4 — Technische Schulden *(1–2 Sessions)*
- [ ] Legacy `UnityEngine.UI.Text` → TextMeshPro (Paket liegt ungenutzt im Projekt)
- [ ] Object-Pooling für Gäste, Schattenflecken und Effekte statt `new GameObject` pro Instanz
- [ ] Unity-eigene Play-Mode-Tests — die 57 Tests decken nur `BalancingCore` ab, nicht die Simulationslogik im `GameManager`
- [ ] `ProjectVersion.txt` gegen die eingesetzte Editor-Version prüfen *(Restpunkt aus v3 Phase A)*

### R5 — iOS *(1–2 Sessions, bewusst zuletzt)*
- [ ] Apple Developer Program
- [ ] CI-Runner mit iOS-Target neben `BuildWebGl`
- [ ] TestFlight-Build
- [ ] Touch-Eingabe auf echtem Gerät prüfen

---

## 6. Bewusste Abweichungen von v3

| v3 forderte | Stand | Grund |
|---|---|---|
| `GuestFlow.CapacityFactor` entfernen | Bleibt | Im Live-Betrieb wirkungslos, aber die einzige ehrliche Näherung für Offline-Ertrag: eine Gast-für-Gast-Simulation über Stunden ist nicht praktikabel. |
| Auftragswarteschlange **pro Station** | Eine gemeinsame Schlange | Bei sieben Stationen in einer Reihe und schmalem Hochformat ist eine Schlange pro Station räumlich nicht lesbar. |
| Fortschrittsringe und Level-Badges an Stationen | Schild mit Geduldsbalken und Ertrag | Die Geduld ist die Information, nach der der Spieler tatsächlich handelt; das Level steht im Dialog. |

---

## 7. Arbeitsweise — was sich bewährt hat

- **Screenshot schlägt Datenabfrage.** Mehrere Fehler (leerer Bildschirm,
  gefüllter Fortschrittsbalken, unsichtbare Schattenflecken) waren über MCP
  unauffällig und nur im Bild zu sehen.
- **Im Zielformat prüfen.** Ein im Querformat beurteiltes Layout sagt über
  ein Hochformat-Handyspiel nichts aus. Für Pixel-genaue Prüfungen gibt es
  den Menüpunkt „Game-View auf Portrait klein (405×720)": bei 1080×1920 wird
  der Game-View mit 0,25× dargestellt, und diese Verkleinerung wirft die
  Kantenglättung weg, bevor ein Screenshot sie zeigen kann.
- **Compilerfehler nicht über `get_console_logs` prüfen.** Die Abfrage meldete
  einmal „keine Fehler", während die Kompilierung fehlgeschlagen war. Der
  verlässliche Indikator ist, ob `set_play_mode_status` tatsächlich
  `isPlaying: true` liefert — Unity startet den Play-Modus bei Fehlern nicht.
- **Werte, die im Editor-Asset stehen, gehören in den Code**, solange das
  Asset nicht unter Versionskontrolle liegt (URP-Pipeline, Post-Processing-
  Profil, Szenenaufbau).
- **Modellgrößen messen, nicht schätzen.** Die Kenney-Kits sind untereinander
  nicht maßstabsgetreu; ein gemeinsamer Skalierungsfaktor führte
  zwangsläufig zu Mülleimern in Thekengröße.

---

## 8. Was weiterhin bewusst nicht gebaut wird

Unverändert aus v3: Werbung, IAP und Zweitwährung (keine Monetarisierung),
wöchentliche Events, Clubs/Ranglisten/Social (brauchen eine Spielerbasis),
Gacha-Systeme, serverautoritative Ökonomie.

---

## 9. Einordnung

Das Ziel aus v3 war „ein Spiel, das im Genre nicht als Prototyp auffällt".
Drei der vier dort genannten Kriterien sind erreicht: eine erste Sitzung, die
trägt; fünf unterscheidbare Locations; eine Szene, an der man den eigenen
Fortschritt abliest. Das vierte — ein Grund, am nächsten Tag zurückzukommen —
ist der Kern von R1 und der einzige inhaltliche Punkt, der noch zwischen dem
aktuellen Stand und diesem Ziel steht.

Der kritische Pfad ist damit nicht mehr technisch, sondern inhaltlich:
**R1 und R2.** Alles andere ist Politur an einem Spiel, das funktioniert.
