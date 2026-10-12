using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper.Duel
{
    // Six reusable spatial voices belong to the rival, so ending a duel cleans
    // up delayed reports, near misses and impact sounds together.
    public sealed class DuelAudioFeedback : MonoBehaviour
    {
        const int VoiceCount = 6;
        const float SoundSpeed = 343f;
        static DuelAudioFeedback active;
        readonly List<AudioClip> ownedClips = new List<AudioClip>();
        readonly RaycastHit[] queryHits = new RaycastHit[16];
        readonly AudioSource[] voices = new AudioSource[VoiceCount];
        readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[VoiceCount];
        readonly float[] busyUntil = new float[VoiceCount];
        readonly Vector3[] eventPositions = new Vector3[VoiceCount];
        readonly AudioClip[][] footstepClips = new AudioClip[5][];
        AudioClip rifleReport, suppressedReport, nearMiss, hardImpact, metalImpact, waterImpact;
        float footstepDistance, lastFootstepAt;
        int stepIndex;

        public void Initialize()
        {
            if (voices[0] != null) return;
            active = this;
            for (int i = 0; i < VoiceCount; i++)
            {
                var voiceObject = new GameObject("Duel spatial sound " + i);
                voiceObject.transform.SetParent(transform, false);
                var source = voiceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.dopplerLevel = 0f;
                source.spread = 0f;
                voices[i] = source;
                filters[i] = voiceObject.AddComponent<AudioLowPassFilter>();
                filters[i].enabled = false;
            }
            rifleReport = LoadOrCreate("HeavyRifleShot", ProceduralAudio.CreateHeavyRifleShot);
            suppressedReport = LoadOrCreate("SuppressedShot", ProceduralAudio.CreateSuppressedShot);
            nearMiss = LoadOrCreate("BulletWhiz", ProceduralAudio.CreateBulletWhiz);
            hardImpact = CreateImpactClip("Duel concrete strike", 0);
            metalImpact = CreateImpactClip("Duel metal strike", 1);
            waterImpact = CreateImpactClip("Duel water strike", 2);
            string[] surfaces = { "Concrete", "Metal", "Wood", "Dirt", "Gravel" };
            for (int i = 0; i < surfaces.Length; i++)
            {
                footstepClips[i] = new AudioClip[5];
                for (int n = 0; n < 5; n++)
                    footstepClips[i][n] = ProceduralAudio.LoadAsset("Footsteps/Footstep_" + surfaces[i] + "_" + n)
                        ?? ProceduralAudio.LoadAsset("Footstep" + surfaces[i])
                        ?? ProceduralAudio.LoadAsset("FootstepConcrete");
            }
        }

        AudioClip LoadOrCreate(string name, Func<AudioClip> create)
        {
            var clip = ProceduralAudio.LoadAsset(name);
            if (clip != null) return clip;
            clip = create();
            if (clip != null) ownedClips.Add(clip);
            return clip;
        }

        AudioClip CreateImpactClip(string name, int kind)
        {
            const int rate = 22050;
            float duration = kind == 2 ? .3f : kind == 1 ? .22f : .12f;
            float[] samples = new float[(int)(rate * duration)];
            var random = new System.Random(1403 + kind);
            float lowNoise = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                lowNoise += .18f * (noise - lowNoise);
                float signal = kind == 1
                    ? noise * .45f + Mathf.Sin(2f * Mathf.PI * 2450f * t) * .4f + Mathf.Sin(2f * Mathf.PI * 3670f * t) * .15f
                    : kind == 2 ? lowNoise * .6f + noise * .2f : noise * .5f + Mathf.Sin(2f * Mathf.PI * 170f * t) * .4f;
                samples[i] = signal * Mathf.Exp(-t * (kind == 2 ? 12f : kind == 1 ? 25f : 40f)) * .8f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            ownedClips.Add(clip);
            return clip;
        }

        public void PlayGunshot(Vector3 muzzle, int weaponIndex, bool soundMasked)
        {
            var listener = Camera.main;
            if (listener == null) return;
            bool suppressed = weaponIndex == 2;
            float distance = Vector3.Distance(listener.transform.position, muzzle);
            float range = suppressed ? 150f : 600f;
            float volume = suppressed ? .55f : 1f;
            if (soundMasked) { range *= .3f; volume *= .16f; }
            Play(suppressed ? suppressedReport : rifleReport, muzzle, volume, suppressed ? 3f : 14f,
                range, distance / SoundSpeed, 30, 1f);
        }

        public void PlayNearMiss(Vector3 muzzle, Vector3 end)
        {
            var listener = Camera.main;
            if (listener == null || !TryClosestPass(muzzle, end, listener.transform.position, out var pass, out float distance)) return;
            // The same visibility test protects against a crack heard through
            // a wall when the Fire packet precedes its actual impact packet.
            if (Occluded(muzzle, pass)) return;
            float travel = Vector3.Distance(muzzle, pass) / 850f;
            Play(nearMiss, pass, Mathf.Lerp(.95f, .3f, distance / 5f), 1f, 18f, travel, 20, 1f);
        }

        public static bool TryClosestPass(Vector3 muzzle, Vector3 end, Vector3 listener, out Vector3 pass, out float distance)
        {
            Vector3 segment = end - muzzle;
            float lengthSquared = segment.sqrMagnitude;
            pass = muzzle;
            distance = float.PositiveInfinity;
            if (!Finite(muzzle) || !Finite(end) || !Finite(listener) || !float.IsFinite(lengthSquared) || lengthSquared < .001f) return false;
            float fraction = Vector3.Dot(listener - muzzle, segment) / lengthSquared;
            // Do not make passing sounds for a shot traveling away from us or
            // stopping before our position along the ray.
            if (fraction <= 0f || fraction >= 1f) return false;
            pass = muzzle + segment * fraction;
            distance = Vector3.Distance(pass, listener);
            return distance < 5f;
        }

        public void AdvanceFootsteps(Vector3 position, float distance, float speed, bool crouching)
        {
            if (distance < 0f || distance > 3f || speed < .15f)
            {
                footstepDistance = 0;
                return;
            }
            footstepDistance += distance;
            bool running = speed > 2.8f && !crouching;
            float stride = crouching ? .85f : running ? 1.7f : 1.1f;
            if (footstepDistance < stride || Time.unscaledTime - lastFootstepAt < .18f) return;
            footstepDistance %= stride;
            lastFootstepAt = Time.unscaledTime;
            var listener = Camera.main;
            if (listener == null || Vector3.SqrMagnitude(listener.transform.position - position) > 48f * 48f) return;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * .35f, Vector3.down, queryHits, .95f, ~0, QueryTriggerInteraction.Ignore);
            Collider floor = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = queryHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                floor = hit.collider;
                nearest = hit.distance;
            }
            if (floor == null) return;
            string surface = floor.name.ToLowerInvariant();
            int kind = surface.Contains("metal") || surface.Contains("ladder") || surface.Contains("steel") ? 1
                : surface.Contains("wood") ? 2
                : surface.Contains("mud") || surface.Contains("dirt") || surface.Contains("terrain") ? 3
                : surface.Contains("gravel") || surface.Contains("cobble") ? 4 : 0;
            var clip = footstepClips[kind][stepIndex++ % 5];
            Play(clip, position + transform.right * (stepIndex % 2 == 0 ? -.12f : .12f),
                crouching ? .22f : running ? .8f : .5f, 2f, crouching ? 18f : 48f, 0f, 110,
                stepIndex % 2 == 0 ? .98f : 1.02f);
        }

        public static void PlaySurfaceImpact(Vector3 point, byte kind)
        {
            if (active == null || kind > 2 || !Finite(point)) return;
            active.Play(kind == 1 ? active.metalImpact : kind == 2 ? active.waterImpact : active.hardImpact,
                point + Vector3.up * .025f, .65f, 2f, 80f, 0f, 80, 1f);
        }

        void Play(AudioClip clip, Vector3 position, float volume, float minDistance, float maxDistance, float delay, int priority, float pitch)
        {
            var listener = Camera.main;
            if (clip == null || listener == null || !Finite(position) || Vector3.SqrMagnitude(position - listener.transform.position) > maxDistance * maxDistance) return;
            int index = -1;
            for (int i = 0; i < VoiceCount; i++)
                if (voices[i] != null && Time.unscaledTime >= busyUntil[i]) { index = i; break; }
            if (index < 0)
            {
                // A footstep never steals an active rifle report or near miss.
                int worstPriority = priority;
                for (int i = 0; i < VoiceCount; i++)
                    if (voices[i] != null && voices[i].priority > worstPriority) { worstPriority = voices[i].priority; index = i; }
            }
            if (index < 0) return; // Bound voice count even during rapid fire.
            var source = voices[index];
            source.Stop();
            source.transform.position = position;
            eventPositions[index] = position;
            source.clip = clip;
            source.priority = priority;
            source.pitch = pitch;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            bool blocked = Occluded(listener.transform.position, position);
            filters[index].enabled = blocked;
            filters[index].cutoffFrequency = blocked ? 1300f : 22000f;
            source.volume = volume * (blocked ? .4f : 1f);
            delay = Mathf.Clamp(delay, 0f, 2f);
            busyUntil[index] = Time.unscaledTime + delay + clip.length / pitch + .02f;
            source.PlayDelayed(delay);
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        void LateUpdate()
        {
            // Voices stay at the event's world position although their owning
            // avatar keeps moving while a distant report is traveling to us.
            for (int i = 0; i < VoiceCount; i++)
                if (voices[i] != null && Time.unscaledTime < busyUntil[i]) voices[i].transform.position = eventPositions[i];
        }

        bool Occluded(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < .3f) return false;
            int count = Physics.RaycastNonAlloc(from, delta / length, queryHits, length - .15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = queryHits[i].collider;
                if (collider != null && !collider.transform.IsChildOf(transform)
                    && collider.GetComponentInParent<CombatActor>() == null) return true;
            }
            return count == queryHits.Length;
        }

        public void ResetFootsteps()
        {
            footstepDistance = 0;
            lastFootstepAt = Time.unscaledTime;
            stepIndex = 0;
        }

        private void OnDestroy()
        {
            if (active == this) active = null;
            foreach (var clip in ownedClips)
                if (clip != null)
                {
                    if (Application.isPlaying) Destroy(clip);
                    else DestroyImmediate(clip);
                }
        }
    }
}
