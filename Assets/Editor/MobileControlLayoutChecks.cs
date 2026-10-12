using System;
using System.Reflection;
using GeoSniper;
using UnityEngine;

public static class MobileControlLayoutChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static bool Near(Rect a, Rect b) => Mathf.Abs(a.x-b.x)<.01f && Mathf.Abs(a.y-b.y)<.01f && Mathf.Abs(a.width-b.width)<.01f && Mathf.Abs(a.height-b.height)<.01f;
    public static void Run()
    {
        checks = 0;
        bool existed = PlayerPrefs.HasKey(MobileControlLayout.PreferenceKey);
        string previous = PlayerPrefs.GetString(MobileControlLayout.PreferenceKey, "");
        var root = new GameObject("Inactive touch control transaction check"); root.SetActive(false);
        try
        {
            PlayerPrefs.DeleteKey(MobileControlLayout.PreferenceKey); MobileControlLayout.ReloadSaved();
            var landscape = new Rect(0, 0, 1600, 720);
            var defaults = MobileControlLayout.Defaults(landscape);
            Require(Near(MobileControlLayout.Resolve(defaults, MobileControl.LeftFire, landscape), new Rect(75, 385, 84, 84)), "Default fire placement changed");
            Require(Near(MobileControlLayout.Resolve(defaults, MobileControl.Move, landscape), new Rect(60, 490, 180, 180)), "Default joystick placement changed");
            Rect dock = MobileControlLayout.Resolve(defaults, MobileControl.WeaponDock, landscape);
            Require(Near(MobileControlLayout.WeaponSlot(dock, 0), new Rect(625, 672, 112, 40)), "Default weapon slot changed");
            var custom = defaults.Copy();
            MobileControlLayout.SetRect(custom, MobileControl.RightFire, new Rect(280, 280, 130, 130), landscape);
            custom.controls[(int)MobileControl.RightFire].opacity = .4f;
            var decoded = MobileControlLayout.Decode(MobileControlLayout.Encode(custom));
            Require(decoded != null && Mathf.Approximately(decoded.controls[2].opacity, .4f), "Control profile did not round-trip");
            foreach (var safe in new[] { landscape, new Rect(40, 20, 2320, 1040), new Rect(24, 48, 1080, 1850) })
            {
                for (int i = 0; i < MobileControlLayout.Count; i++)
                {
                    Rect r = MobileControlLayout.Resolve(decoded, (MobileControl)i, safe);
                    Require(r.xMin >= safe.xMin - .001f && r.yMin >= safe.yMin - .001f && r.xMax <= safe.xMax + .001f && r.yMax <= safe.yMax + .001f, "A control left the notched / rotated safe area");
                    Require(r.Contains(r.center), "Rendered control center is not a touch target");
                    Require(!MobileControlLayout.OverlapsReservedUI(r, safe), "Control overlapped a clickable combat HUD area");
                }
                var mirrored = MobileControlLayout.LeftHanded(safe);
                Require(MobileControlLayout.Resolve(mirrored, MobileControl.Move, safe).center.x > safe.center.x, "Left-handed preset did not move the joystick");
                Require(MobileControlLayout.Resolve(mirrored, MobileControl.RightFire, safe).center.x < safe.center.x, "Left-handed preset did not move primary fire");
            }
            custom.controls[2].x = -5; custom.controls[2].y = 10; custom.controls[2].width = .001f; custom.controls[2].height = .001f; custom.controls[2].opacity = 0;
            var repaired = MobileControlLayout.Decode(MobileControlLayout.Encode(custom));
            Rect minimum = MobileControlLayout.Resolve(repaired, MobileControl.RightFire, landscape);
            Require(minimum.width >= 48 && minimum.height >= 48 && minimum.x >= 0 && minimum.yMax <= 720 && repaired.controls[2].opacity >= .25f, "Corrupt values escaped touch / visibility clamps");
            for (int reservedIndex = 0; reservedIndex < MobileControlLayout.ReservedCount; reservedIndex++)
            {
                var reserved = MobileControlLayout.ReservedUI(reservedIndex, landscape);
                MobileControlLayout.SetRect(custom, MobileControl.LeftFire, new Rect(reserved.center.x - 50, reserved.center.y - 50, 100, 100), landscape);
                Require(!MobileControlLayout.OverlapsReservedUI(MobileControlLayout.Resolve(custom, MobileControl.LeftFire, landscape), landscape), "Dragging fire over another HUD action caused competing input");
            }
            Require(MobileControlLayout.Decode("not json") == null && MobileControlLayout.Decode("{\"version\":1,\"controls\":[]}") == null, "Invalid profile did not fall back to defaults");
            MobileControlLayout.Save(decoded);
            string beforeCancel = PlayerPrefs.GetString(MobileControlLayout.PreferenceKey);
            var game = root.AddComponent<GeoSniperGame>();
            game.BeginTouchControlLayout();
            Require(game.CustomizingTouchControls && MobileControlLayout.Editing, "Editor did not block gameplay input");
            var draft = (MobileControlLayoutData)typeof(GeoSniperGame).GetField("touchLayoutDraft", Flags).GetValue(game);
            draft.controls[2].x = .9f;
            game.EndTouchControlLayout(false);
            Require(PlayerPrefs.GetString(MobileControlLayout.PreferenceKey) == beforeCancel && !game.CustomizingTouchControls && !MobileControlLayout.Editing, "Cancel changed the saved profile or retained input block");
            game.BeginTouchControlLayout();
            draft = (MobileControlLayoutData)typeof(GeoSniperGame).GetField("touchLayoutDraft", Flags).GetValue(game);
            draft.controls[2].x = .9f;
            game.EndTouchControlLayout(true);
            Require(MobileControlLayout.Decode(PlayerPrefs.GetString(MobileControlLayout.PreferenceKey)).controls[2].x == .9f, "Save did not apply the edited profile");
            Require(MobileCombatInput.RightFireRect == MobileControlLayout.GetRect(MobileControl.RightFire) && MobileCombatInput.MovementRect.center == MobileCombatInput.LeftCenter, "Draw and input rectangles diverged");
            Rect cached = MobileCombatInput.RightFireRect;
            PlayerPrefs.SetString(MobileControlLayout.PreferenceKey, beforeCancel);
            Require(MobileCombatInput.RightFireRect == cached, "Gameplay reread preferences instead of retaining its cached profile");
            var input = root.AddComponent<MobileCombatInput>();
            typeof(MobileCombatInput).GetField("leftFireFinger", Flags).SetValue(input, 42);
            typeof(MobileCombatInput).GetProperty("Move").SetValue(input, Vector2.one);
            typeof(MobileCombatInput).GetProperty("FireHeld").SetValue(input, true);
            typeof(MobileControlLayout).GetProperty("Editing").SetValue(null, true);
            typeof(MobileCombatInput).GetMethod("Update", Flags).Invoke(input, null);
            Require(input.Move == Vector2.zero && !input.FireHeld && !input.FirePressed && input.IsPointerOverUI, "Control editing fired or moved the player");
            MobileControlLayout.Cancel();
            MobileControlLayout.Save(defaults, true);
            Require(!PlayerPrefs.HasKey(MobileControlLayout.PreferenceKey) && MobileCombatInput.LeftFireRect == MobileControlLayout.LegacyRect(MobileControl.LeftFire, MobileCombatInput.Safe), "Reset defaults retained a custom placement");
            Debug.Log("PASS: " + checks + " touch control normalization, bounds, preset, persistence, cache and editor input checks");
        }
        finally
        {
            MobileControlLayout.Cancel();
            if (existed) PlayerPrefs.SetString(MobileControlLayout.PreferenceKey, previous); else PlayerPrefs.DeleteKey(MobileControlLayout.PreferenceKey);
            PlayerPrefs.Save(); MobileControlLayout.ReloadSaved();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
