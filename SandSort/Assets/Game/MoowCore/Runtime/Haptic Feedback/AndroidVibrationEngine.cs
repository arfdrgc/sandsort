using System.Collections.Generic;
using UnityEngine;

namespace Moow {
    // Android haptics that work on any manufacturer (2026-09-25 rewrite). On first use it probes the
    // device once and picks the richest backend it can actually play:
    //
    //   Predefined  VibrationEffect.createPredefined — API 30+, and only when the device answers
    //               "supported: YES" for click / tick / heavy click (no guessing on UNKNOWN).
    //   Effect      amplitude one-shots / waveforms — API 26+. Devices without amplitude control get
    //               slightly longer pulses at the default strength instead of amplitudes they'd ignore.
    //   Legacy      plain on/off durations (Vibrator.vibrate(long)) — every API level.
    //   Handheld    Unity's Handheld.Vibrate, only when no Vibrator could be obtained at all.
    //
    // Every vibration is tagged as game/media usage (VibrationAttributes on API 33+, AudioAttributes
    // below). An untagged short click is classified as *touch feedback* on Android 13+, which follows
    // the system "touch feedback" setting and is silently dropped by some skins (seen on a Xiaomi 15T
    // Pro / HyperOS while an older Xiaomi played it).
    //
    // Nothing here throws to the caller: a Java exception demotes the backend one step and retries,
    // and if even the tagged basic vibration fails the tags are dropped and the probe starts over.
    // The chosen setup is logged once ("[AndroidVibrationEngine] ...") so a device can be diagnosed
    // from logcat.
    public static class AndroidVibrationEngine {

        // Java constants.
        const int DEFAULT_AMPLITUDE = -1;           // VibrationEffect.DEFAULT_AMPLITUDE
        const int EFFECT_SUPPORT_YES = 1;           // Vibrator.VIBRATION_EFFECT_SUPPORT_YES
        const int VIBRATION_USAGE_MEDIA = 0x13;     // VibrationAttributes.USAGE_MEDIA
        const int AUDIO_USAGE_GAME = 14;            // AudioAttributes.USAGE_GAME
        const int AUDIO_CONTENT_SONIFICATION = 4;   // AudioAttributes.CONTENT_TYPE_SONIFICATION

        // Handheld.Vibrate is a long buzz; rate limited so extraction pulses can't chain it into a rumble.
        const float HANDHELD_MIN_INTERVAL = 0.4f;

        enum Tier { Predefined, Effect, Legacy, Handheld }

        struct Profile {
            public int predefined;      // VibrationEffectID, or -1 for waveform profiles
            public long milliseconds;   // one-shot length with amplitude control
            public int amplitude;       // 1..255
            public long onOffMilliseconds; // length without amplitude control (ERM motors need longer)
            public long[] timings;      // waveform profiles only
            public int[] amplitudes;

            public bool isWaveform => timings != null;
        }

