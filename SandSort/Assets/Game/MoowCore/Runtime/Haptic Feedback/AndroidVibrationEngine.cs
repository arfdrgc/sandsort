using System.Collections.Generic;
using UnityEngine;

namespace Moow {
    public static class AndroidVibrationEngine {
        private static AndroidJavaObject _vibrator;
        private static AndroidJavaClass _vibrationEffectClass;
        private static int _androidAPILevel;

        private const long DEFAULT_MILLISECONDS = 20;
        // Amplitude values must be between 0 and 255

        static AndroidVibrationEngine() {

#if UNITY_ANDROID && !UNITY_EDITOR
        _vibrator = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                        .GetStatic<AndroidJavaObject>("currentActivity")
                        .Call<AndroidJavaObject>("getSystemService", "vibrator"); // Vibration service from Current Activity instance.
        _androidAPILevel = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
        if (checkAPICondition(26)) {
            _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
        }
#endif
// Trick Unity into giving the App vibration permission when it builds.
// This check will always be false, but the compiler doesn't know that.
#if UNITY_IOS || UNITY_ANDROID
            if(Application.isEditor) {
                Handheld.Vibrate();
            }
#endif
        }

        public static void feedback(HapticFeedbackType type) {
            switch (type) {
                case HapticFeedbackType.FeedbackLight:
                createPredefined((int)VibrationEffectID.EffectClick);
                break;

                case HapticFeedbackType.FeedbackMedium:
                createPredefined((int)VibrationEffectID.EffectClick);
                break;

                case HapticFeedbackType.FeedbackHeavy:
                createPredefined((int)VibrationEffectID.EffectHeavyClick);
                break;

                case HapticFeedbackType.FeedbackSuccess:
                createWaveform(new long[] { 25, 125, 25 }, new int[] { (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Light, }, -1);
                break;

                case HapticFeedbackType.FeedbackWarning:
                createWaveform(new long[] { 25, 200, 25 }, new int[] { (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Medium }, -1);
                break;

                case HapticFeedbackType.FeedbackError:
                createWaveform(new long[] { 25, 75, 25, 75, 25, 75, 25 },
               new int[] { (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Light, 0, (int)AmplitudeValues.Heavy }, -1);
                break;

                case HapticFeedbackType.FeedbackSelection:
                createPredefined((int)VibrationEffectID.EffectClick); // 0
                break;
            }
        }

        // VibrationEffect
        // Newer Methods: Min API Level 26+

        // API 29+
        public static void createPredefined(int effectId) {
            if (checkAPICondition(29)) {
                AndroidJavaObject vibrationEffect = createVibrationEffect("createPredefined", effectId);
                vibrate(vibrationEffect);
            } else {
                Debug.Log("[AndroidVibrationEngine::createPredefined] API 29 Not Supported! Current API Level: " + _androidAPILevel);
                int amplitude = amplitudeMapper((VibrationEffectID)effectId);
                createOneShot(DEFAULT_MILLISECONDS, amplitude);
            }
        }

        // API 26+
        public static void createOneShot(long milliseconds, int amplitude) {
            if (checkAPICondition(26)) {
                AndroidJavaObject vibrationEffect = createVibrationEffect("createOneShot", new object[] { milliseconds, amplitude });
                vibrate(vibrationEffect);
            } else {
                Debug.Log("[AndroidVibrationEngine::createOneShot] API 26 Not Supported! Current API Level: " + _androidAPILevel);
                vibrate(milliseconds);
            }
        }

        // API 26+
        public static void createWaveform(long[] timings, int[] amplitudes, int repeat) {
            if (checkAPICondition(26)) {
                AndroidJavaObject vibrationEffect = createVibrationEffect("createWaveform", new object[] { timings, amplitudes, repeat });
                vibrate(vibrationEffect);
            } else if (checkAPICondition(21)) {
                Debug.Log("[AndroidVibrationEngine::createWaveform] API 26 Not Supported! Current API Level: " + _androidAPILevel);
                long[] pattern = remapWaveFormToPattern(timings, amplitudes);
                vibrate(pattern, -1);
            } else {
                Debug.Log("[AndroidVibrationEngine::createWaveform] API 21 Not Supported! Current API Level: " + _androidAPILevel);
                vibrate(DEFAULT_MILLISECONDS);
            }
        }

        // API 26+
        public static void createWaveform(long[] timings, int repeat) {
            if (checkAPICondition(26)) {
                AndroidJavaObject vibrationEffect = createVibrationEffect("createWaveform", new object[] { timings, repeat });
                vibrate(vibrationEffect);
            } else if (checkAPICondition(21)) {
                Debug.Log("[AndroidVibrationEngine::createWaveform] API 26 Not Supported! Current API Level: " + _androidAPILevel);
                long[] pattern = timings; // Same logic as Pattern
                vibrate(pattern, -1);
            } else {
                Debug.Log("[AndroidVibrationEngine::createWaveform] API 21 Not Supported! Current API Level: " + _androidAPILevel);
                vibrate(DEFAULT_MILLISECONDS);
            }
        }

        private static AndroidJavaObject createVibrationEffect(string functionName, params object[] args) {
            return _vibrationEffectClass.CallStatic<AndroidJavaObject>(functionName, args);
        }

        private static void vibrate(AndroidJavaObject vibrationEffect) {
            _vibrator.Call("vibrate", vibrationEffect);
        }

        // Older Methods: Min API Level 1+
        // API 1+
        public static void vibrate(long milliseconds) {
            _vibrator.Call("vibrate", milliseconds);
        }

        // API 21+
        public static void vibrate(long[] pattern, int repeat) {
            //Pass in an array of ints that are the durations for which to turn on or off the vibrator in milliseconds.
            //The first value indicates the number of milliseconds to wait before turning the vibrator on.
            //The next value indicates the number of milliseconds for which to keep the vibrator on
            _vibrator.Call("vibrate", pattern, repeat);
        }

        // COMMON METHODS
        public static void Cancel() {
            _vibrator.Call("cancel");
        }

        public static void Dispose() {
            _vibrator.Dispose();
        }

        // HELPER METHODS
        public static bool isAndroid {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#endif
                return false;
            }
        }

        // API 11+
        public static bool hasVibrator {
            get {
                // API 11+
                if (checkAPICondition(11))
                    return _vibrator.Call<bool>("hasVibrator");
                return false;
            }
        }

        private static bool checkAPICondition(int neededAPILevel) {
            return (_androidAPILevel >= neededAPILevel);
        }

        private static int amplitudeMapper(VibrationEffectID effectID) {
            AmplitudeValues amplitude = AmplitudeValues.Default;
            switch (effectID) {
                case VibrationEffectID.DefaultAmplitude:
                amplitude = AmplitudeValues.Default;
                break;
                case VibrationEffectID.EffectClick:
                amplitude = AmplitudeValues.Selection;
                break;
                case VibrationEffectID.EffectDoubleClick:
                amplitude = AmplitudeValues.Light;
                break;
                case VibrationEffectID.EffectTick:
                amplitude = AmplitudeValues.Medium;
                break;
                case VibrationEffectID.EffectHeavyClick:
                amplitude = AmplitudeValues.Heavy;
                break;
                default:
                break;
            }
            return (int)amplitude;
        }

        private static long[] remapWaveFormToPattern(long[] timings, int[] amplitudes) {
            ConvertingOperation operation = ConvertingOperation.None;
            List<long> patternList = new List<long>();
            long delay = 0L;
            long duration = 0L;
            for (int i = 0; i < amplitudes.Length; i++) {
                int val = amplitudes[i];
                if (val <= 0) { // Set as Delay
                    ConvertingOperation thisOperation = ConvertingOperation.Delay;
                    delay += timings[i];
                    if (operation != ConvertingOperation.None && operation != thisOperation) {
                        patternList.Add(delay);
                        patternList.Add(duration);
                        delay = 0L;
                        duration = 0L;
                        operation = ConvertingOperation.None;
                    } else {
                        operation = ConvertingOperation.Delay;
                    }
                } else { // Set as Duration
                    ConvertingOperation thisOperation = ConvertingOperation.Duration;
                    duration += timings[i];
                    if (operation != ConvertingOperation.None && operation != thisOperation) {
                        patternList.Add(delay);
                        patternList.Add(duration);
                        delay = 0L;
                        duration = 0L;
                        operation = ConvertingOperation.None;
                    } else {
                        operation = ConvertingOperation.Duration;
                    }
                }
            }

            // Last Singular Check
            if (operation == ConvertingOperation.Duration && duration > 0L) {
                patternList.Add(delay);
                patternList.Add(duration);
            }

            return patternList.ToArray();
        }

        enum ConvertingOperation {
            None,
            Delay,
            Duration,
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