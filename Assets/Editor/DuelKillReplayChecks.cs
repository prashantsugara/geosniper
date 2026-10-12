using System;
using System.Reflection;
using GeoSniper.Duel;
using UnityEngine;

public static class DuelKillReplayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Action<bool, string> check, Camera camera)
    {
        DuelKillReplay.EnsureForCamera(camera);
        var recorder = camera.GetComponent<DuelKillReplay>();
        var prepare = typeof(DuelKillReplay).GetMethod("PrepareFrame", Private);
        var commit = typeof(DuelKillReplay).GetMethod("CommitFrame", Private);
        var sizing = typeof(DuelKillReplay).GetMethod("GetCaptureSize", BindingFlags.Static | BindingFlags.NonPublic);
        object[] wideSize = { 2400, 1080, 0, 0 };
        object[] portraitSize = { 1080, 2400, 0, 0 };
        sizing.Invoke(null, wideSize);
        sizing.Invoke(null, portraitSize);
        check((int)wideSize[2] <= 384 && (int)wideSize[3] <= 216
            && (int)portraitSize[2] <= 384 && (int)portraitSize[3] <= 216
            && Mathf.Abs((int)portraitSize[2] / (float)(int)portraitSize[3] - 1080f / 2400f) < .01f,
            "Replay captures fit the phone memory budget and preserve portrait/wide aspect");

        for (int i = 0; i < DuelKillReplay.FrameCapacity + 5; i++)
        {
            prepare.Invoke(recorder, new object[] { 64, 36 });
            commit.Invoke(recorder, new object[] { i * DuelKillReplay.CaptureInterval });
        }
        var textures = (RenderTexture[])typeof(DuelKillReplay).GetField("frames", Private).GetValue(recorder);
        check(DuelKillReplay.BufferedFrameCount == DuelKillReplay.FrameCapacity,
            "Replay ring overwrites old views and never exceeds its fixed frame budget");
        DuelKillReplay.BeginReplay("SPECTRE", 1, 2, 85, 120);
        check(DuelKillReplay.Active && DuelKillReplay.PlaybackTexture == textures[5]
            && DuelKillReplay.Recap.Killer == "SPECTRE" && DuelKillReplay.Recap.BodyPart == "HEAD",
            "Last-moments playback starts at the oldest retained view with confirmed damage recap");
        var manager = SniperDuelManager.ActiveInstance;
        var phase = typeof(SniperDuelManager).GetProperty("MatchState");
        var previousPhase = manager.MatchState;
        phase.SetValue(manager, DuelMatchState.InRound);
        typeof(DuelKillReplay).GetMethod("LateUpdate", Private).Invoke(recorder, null);
        check(DuelKillReplay.Active,
            "Confirmed victim replay survives a delayed host round-result packet");
        phase.SetValue(manager, previousPhase);
        var newest = textures[4];
        typeof(DuelKillReplay).GetField("playbackBeganAt", Private).SetValue(recorder, Time.unscaledTime - DuelKillReplay.PlaybackDuration);
        check(DuelKillReplay.PlaybackTexture == newest,
            "Last-moments playback finishes at the latest recorded view without showing live frames");
        DuelKillReplay.StopPlayback();
        check(!DuelKillReplay.Active && DuelKillReplay.PlaybackTexture == null
            && DuelKillReplay.BufferedFrameCount == 0 && DuelKillReplay.Recap.HasValue,
            "Skipping replay releases all frame textures while preserving the elimination recap");
        DuelKillReplay.BeginReplay("", 99, 99, float.NaN, float.PositiveInfinity);
        check(!DuelKillReplay.Active && DuelKillReplay.Recap.HasValue
            && DuelKillReplay.Recap.Damage == 0 && DuelKillReplay.Recap.Distance == 0,
            "Missing replay frames still show a safe recap without invalid damage or distance");
        DuelKillReplay.Clear();
        check(!DuelKillReplay.Active && !DuelKillReplay.Recap.HasValue && DuelKillReplay.BufferedFrameCount == 0,
            "Round, reconnect and leave cleanup removes historical frames and stale damage recap");
        UnityEngine.Object.DestroyImmediate(recorder);
    }
}
