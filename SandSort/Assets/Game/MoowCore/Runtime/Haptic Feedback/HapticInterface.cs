using System.Runtime.InteropServices;
using UnityEngine;

namespace Moow {
    public static class HapticIOSInterface {

        public static void unityHapticFeedeback(int type) {
            _unityHapticFeedback(type);
        }

        public static bool unityHapticIsSupport() {
            return _unityHapticIsSupport();
        }

        public static void unityHapticEnable() {
            _unityHapticEnable();
        }

        public static void unityHapticDisable() {
            _unityHapticDisable();
        }

        // Native-plugin function signaturs
#if PLATFORM_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _unityHapticFeedback(int type);
        [DllImport("__Internal")]
        private static extern bool _unityHapticIsSupport();
        [DllImport("__Internal")]
        private static extern void _unityHapticEnable();
        [DllImport("__Internal")]
        private static extern bool _unityHapticDisable();
#else
        private static void _unityHapticFeedback(int type) { }
        private static bool _unityHapticIsSupport() { return false; }
        private static void _unityHapticEnable() { }
        private static void _unityHapticDisable() { }
#endif
    }

#if UNITY_ANDROID
    public static class HapticAndroidInterface {

        public static void unityHapticFeedeback(int type) {

#if NEW_HAPTIC
            if (AndroidVibrationEngine.isAndroid) {
                HapticFeedbackType hapticType = (HapticFeedbackType)type;
                NewAndroidVibrationEngine.feedback(hapticType);
            } else {
                Handheld.Vibrate();
            }
#else
            if (AndroidVibrationEngine.isAndroid) {
                HapticFeedbackType hapticType = (HapticFeedbackType)type;
                AndroidVibrationEngine.feedback(hapticType);
            } else {
                Handheld.Vibrate();
            }
#endif
        }

        public static bool unityHapticIsSupport() {
            if (AndroidVibrationEngine.isAndroid) {
                return AndroidVibrationEngine.hasVibrator;
            }
            return false;
        }
    }
#endif
        }