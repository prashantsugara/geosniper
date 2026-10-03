using System;
using UnityEngine;

namespace GeoSniper
{
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> _clipCache = new System.Collections.Generic.Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCache() => _clipCache.Clear();

        public static AudioClip LoadAsset(string name)
        {
            if (_clipCache.TryGetValue(name, out var cached) && cached != null)
                return cached;

            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip != null)
            {
                _clipCache[name] = clip;
                return clip;
            }
            return null;
        }

        public static AudioClip CreateHeadshotPing()
        {
            var loaded = LoadAsset("HeadshotPing");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.45f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // High frequency metallic chime (2400Hz + 4800Hz overtone) with exponential decay
                float ping = (Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 4800f * t) * 0.4f);
                float env = Mathf.Exp(-14f * t);
                data[i] = ping * env * 0.8f;
            }

            AudioClip clip = AudioClip.Create("HeadshotPing", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            _clipCache["HeadshotPing"] = clip;
            return clip;
        }

        public static AudioClip CreateBodyHitThud()
        {
            var loaded = LoadAsset("BodyHitThud");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.25f);
            float[] data = new float[samples];
            var rng = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float thud = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.8f + noise * 0.4f;
                float env = Mathf.Exp(-20f * t);
                data[i] = thud * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create("BodyHitThud", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            _clipCache["BodyHitThud"] = clip;
            return clip;
        }

        public static AudioClip CreateBulletWhiz()
        {
            var loaded = LoadAsset("BulletWhiz");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.30f);
            float[] data = new float[samples];
            var rng = new System.Random(1337);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                // Frequency sweep from 1800Hz down to 600Hz (Doppler whiz effect)
                float freq = Mathf.Lerp(1800f, 600f, t / 0.30f);
                float whiz = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + noise * 0.5f;
                float env = Mathf.Sin(Mathf.PI * (t / 0.30f)); // Bell curve envelope
                data[i] = whiz * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("BulletWhiz", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateThermalToggle()
        {
            int samples = (int)(SampleRate * 0.20f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // High-tech ascending dual frequency sweep (1200Hz to 2200Hz)
                float freq = Mathf.Lerp(1200f, 2200f, t / 0.20f);
                float beep = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
                float env = 1f - (t / 0.20f);
                data[i] = beep * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("ThermalToggle", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateHeavyRifleShot()
        {
            var loaded = LoadAsset("HeavyRifleShot");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.75f);
            float[] data = new float[samples];
            var rng = new System.Random(999);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float bassBoom = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.9f;
                float crack = noise * 0.8f;
                float env = Mathf.Exp(-8f * t);
                data[i] = Mathf.Clamp((bassBoom + crack) * env, -1f, 1f) * 0.9f;
            }

            AudioClip clip = AudioClip.Create("HeavyRifleShot", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateHeartbeat()
        {
            var loaded = LoadAsset("Heartbeat");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.65f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // First beat (lub) at t = 0
                if (t >= 0f && t < 0.25f)
                {
                    float env = Mathf.Sin(Mathf.PI * (t / 0.25f));
                    float tone = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.3f;
                    sample += tone * env * 0.85f;
                }
                // Second beat (dub) at t = 0.16s
                float t2 = t - 0.16f;
                if (t2 >= 0f && t2 < 0.22f)
                {
                    float env = Mathf.Sin(Mathf.PI * (t2 / 0.22f));
                    float tone = Mathf.Sin(2f * Mathf.PI * 65f * t2) * 0.6f + Mathf.Sin(2f * Mathf.PI * 95f * t2) * 0.25f;
                    sample += tone * env * 0.75f;
                }

                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SniperHeartbeat", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateVehicleExplosion()
        {
            var loaded = LoadAsset("VehicleExplosion");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 1.6f);
            float[] data = new float[samples];
            var rng = new System.Random(777);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                // Initial detonation shockwave (crack + sub-bass drop 90Hz -> 32Hz)
                float sweepFreq = Mathf.Lerp(90f, 32f, Mathf.Clamp01(t / 0.4f));
                float subBass = Mathf.Sin(2f * Mathf.PI * sweepFreq * t) * Mathf.Exp(-4.5f * t) * 0.95f;
                // Debris and fire rumble
                float rumble = noise * Mathf.Exp(-3.2f * t) * 0.75f;
                // Lingering low-frequency atmospheric roll
                float roll = Mathf.Sin(2f * Mathf.PI * 28f * t) * Mathf.Exp(-1.8f * t) * 0.45f;
                data[i] = Mathf.Clamp(subBass + rumble + roll, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("VehicleExplosion", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateSuppressedShot()
        {
            var loaded = LoadAsset("SuppressedShot");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.28f);
            float[] data = new float[samples];
            var rng = new System.Random(404);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                // Mechanical bolt click
                float click = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-90f * t) * 0.45f;
                // Suppressed gas expansion hiss through baffles
                float hiss = noise * Mathf.Exp(-22f * t) * 0.65f;
                // Low thud
                float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-35f * t) * 0.5f;
                data[i] = Mathf.Clamp(click + hiss + thud, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("SuppressedShot", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateAmbientMusic()
        {
            int samples=(int)(SampleRate*8f); float[] data=new float[samples];
            float[] notes={110f,146.83f,164.81f,220f};
            for(int i=0;i<samples;i++)
            {
                float t=(float)i/SampleRate, fade=Mathf.Min(1f,Mathf.Min(t,8f-t)*2f);
                float pulse=.5f+.5f*Mathf.Sin(2f*Mathf.PI*.18f*t);
                float tone=Mathf.Sin(2f*Mathf.PI*notes[0]*t)*.20f+Mathf.Sin(2f*Mathf.PI*notes[1]*t)*.11f;
                tone+=Mathf.Sin(2f*Mathf.PI*notes[2]*t)*.08f+Mathf.Sin(2f*Mathf.PI*notes[3]*t)*.04f;
                data[i]=tone*pulse*fade;
            }
            AudioClip clip=AudioClip.Create("Urban ambient music",samples,1,SampleRate,false);
            clip.SetData(data,0); return clip;
        }

        public static AudioClip CreateVictoryFanfare()
        {
            int samples = (int)(SampleRate * 1.8f);
            float[] data = new float[samples];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            float[] startTimes = { 0.0f, 0.18f, 0.36f, 0.54f };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteT = t - startTimes[n];
                    if (noteT >= 0f)
                    {
                        float decay = (n == 3) ? 2.0f : 3.5f;
                        float env = Mathf.Exp(-decay * noteT);
                        float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * 0.45f
                                   + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * noteT) * 0.25f
                                   + Mathf.Sin(2f * Mathf.PI * notes[n] * 3f * noteT) * 0.12f;
                        sample += tone * env;
                    }
                }

                data[i] = Mathf.Clamp(sample * 0.7f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("VictoryFanfare", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateRadioChirp()
        {
            int samples = (int)(SampleRate * 0.28f);
            float[] data = new float[samples];
            var rng = new System.Random(777);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // Dual-tone digital radio handshake (0 to 65ms)
                if (t < 0.035f)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * 2100f * t) * 0.35f;
                }
                else if (t < 0.070f)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * 1720f * (t - 0.035f)) * 0.35f;
                }

                // Squelch static burst (0.070 to 0.26s)
                if (t >= 0.065f)
                {
                    float tNoise = t - 0.065f;
                    float noise = ((float)rng.NextDouble() * 2f - 1f);
                    float env = Mathf.Exp(-12f * tNoise);
                    // Bandpass filter approximation
                    float hiss = noise * 0.25f + Mathf.Sin(2f * Mathf.PI * 3400f * tNoise) * 0.08f;
                    sample += hiss * env;
                }

                data[i] = Mathf.Clamp(sample * 0.8f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("TacticalRadioChirp", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateSupersonicSnap()
        {
            var loaded = LoadAsset("SupersonicSnap");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.09f);
            float[] data = new float[samples];
            var rng = new System.Random(404);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // Steep sonic crack impulse
                float env = Mathf.Exp(-65f * t);
                float crack = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.7f + ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                data[i] = Mathf.Clamp(crack * env * 0.95f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SupersonicSnap", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateShellClink()
        {
            var loaded = LoadAsset("ShellClink");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.24f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // Primary brass impact (t = 0)
                float env1 = Mathf.Exp(-22f * t);
                sample += (Mathf.Sin(2f * Mathf.PI * 3150f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 5300f * t) * 0.35f) * env1;

                // Secondary bounce (t = 0.08s)
                float t2 = t - 0.08f;
                if (t2 >= 0f)
                {
                    float env2 = Mathf.Exp(-32f * t2);
                    sample += (Mathf.Sin(2f * Mathf.PI * 3600f * t2) * 0.4f + Mathf.Sin(2f * Mathf.PI * 5800f * t2) * 0.2f) * env2;
                }

                data[i] = Mathf.Clamp(sample * 0.75f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ShellClink", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateBoltCycle()
        {
            var loaded = LoadAsset("BoltCycle");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.42f);
            float[] data = new float[samples];
            var rng = new System.Random(999);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // 1. Bolt unlock & pull (0 to 120ms): heavy metallic scrape & click
                if (t < 0.12f)
                {
                    float env = Mathf.Exp(-20f * t);
                    sample += (Mathf.Sin(2f * Mathf.PI * 850f * t) * 0.5f + ((float)rng.NextDouble() * 2f - 1f) * 0.3f) * env;
                }
                // 2. Shell chambering slide (120ms to 240ms)
                else if (t >= 0.12f && t < 0.26f)
                {
                    float t2 = t - 0.12f;
                    float env2 = Mathf.Exp(-15f * t2);
                    sample += (Mathf.Sin(2f * Mathf.PI * 1250f * t2) * 0.45f + ((float)rng.NextDouble() * 2f - 1f) * 0.25f) * env2;
                }
                // 3. Heavy bolt lock snap (260ms to 400ms)
                else if (t >= 0.26f)
                {
                    float t3 = t - 0.26f;
                    float env3 = Mathf.Exp(-28f * t3);
                    sample += (Mathf.Sin(2f * Mathf.PI * 2400f * t3) * 0.7f + Mathf.Sin(2f * Mathf.PI * 1100f * t3) * 0.5f) * env3;
                }

                data[i] = Mathf.Clamp(sample * 0.85f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("BoltCycle", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateAccoladeStinger()
        {
            int samples = (int)(SampleRate * 0.55f);
            float[] data = new float[samples];
            float[] notes = { 440f, 554.37f, 659.25f, 880f }; // A4, C#5, E5, A5 military brass arpeggio

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * 0.08f;
                    if (t >= start)
                    {
                        float dt = t - start;
                        float env = Mathf.Exp(-9f * dt);
                        sample += (Mathf.Sin(2f * Mathf.PI * notes[n] * dt) * 0.4f + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * dt) * 0.15f) * env;
                    }
                }

                data[i] = Mathf.Clamp(sample * 0.8f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("AccoladeStinger", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateKnifeSlash()
        {
            var loaded = LoadAsset("KnifeSlash");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.28f);
            float[] data = new float[samples];
            var rng = new System.Random(1337);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / 0.28f;
                // Fast aerodynamic swoosh (frequency sweep 2200Hz -> 350Hz)
                float sweep = Mathf.Lerp(2200f, 350f, progress);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float env = Mathf.Sin(progress * Mathf.PI);
                float swoosh = (Mathf.Sin(2f * Mathf.PI * sweep * t) * 0.4f + noise * 0.6f) * env;
                // High frequency metallic edge whistle
                float ring = Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-12f * t) * 0.25f;
                data[i] = Mathf.Clamp((swoosh + ring) * 0.85f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("KnifeSlash", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateKnifeStab()
        {
            var loaded = LoadAsset("KnifeStab");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.35f);
            float[] data = new float[samples];
            var rng = new System.Random(2048);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / 0.35f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                // Deep flesh impact thump (110Hz -> 45Hz)
                float lowThump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 45f, progress) * t) * Mathf.Exp(-14f * t) * 0.85f;
                // Sharp blade penetration squelch / slice (1600Hz noise + scrape)
                float squelch = (noise * 0.6f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.4f) * Mathf.Exp(-18f * t) * 0.7f;
                data[i] = Mathf.Clamp(lowThump + squelch, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("KnifeStab", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateSoundMaskRumble()
        {
            var loaded = LoadAsset("SoundMaskRumble");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 3.8f);
            float[] data = new float[samples];
            var rng = new System.Random(4096);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / 3.8f;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float lowFreq = Mathf.Sin(2f * Mathf.PI * 46f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 28f * t) * 0.35f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.2f;
                data[i] = Mathf.Clamp((lowFreq + noise) * envelope * 0.9f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("SoundMaskRumble", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateRicochetMiss()
        {
            var loaded = LoadAsset("RicochetMiss");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 0.38f);
            float[] data = new float[samples];
            var rng = new System.Random(8812);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / 0.38f;
                // High velocity supersonic concrete strike + metallic whine
                float sweepFreq = 2600f + Mathf.Sin(progress * Mathf.PI * 1.5f) * 1200f;
                float whine = Mathf.Sin(2f * Mathf.PI * sweepFreq * t);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float impact = (noise * 0.75f + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.25f) * Mathf.Exp(-32f * t);
                float env = Mathf.Exp(-8f * t);
                data[i] = Mathf.Clamp((impact * 0.85f + whine * env * 0.4f) * 0.85f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("RicochetMiss", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateHelicopterRotorLoop()
        {
            var loaded = LoadAsset("HelicopterRotor");
            if (loaded != null) return loaded;

            // 2.0s loop: 17.0 Hz * 2.0s = 34 exact blade cycles (zero phase click at boundary)
            int samples = SampleRate * 2;
            float[] data = new float[samples];
            var rng = new System.Random(7721);
            float bladeHz = 17.0f;
            float lpNoise = 0f;
            const float alpha = 0.08f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float phase = (bladeHz * t) % 1.0f; // 0 to 1 per blade pulse

                // 1. Sharp acoustic pressure pulse (compression then expansion)
                float thud = 0f;
                if (phase < 0.35f)
                {
                    float p = phase / 0.35f;
                    float env = Mathf.Sin(p * Mathf.PI);
                    thud = (Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 84f * t) * 0.3f) * env;
                }

                // 2. Low-pass aerodynamic blade wash noise
                float white = (float)rng.NextDouble() * 2f - 1f;
                lpNoise += alpha * (white - lpNoise);
                float washEnv = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI)), 2.5f);
                float wash = lpNoise * (washEnv * 0.7f + 0.15f);

                // 3. Subtle dual-spool turbine whine (very quiet, soft resonance)
                float turbineMod = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * bladeHz * t);
                float turbine = (Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.035f + Mathf.Sin(2f * Mathf.PI * 1230f * t) * 0.018f) * turbineMod;

                // 4. Tail rotor high-frequency chop (76.5 Hz)
                float tailPhase = (bladeHz * 4.5f * t) % 1.0f;
                float tailChop = Mathf.Sin(tailPhase * 2f * Mathf.PI) * 0.08f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * bladeHz * t));

                data[i] = Mathf.Clamp((thud * 0.65f + wash * 0.4f + turbine + tailChop) * 0.95f, -1f, 1f);
            }

            // Crossfade 256 samples at loop edges
            int xfade = 256;
            for (int i = 0; i < xfade; i++)
            {
                float blend = (float)i / xfade;
                data[i] = data[i] * blend + data[samples - xfade + i] * (1f - blend);
            }

            AudioClip clip = AudioClip.Create("HelicopterRotor", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateExtractionRadio()
        {
            var loaded = LoadAsset("ExtractionRadio") ?? LoadAsset("HQDispatch");
            return loaded;
        }

        public static AudioClip CreateRainLoop()
        {
            var loaded = LoadAsset("RainLoop");
            if (loaded != null) return loaded;

            int samples = (int)(SampleRate * 2.5f);
            float[] data = new float[samples];
            var rng = new System.Random(5521);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.12f;
                data[i] = Mathf.Clamp(noise + rumble, -1f, 1f) * 0.7f;
            }
            AudioClip clip = AudioClip.Create("RainLoop", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateHQDispatchTransmission()
        {
            return RadioCommsChannel.GetNextHQDispatchClip();
        }

        public static AudioClip CreateFootstep(string surface, bool sprint)
        {
            string surfKey = "Concrete";
            if (!string.IsNullOrEmpty(surface))
            {
                if (surface.IndexOf("mud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    surface.IndexOf("dirt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    surface.IndexOf("terrain", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    surfKey = "Dirt";
                }
                else if (surface.IndexOf("gravel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         surface.IndexOf("cobble", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    surfKey = "Gravel";
                }
                else if (surface.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         surface.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    surfKey = "Metal";
                }
                else if (surface.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    surfKey = "Wood";
                }
            }

            int varIdx = UnityEngine.Random.Range(0, 5);
            AudioClip clip = LoadAsset("Footsteps/Footstep_" + surfKey + "_" + varIdx);
            if (clip == null) clip = LoadAsset("Footstep" + surfKey);
            if (clip == null) clip = LoadAsset("FootstepConcrete");
            return clip;
        }

        public static AudioClip CreateEnemyPatrolChatter(int index = -1)
        {
            if (index >= 0)
            {
                int idx = Mathf.Abs(index) % 4;
                return LoadAsset("EnemyChatter_" + idx) ?? LoadAsset("EnemyChatter_0");
            }
            return RadioCommsChannel.GetNextPatrolChatterClip();
        }

        public static AudioClip CreateEnemyAlertBark(int index = -1)
        {
            if (index >= 0)
            {
                int idx = Mathf.Abs(index) % 3;
                return LoadAsset("EnemyAlert_" + idx) ?? LoadAsset("EnemyAlert_0");
            }
            return RadioCommsChannel.GetNextAlertBarkClip();
        }

        public static AudioClip CreateEnemyCombatBark(int index = -1)
        {
            if (index >= 0)
            {
                int idx = Mathf.Abs(index) % 3;
                return LoadAsset("EnemyCombat_" + idx) ?? LoadAsset("EnemyCombat_0");
            }
            return RadioCommsChannel.GetNextCombatBarkClip();
        }
    }
}

