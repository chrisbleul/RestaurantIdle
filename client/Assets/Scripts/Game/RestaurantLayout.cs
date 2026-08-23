using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Grundriss des Lokals -- gemeinsame Quelle fuer den Szenenaufbau im
    /// Editor (CIBuild) und die laufende Gast-Simulation (GameManager).
    /// Vorher standen dieselben Positionen doppelt: die Stationen entlang der
    /// Welt-X-Achse in CIBuild, Eingang/Ausgang/Warteplaetze als separate
    /// Konstanten im GameManager. Jede Layout-Aenderung musste an beiden
    /// Stellen von Hand nachgezogen werden.
    ///
    /// Wichtiger noch ist die Richtung: die Kamera steht isometrisch
    /// (Euler 55/45/0). Welt-+X zeigt auf dem Bildschirm nach rechts OBEN,
    /// Welt-+Z nach links oben. Eine Reihe entlang X laeuft im Bild also
    /// diagonal -- auf einem Portrait-Bildschirm (1080x1920) bestimmt sie
    /// damit die Bildbreite und laesst oben wie unten grosse leere Flaechen.
    /// Genau das war im ersten Portrait-Screenshot zu sehen.
    ///
    /// Die Diagonale (1,0,1) steht dagegen senkrecht auf der Bildschirm-
    /// Horizontalen: sie laeuft im Bild exakt nach oben. Eine Theke entlang
    /// dieser Achse fuellt die hohe Bildmitte, statt die schmale Breite zu
    /// sprengen.
    /// </summary>
    public static class RestaurantLayout
    {
        private const float Diagonal = 0.70710678f;

        /// <summary>Verlauf der Thekenreihe -- im Bild senkrecht nach oben.</summary>
        public static readonly Vector3 CounterDirection = new Vector3(Diagonal, 0f, Diagonal);

        /// <summary>Vom Tresen weg in den Gastraum -- im Bild nach links.</summary>
        public static readonly Vector3 GuestSide = new Vector3(-Diagonal, 0f, Diagonal);

        /// <summary>Rotation, die die lokale +X-Achse eines Modells auf CounterDirection dreht.</summary>
        public static readonly Quaternion CounterRotation = Quaternion.Euler(0f, -45f, 0f);

        public const float StationSpacing = 1.15f;

        /// <summary>Abstand des Warteplatzes vom Stationsmittelpunkt.</summary>
        public const float GuestStandDistance = 0.95f;

        /// <summary>Fusshoehe der Gast-Sprites (halbe Sprite-Hoehe ueber dem Boden).</summary>
        public const float GuestGroundY = 0.4f;

        public const int QueueCapacity = 4;
        private const float QueueSlotDistance = 0.45f;

        public static Vector3 StationPosition(int index) => CounterDirection * (index * StationSpacing);

        public static Vector3 GuestStandPosition(Vector3 stationPosition) =>
            OnGround(stationPosition + GuestSide * GuestStandDistance);

        /// <summary>Eingang: unterhalb der ersten Station, auf der Gastseite.</summary>
        public static Vector3 Entrance =>
            OnGround(StationPosition(0) + GuestSide * GuestStandDistance - CounterDirection * 2.3f);

        /// <summary>Ausgang seitlich versetzt, damit hinausgehende Gaeste nicht durch die Schlange laufen.</summary>
        public static Vector3 Exit =>
            OnGround(StationPosition(0) + GuestSide * 2.8f - CounterDirection * 3.6f);

        public static Vector3 QueueSlot(int slot) =>
            OnGround(Entrance - CounterDirection * (QueueSlotDistance * (slot + 1)));

        private static Vector3 OnGround(Vector3 position) => new Vector3(position.x, GuestGroundY, position.z);
    }
}
