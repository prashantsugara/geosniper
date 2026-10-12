using System;
using UnityEngine;

namespace GeoSniper
{
    public enum MobileControl { Move, LeftFire, RightFire, Scope, Jump, Crouch, Reload, Sprint, Knife, Breath, FLIR, WeaponDock }

    [Serializable]
    public sealed class MobileControlPlacement
    {
        public float x, y, width, height, opacity = 1f;
        public MobileControlPlacement Copy() => (MobileControlPlacement)MemberwiseClone();
    }

    [Serializable]
    public sealed class MobileControlLayoutData
    {
        public int version = 1;
        public MobileControlPlacement[] controls;
        public MobileControlLayoutData Copy()
        {
            var copy = new MobileControlLayoutData { controls = new MobileControlPlacement[MobileControlLayout.Count] };
            for (int i = 0; i < copy.controls.Length; i++) copy.controls[i] = controls[i].Copy();
            return copy;
        }
    }

    // Positions are fractions of the usable safe area; size uses its shorter axis.
    // The saved profile is parsed once and rectangles are rebuilt only when the screen changes.
    public static class MobileControlLayout
    {
        public const int Count = 12;
        public const int ReservedCount = 4;
        public const string PreferenceKey = "GeoSniper.TouchLayout.v1";
        public static bool Editing { get; internal set; }
        static bool loaded, rectanglesValid;
        static MobileControlLayoutData saved;
        static Rect previousSafe;
        static readonly Rect[] rectangles = new Rect[Count];
        static readonly string[] names = { "MOVE", "LEFT FIRE", "AIM / FIRE", "ADS", "JUMP", "CROUCH", "RELOAD", "SPRINT", "KNIFE", "BREATH", "FLIR", "WEAPONS" };
        public static string Name(MobileControl control) => names[(int)control];
        public static bool AimOnLeft => GetRect(MobileControl.Move).center.x > MobileCombatInput.Safe.center.x;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            saved = Decode(PlayerPrefs.GetString(PreferenceKey, ""));
        }

        public static Rect GetRect(MobileControl control)
        {
            Load();
            Rect safe = MobileCombatInput.Safe;
            if (!rectanglesValid || safe != previousSafe)
            {
                previousSafe = safe;
                rectanglesValid = true;
                for (int i = 0; i < Count; i++) rectangles[i] = saved == null ? LegacyRect((MobileControl)i, safe) : Resolve(saved, (MobileControl)i, safe);
            }
            return rectangles[(int)control];
        }

