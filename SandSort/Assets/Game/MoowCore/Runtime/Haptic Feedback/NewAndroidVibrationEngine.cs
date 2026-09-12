
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moow {
    public static class NewAndroidVibrationEngine {
        private static AndroidJavaClass _unityClass;
        private static AndroidJavaObject _unityActivity;
        private static AndroidJavaObject _pluginInstance;
        private static AndroidJavaObject _vibrator;

        private const string pluginInstanceClass = "com.adengames.pluginhaptic.HapticPlugin";

        static NewAndroidVibrationEngine() {

#if UNITY_ANDROID
            _vibrator = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                            .GetStatic<AndroidJavaObject>("currentActivity")
                            .Call<AndroidJavaObject>("getSystemService", "vibrator");

            //_unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _pluginInstance = new AndroidJavaObject(pluginInstanceClass);
            _pluginInstance.Call("setVibrator", _vibrator);
            //_unityActivity = _unityClass.GetStatic<AndroidJavaObject>("currentActivity");
            //_vibrator = _pluginInstance.Call<AndroidJavaObject>("getVibrator", _unityActivity);
#endif

        }

        public static void feedback(HapticFeedbackType type) {
            switch(type) {
                case HapticFeedbackType.FeedbackLight:
                    _pluginInstance.Call(HapticMethod.Light);
                    break;

                case HapticFeedbackType.FeedbackMedium:
                    _pluginInstance.Call(HapticMethod.Medium);
                    break;

                case HapticFeedbackType.FeedbackHeavy:
                    _pluginInstance.Call(HapticMethod.Heavy);
                    break;

                case HapticFeedbackType.FeedbackSuccess:
                    _pluginInstance.Call(HapticMethod.Success);
                    break;

                case HapticFeedbackType.FeedbackSoft:
                    _pluginInstance.Call(HapticMethod.Soft);
                    break;

                case HapticFeedbackType.FeedbackRigid:
                    _pluginInstance.Call(HapticMethod.Rigid);
                    break;

                case HapticFeedbackType.FeedbackWarning:
                    _pluginInstance.Call(HapticMethod.Warning);
                    break;

                case HapticFeedbackType.FeedbackError:
                    _pluginInstance.Call(HapticMethod.Error);
                    break;

                case HapticFeedbackType.FeedbackSelection:
                    _pluginInstance.Call(HapticMethod.Selection);
                    break;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="timings">in case of amplitude is active</param>
        /// <param name="amplitudes">in case of amplitude is active</param>
        /// <param name="onOffTiming">in case of amplitude is deactive</param>
        public static void CustomWaveform(long[] timings, int[] amplitudes, long[] onOffTiming) {
            _pluginInstance.Call(HapticMethod.CustomWaveform, timings, amplitudes, onOffTiming);
        }

        /// <summary>
        /// </summary>
        /// <param name="millisecond">haptic duration</param>
        /// <param name="amplitude">in case of amplitude is active</param>
        public static void CustomOnShot(long millisecond, int amplitude) {
            _pluginInstance.Call(HapticMethod.CustomOnShot, millisecond, amplitude);
        }
    }

    public sealed class HapticMethod {
        public static string Light = "Light";
        public static string Medium = "Medium";
        public static string Heavy = "Heavy";
        public static string Soft = "Soft";
        public static string Rigid = "Rigid";
        public static string Success = "Success";
        public static string Warning = "Warning";
        public static string Error = "Error";
        public static string Selection = "Selection";
        public static string CustomWaveform = "CustomWaveform";
        public static string CustomOnShot = "CustomOnShot";
    }
}