using UnityEngine;

namespace GeoSniper
{
    /// <summary>
    /// Coordinates half-duplex tactical radio network comms.
    /// Guarantees that only ONE radio / voice message plays at a time globally,
    /// enforces radio silence intervals between transmissions, prevents overlapping voices,
    /// and ensures non-repeating clip selection.
    /// </summary>
    public static class RadioCommsChannel
    {
        public enum Priority
        {
            Low = 0,     // Ambient squad patrol murmur / chatter
            Medium = 1,  // Urgent combat & alert barks
            High = 2     // HQ Mission Dispatch / Command Orders
        }

        private static float _channelBusyUntil = -1f;
        private static float _lastTransmissionEndTime = -1f;
        private static Priority _currentPriority = Priority.Low;
        private static AudioSource _currentSpeaker = null;

        // Anti-repetition indices
        private static int _lastHqIndex = -1;
        private static int _lastPatrolIndex = -1;
        private static int _lastAlertIndex = -1;
        private static int _lastCombatIndex = -1;

        // Mission dispatch flag (ensures HQ only dispatches once per mission)
        private static bool _hqDispatched = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetChannel()
        {
            _channelBusyUntil = -1f;
            _lastTransmissionEndTime = -1f;
            _currentPriority = Priority.Low;
            _currentSpeaker = null;
            _lastHqIndex = -1;
            _lastPatrolIndex = -1;
            _lastAlertIndex = -1;
            _lastCombatIndex = -1;
            _hqDispatched = false;
        }

        public static void ResetMissionDispatch()
        {
            _hqDispatched = false;
        }

        public static bool IsChannelBusy => Time.time < _channelBusyUntil;

        public static bool CanTransmit(Priority priority, float requiredSilence = 2.0f)
        {
            // If channel is currently broadcasting
            if (IsChannelBusy)
            {
                // Command and extraction calls must not be lost to a one-shot enemy bark.
                if (priority == Priority.High && _currentPriority < Priority.High)
                {
                    if (_currentSpeaker != null && _currentSpeaker.isPlaying)
                    {
                        _currentSpeaker.Stop();
                    }
                    return true;
                }
                return false;
            }

            // Enforce minimum radio silence interval after the last transmission ended
            if (Time.time < _lastTransmissionEndTime + requiredSilence)
            {
                return priority == Priority.High && _currentPriority < Priority.High;
            }

            return true;
        }

        public static bool PlayTransmission(AudioSource speaker, AudioClip clip, Priority priority, float volume = 1f, float pitch = 1f, float silenceCooldownAfter = 3.5f)
        {
            if (speaker == null || clip == null) return false;

            if (!CanTransmit(priority, silenceCooldownAfter > 0 ? 1.0f : 0f))
            {
                return false;
            }

            speaker.pitch = pitch;
            speaker.PlayOneShot(clip, volume);

            _currentSpeaker = speaker;
            _currentPriority = priority;
            _channelBusyUntil = Time.time + clip.length;
            _lastTransmissionEndTime = _channelBusyUntil + silenceCooldownAfter;
            return true;
        }

        // --- Anti-Repetition Clip Selectors ---

        public static AudioClip GetNextHQDispatchClip()
        {
            int count = 3;
            int nextIdx = Random.Range(0, count);
            if (nextIdx == _lastHqIndex) nextIdx = (nextIdx + 1) % count;
            _lastHqIndex = nextIdx;

            return ProceduralAudio.LoadAsset("HQDispatch_" + nextIdx) 
                ?? ProceduralAudio.LoadAsset("HQDispatch") 
                ?? ProceduralAudio.LoadAsset("ExtractionRadio");
        }

        public static AudioClip GetNextPatrolChatterClip()
        {
            int count = 4;
            int nextIdx = Random.Range(0, count);
            if (nextIdx == _lastPatrolIndex) nextIdx = (nextIdx + 1) % count;
            _lastPatrolIndex = nextIdx;

            return ProceduralAudio.LoadAsset("EnemyChatter_" + nextIdx) 
                ?? ProceduralAudio.LoadAsset("EnemyChatter_0");
        }

        public static AudioClip GetNextAlertBarkClip()
        {
            int count = 3;
            int nextIdx = Random.Range(0, count);
            if (nextIdx == _lastAlertIndex) nextIdx = (nextIdx + 1) % count;
            _lastAlertIndex = nextIdx;

            return ProceduralAudio.LoadAsset("EnemyAlert_" + nextIdx) 
                ?? ProceduralAudio.LoadAsset("EnemyAlert_0");
        }

        public static AudioClip GetNextCombatBarkClip()
        {
            int count = 3;
            int nextIdx = Random.Range(0, count);
            if (nextIdx == _lastCombatIndex) nextIdx = (nextIdx + 1) % count;
            _lastCombatIndex = nextIdx;

            return ProceduralAudio.LoadAsset("EnemyCombat_" + nextIdx) 
                ?? ProceduralAudio.LoadAsset("EnemyCombat_0");
        }

        public static bool TryPlayHQDispatch(AudioSource speaker, out string caption, out AudioClip playedClip)
        {
            caption = null;
            playedClip = null;
            if (_hqDispatched) return false;
            if (speaker == null) return false;

            var clip = GetNextHQDispatchClip();
            if (clip == null) return false;

            if (PlayTransmission(speaker, clip, Priority.High, volume: 1.0f, pitch: 1.0f, silenceCooldownAfter: 4.5f))
            {
                _hqDispatched = true;
                playedClip = clip;
                caption = clip.name.Contains("HQDispatch_1") ? "Vanguard, green to engage. Maintain radio discipline."
                    : clip.name.Contains("HQDispatch_2") ? "All stations, enemy patrols in the district. Stay sharp."
                    : "Command to Vanguard. Proceed to waypoint and identify targets.";
                return true;
            }
            return false;
        }

        public static bool TryPlayHQDispatch(AudioSource speaker)
        {
            return TryPlayHQDispatch(speaker, out _, out _);
        }
    }
}
