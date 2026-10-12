using System;
using System.Collections;
using UnityEngine;

namespace GeoSniper.Duel
{
    public readonly struct DuelDamageRecap
    {
        public readonly string Killer;
        public readonly int WeaponIndex;
        public readonly byte Zone;
        public readonly float Damage;
        public readonly float Distance;
        public bool HasValue => !string.IsNullOrEmpty(Killer);
        public string Weapon => WeaponCatalog.Get(WeaponIndex).Name;
        public string BodyPart => Zone == 2 ? "HEAD" : Zone == 1 ? "TORSO" : "LIMB";

        public DuelDamageRecap(string killer, int weaponIndex, byte zone, float damage, float distance)
        {
            Killer = string.IsNullOrWhiteSpace(killer) ? "RIVAL" : killer.Trim();
            if (Killer.Length > 40) Killer = Killer.Substring(0, 40);
            WeaponIndex = Mathf.Clamp(weaponIndex, 0, WeaponCatalog.Count-1);
            Zone = (byte)Mathf.Clamp(zone, 0, 2);
            Damage = float.IsFinite(damage) ? Mathf.Clamp(damage, 0, 100) : 0;
            Distance = float.IsFinite(distance) ? Mathf.Clamp(distance, 0, 5000) : 0;
        }
    }

    /// <summary>
    /// Records only the victim's already-rendered view. Playback never moves
    /// network actors, replays gameplay events, or shows the live opponent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelKillReplay : MonoBehaviour
    {
        public const int FrameCapacity = 17;
        public const float CaptureInterval = .125f;
        public const float PlaybackDuration = 2.2f;
        const int MaxWidth = 384;
        const int MaxHeight = 216;

        static DuelKillReplay current;
        readonly RenderTexture[] frames = new RenderTexture[FrameCapacity];
        readonly float[] frameTimes = new float[FrameCapacity];
        readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        Camera gameplayCamera;
        Coroutine captureRoutine;
        int nextFrame, frameCount, playbackStart;
        float nextCaptureAt, playbackBeganAt;
        bool isReplaying, captureUnavailable;
        DuelDamageRecap recap;

        public static bool Active => current != null && current.isReplaying;
        public static DuelDamageRecap Recap => current != null ? current.recap : default;
        public static int BufferedFrameCount => current != null ? current.frameCount : 0;
        public static float Progress => Active ? Mathf.Clamp01((Time.unscaledTime - current.playbackBeganAt) / PlaybackDuration) : 1;
        public static Texture PlaybackTexture => Active ? current.CurrentFrame() : null;

        public static void EnsureForCamera(Camera camera)
        {
            if (camera == null) { Clear(); return; }
            if (current != null && current.gameplayCamera != camera) current.ResetRecording(true);
            var recorder = camera.GetComponent<DuelKillReplay>();
            if (recorder == null) recorder = camera.gameObject.AddComponent<DuelKillReplay>();
            current = recorder;
            recorder.gameplayCamera = camera;
            recorder.captureUnavailable = false;
            recorder.enabled = true;
            recorder.StartCapture();
        }

        public static void BeginReplay(string killer, int weaponIndex, byte zone, float damage, float distance)
        {
            if (current == null)
            {
                var manager = SniperDuelManager.ActiveInstance;
                if (manager != null) EnsureForCamera(manager.GameplayCamera);
            }
            if (current == null) return;
            current.recap = new DuelDamageRecap(killer, weaponIndex, zone, damage, distance);
            // A recap still works when capture is unavailable or the round was
            // too short to record any meaningful view.
            current.isReplaying = current.frameCount >= 2;
            current.playbackStart = (current.nextFrame - current.frameCount + FrameCapacity) % FrameCapacity;
            current.playbackBeganAt = Time.unscaledTime;
        }

        public static void StopPlayback()
        {
            if (current == null) return;
            current.isReplaying = false;
            current.ReleaseFrames();
        }

        public static void Clear()
        {
            if (current != null) current.ResetRecording(true);
        }

        void StartCapture()
        {
            if (captureRoutine == null && Application.isPlaying && isActiveAndEnabled)
                captureRoutine = StartCoroutine(CaptureViews());
        }

        IEnumerator CaptureViews()
        {
            while (enabled)
            {
                yield return endOfFrame;
                var manager = SniperDuelManager.ActiveInstance;
                if (captureUnavailable || isReplaying || manager == null || manager.GameplayCamera != gameplayCamera
                    || manager.MatchState != DuelMatchState.InRound || !manager.CanShowRivalLocator
                    || Time.unscaledTime < nextCaptureAt || !gameplayCamera.enabled) continue;
                nextCaptureAt = Time.unscaledTime + CaptureInterval;
                // End-of-frame capture includes the scope mask and interface.
                // Capture at display size, then downsample on the GPU: native
                // screenshot APIs need not support a differently-sized target.
                RenderTexture screenCapture = null;
                try
                {
                    GetCaptureSize(Screen.width, Screen.height, out int width, out int height);
                    RenderTexture target = PrepareFrame(width, height);
                    if (target == null) { captureUnavailable = true; continue; }
                    screenCapture = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 0, RenderTextureFormat.ARGB32);
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(screenCapture);
                    Graphics.Blit(screenCapture, target);
                    CommitFrame(Time.unscaledTime);
                }
                catch (Exception)
                {
                    // Unsupported capture must never interrupt the duel.
                    captureUnavailable = true;
                    ReleaseFrames();
                }
                finally
                {
                    if (screenCapture != null) RenderTexture.ReleaseTemporary(screenCapture);
                }
            }
            captureRoutine = null;
        }

