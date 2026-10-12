using UnityEngine;

namespace GeoSniper
{
    public static class CombatHudPreferences
    {
        const string DetailsKey = "GeoSniper.TacticalHudDetails";
        public const int ScopeLabelFont = 12;
        public const int ScopeValueFont = 18;
        public const int StatusFont = 14;
        public static bool TacticalDetails
        {
            get => PlayerPrefs.GetInt(DetailsKey, 0) == 1;
            set { PlayerPrefs.SetInt(DetailsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static Rect SafeCanvas(float width, float height)
        {
            if (Screen.width <= 0 || Screen.height <= 0 || Screen.safeArea.width <= 0) return new Rect(0, 0, width, height);
            var safe = Screen.safeArea;
            return new Rect(safe.x * width / Screen.width, (Screen.height - safe.yMax) * height / Screen.height,
                safe.width * width / Screen.width, safe.height * height / Screen.height);
        }

        public static Rect UtilityDock(Rect safe) => new Rect(safe.x + 14, safe.y + 62, 98, 204);
        public static Rect PlayerStatus(Rect safe) => new Rect(safe.center.x - Mathf.Min(364, safe.width - 32) / 2,
            safe.yMax - 92, Mathf.Min(364, safe.width - 32), 34);
        public static Rect PlayerStatus(Rect safe, Rect weaponDock)
        {
            Rect status = PlayerStatus(safe);
            status.x = weaponDock.center.x - status.width / 2;
            status.y = weaponDock.y - status.height - 10;
            if (status.y < safe.y + 62) status.y = weaponDock.yMax + 10;
            status = Clamp(status, safe);
            if (MobileControlLayout.OverlapsReservedUI(status,safe) || status.Overlaps(weaponDock))
            {
                status.y = weaponDock.yMax + 10;
                status = Clamp(status,safe);
                if (MobileControlLayout.OverlapsReservedUI(status,safe) || status.Overlaps(weaponDock)) status = PlayerStatus(safe);
            }
            return status;
        }

        public static Rect[] ScopeInstruments(float width, float height, Rect safe)
        {
            float d = Mathf.Min(width, height) * .92f;
            float left = (width - d) / 2, right = (width + d) / 2;
            float room = Mathf.Min(left - safe.x - 96, safe.xMax - right - 28);
            float panelWidth = Mathf.Clamp(room, 148, 180);
            bool wings = room >= 148;
            float leftX = wings ? safe.x + 14 : width / 2 - d * .34f;
            float rightX = wings ? right + 14 : width / 2 + d * .12f;
            float top = wings ? safe.y + Mathf.Clamp(height * .23f, 108, 168) : height / 2 - d * .37f;
            float lower = wings ? top + 64 : height / 2 + d * .31f;
            return new[] {
                Clamp(new Rect(leftX, top, panelWidth, 52), safe),
                Clamp(new Rect(rightX, top, panelWidth, 52), safe),
                Clamp(new Rect(leftX, lower, panelWidth, 52), safe),
                Clamp(new Rect(rightX, lower, panelWidth, 52), safe)
            };
        }

        public static Rect Clamp(Rect rect, Rect safe)
        {
            rect.width = Mathf.Min(rect.width, safe.width - 16);
            rect.height = Mathf.Min(rect.height, safe.height - 16);
            rect.x = Mathf.Clamp(rect.x, safe.x + 8, safe.xMax - rect.width - 8);
            rect.y = Mathf.Clamp(rect.y, safe.y + 8, safe.yMax - rect.height - 8);
            return rect;
        }
    }
}
