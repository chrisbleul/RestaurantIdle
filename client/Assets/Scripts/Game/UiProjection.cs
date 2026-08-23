using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Weltposition -> Position im Screen-Space-Overlay-Canvas. Der
    /// CanvasScaler rechnet das gesamte UI auf eine Referenzaufloesung um
    /// (siehe GameManager.BuildUi), deshalb ist der rohe Pixelwert aus
    /// WorldToScreenPoint auf jedem Geraet mit anderer Aufloesung an der
    /// falschen Stelle -- der Faktor hier korrigiert genau das.
    /// </summary>
    public static class UiProjection
    {
        public static Vector2 WorldToCanvas(RectTransform canvasRect, Vector3 worldPosition)
        {
            if (canvasRect == null || Camera.main == null)
            {
                return Vector2.zero;
            }

            var screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
            var scale = canvasRect.rect.height / Mathf.Max(1f, Screen.height);
            return new Vector2(screenPoint.x * scale, screenPoint.y * scale);
        }
    }
}
