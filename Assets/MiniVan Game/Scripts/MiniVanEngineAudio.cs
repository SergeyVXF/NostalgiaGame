using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Dual-layer procedural engine audio (idle bed + load growl).
    /// Much softer / more van-like than a raw saw stack.
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniVanEngineAudio : MonoBehaviour
    {
        [SerializeField] private MiniVanVehicle vehicle;
        [SerializeField] private AudioSource idleSource;
        [SerializeField] private AudioSource loadSource;
        [SerializeField] private AudioSource cueSource;
        [SerializeField] private AudioSource oneShotSource;

        [Header("Optional overrides (leave empty to use generated loops)")]
        public AudioClip IdleLoopOverride;
        public AudioClip LoadLoopOverride;
        public AudioClip StarterOverride;
        public AudioClip StallOverride;
        public AudioClip GearShiftOverride;
        public AudioClip HandbrakeOnOverride;
        public AudioClip HandbrakeOffOverride;
        public AudioClip LuggingCueOverride;
        public AudioClip OverrevCueOverride;

        [Header("Mix (0..1 — Unity AudioSource не громче 1)")]
        [Range(0f, 1f)] public float IdleVolume = 0.5f;
        [Range(0f, 1f)] public float MaxLoadVolume = 1f;
        [Range(0f, 1f)] public float StarterVolume = 1f;
        [Range(0f, 1f)] public float StallVolume = 1f;
        [Range(0f, 1f)] public float GearShiftVolume = 1f;
        [Range(0f, 1f)] public float HandbrakeVolume = 1f;
        [Tooltip("Overall loudness of generated loops. Keep ~1.5–3 for clean sound; high values distort.")]
        [Range(0.5f, 8f)] public float ClipGain = 2.4f;
        [Header("Shift-by-ear cues")]
        [Range(0f, 1f)] public float LuggingCueVolume = 0.7f;
        [Range(0f, 1f)] public float OverrevCueVolume = 0.9f;
        public float IdleMinPitch = 0.85f;
        public float IdleMaxPitch = 1.45f;
        public float LoadMinPitch = 0.9f;
        public float LoadMaxPitch = 1.7f;
        public float VolumeLerpSpeed = 8f;
        public float PitchLerpSpeed = 7f;
        [Range(0f, 1f)] public float SpatialBlend = 0.12f;
        public float MinDistance = 22f;
        public float MaxDistance = 90f;

        private static AudioClip sharedIdleClip;
        private static AudioClip sharedLoadClip;
        private static AudioClip sharedStarterClip;
        private static AudioClip sharedShiftClip;
        private static AudioClip sharedLuggingClip;
        private static AudioClip sharedOverrevClip;
        private static float sharedClipGain = -1f;
        private const int SynthRevision = 3;
        private static int sharedSynthRevision = -1;

        private float idleVolume;
        private float loadVolume;
        private float cueVolume;
        private float idlePitch = 1f;
        private float loadPitch = 1f;
        private float cuePitch = 1f;
        private bool wasEngineOn;
        private bool hasGearSample;
        private int lastGearValue;
        private bool hasHandbrakeSample;
        private bool lastHandbrakeLocked;

        private void Awake()
        {
            vehicle = vehicle != null ? vehicle : GetComponent<MiniVanVehicle>();
            EnsureAudioSources();
        }

        private void OnDisable()
        {
            StopSources();
        }

        public void Tick()
        {
            if (vehicle == null)
            {
                vehicle = GetComponent<MiniVanVehicle>();
            }

            EnsureAudioSources();
            if (vehicle == null || idleSource == null || loadSource == null)
            {
                return;
            }

            EnsureGeneratedClips();
            AssignClips();

            bool engineOn = vehicle.EngineOn.Value;
            float rpm = Mathf.Max(0f, vehicle.EngineRpm.Value);
            float load = Mathf.Clamp01(vehicle.EngineLoad.Value);
            float sweetSpot = Mathf.Clamp01(vehicle.EngineSweetSpot01.Value);
            float idleRpm = Mathf.Max(100f, vehicle.IdleRpm);
            float redline = Mathf.Max(idleRpm + 100f, vehicle.RedlineRpm);
            float rpm01 = Mathf.Clamp01(Mathf.InverseLerp(idleRpm * 0.4f, redline, rpm));

            float targetIdleVol = 0f;
            float targetLoadVol = 0f;
            float targetIdlePitch = IdleMinPitch;
            float targetLoadPitch = LoadMinPitch;

            float center = Mathf.Clamp(vehicle.SweetSpotCenterRpm01, 0.15f, 0.85f);
            float halfWidth = Mathf.Max(0.04f, vehicle.SweetSpotHalfWidthRpm01);
            float bandLow = center - halfWidth;
            float bandHigh = center + halfWidth;
            float luggingAmount = 0f;
            float overrevAmount = 0f;

            if (engineOn && rpm > 40f)
            {
                // Idle bed always present while running.
                // Load layer follows the pedal immediately — RPM only adds a little extra body.
                targetIdleVol = Mathf.Lerp(IdleVolume * 0.75f, IdleVolume, 1f - load * 0.35f);
                float pedal = Mathf.Clamp01(load);
                float loadAmount = Mathf.Clamp01(pedal * 0.92f + rpm01 * 0.18f);
                // Soft ease (not squared) so light throttle already opens the load layer.
                float loadCurve = loadAmount * (0.35f + 0.65f * loadAmount);
                targetLoadVol = Mathf.Lerp(0f, MaxLoadVolume, loadCurve);
                // Guarantee audible reaction as soon as gas is pressed, even at crawl RPM on 1st.
                if (pedal > 0.08f)
                {
                    targetLoadVol = Mathf.Max(targetLoadVol, MaxLoadVolume * Mathf.Lerp(0.35f, 0.85f, pedal));
                }

                targetIdlePitch = Mathf.Lerp(IdleMinPitch, IdleMaxPitch, rpm01);
                targetLoadPitch = Mathf.Lerp(LoadMinPitch, LoadMaxPitch, Mathf.Max(rpm01, pedal * 0.35f));
                targetLoadPitch *= Mathf.Lerp(1f, 1.05f, load);

                // Clear "shift by ear" cues: strain below the band, scream above it.
                float throttlePresence = Mathf.Clamp01(Mathf.Max(load, 0.2f));
                if (rpm01 < bandLow)
                {
                    luggingAmount = Mathf.Clamp01(Mathf.InverseLerp(bandLow, bandLow * 0.25f, rpm01)) * throttlePresence;
                }
                else if (rpm01 > bandHigh)
                {
                    // Fast onset right after leaving the green band, then fill toward redline.
                    float t = Mathf.Clamp01(Mathf.InverseLerp(bandHigh, 0.9f, rpm01));
                    overrevAmount = Mathf.Clamp01(Mathf.Pow(t, 0.4f));
                }

                if (luggingAmount > 0.01f)
                {
                    // Keep load audible while pedaling — cue layer carries the "struggling" color.
                    float wobble = Mathf.Sin(Time.time * 17f) * 0.055f * luggingAmount;
                    targetIdlePitch *= Mathf.Lerp(1f, 0.78f, luggingAmount) + wobble;
                    targetLoadPitch *= Mathf.Lerp(1f, 0.9f, luggingAmount) + wobble * 0.6f;
                    targetIdleVol = Mathf.Min(1f, targetIdleVol + luggingAmount * 0.12f);
                }
                else if (overrevAmount > 0.01f)
                {
                    // Busier as soon as you leave green — cue + slight load thicken.
                    targetLoadPitch *= Mathf.Lerp(1f, 1.08f, overrevAmount);
                    targetIdlePitch *= Mathf.Lerp(1f, 1.03f, overrevAmount);
                    targetLoadVol = Mathf.Min(1f, targetLoadVol + overrevAmount * 0.12f);
                    targetIdleVol *= Mathf.Lerp(1f, 0.9f, overrevAmount);
                }
                else if (load > 0.15f && sweetSpot > 0.35f)
                {
                    float band = Mathf.InverseLerp(0.35f, 1f, sweetSpot) * load;
                    targetLoadVol = Mathf.Min(MaxLoadVolume * 1.15f, targetLoadVol + band * 0.08f);
                    targetIdleVol *= Mathf.Lerp(1f, 0.92f, band);
                }
            }

            float dt = Time.deltaTime;
            // Volumes are 0..1. Values above 1 in the inspector used to do nothing (Unity clamps AudioSource.volume).
            float targetIdleClamped = Mathf.Clamp01(targetIdleVol);
            float targetLoadClamped = Mathf.Clamp01(targetLoadVol);
            idleVolume = Mathf.MoveTowards(idleVolume, targetIdleClamped, VolumeLerpSpeed * dt);
            // Snap load up faster than down so gas feels instant.
            float loadLerp = targetLoadClamped > loadVolume ? VolumeLerpSpeed * 2.4f : VolumeLerpSpeed;
            loadVolume = Mathf.MoveTowards(loadVolume, targetLoadClamped, loadLerp * dt);
            idlePitch = Mathf.MoveTowards(idlePitch, targetIdlePitch, PitchLerpSpeed * dt);
            loadPitch = Mathf.MoveTowards(loadPitch, targetLoadPitch, PitchLerpSpeed * dt);

            idleSource.volume = Mathf.Clamp01(idleVolume);
            loadSource.volume = Mathf.Clamp01(loadVolume);
            idleSource.pitch = idlePitch;
            loadSource.pitch = loadPitch;

            UpdateRpmCue(engineOn, luggingAmount, overrevAmount, rpm01, dt);

            bool wantPlay = engineOn && (idleVolume > 0.008f || loadVolume > 0.008f || cueVolume > 0.008f);
            if (wantPlay)
            {
                if (!idleSource.isPlaying)
                {
                    idleSource.Play();
                }

                if (!loadSource.isPlaying)
                {
                    loadSource.Play();
                }
            }
            else
            {
                StopSources();
            }

            if (engineOn && !wasEngineOn)
            {
                PlayEngineOneShot(StarterOverride != null ? StarterOverride : sharedStarterClip, StarterVolume, createStarterFallback: true);
            }
            else if (!engineOn && wasEngineOn)
            {
                // Stall / key-off — only if a clip is assigned (no procedural fallback).
                if (StallOverride != null)
                {
                    PlayEngineOneShot(StallOverride, StallVolume, createStarterFallback: false);
                }
            }

            wasEngineOn = engineOn;
            UpdateGearShiftSound(engineOn);
            UpdateHandbrakeSound();
        }

        private void UpdateHandbrakeSound()
        {
            bool locked = vehicle.HandbrakeLocked.Value;
            if (!hasHandbrakeSample)
            {
                lastHandbrakeLocked = locked;
                hasHandbrakeSample = true;
                return;
            }

            if (locked == lastHandbrakeLocked)
            {
                return;
            }

            lastHandbrakeLocked = locked;
            AudioClip clip = locked ? HandbrakeOnOverride : HandbrakeOffOverride;
            if (clip != null)
            {
                PlayEngineOneShot(clip, HandbrakeVolume, createStarterFallback: false);
            }
        }

        private void UpdateGearShiftSound(bool engineOn)
        {
            int gear = vehicle.CurrentGear.Value;
            if (!hasGearSample)
            {
                lastGearValue = gear;
                hasGearSample = true;
                return;
            }

            if (gear == lastGearValue)
            {
                return;
            }

            int previous = lastGearValue;
            lastGearValue = gear;
            if (!engineOn)
            {
                return;
            }

            // Any shift that touches a drive gear (incl. into/out of N while rolling or crawling).
            if (!IsForwardGear(previous) && !IsForwardGear(gear) &&
                previous != (int)MiniVanGear.Reverse && gear != (int)MiniVanGear.Reverse)
            {
                return;
            }

            AudioClip shiftClip = GearShiftOverride != null ? GearShiftOverride : sharedShiftClip;
            if (shiftClip == null)
            {
                sharedShiftClip = CreateGearShiftClip(Mathf.Clamp(ClipGain, 0.5f, 8f));
                shiftClip = sharedShiftClip;
            }

            // Play on the van's one-shot source (PlayClipAtPoint was fully 3D and nearly inaudible in cabin).
            PlayEngineOneShot(shiftClip, GearShiftVolume, createStarterFallback: false);
        }

        private void PlayEngineOneShot(AudioClip clip, float volume, bool createStarterFallback)
        {
            if (clip == null && createStarterFallback)
            {
                sharedStarterClip = CreateStarterClip(Mathf.Clamp(ClipGain, 0.5f, 8f));
                clip = sharedStarterClip;
            }

            if (clip == null || oneShotSource == null)
            {
                return;
            }

            oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void UpdateRpmCue(bool engineOn, float luggingAmount, float overrevAmount, float rpm01, float dt)
        {
            if (cueSource == null)
            {
                return;
            }

            float targetCueVol = 0f;
            float targetCuePitch = 1f;
            AudioClip wanted = null;

            if (engineOn)
            {
                if (luggingAmount > overrevAmount && luggingAmount > 0.04f)
                {
                    wanted = LuggingCueOverride != null ? LuggingCueOverride : sharedLuggingClip;
                    targetCueVol = Mathf.Clamp01(luggingAmount * LuggingCueVolume);
                    targetCuePitch = Mathf.Lerp(0.78f, 1.05f, rpm01);
                }
                else if (overrevAmount > 0.015f)
                {
                    wanted = OverrevCueOverride != null ? OverrevCueOverride : sharedOverrevClip;
                    // Audible immediately out of green (~35% cue), then grows toward redline.
                    float presence = Mathf.Lerp(0.35f, 1f, overrevAmount);
                    targetCueVol = Mathf.Clamp01(presence * OverrevCueVolume);
                    // Keep cue near unity pitch — high AudioSource.pitch turned the old tones into a scream.
                    targetCuePitch = Mathf.Lerp(0.97f, 1.06f, overrevAmount);
                }
            }

            if (wanted != null && cueSource.clip != wanted)
            {
                bool wasPlaying = cueSource.isPlaying;
                cueSource.clip = wanted;
                if (wasPlaying || targetCueVol > 0.02f)
                {
                    cueSource.Play();
                }
            }

            cueVolume = Mathf.MoveTowards(cueVolume, targetCueVol, VolumeLerpSpeed * dt);
            cuePitch = Mathf.MoveTowards(cuePitch, targetCuePitch, PitchLerpSpeed * dt);
            cueSource.volume = Mathf.Clamp01(cueVolume);
            cueSource.pitch = cuePitch;

            if (cueVolume > 0.01f)
            {
                if (!cueSource.isPlaying && cueSource.clip != null)
                {
                    cueSource.Play();
                }
            }
            else if (cueSource.isPlaying)
            {
                cueSource.Stop();
            }
        }

        private static bool IsForwardGear(int gearValue)
        {
            MiniVanGear gear = (MiniVanGear)gearValue;
            return MiniVanGearUtility.IsForward(gear);
        }

        private void EnsureAudioSources()
        {
            if (idleSource == null)
            {
                idleSource = FindOrCreateSource("EngineAudio Idle");
            }

            if (loadSource == null)
            {
                loadSource = FindOrCreateSource("EngineAudio Load");
            }

            if (cueSource == null)
            {
                cueSource = FindOrCreateSource("EngineAudio Cue");
            }

            if (oneShotSource == null)
            {
                oneShotSource = FindOrCreateSource("EngineAudio OneShot");
            }

            ConfigureSource(idleSource);
            ConfigureSource(loadSource);
            ConfigureSource(cueSource);
            ConfigureOneShotSource(oneShotSource);
            cueSource.priority = 40;
        }

        private void ConfigureOneShotSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            // Almost 2D in cabin so starter/shift cut through clearly.
            source.spatialBlend = Mathf.Min(0.15f, SpatialBlend);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(MinDistance, 25f);
            source.maxDistance = Mathf.Max(MaxDistance, 90f);
            source.dopplerLevel = 0f;
            source.priority = 32;
            source.spread = 60f;
            source.volume = 1f;
        }

        private AudioSource FindOrCreateSource(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null)
            {
                source = child.gameObject.AddComponent<AudioSource>();
            }

            return source;
        }

        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = SpatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.dopplerLevel = 0.08f;
            source.priority = 48;
            source.spread = 35f;
        }

        private void EnsureGeneratedClips()
        {
            float gain = Mathf.Clamp(ClipGain, 0.5f, 8f);
            if (sharedIdleClip == null ||
                !Mathf.Approximately(sharedClipGain, gain) ||
                sharedSynthRevision != SynthRevision)
            {
                sharedClipGain = gain;
                sharedSynthRevision = SynthRevision;
                sharedIdleClip = CreateIdleLoopClip(gain);
                sharedLoadClip = CreateLoadLoopClip(gain);
                sharedStarterClip = CreateStarterClip(gain);
                sharedShiftClip = CreateGearShiftClip(gain);
                sharedLuggingClip = CreateLuggingCueClip(gain);
                sharedOverrevClip = CreateOverrevCueClip(gain);
            }

            if (sharedLuggingClip == null)
            {
                sharedLuggingClip = CreateLuggingCueClip(gain);
            }

            if (sharedOverrevClip == null)
            {
                sharedOverrevClip = CreateOverrevCueClip(gain);
            }
        }

        private void AssignClips()
        {
            AudioClip idleClip = IdleLoopOverride != null ? IdleLoopOverride : sharedIdleClip;
            AudioClip loadClip = LoadLoopOverride != null ? LoadLoopOverride : sharedLoadClip;

            if (idleSource.clip != idleClip)
            {
                bool wasPlaying = idleSource.isPlaying;
                idleSource.clip = idleClip;
                if (wasPlaying)
                {
                    idleSource.Play();
                }
            }

            if (loadSource.clip != loadClip)
            {
                bool wasPlaying = loadSource.isPlaying;
                loadSource.clip = loadClip;
                if (wasPlaying)
                {
                    loadSource.Play();
                }
            }
        }

        private void StopSources()
        {
            if (idleSource != null && idleSource.isPlaying)
            {
                idleSource.Stop();
            }

            if (loadSource != null && loadSource.isPlaying)
            {
                loadSource.Stop();
            }

            if (cueSource != null && cueSource.isPlaying)
            {
                cueSource.Stop();
            }

            cueVolume = 0f;
        }

        // --- Synthesis -------------------------------------------------------
        // Clean harmonic engine bed (not pulse/fart synthesis). Loudness via normalize + mild gain.

        private static AudioClip CreateIdleLoopClip(float gain)
        {
            const int sampleRate = 44100;
            const float duration = 2f; // longer loop = less obvious seam
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float noiseLp = 0f;
            float bodyLp = 0f;

            // Fundamental ~40 Hz: warm van idle, integer cycles over 2s.
            const float f0 = 40f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float harm = SoftSawHarmonic(t, f0, 0.55f) * 0.42f
                           + SoftSawHarmonic(t, f0 * 2f, 0.4f) * 0.22f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 3f * t) * 0.1f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 4f * t) * 0.05f;

                // Very light muffler air — kept low so it doesn't rasp.
                float white = HashNoise(i) * 0.5f + HashNoise(i * 2 + 11) * 0.5f;
                noiseLp = Mathf.Lerp(noiseLp, white, 0.04f);
                float air = noiseLp * 0.06f;

                // Slow amplitude "breath" so idle isn't a dead tone.
                float breath = 0.92f + 0.08f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);
                float raw = (harm + air) * breath;
                bodyLp = Mathf.Lerp(bodyLp, raw, 0.18f);
                samples[i] = bodyLp;
            }

            FinalizeLoop(samples, gain * 0.85f, sampleRate);
            AudioClip clip = AudioClip.Create("MiniVan Engine Idle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateLoadLoopClip(float gain)
        {
            const int sampleRate = 44100;
            const float duration = 2f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float noiseLp = 0f;
            float bodyLp = 0f;

            // Higher fundamental + more mid harmonics = working engine, still clean.
            const float f0 = 58f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float harm = SoftSawHarmonic(t, f0, 0.5f) * 0.38f
                           + SoftSawHarmonic(t, f0 * 2f, 0.38f) * 0.24f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 3f * t) * 0.14f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 5f * t) * 0.07f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 7f * t) * 0.035f;

                float white = HashNoise(i + 91) * 0.55f + HashNoise(i * 3 + 7) * 0.45f;
                noiseLp = Mathf.Lerp(noiseLp, white, 0.07f);
                float exhaust = noiseLp * 0.1f;

                float raw = harm + exhaust;
                bodyLp = Mathf.Lerp(bodyLp, raw, 0.22f);
                samples[i] = bodyLp;
            }

            FinalizeLoop(samples, gain, sampleRate);
            AudioClip clip = AudioClip.Create("MiniVan Engine Load", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateStarterClip(float gain)
        {
            const int sampleRate = 44100;
            const float duration = 0.42f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float noiseLp = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float u = t / duration;
                float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((duration - t) / 0.14f);
                float hz = Mathf.Lerp(32f, 64f, u * u);
                float crank = SoftSawHarmonic(t, hz, 0.45f) * 0.55f
                            + Mathf.Sin(2f * Mathf.PI * hz * 2f * t) * 0.2f;
                noiseLp = Mathf.Lerp(noiseLp, HashNoise(i + 200), 0.12f);
                samples[i] = (crank + noiseLp * 0.08f) * envelope;
            }

            NormalizePeak(samples, Mathf.Clamp(gain * 0.7f, 0.4f, 1.2f));
            AudioClip clip = AudioClip.Create("MiniVan Engine Starter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateGearShiftClip(float gain)
        {
            const int sampleRate = 44100;
            const float duration = 0.1f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((duration - t) / 0.05f);
                float thunk = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.55f
                            + Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.22f
                            + HashNoise(i + 400) * 0.1f * Mathf.Exp(-t * 40f);
                samples[i] = thunk * envelope;
            }

            NormalizePeak(samples, Mathf.Clamp(gain * 0.9f, 0.5f, 1.3f));
            AudioClip clip = AudioClip.Create("MiniVan Gear Shift", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateLuggingCueClip(float gain)
        {
            const int sampleRate = 44100;
            const float duration = 2f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float lp = 0f;

            // Slow, uneven low rumble — "engine struggling", not toilet thumps.
            const float f0 = 28f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float wobble = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 3.2f * t)
                                   + 0.08f * Mathf.Sin(2f * Mathf.PI * 5.1f * t);
                float harm = SoftSawHarmonic(t, f0 * wobble, 0.5f) * 0.5f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 2f * t) * 0.18f;
                float white = HashNoise(i + 700);
                lp = Mathf.Lerp(lp, white, 0.05f);
                samples[i] = harm + lp * 0.05f;
            }

            FinalizeLoop(samples, gain * 0.55f, sampleRate);
            AudioClip clip = AudioClip.Create("MiniVan Engine Lugging", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateOverrevCueClip(float gain)
        {
            const int sampleRate = 44100;
            const float duration = 2f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float noiseLp = 0f;
            float bodyLp = 0f;

            // Busy but still harmonic — intake rush + denser mids, no siren tones.
            const float f0 = 72f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float harm = SoftSawHarmonic(t, f0, 0.42f) * 0.32f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 2f * t) * 0.18f
                           + Mathf.Sin(2f * Mathf.PI * f0 * 3f * t) * 0.1f
                           + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.05f;

                float white = HashNoise(i + 900) * 0.5f + HashNoise(i * 5 + 3) * 0.5f;
                noiseLp = Mathf.Lerp(noiseLp, white, 0.1f);
                float rush = noiseLp * 0.14f;

                float raw = harm + rush;
                bodyLp = Mathf.Lerp(bodyLp, raw, 0.25f);
                samples[i] = bodyLp;
            }

            FinalizeLoop(samples, gain * 0.5f, sampleRate);
            AudioClip clip = AudioClip.Create("MiniVan Engine Overrev", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Soft band-limited-ish saw via a few sine partials (no aliasing splat).</summary>
        private static float SoftSawHarmonic(float time, float hz, float brightness)
        {
            float phase = 2f * Mathf.PI * hz * time;
            float b = Mathf.Clamp01(brightness);
            return Mathf.Sin(phase)
                 + Mathf.Sin(phase * 2f) * (0.45f * b)
                 + Mathf.Sin(phase * 3f) * (0.22f * b)
                 + Mathf.Sin(phase * 4f) * (0.1f * b * b);
        }

        private static void FinalizeLoop(float[] samples, float gain, int sampleRate)
        {
            CrossfadeLoop(samples, Mathf.RoundToInt(sampleRate * 0.05f));
            NormalizePeak(samples, Mathf.Clamp(gain * 0.55f, 0.35f, 1.15f));
        }

        private static void NormalizePeak(float[] samples, float targetPeak)
        {
            float peak = 0.0001f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Mathf.Abs(samples[i]);
                if (a > peak)
                {
                    peak = a;
                }
            }

            float scale = targetPeak / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * scale, -1f, 1f);
            }
        }

        private static float HashNoise(int seed)
        {
            float n = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return (n - Mathf.Floor(n)) * 2f - 1f;
        }

        private static void CrossfadeLoop(float[] samples, int fadeSamples)
        {
            fadeSamples = Mathf.Clamp(fadeSamples, 1, samples.Length / 4);
            for (int i = 0; i < fadeSamples; i++)
            {
                float t = i / (float)fadeSamples;
                int end = samples.Length - fadeSamples + i;
                float a = samples[end];
                float b = samples[i];
                samples[i] = Mathf.Lerp(a, b, t);
            }
        }
    }
}
