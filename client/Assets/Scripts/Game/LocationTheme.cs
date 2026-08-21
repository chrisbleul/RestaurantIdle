using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// PLANv2.md Abschnitt 6: fuenf Location-Stufen. Bewusst nur Name +
    /// Farbpalette statt eigener 3D-Assets pro Location (realistischer
    /// Rahmen, siehe PLANv2.md Abschnitt 2: "Feature-Paritaet ist nicht die
    /// Messlatte") -- macht die Renovierung trotzdem sichtbar statt nur
    /// eine Zahl zurueckzusetzen. Volle eigene Gebaeudehuellen pro Location
    /// waeren der naechste Ausbauschritt.
    /// </summary>
    public static class LocationTheme
    {
        public struct Theme
        {
            public string Name;
            public Color Ground;
            public Color Wall;
        }

        private static readonly Theme[] Themes =
        {
            new Theme { Name = "Limonadenstand", Ground = new Color(0.55f, 0.75f, 0.45f), Wall = Color.white },
            new Theme { Name = "Food Truck", Ground = new Color(0.55f, 0.55f, 0.58f), Wall = new Color(0.85f, 0.25f, 0.2f) },
            new Theme { Name = "Cafe", Ground = new Color(0.75f, 0.6f, 0.45f), Wall = new Color(0.95f, 0.9f, 0.8f) },
            new Theme { Name = "Diner", Ground = new Color(0.3f, 0.35f, 0.45f), Wall = new Color(0.85f, 0.85f, 0.9f) },
            new Theme { Name = "Restaurant", Ground = new Color(0.25f, 0.18f, 0.22f), Wall = new Color(0.75f, 0.6f, 0.25f) },
        };

        public static int MaxIndex => Themes.Length - 1;

        public static Theme For(int locationIndex) => Themes[Mathf.Clamp(locationIndex, 0, MaxIndex)];
    }
}
