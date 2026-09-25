using System.Collections;
using UnityEngine;

namespace Moow {
    public class HapticManager:SingletonDontDestroy<HapticManager> {

        [SerializeField] HapticDebugger _debugger;
        [SerializeField] BaseDataSO _dataSO;

        private Coroutine _repeatCoroutine;
        private Coroutine _customCoroutine;
        private Coroutine _conditionalRepetableCoroutine;

        #region STATICS
        static public void Feedback(HapticFeedbackType type) {
            instance?.feedback(type);
        }

        static public void Repeat(HapticFeedbackType type = HapticFeedbackType.FeedbackRigid, float count = 1, float interval = .1f) {
            instance?.repeat(type, count, interval);
        }

        static public void CreateCustom(HapticFeedbackType[] types, float[] intervals) {
            instance?.createCustom(types, intervals);
        }

        static public void StartHaptic(HapticFeedbackType type = HapticFeedbackType.FeedbackRigid, float interval = .1f) {
            instance?.startHaptic(type, interval);
        }

        static public void StopHaptic() {
            instance?.stopHaptic();
        }
        #endregion

        #region BASE
        private void Start() {
            enable();
        }
        #endregion

        #region METHODS
        public void feedback(HapticFeedbackType type) {
            if(!isActive)
                return;

            if(_debugger != null) {
                _debugger.Feedback(type);
            }

#if UNITY_IOS && !UNITY_EDITOR
        HapticIOSInterface.unityHapticFeedeback((int)type);
#elif UNITY_ANDROID && !UNITY_EDITOR
        HapticAndroidInterface.unityHapticFeedeback((int)type);
#endif
        }

        public void repeat(HapticFeedbackType type, float count, float interval) {
			if (!isActive) return;

			if (_repeatCoroutine != null)
                StopCoroutine(_repeatCoroutine);

            _repeatCoroutine = StartCoroutine(C_Repeat(type, count, interval));
        }

        public void createCustom(HapticFeedbackType[] types, float[] intervals) {
			if (!isActive) return;

			if (types.Length != intervals.Length) {
                throw new System.Exception("[HapticManageR::createCustom] type and interval list length is not equal!");
            }

            if(_customCoroutine != null)
                StopCoroutine(_customCoroutine);

            _customCoroutine = StartCoroutine(C_CustomHaptic(types, intervals));
        }

        public void startHaptic(HapticFeedbackType type, float interval) {
			if (!isActive) return;

			if (_conditionalRepetableCoroutine != null)
                StopCoroutine(_conditionalRepetableCoroutine);

            _conditionalRepetableCoroutine = StartCoroutine(C_ConditionalHaptic(type, interval));
        }

        public void stopHaptic() {
            if(_conditionalRepetableCoroutine != null)
                StopCoroutine(_conditionalRepetableCoroutine);
            _conditionalRepetableCoroutine = null;
        }

        public bool isSupport() {
#if UNITY_IOS && !UNITY_EDITOR
            return HapticIOSInterface.unityHapticIsSupport();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return HapticAndroidInterface.unityHapticIsSupport();
#else
            return false;
#endif
        }

        public void enable() {
#if UNITY_IOS && !UNITY_EDITOR
        HapticIOSInterface.unityHapticEnable();
#elif UNITY_ANDROID && !UNITY_EDITOR
#endif
        }

        public void disable() {
#if UNITY_IOS && !UNITY_EDITOR
        HapticIOSInterface.unityHapticDisable();
#elif UNITY_ANDROID && !UNITY_EDITOR
#endif
        }
        #endregion

        #region COROUTINE
        IEnumerator C_Repeat(HapticFeedbackType type, float count, float interval) {
            WaitForSeconds wait = new WaitForSeconds(interval);
            for(int i = 0;i < count;i++) {
                feedback(type);
                yield return wait;
            }
        }

        IEnumerator C_CustomHaptic(HapticFeedbackType[] types, float[] intervals) {
            WaitForSeconds wait = null;
            for(int i = 0;i < types.Length;i++) {
                wait = new WaitForSeconds(intervals[i]);
                yield return wait;
                feedback(types[i]);
            }
        }

        IEnumerator C_ConditionalHaptic(HapticFeedbackType type, float interval) {
            WaitForSeconds wait = new WaitForSeconds(interval);
            while(true) {
                feedback(type);
                yield return wait;
            }
        }
        #endregion

        #region HELPER
        public bool isActive {
            get => _dataSO == null || !_dataSO.disableHaptic;
            set => _dataSO.disableHaptic = !value;
        }

        public int levelIndex => _dataSO.level;



        public Coroutine conditionalRepetableCoroutine => _conditionalRepetableCoroutine;
        #endregion
    }

    public enum HapticFeedbackType {
        FeedbackLight = 0,
        FeedbackMedium = 1,
        FeedbackHeavy = 2,
        FeedbackSoft = 3,
        FeedbackRigid = 4,

        FeedbackSuccess,
        FeedbackWarning,
        FeedbackError,
        FeedbackSelection,
    }
}