        void LateUpdate()
        {
            if (!isReplaying) return;
            var manager = SniperDuelManager.ActiveInstance;
            // The reliable hit result can precede the reliable round result.
            // Preserve playback while that confirmed death waits for scores.
            if (manager == null || manager.MatchState == DuelMatchState.Idle
                || manager.MatchState == DuelMatchState.LobbyWait || manager.MatchState == DuelMatchState.Warmup)
            { ResetRecording(true); return; }
            if (Time.unscaledTime - playbackBeganAt >= PlaybackDuration) StopPlayback();
        }

        static void GetCaptureSize(int sourceWidth, int sourceHeight, out int width, out int height)
        {
            float scale = Mathf.Min(1f, Mathf.Min(MaxWidth / (float)Mathf.Max(1, sourceWidth), MaxHeight / (float)Mathf.Max(1, sourceHeight)));
            width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
        }

        RenderTexture PrepareFrame(int width, int height)
        {
            var target = frames[nextFrame];
            if (target != null && (target.width != width || target.height != height))
            {
                // Rotation/resolution changes invalidate earlier frame aspect.
                ReleaseFrames();
                target = null;
            }
            if (target == null)
            {
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Duel last moments " + nextFrame,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1
                };
                if (!target.Create()) { DestroyTexture(target); return null; }
                frames[nextFrame] = target;
            }
            return target;
        }

        void CommitFrame(float capturedAt)
        {
            frameTimes[nextFrame] = capturedAt;
            nextFrame = (nextFrame + 1) % FrameCapacity;
            frameCount = Mathf.Min(frameCount + 1, FrameCapacity);
        }

        Texture CurrentFrame()
        {
            if (frameCount == 0) return null;
            float first = frameTimes[playbackStart];
            int lastIndex = (playbackStart + frameCount - 1) % FrameCapacity;
            float targetTime = Mathf.Lerp(first, frameTimes[lastIndex], Progress);
            int selected = playbackStart;
            for (int i = 1; i < frameCount; i++)
            {
                int index = (playbackStart + i) % FrameCapacity;
                if (frameTimes[index] > targetTime) break;
                selected = index;
            }
            return frames[selected];
        }

        void ResetRecording(bool clearRecap)
        {
            isReplaying = false;
            ReleaseFrames();
            if (clearRecap) recap = default;
            nextCaptureAt = 0;
            if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
            enabled = false;
        }

        void ReleaseFrames()
        {
            for (int i = 0; i < FrameCapacity; i++)
            {
                if (frames[i] != null) { frames[i].Release(); DestroyTexture(frames[i]); frames[i] = null; }
                frameTimes[i] = 0;
            }
            nextFrame = frameCount = playbackStart = 0;
        }

        static void DestroyTexture(RenderTexture texture)
        {
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }

        void OnDisable() => ResetRecording(true);
        void OnApplicationPause(bool paused) { if (paused && current == this) Clear(); }
        void OnDestroy()
        {
            ResetRecording(true);
            if (current == this) current = null;
        }
    }
}
