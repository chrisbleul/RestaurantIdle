# Restaurant Idle Game — Projektplan

**Plattform:** iOS (Unity), zunächst ohne App Store
**Backend:** EC2 (`cgo-app.de`), Postgres, `cgo-auth`
**Stand:** August 2026

---

## 1. Spielkonzept

### Core Loop

```
Gäste kommen  →  Gerichte werden produziert  →  Umsatz
      ↑                                            ↓
      └────  Investition in Stationen/Personal  ───┘
```

### Ressourcen

| Ressource | Rolle |
|---|---|
| **Umsatz (€)** | Hauptwährung, aus verkauften Gerichten |
| **Gästestrom** | Limitiert den Absatz, skaliert über Marketing/Ruf |
| **Michelin-Sterne** | Prestige-Währung, bleibt über Reset hinweg erhalten |

### Content-Achsen

**Stationen (Producer)** — je eigene Zykluszeit und Ertrag:

1. Kaffeemaschine (2 s)
2. Fritteuse (5 s)
3. Grill (15 s)
4. Pizzaofen (45 s)
5. Sushi-Bar (2 min)
6. Patisserie (5 min)
7. Chef's Table (15 min)

**Personal (Automatisierung)** — ein Manager pro Station ersetzt das manuelle Antippen. Erst dadurch entsteht der eigentliche Idle-Charakter.

**Upgrades**

- Rezepte → ×2 Ertrag einer Station
- Geräte → ×0,5 Zykluszeit
- Marketing → Gästestrom
- Ausstattung → globaler Multiplikator

---

## 2. Balancing-Mathematik

Der eigentliche Kern des Spiels. Wird als **reines C#-Modul ohne Unity-Abhängigkeit** implementiert und mit Unit-Tests abgesichert.

| Element | Formel | Richtwert |
|---|---|---|
| Kosten n-ter Kauf | `c₀ · r^n` | r = 1.07 (früh) → 1.15 (spät) |
| Meilenstein-Boni | ×2 bei 25 / 50 / 100 / 200 Stück | erzeugt spürbare Sprünge |
| Prestige-Ertrag | `k · √(Lifetime-Umsatz)` | k so wählen, dass Reset nach ~1 Std. lohnt |
| Offline-Ertrag | 100 % für 2 Std., danach 50 %, Cap bei 24 Std. | verhindert Fire-and-Forget |

### Zahlenformat

Die Werte sprengen im mittleren Spielverlauf jeden `double`. Von Anfang an **BreakInfinity.cs** verwenden. Ein nachträglicher Umbau zieht sich durch die komplette Codebase.

---

## 3. Tech-Stack

### Engine: Unity

| Grund | Detail |
|---|---|
| **C#** | Direkter Anschluss an vorhandenen .NET-Hintergrund |
| **Asset Store** | Isometrische Restaurant-Tilesets, Charaktere, Partikel, DOTween — hier entsteht die Optik |
| **URP 2D** | Light2D, Normal Maps, Shader Graph → weiche Küchenbeleuchtung, glühende Öfen, Schatten |
| **Ökosystem** | Idle/Hypercasual ist Unitys Kernmarkt, entsprechend viel Material |

*Alternative Godot 4:* schlanker und quelloffen, hervorragender 2D-Renderer — aber deutlich mageres Asset-Ökosystem. Genau das ist bei „soll gut aussehen" der Engpass.

### Weitere Bausteine

- **BreakInfinity.cs** — Large-Number-Arithmetik
- **DOTween** — Tweening für UI-Juice
- **Backend:** Node/Express auf EC2, Postgres für Saves
- **Auth:** bestehendes `cgo-auth` via REST

---

## 4. iOS-Deployment

### Das Nadelöhr

Jeder native iOS-Build braucht **macOS mit Xcode**. Unity, Godot und Defold erzeugen alle nur ein Xcode-Projekt, das dort kompiliert und signiert wird. Keine Engine umgeht das.

Ein Android-APK auf dem iPhone laufen zu lassen ist ebenfalls ausgeschlossen: Dalvik-Bytecode gegen Android-Framework vs. signierte Mach-O-Binaries gegen UIKit/Metal. Emulatoren scheitern daran, dass iOS JIT-Kompilierung außerhalb angehängter Debugger unterbindet.

### Distributionswege ohne App Store

| Weg | Kosten | Haltbarkeit | Aufwand |
|---|---|---|---|
| Free Provisioning | 0 € | **7 Tage**, max. 3 Apps | Mac per Kabel |
| Developer Program + Ad Hoc | 99 €/Jahr | 1 Jahr, 100 Geräte | UDIDs registrieren |
| **TestFlight (Internal)** | 99 €/Jahr | 90 Tage/Build | kein Review, Install per Link |

→ **TestFlight ist der Sweet Spot.** Die 7-Tage-Zertifikate nerven ab der zweiten Woche massiv; die 99 € sind realistisch nicht einsparbar.

### Ohne eigenen Mac

