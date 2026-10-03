using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    /// <summary>
    /// Zero-allocation GUIStyle cache for Immediate Mode GUI (OnGUI).
    /// Prevents hundred+ allocations per frame on mobile devices.
    /// </summary>
    public static class GUIStyleCache
    {
        private static readonly Dictionary<int, GUIStyle> _labelCache = new Dictionary<int, GUIStyle>(64);
        private static readonly Dictionary<int, GUIStyle> _boxCache = new Dictionary<int, GUIStyle>(16);

        public static GUIStyle Get(int fontSize, FontStyle fontStyle = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft, Color? textColor = null, bool wordWrap = false)
        {
            Color color = textColor ?? Color.white;
            int key = fontSize ^ ((int)fontStyle << 7) ^ ((int)alignment << 10) ^ (color.GetHashCode() << 15) ^ (wordWrap ? 1 : 0);
            if (!_labelCache.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = fontStyle,
                    alignment = alignment,
                    wordWrap = wordWrap,
                    clipping = TextClipping.Overflow,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = color }
                };
                _labelCache[key] = style;
            }
            return style;
        }

        public static GUIStyle GetBox(int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter, Color? textColor = null)
        {
            Color color = textColor ?? Color.white;
            int key = fontSize ^ ((int)alignment << 8) ^ (color.GetHashCode() << 14);
            if (!_boxCache.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = fontSize,
                    alignment = alignment,
                    normal = { textColor = color }
                };
                _boxCache[key] = style;
            }
            return style;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            _labelCache.Clear();
            _boxCache.Clear();
        }
    }
}
