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

        /// <summary>Reihen-Position relativ zur Mitte, statt von Index 0 aus wachsend -- die Reihe bleibt beim Rauszoomen mittig statt einseitig zu wandern.</summary>
        private static float RowOffset(int index) => (index - (StationCount - 1) / 2f) * StationSpacing;

        public static Vector3 StationPosition(int index) => RowDirection * RowOffset(index);

        /// <summary>Gast-Warteplatz direkt hinter dem Tresen, quer zur zugehoerigen Station.</summary>
        public static Vector3 GuestStandPosition(int stationIndex) => GuestStandPosition(StationPosition(stationIndex));

        /// <summary>Ueberladen fuer Aufrufer, die schon eine Weltposition der Station haben.</summary>
        public static Vector3 GuestStandPosition(Vector3 stationPosition) =>
            OnGround(stationPosition + DepthDirection * (CounterGap + GuestStandDistance));

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

        /// <summary>Ausgang auf der Gastraumseite, versetzt, damit hinausgehende Gaeste nicht durch die Schlange laufen.</summary>
        public static Vector3 Exit =>
            OnGround(Entrance + DepthDirection * 1.3f - RowDirection * 0.6f);

        private static Vector3 OnGround(Vector3 position) => new Vector3(position.x, GuestGroundY, position.z);
    }
}
