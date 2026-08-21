using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Markiert ein 3D-Szenen-Objekt als anklickbare Station (PLANv2.md
    /// Abschnitt 1.3, Tap-Layer: "antippen, um einzusammeln oder zu
    /// beschleunigen"). StationIndex muss zur Position in
    /// StationCatalog.All passen -- wird beim Platzieren der Szene gesetzt
    /// (CIBuild.cs), nicht zur Laufzeit ermittelt.
    /// </summary>
    public class StationHotspot : MonoBehaviour
    {
        public int StationIndex;
    }
}