        public static float Opacity(MobileControl control) { Load(); return saved == null ? 1 : saved.controls[(int)control].opacity; }
        public static MobileControlLayoutData BeginDraft(Rect safe) { Load(); return saved == null ? Defaults(safe) : saved.Copy(); }
        public static void Save(MobileControlLayoutData draft, bool restoreDefaults = false)
        {
            if (restoreDefaults) { PlayerPrefs.DeleteKey(PreferenceKey); saved = null; }
            else
            {
                var validated = Decode(Encode(draft));
                if (validated == null) { Editing = false; return; }
                saved = validated;
                PlayerPrefs.SetString(PreferenceKey, Encode(saved));
            }
            loaded = true;
            rectanglesValid = false;
            PlayerPrefs.Save();
            Editing = false;
        }
        public static void Cancel() { Editing = false; }
        public static void ReloadSaved() { loaded = rectanglesValid = false; saved = null; }
        public static string Encode(MobileControlLayoutData data) => JsonUtility.ToJson(data);
        public static MobileControlLayoutData Decode(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<MobileControlLayoutData>(json);
                if (data == null || data.version != 1 || data.controls == null || data.controls.Length != Count) return null;
                foreach (var c in data.controls)
                {
                    if (c == null || !Finite(c.x) || !Finite(c.y) || !Finite(c.width) || !Finite(c.height) || !Finite(c.opacity)) return null;
                    c.x = Mathf.Clamp01(c.x); c.y = Mathf.Clamp01(c.y);
                    c.width = Mathf.Clamp(c.width, .02f, 1f); c.height = Mathf.Clamp(c.height, .02f, 1f);
                    c.opacity = Mathf.Clamp(c.opacity, .25f, 1f);
                }
                return data;
            }
            catch (ArgumentException) { return null; }
        }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

        public static MobileControlLayoutData Defaults(Rect safe)
        {
            var data = new MobileControlLayoutData { controls = new MobileControlPlacement[Count] };
            for (int i = 0; i < Count; i++)
            {
                Rect r = LegacyRect((MobileControl)i, safe);
                data.controls[i] = new MobileControlPlacement();
                SetRect(data, (MobileControl)i, r, safe);
            }
            return data;
        }
        public static MobileControlLayoutData LeftHanded(Rect safe)
        {
            var data = Defaults(safe);
            for (int i = 0; i < Count; i++) data.controls[i].x = 1 - data.controls[i].x;
            return data;
        }
        public static Rect Resolve(MobileControlLayoutData data, MobileControl control, Rect safe)
        {
            var c = data.controls[(int)control];
            float basis = Mathf.Min(safe.width, safe.height);
            Vector2 minimum = control == MobileControl.Move ? new Vector2(120, 120) : control == MobileControl.WeaponDock ? new Vector2(210, 40) : new Vector2(48, 48);
            float maxWidth = control == MobileControl.WeaponDock ? Mathf.Min(720, safe.width * .8f) : control == MobileControl.Move ? 300 : 240;
            float maxHeight = control == MobileControl.WeaponDock ? 96 : control == MobileControl.Move ? 300 : 240;
            float width = Mathf.Min(safe.width, Mathf.Clamp(basis * c.width, minimum.x, maxWidth));
            float height = Mathf.Min(safe.height, Mathf.Clamp(basis * c.height, minimum.y, maxHeight));
            return AvoidReservedUI(Clamp(new Rect(safe.x + safe.width * c.x - width / 2, safe.y + safe.height * c.y - height / 2, width, height), safe), safe);
        }
        public static Rect Clamp(Rect r, Rect safe)
        {
            r.width = Mathf.Clamp(r.width, 1, Mathf.Max(1, safe.width));
            r.height = Mathf.Clamp(r.height, 1, Mathf.Max(1, safe.height));
            r.x = Mathf.Clamp(r.x, safe.x, safe.xMax - r.width);
            r.y = Mathf.Clamp(r.y, safe.y, safe.yMax - r.height);
            return r;
        }
        public static void SetRect(MobileControlLayoutData data, MobileControl control, Rect rectangle, Rect safe)
        {
            rectangle = AvoidReservedUI(Clamp(rectangle, safe), safe);
            var c = data.controls[(int)control];
            float basis = Mathf.Max(1, Mathf.Min(safe.width, safe.height));
            c.x = (rectangle.center.x - safe.x) / Mathf.Max(1, safe.width);
            c.y = (rectangle.center.y - safe.y) / Mathf.Max(1, safe.height);
            c.width = rectangle.width / basis; c.height = rectangle.height / basis;
        }
        public static Rect WeaponSlot(Rect dock, int index)
        {
            float gap = dock.width / 50f, width = (dock.width - 2 * gap) / 3;
            return new Rect(dock.x + index * (width + gap), dock.y, width, dock.height);
        }
        public static Rect ReservedUI(int index, Rect safe)
        {
            switch (index)
            {
                case 0: return new Rect(safe.x, safe.y, safe.width, 108);
                case 1: return CombatHudPreferences.UtilityDock(safe);
                case 2: return new Rect(safe.xMax - 220, safe.y + 44, 220, 316);
                default: return new Rect(CombatHudPreferences.UtilityDock(safe).xMax + 20, safe.y + 170, 260, 172);
            }
        }
        public static bool OverlapsReservedUI(Rect rectangle, Rect safe)
        {
            for (int i = 0; i < ReservedCount; i++) if (rectangle.Overlaps(ReservedUI(i, safe))) return true;
            return false;
        }
        public static bool ReservedUIContains(Vector2 point, Rect safe)
        {
            for (int i = 0; i < ReservedCount; i++) if (ReservedUI(i, safe).Contains(point)) return true;
            return false;
        }
        static Rect AvoidReservedUI(Rect rectangle, Rect safe)
        {
            if (!OverlapsReservedUI(rectangle, safe)) return rectangle;
            Rect best = rectangle;
            float distance = float.PositiveInfinity;
            // Candidate edges include the desired position, safe edges and each HUD exclusion.
            // Pairing X/Y candidates also handles controls dragged into a HUD corner.
            for (int x = 0; x < 3 + 2 * ReservedCount; x++)
                for (int y = 0; y < 3 + 2 * ReservedCount; y++)
                {
                    float px = x == 0 ? rectangle.x : x == 1 ? safe.x : x == 2 ? safe.xMax - rectangle.width :
                        (x % 2 == 1 ? ReservedUI((x - 3) / 2, safe).x - rectangle.width : ReservedUI((x - 3) / 2, safe).xMax);
                    float py = y == 0 ? rectangle.y : y == 1 ? safe.y : y == 2 ? safe.yMax - rectangle.height :
                        (y % 2 == 1 ? ReservedUI((y - 3) / 2, safe).y - rectangle.height : ReservedUI((y - 3) / 2, safe).yMax);
                    Rect candidate = Clamp(new Rect(px, py, rectangle.width, rectangle.height), safe);
                    if (OverlapsReservedUI(candidate, safe)) continue;
                    float d = (candidate.center - rectangle.center).sqrMagnitude;
                    if (d < distance) { distance = d; best = candidate; }
                }
            return best;
        }
        public static Rect LegacyRect(MobileControl control, Rect safe)
        {
            switch (control)
            {
                case MobileControl.Move: return Clamp(new Rect(safe.x + 60, safe.yMax - 230, 180, 180), safe);
                case MobileControl.LeftFire: return Clamp(new Rect(safe.x + 75, safe.yMax - 335, 84, 84), safe);
                case MobileControl.RightFire: return Clamp(new Rect(safe.xMax - 245, safe.yMax - 205, 92, 92), safe);
                case MobileControl.Scope: return Clamp(new Rect(safe.xMax - 145, safe.yMax - 295, 76, 76), safe);
                case MobileControl.Jump: return Clamp(new Rect(safe.xMax - 95, safe.yMax - 180, 64, 64), safe);
                case MobileControl.Crouch: return Clamp(new Rect(safe.xMax - 145, safe.yMax - 105, 64, 64), safe);
                case MobileControl.Reload: return Clamp(new Rect(safe.xMax - 225, safe.yMax - 105, 64, 64), safe);
                case MobileControl.Sprint: return Clamp(new Rect(safe.x + 95, safe.yMax - 235, 60, 60), safe);
                case MobileControl.Knife: return Clamp(new Rect(safe.xMax - 315, safe.yMax - 185, 78, 78), safe);
                case MobileControl.Breath: return Clamp(new Rect(safe.xMax - 235, safe.yMax - 305, 70, 70), safe);
                case MobileControl.FLIR: return Clamp(new Rect(safe.xMax - 315, safe.yMax - 305, 70, 70), safe);
                default: return Clamp(new Rect(safe.center.x - 175, safe.yMax - 48, 350, 40), safe);
            }
        }
    }
}
