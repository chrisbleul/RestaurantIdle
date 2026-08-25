using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Grundriss des Lokals -- gemeinsame Quelle fuer den Szenenaufbau im
    /// Editor (CIBuild) und die laufende Gast-Simulation (GameManager).
    ///
    /// Eatventure-Layout (Nutzer-Feedback): EIN durchgehender Tresen trennt
    /// einen Kuechenstreifen (Personal/Stationen, nah an der Kamera) von
    /// einem Gastraum (Gaeste, weiter weg dahinter) -- vorher hatte jede
    /// Station ihren eigenen kleinen Warteplatz direkt daneben, ohne
    /// gemeinsamen Tresen.
    ///
    /// Die Kamera steht isometrisch (Euler 55/45/0): DepthDirection (1,0,1)
    /// laeuft im Bild nach oben (weiter von der Kamera weg = weiter oben im
    /// Bild), RowDirection (-1,0,1) laeuft im Bild nach links/rechts -- beide
    /// stehen im Bild senkrecht aufeinander (siehe RecomputeCameraTarget in
    /// GameManager, das genau diese Kamera-Ebene nutzt). Die Stationsreihe
    /// liegt jetzt auf RowDirection (Bildbreite): das alte Layout legte sie
    /// auf DepthDirection, damit sie die schmale Portrait-Breite nicht
    /// sprengt -- mit Pan-Steuerung (Nutzer-Feedback) muss die Reihe nicht
    /// mehr komplett ins Bild passen, der Spieler kann seitlich nachschauen.
    /// </summary>
    public static class RestaurantLayout
    {
        private const float Diagonal = 0.70710678f;

        /// <summary>
        /// Von der Kueche zum Gastraum -- im Bild nach oben (weiter von der
        /// Kamera weg). Vorzeichen zweimal empirisch korrigiert: die erste
        /// Fassung zeigte Gaeste nah/unten und Kueche fern/oben (falsch
        /// herum), die zweite (testweise geflippte) Fassung drehte es zu
        /// weit -- Kueche nah/unten UND Wand nah durch den Tresen blockiert.
        /// Diese Version haelt Kueche bei Tiefe 0 als Referenz und schiebt
        /// nur Gastraum/Wand ins Bild nach oben (positive Richtung).
        /// </summary>
        public static readonly Vector3 DepthDirection = new Vector3(Diagonal, 0f, Diagonal);

        /// <summary>Reihen-Achse: Stationen und Gaeste-Warteplaetze liegen hier nebeneinander -- im Bild nach links/rechts.</summary>
        public static readonly Vector3 RowDirection = new Vector3(-Diagonal, 0f, Diagonal);

        /// <summary>
        /// Rotation, die die lokale +X-Achse eines Modells auf RowDirection
        /// dreht (Stationsreihe, Tresen, Rueckwand). RowDirection steht 90
        /// Grad zur alten CounterDirection -- der Wert war beim Umbau
        /// zunaechst nur umbenannt, nicht neu berechnet worden: Modelle
        /// standen dadurch quer statt in der Reihe zu liegen.
        /// </summary>
        public static readonly Quaternion RowRotation = Quaternion.Euler(0f, -135f, 0f);

        public const float StationSpacing = 1.15f;

        /// <summary>Zielhoehe der Thekenmodelle -- gemeinsame Referenz fuer den Szenenaufbau (CIBuild) und die Charaktergroesse (GameManager), damit Personen nicht unabhaengig von den Geraeten skaliert werden.</summary>
        public const float CounterHeight = 1.05f;

        /// <summary>Abstand Kueche -> Tresen entlang DepthDirection.</summary>
        public const float CounterGap = 0.85f;

        /// <summary>Abstand des Gast-Warteplatzes hinter dem Tresen.</summary>
        public const float GuestStandDistance = 0.6f;

        /// <summary>Fusshoehe der Gast-Sprites (halbe Sprite-Hoehe ueber dem Boden).</summary>
        public const float GuestGroundY = 0.4f;

        public const int QueueCapacity = 4;
        private const float QueueSlotDistance = 0.65f;

        /// <summary>Anzahl Stationen -- fuer die zentrierte Reihe (RowOffset), muss zu StationCatalog.All.Length passen.</summary>
        public const int StationCount = 7;

        /// <summary>
        /// Nutzer-Feedback ("Geraete besser im Arbeitsbereich verteilt",
        /// "Bewegen des Bildschirms soll nicht noetig sein"): die vorherige
        /// einzelne 7 breite Reihe brauchte entweder Pan oder starkes
        /// Rauszoomen. Die Stationen stehen jetzt in zwei Kuechenreihen --
        /// vordere Reihe (auf dem Tresen, Gast-Seite) und hintere Reihe
        /// (freistehend auf dem Boden, tiefer in der Kueche) --, halb so
        /// breit wie vorher und damit eher ohne Ziehen im Bild.
        /// </summary>
        public const int FrontRowCount = 4;

        private const int BackRowCount = StationCount - FrontRowCount;

        /// <summary>Abstand der hinteren Kuechenreihe von der vorderen (Tresen-)Reihe, entgegen DepthDirection (tiefer in die Kueche, weg vom Gastraum).</summary>
        public const float BackRowDepthOffset = 1.6f;

        private static bool IsFrontRow(int stationIndex) => stationIndex < FrontRowCount;

        private static int ColumnIndex(int stationIndex) => IsFrontRow(stationIndex) ? stationIndex : stationIndex - FrontRowCount;

        private static int ColumnsInRow(int stationIndex) => IsFrontRow(stationIndex) ? FrontRowCount : BackRowCount;

        /// <summary>Reihen-Position relativ zur Mitte, statt von Index 0 aus wachsend -- jede Kuechenreihe bleibt beim Rauszoomen mittig statt einseitig zu wandern.</summary>
        private static float ColumnOffset(int column, int columnsInRow) => (column - (columnsInRow - 1) / 2f) * StationSpacing;

        /// <summary>Reine Spaltenposition einer Station (nur RowDirection, ohne Tiefe) -- gemeinsame Basis fuer Geraete- UND Gastposition.</summary>
        private static Vector3 ColumnPosition(int stationIndex) => RowDirection * ColumnOffset(ColumnIndex(stationIndex), ColumnsInRow(stationIndex));

        /// <summary>Geraeteposition: vordere Reihe auf Tiefe 0 (Tresen), hintere Reihe BackRowDepthOffset weiter in die Kueche.</summary>
        public static Vector3 StationPosition(int index) =>
            IsFrontRow(index) ? ColumnPosition(index) : ColumnPosition(index) - DepthDirection * BackRowDepthOffset;

        /// <summary>
        /// Gast-Warteplatz -- bewusst IMMER auf der Spaltenposition an der
        /// vorderen Tresenlinie, unabhaengig davon, ob das zugehoerige
        /// Geraet in der vorderen oder hinteren Kuechenreihe steht. Ein
        /// Gast, der fuer eine Station der hinteren Reihe an DEREN
        /// tatsaechlicher (weiter hinten liegender) Tiefe warten wuerde,
        /// stuende mitten in der vorderen Reihe -- die hintere Reihe ist
        /// reine Kuechen-Deko/Arbeitsplatz, keine eigene Gastfront.
        /// </summary>
        public static Vector3 GuestStandPosition(int stationIndex) =>
            OnGround(ColumnPosition(stationIndex) + DepthDirection * (CounterGap + GuestStandDistance));

        /// <summary>
        /// Gemeinsame Warteschlange seitlich neben der Stationsreihe, auf
        /// Gastraum-Tiefe -- ein Tresen, eine Schlange, statt einer eigenen
        /// pro Station. Gaeste stehen sichtbar am Tresen an, jenseits von
        /// Station 0 (nicht der letzten Station!): Station 0 ist immer als
        /// erste freigeschaltet, die Schlange bleibt so auch im Fruehspiel
        /// (nur eine Station sichtbar) in der Naehe des relevanten
        /// Bildausschnitts -- an der letzten Station haengend zog sie die
        /// Kamera-Rahmung (RecomputeCameraTarget) frueh auf einen Bereich
        /// von 9+ Weltweinheiten Breite auseinander.
        /// </summary>
        public static Vector3 QueueSlot(int slot) =>
            OnGround(StationPosition(0)
                + DepthDirection * (CounterGap + GuestStandDistance)
                - RowDirection * (StationSpacing * 0.75f + QueueSlotDistance * (slot + 1)));

        /// <summary>Eingang jenseits des letzten Warteplatzes -- Gaeste laufen von dort seitlich ins Bild herein.</summary>
        public static Vector3 Entrance =>
            OnGround(QueueSlot(QueueCapacity - 1) + RowDirection * 1.1f);

        /// <summary>
        /// Nutzer-Feedback ("linke Seite ist abgeschnitten", "Leute laufen
        /// durch die Tische"): die Sitzgruppen im Gastraum wurden bisher
        /// ueber StationPosition(i*2-1) verankert -- eine Formel aus der
        /// alten 7-breiten Reihe, die zufaellig einen brauchbaren Abstand
        /// ergab. Mit der neuen, schmaleren FrontRowCount-Reihe rueckte
        /// dieselbe Formel spuerbar naeher an die Gast-Wartelinie
        /// (GuestStandPosition/QueueSlot, Tiefe CounterGap+GuestStandDistance
        /// ~1.45) heran -- Gaeste liefen sichtbar durch die Tische. Eine
        /// eigene, zentrierte Formel mit klarem Tiefenabstand zur
        /// Warteschlange behebt beides: genug Abstand zur Laufspur UND (ueber
        /// RecomputeCameraTarget.Include) ein garantiert sichtbarer Rand.
        /// </summary>
        public const int DiningTableCount = 3;

        private const float DiningTableSpacing = StationSpacing * 1.4f;

        /// <summary>Tiefe der Sitzgruppen -- deutlich jenseits der Gast-Wartelinie (CounterGap + GuestStandDistance ~1.45), damit niemand beim Anstehen/Bedienen durch einen Tisch laeuft.</summary>
        private const float DiningTableDepth = 2.8f;

        public static Vector3 DiningTablePosition(int index) =>
            RowDirection * ((index - (DiningTableCount - 1) / 2f) * DiningTableSpacing) + DepthDirection * DiningTableDepth;

        /// <summary>Ausgang auf der Gastraumseite, versetzt, damit hinausgehende Gaeste nicht durch die Schlange laufen.</summary>
        public static Vector3 Exit =>
            OnGround(Entrance + DepthDirection * 1.3f - RowDirection * 0.6f);

        private static Vector3 OnGround(Vector3 position) => new Vector3(position.x, GuestGroundY, position.z);
    }
}
