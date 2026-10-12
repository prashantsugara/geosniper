using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        public bool CustomizingTouchControls { get; private set; }
        MobileControlLayoutData touchLayoutDraft;
        MobileControl selectedTouchControl;
        bool draggingTouchControl, touchLayoutReset;
        Vector2 touchDragOffset;

        public void BeginTouchControlLayout()
        {
            if (ready || loading) return;
            touchLayoutDraft = MobileControlLayout.BeginDraft(MobileCombatInput.Safe);
            selectedTouchControl = MobileControl.Move;
            draggingTouchControl = touchLayoutReset = false;
            CustomizingTouchControls = MobileControlLayout.Editing = true;
        }

        public void EndTouchControlLayout(bool save)
        {
            if (save) MobileControlLayout.Save(touchLayoutDraft, touchLayoutReset);
            else MobileControlLayout.Cancel();
            touchLayoutDraft = null;
            draggingTouchControl = false;
            CustomizingTouchControls = false;
            showingAimSettings = true;
        }

        public void DrawTouchControlLayout(float w, float h)
        {
            if (touchLayoutDraft == null) { EndTouchControlLayout(false); return; }
            CommandGUI.Fill(new Rect(0, 0, w, h), CommandGUI.ThemeBg);
            var title = GUIStyleCache.Get(22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            var label = GUIStyleCache.Get(13, FontStyle.Normal, TextAnchor.MiddleLeft, CommandGUI.Text, true);
            GUI.Label(new Rect(20, 10, w - 40, 34), "CUSTOMIZE TOUCH CONTROLS", title);
            GUI.Label(new Rect(20, 45, w - 40, 28), "Drag a control to move it. Select it to adjust size and visibility.", label);
            bool portrait = w < h;
            Rect inspector = portrait ? new Rect(20, h - 330, w - 40, 250) : new Rect(w - 238, 88, 218, h - 178);
            Rect space = portrait ? new Rect(20, 88, w - 40, h - 438) : new Rect(20, 88, w - 278, h - 178);
            Rect safe = MobileCombatInput.Safe;
            float previewScale = Mathf.Max(.01f, Mathf.Min(space.width / safe.width, space.height / safe.height));
            Rect preview = new Rect(space.center.x - safe.width * previewScale / 2, space.center.y - safe.height * previewScale / 2, safe.width * previewScale, safe.height * previewScale);
            DrawTouchPreview(touchLayoutDraft, safe, preview, selectedTouchControl);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && preview.Contains(e.mousePosition))
            {
                // Smaller buttons have priority over the joystick if the player stacks them.
                for (int i = MobileControlLayout.Count - 1; i >= 0; i--)
                {
                    MobileControl control = (MobileControl)i;
                    Rect r = PreviewControlRect(touchLayoutDraft, control, safe, preview);
                    if (!r.Contains(e.mousePosition)) continue;
                    selectedTouchControl = control;
                    touchDragOffset = e.mousePosition - r.center;
                    draggingTouchControl = true;
                    e.Use(); break;
                }
            }
            if (draggingTouchControl && e.type == EventType.MouseDrag)
            {
                Rect r = MobileControlLayout.Resolve(touchLayoutDraft, selectedTouchControl, safe);
                Vector2 center = safe.position + (e.mousePosition - touchDragOffset - preview.position) / previewScale;
                r.center = center;
                MobileControlLayout.SetRect(touchLayoutDraft, selectedTouchControl, r, safe);
                touchLayoutReset = false;
                e.Use();
            }
            if (e.type == EventType.MouseUp) draggingTouchControl = false;

            float ix = inspector.x, iy = inspector.y, iw = inspector.width;
            GUI.Label(new Rect(ix, iy, iw, 28), MobileControlLayout.Name(selectedTouchControl), GUIStyleCache.Get(16, FontStyle.Bold, TextAnchor.MiddleCenter, CommandGUI.AccentCyan));
            if (CommandGUI.DrawButton(new Rect(ix, iy + 34, iw / 2 - 4, 34), "PREVIOUS", false, 11)) selectedTouchControl = (MobileControl)(((int)selectedTouchControl + MobileControlLayout.Count - 1) % MobileControlLayout.Count);
            if (CommandGUI.DrawButton(new Rect(ix + iw / 2 + 4, iy + 34, iw / 2 - 4, 34), "NEXT", false, 11)) selectedTouchControl = (MobileControl)(((int)selectedTouchControl + 1) % MobileControlLayout.Count);
            var placement = touchLayoutDraft.controls[(int)selectedTouchControl];
            Rect actual = MobileControlLayout.Resolve(touchLayoutDraft, selectedTouchControl, safe);
            Rect initial = MobileControlLayout.LegacyRect(selectedTouchControl, safe);
            float size = actual.width / initial.width;
            GUI.Label(new Rect(ix, iy + 80, iw, 24), "SIZE  " + Mathf.RoundToInt(size * 100) + "%", label);
            float nextSize = CommandGUI.HorizontalSlider(new Rect(ix, iy + 108, iw, 24), size, .65f, 1.8f);
            if (Mathf.Abs(nextSize - size) > .002f)
            {
                actual = new Rect(actual.center.x - initial.width * nextSize / 2, actual.center.y - initial.height * nextSize / 2, initial.width * nextSize, initial.height * nextSize);
                MobileControlLayout.SetRect(touchLayoutDraft, selectedTouchControl, actual, safe);
                touchLayoutReset = false;
            }
            GUI.Label(new Rect(ix, iy + 144, iw, 24), "OPACITY  " + Mathf.RoundToInt(placement.opacity * 100) + "%", label);
            float nextOpacity = CommandGUI.HorizontalSlider(new Rect(ix, iy + 172, iw, 24), placement.opacity, .25f, 1f);
            if (nextOpacity != placement.opacity) { placement.opacity = nextOpacity; touchLayoutReset = false; }
            float presetsY = portrait ? iy + 208 : iy + 228;
            float presetWidth = portrait ? iw / 2 - 4 : iw;
            if (CommandGUI.DrawButton(new Rect(ix, presetsY, presetWidth, 42), "LEFT-HANDED", false, 12))
            { touchLayoutDraft = MobileControlLayout.LeftHanded(safe); touchLayoutReset = false; draggingTouchControl = false; }
            if (CommandGUI.DrawButton(new Rect(portrait ? ix + iw / 2 + 4 : ix, portrait ? presetsY : presetsY + 54, presetWidth, 42), "RESET DEFAULTS", false, 12))
            { touchLayoutDraft = MobileControlLayout.Defaults(safe); touchLayoutReset = true; draggingTouchControl = false; }
            if (!portrait) GUI.Label(new Rect(ix, presetsY + 110, iw, 74), "Breath and FLIR appear while scoped. Weapon buttons appear when more than one rifle is owned.", label);
            float bw = Mathf.Min(220, (w - 60) / 2);
            if (CommandGUI.DrawButton(new Rect(w / 2 - bw - 8, h - 64, bw, 44), "CANCEL", false, 14)) EndTouchControlLayout(false);
            if (CommandGUI.DrawButton(new Rect(w / 2 + 8, h - 64, bw, 44), "SAVE CONTROLS", true, 14)) EndTouchControlLayout(true);
        }

        static Rect PreviewControlRect(MobileControlLayoutData data, MobileControl control, Rect safe, Rect preview)
        {
            Rect r = MobileControlLayout.Resolve(data, control, safe);
            float scale = preview.width / safe.width;
            return new Rect(preview.x + (r.x - safe.x) * scale, preview.y + (r.y - safe.y) * scale, r.width * scale, r.height * scale);
        }

        // Shared by the editor and capture checks; this does not alter or apply the saved profile.
        public static void DrawTouchPreview(MobileControlLayoutData data, Rect safe, Rect preview, MobileControl selected)
        {
            CommandGUI.Fill(preview, new Color(.045f, .075f, .09f));
            float previewScale = preview.width / safe.width;
            for (int i = 0; i < MobileControlLayout.ReservedCount; i++)
            {
                Rect reserved = MobileControlLayout.ReservedUI(i, safe);
                reserved = new Rect(preview.x + (reserved.x-safe.x)*previewScale, preview.y + (reserved.y-safe.y)*previewScale, reserved.width*previewScale, reserved.height*previewScale);
                CommandGUI.Fill(reserved, new Color(.15f, .2f, .24f, .4f));
                GUI.Label(reserved, i == 0 ? "MATCH STATUS" : i == 1 ? "UTILITY" : i == 2 ? "MAP" : "MISSION ACTIONS", GUIStyleCache.Get(10, FontStyle.Normal, TextAnchor.MiddleCenter, CommandGUI.Muted));
            }
            CommandGUI.Fill(new Rect(preview.center.x - 1, preview.center.y - 12, 2, 24), new Color(.4f, .5f, .6f, .5f));
            CommandGUI.Fill(new Rect(preview.center.x - 12, preview.center.y - 1, 24, 2), new Color(.4f, .5f, .6f, .5f));
            Rect status = CombatHudPreferences.PlayerStatus(safe, MobileControlLayout.Resolve(data, MobileControl.WeaponDock, safe));
            status = new Rect(preview.x + (status.x-safe.x)*previewScale, preview.y + (status.y-safe.y)*previewScale, status.width*previewScale, status.height*previewScale);
            CommandGUI.Fill(status, new Color(.08f, .18f, .16f));
            GUI.Label(status, "HEALTH  //  AMMO", GUIStyleCache.Get(10, FontStyle.Bold, TextAnchor.MiddleCenter, CommandGUI.AccentGreen));
            for (int i = 0; i < MobileControlLayout.Count; i++)
            {
                var control = (MobileControl)i;
                MobileCombatInput.DrawLayoutPreviewControl(control, PreviewControlRect(data, control, safe, preview), data.controls[i].opacity, control == selected);
            }
        }
    }
}