        // Indexed by (int)HapticFeedbackType. Predefined ids keep the original mapping.
        static readonly Profile[] PROFILES = {
            oneShot(VibrationEffectID.EffectClick, 20, 90, 20),         // Light
            oneShot(VibrationEffectID.EffectClick, 25, 150, 30),        // Medium
            oneShot(VibrationEffectID.EffectHeavyClick, 35, 255, 45),   // Heavy
            oneShot(VibrationEffectID.EffectTick, 15, 60, 20),          // Soft
            oneShot(VibrationEffectID.EffectClick, 20, 180, 25),        // Rigid
            waveform(new long[] { 25, 125, 25 },                        // Success
                new int[] { (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Light }),
            waveform(new long[] { 25, 200, 25 },                        // Warning
                new int[] { (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Medium }),
            waveform(new long[] { 25, 75, 25, 75, 25, 75, 25 },         // Error
                new int[] { (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Light, 0,
                            (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Heavy }),
            oneShot(VibrationEffectID.EffectClick, 15, 70, 20),         // Selection
        };

        static bool s_initialized;
        static int s_apiLevel;
        static AndroidJavaObject s_vibrator;
        static AndroidJavaClass s_effectClass;
        static AndroidJavaObject s_effectAttributes;  // VibrationAttributes (33+) or AudioAttributes
        static AndroidJavaObject s_audioAttributes;   // for the legacy vibrate(long, AudioAttributes)
        static bool s_tagged;
        static bool s_hasAmplitudeControl;
        static Tier s_bestTier;
        static Tier s_tier;
        static readonly AndroidJavaObject[] s_effectCache = new AndroidJavaObject[PROFILES.Length];
        static float s_lastHandheldTime = float.NegativeInfinity;

        // Statics survive "Enter Play Mode without domain reload"; start every session unprobed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void resetStatics() {
            Dispose();
            s_initialized = false;
            s_lastHandheldTime = float.NegativeInfinity;
        }

        public static void feedback(HapticFeedbackType type) {
            int index = (int)type;
            if (index < 0 || index >= PROFILES.Length) return;
            if (!ensureInitialized()) {
                handheldVibrate();
                return;
            }
            play(PROFILES[index], index);
        }

        // VibrationEffect ids (VibrationEffectID). Played as a predefined effect when the device supports
        // it, otherwise as an equivalent one-shot.
        public static void createPredefined(int effectId) {
            Profile profile = oneShot((VibrationEffectID)effectId, 20, 120, 25);
            if (ensureInitialized()) play(profile, -1);
            else handheldVibrate();
        }

        public static void createOneShot(long milliseconds, int amplitude) {
            Profile profile = oneShotRaw(milliseconds, amplitude, milliseconds);
            if (ensureInitialized()) play(profile, -1);
            else handheldVibrate();
        }

        // repeat is ignored: every haptic here is a one-off (-1).
        public static void createWaveform(long[] timings, int[] amplitudes, int repeat) {
            if (timings == null || amplitudes == null || timings.Length != amplitudes.Length) return;
            if (ensureInitialized()) play(waveform(timings, amplitudes), -1);
            else handheldVibrate();
        }

        public static void createWaveform(long[] timings, int repeat) {
            if (timings == null) return;
            // Classic on/off pattern: even entries are waits, odd entries are vibration.
            int[] amplitudes = new int[timings.Length];
            for (int i = 0; i < amplitudes.Length; i++) amplitudes[i] = i % 2 == 0 ? 0 : DEFAULT_AMPLITUDE;
            createWaveform(timings, amplitudes, repeat);
        }

        public static void vibrate(long milliseconds) {
            createOneShot(milliseconds, DEFAULT_AMPLITUDE);
        }

        public static void Cancel() {
            if (s_vibrator == null) return;
            try {
                s_vibrator.Call("cancel");
            } catch (System.Exception) {
            }
        }

        public static void Dispose() {
            for (int i = 0; i < s_effectCache.Length; i++) {
                s_effectCache[i]?.Dispose();
                s_effectCache[i] = null;
            }
            s_effectAttributes?.Dispose();
            if (s_audioAttributes != s_effectAttributes) s_audioAttributes?.Dispose();
            s_effectClass?.Dispose();
            s_vibrator?.Dispose();
            s_effectAttributes = null;
            s_audioAttributes = null;
            s_effectClass = null;
            s_vibrator = null;
            s_initialized = false;
        }

        // HELPER METHODS
        public static bool isAndroid {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool hasVibrator {
            get {
                if (!ensureInitialized()) return false;
                try {
                    return s_vibrator.Call<bool>("hasVibrator");
                } catch (System.Exception) {
                    return false;
                }
            }
        }

        public static bool hasAmplitudeControl => ensureInitialized() && s_hasAmplitudeControl;

        // PLAYBACK
        static void play(Profile profile, int cacheIndex) {
            while (true) {
                if (s_tier == Tier.Handheld) {
                    handheldVibrate();
                    return;
                }
                try {
                    playOn(s_tier, profile, cacheIndex);
                    return;
                } catch (System.Exception exception) {
                    Debug.LogWarning($"[AndroidVibrationEngine] {s_tier} (tagged {s_tagged}) failed, falling back: {exception.Message}");
                    demote();
                }
            }
        }

        static void playOn(Tier tier, Profile profile, int cacheIndex) {
            if (tier == Tier.Legacy) {
                playLegacy(profile);
                return;
            }

            AndroidJavaObject effect = cacheIndex >= 0 ? s_effectCache[cacheIndex] : null;
            bool cached = effect != null;
            if (!cached) {
                effect = createEffect(tier, profile);
                if (cacheIndex >= 0) {
                    s_effectCache[cacheIndex] = effect;
                    cached = true;
                }
            }

            try {
                if (s_tagged) s_vibrator.Call("vibrate", effect, s_effectAttributes);
                else s_vibrator.Call("vibrate", effect);
            } finally {
                if (!cached) effect.Dispose();
            }
        }

        static AndroidJavaObject createEffect(Tier tier, Profile profile) {
            if (tier == Tier.Predefined && profile.predefined >= 0)
                return s_effectClass.CallStatic<AndroidJavaObject>("createPredefined", profile.predefined);

            if (profile.isWaveform)
                return s_effectClass.CallStatic<AndroidJavaObject>("createWaveform", profile.timings, profile.amplitudes, -1);

            long milliseconds = s_hasAmplitudeControl ? profile.milliseconds : profile.onOffMilliseconds;
            int amplitude = s_hasAmplitudeControl ? profile.amplitude : DEFAULT_AMPLITUDE;
            return s_effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
        }

        static void playLegacy(Profile profile) {
            if (profile.isWaveform) {
                long[] pattern = remapWaveFormToPattern(profile.timings, profile.amplitudes);
                if (s_tagged) s_vibrator.Call("vibrate", pattern, -1, s_audioAttributes);
                else s_vibrator.Call("vibrate", pattern, -1);
            } else {
                if (s_tagged) s_vibrator.Call("vibrate", profile.onOffMilliseconds, s_audioAttributes);
                else s_vibrator.Call("vibrate", profile.onOffMilliseconds);
            }
        }

        // Predefined -> Effect -> Legacy; after tagged Legacy, retry the whole chain untagged; then Handheld.
        static void demote() {
            clearEffectCache();
            if (s_tier == Tier.Predefined && s_apiLevel >= 26) s_tier = Tier.Effect;
            else if (s_tier != Tier.Legacy) s_tier = Tier.Legacy;
            else if (s_tagged) {
                s_tagged = false;
                s_tier = s_bestTier;
            } else s_tier = Tier.Handheld;
        }

        static void handheldVibrate() {
            float now = Time.realtimeSinceStartup;
            if (now - s_lastHandheldTime < HANDHELD_MIN_INTERVAL) return;
            s_lastHandheldTime = now;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        // SETUP
        static bool ensureInitialized() {
            if (s_initialized) return s_vibrator != null;
            s_initialized = true;
            if (!isAndroid) return false;

            try {
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                    s_apiLevel = version.GetStatic<int>("SDK_INT");
                s_vibrator = acquireVibrator();
            } catch (System.Exception exception) {
                Debug.LogWarning($"[AndroidVibrationEngine] No Vibrator service, using Handheld.Vibrate: {exception.Message}");
                s_vibrator = null;
            }

            if (s_vibrator == null) {
                s_tier = s_bestTier = Tier.Handheld;
                return false;
            }

            if (s_apiLevel >= 26) {
                s_effectClass = tryCreate(() => new AndroidJavaClass("android.os.VibrationEffect"));
                s_hasAmplitudeControl = tryCall(() => s_vibrator.Call<bool>("hasAmplitudeControl"));
            }

            s_audioAttributes = tryCreate(() => {
                using (AndroidJavaObject builder = new AndroidJavaObject("android.media.AudioAttributes$Builder")) {
                    builder.Call<AndroidJavaObject>("setUsage", AUDIO_USAGE_GAME).Dispose();
                    builder.Call<AndroidJavaObject>("setContentType", AUDIO_CONTENT_SONIFICATION).Dispose();
                    return builder.Call<AndroidJavaObject>("build");
                }
            });
            s_effectAttributes = s_apiLevel >= 33
                ? tryCreate(() => {
                    using (AndroidJavaClass attributes = new AndroidJavaClass("android.os.VibrationAttributes"))
                        return attributes.CallStatic<AndroidJavaObject>("createForUsage", VIBRATION_USAGE_MEDIA);
                })
                : null;
            if (s_effectAttributes == null) s_effectAttributes = s_audioAttributes;
            s_tagged = s_audioAttributes != null;

            if (s_effectClass == null) s_bestTier = Tier.Legacy;
            else if (s_apiLevel >= 30 && predefinedEffectsSupported()) s_bestTier = Tier.Predefined;
            else s_bestTier = Tier.Effect;
            s_tier = s_bestTier;

            Debug.Log($"[AndroidVibrationEngine] API {s_apiLevel}, backend {s_tier}, amplitude control {s_hasAmplitudeControl}, " +
                      $"tagged {s_tagged} ({(s_apiLevel >= 33 && s_effectAttributes != s_audioAttributes ? "VibrationAttributes" : "AudioAttributes")})");
            return true;
        }

        static AndroidJavaObject acquireVibrator() {
            AndroidJavaObject context = currentContext();
            if (context == null) return null;

            if (s_apiLevel >= 31) {
                AndroidJavaObject vibrator = tryCreate(() => {
                    using (AndroidJavaObject manager = context.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                        return manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                });
                if (vibrator != null) return vibrator;
            }
            return context.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }

        // Owned by Unity, never disposed here.
        static AndroidJavaObject currentContext() {
            AndroidJavaObject context = tryCreate(() => UnityEngine.Android.AndroidApplication.currentContext);
            if (context != null) return context;
            return tryCreate(() => {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    return player.GetStatic<AndroidJavaObject>("currentActivity");
            });
        }

        static bool predefinedEffectsSupported() {
            int[] effects = { (int)VibrationEffectID.EffectClick, (int)VibrationEffectID.EffectTick, (int)VibrationEffectID.EffectHeavyClick };
            return tryCall(() => s_vibrator.Call<int>("areAllEffectsSupported", new object[] { effects }) == EFFECT_SUPPORT_YES);
        }

        static void clearEffectCache() {
            for (int i = 0; i < s_effectCache.Length; i++) {
                s_effectCache[i]?.Dispose();
                s_effectCache[i] = null;
            }
        }

        static T tryCreate<T>(System.Func<T> create) where T : class {
            try {
                return create();
            } catch (System.Exception) {
                return null;
            }
        }

        static bool tryCall(System.Func<bool> call) {
            try {
                return call();
            } catch (System.Exception) {
                return false;
            }
        }

        static Profile oneShot(VibrationEffectID predefined, long milliseconds, int amplitude, long onOffMilliseconds) {
            Profile profile = oneShotRaw(milliseconds, amplitude, onOffMilliseconds);
            profile.predefined = (int)predefined;
            return profile;
        }

        static Profile oneShotRaw(long milliseconds, int amplitude, long onOffMilliseconds) {
            return new Profile {
                predefined = -1,
                milliseconds = milliseconds,
                amplitude = amplitude,
                onOffMilliseconds = onOffMilliseconds,
            };
        }

        static Profile waveform(long[] timings, int[] amplitudes) {
            return new Profile { predefined = -1, timings = timings, amplitudes = amplitudes };
        }

        // Waveform (timings + amplitudes) -> classic on/off pattern starting with a wait.
        private static long[] remapWaveFormToPattern(long[] timings, int[] amplitudes) {
            List<long> patternList = new List<long>();
            bool vibrating = false;
            long segment = 0L;
            for (int i = 0; i < amplitudes.Length; i++) {
                bool on = amplitudes[i] != 0;
                if (on != vibrating) {
                    patternList.Add(segment);
                    segment = 0L;
                    vibrating = on;
                }
                segment += timings[i];
            }
            if (vibrating) patternList.Add(segment);
            return patternList.ToArray();
        }
    }

    enum VibrationEffectID {
        DefaultAmplitude = -1, // API 26+
        EffectClick = 0,  // API 29+
        EffectDoubleClick = 1,  // API 29+
        EffectTick = 2,  // API 29+
        EffectHeavyClick = 5,  // API 29+
    }

    enum AmplitudeValues {
        Default = 30,
        Selection = 15,
        Light = 50,
        Medium = 150,
        Heavy = 250,
    }
}