**Cloud-CI (empfohlen)** — Codemagic oder GitHub Actions mit macOS-Runnern. Codemagic unterstützt Unity explizit und hat ein kostenloses Monatskontingent (Konditionen vor dem Setup prüfen). Zertifikat und Provisioning Profile als Secrets hinterlegen, Runner baut und lädt direkt zu TestFlight. Stolperstein ist die Unity-Lizenzaktivierung auf dem Runner — einmalig einrichten.

**Nicht empfehlenswert:** EC2 Mac Instances. 24-Stunden-Mindestlaufzeit pro Dedicated Host, ~25 €/Tag.

### Tägliche Iteration ohne Signing

**Unity WebGL auf die EC2** deployen (`cgo-app.de/restaurant`), in Safari öffnen, zum Home-Bildschirm hinzufügen. Damit sind Touch-Input, Layout und Lesbarkeit auf dem echten Display testbar — ohne jede Signing-Kette.

Grenzen: Performance und Speicherverhalten sind nicht repräsentativ (WebGL auf iOS Safari ist deutlich langsamer als nativ), und iOS-spezifische Plugins (Game Center, IAP) fallen weg. Für ein Idle-Game reicht es zur Bewertung von Feel und Optik trotzdem.

*Unity Remote 5* streamt komprimiertes Video — brauchbar für Eingabetests, unbrauchbar zur Beurteilung der Grafik.

---

## 5. Backend auf EC2

Die Engine läuft ausschließlich lokal. Die EC2 ist reines Backend.

```yaml
services:
  restaurant-api:
    build: ./api
    environment:
      DATABASE_URL: postgres://...
      AUTH_URL: http://cgo-auth:PORT
    expose: ["4000"]
```

Reverse Proxy routet `/restaurant/api` → `restaurant-api`.

### Server-autoritativer Offline-Progress

`last_seen_at` in Postgres speichern, Offline-Ertrag **serverseitig** beim Login berechnen. Clientseitig gerechnet dreht der erste Spieler die Systemuhr vor.

### Save-Schema (Skizze)

| Feld | Typ |
|---|---|
| `user_id` | FK → `cgo-auth` |
| `state` | JSONB (Stationen, Upgrades, Manager) |
| `lifetime_revenue` | NUMERIC |
| `prestige_stars` | INT |
| `last_seen_at` | TIMESTAMPTZ |

---

## 6. Optik

Die Engine liefert nur die Fähigkeit. Der Unterschied zwischen „Prototyp" und „gekauft" sind vier Dinge:

1. **Konsistente Art Direction** — ein Asset-Pack durchziehen, nicht mischen
2. **Juice** — jede hochzählende Zahl tweent, Münzen fliegen zum Zähler, Buttons federn. DOTween, ~20 Zeilen, 80 % des Effekts
3. **Partikel & Licht** — Dampf über Töpfen, Ofenglut, Feierabendlicht
4. **UI-Typografie** — eine gute Schrift und großzügige Abstände schlagen jeden Shader

---

## 7. Umsetzungsreihenfolge

### Phase 0 — Pipeline absichern
- [ ] Entscheidung Mac vs. Cloud-CI
- [ ] Apple Developer Program (99 €/Jahr)
- [ ] **Leere Unity-Szene aufs iPhone bringen**

> Die Signing-Kette ist der frustrierendste Teil des Projekts. Sie gehört vor die Motivation, nicht hinter sie.

### Phase 1 — Mathematik
- [ ] Balancing als reines C#-Modul, ohne UI
- [ ] Unit-Tests für Kostenkurven, Meilensteine, Prestige, Offline-Ertrag
- [ ] BreakInfinity.cs eingebunden

### Phase 2 — Grauer Prototyp
- [ ] Eine Station, ein Upgrade, lokaler Save
- [ ] Tick-Loop mit Delta-Time
- [ ] **Entscheidungspunkt: Macht die Kurve grau Spaß?**

### Phase 3 — Content-Breite
- [ ] 7 Stationen, Meilensteine, Manager
- [ ] Marketing/Gästestrom

### Phase 4 — Art Pass
- [ ] Asset-Pack auswählen und durchziehen
- [ ] DOTween-Juice auf alle Interaktionen
- [ ] URP 2D Beleuchtung, Partikel

### Phase 5 — Backend
- [ ] API auf EC2, Postgres-Schema
- [ ] `cgo-auth` anbinden
- [ ] Serverseitiger Offline-Progress

### Phase 6 — Prestige
- [ ] Michelin-Sterne, Reset-Loop
- [ ] Erst wenn 1–2 Std. Spielzeit sich rund anfühlen

---

## 8. Kritische Punkte

| Risiko | Gegenmaßnahme |
|---|---|
| Mit der Grafik anfangen | Phase 2 als harter Gate — grau muss es Spaß machen |
| Signing-Kette blockiert spät | Phase 0 vor allem anderen |
| Zahlen-Overflow | BreakInfinity.cs ab Zeile 1 |
| Systemuhr-Manipulation | Offline-Progress serverseitig |
| WebGL verdeckt Plattform-Probleme | Regelmäßig echte iOS-Builds, nicht nur WebGL |
