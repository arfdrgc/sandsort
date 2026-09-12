using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Moow {

    public class HapticDebugger: MonoBehaviour {
        [SerializeField] Texture _debugTexture;
        List<HapticFeedbackData> _lastFeedbacks;

        private void Start() {
            _lastFeedbacks = new List<HapticFeedbackData>();
        }

        public void Feedback(HapticFeedbackType type) {
#if UNITY_EDITOR
            _lastFeedbacks.Add(new HapticFeedbackData(type));
#endif
        }

#if UNITY_EDITOR
        private void OnGUI() {
            for(int i = _lastFeedbacks.Count - 1;i >= 0;i--) {
                HapticFeedbackData feedbackData = _lastFeedbacks[i];
                feedbackData.debug(i, _debugTexture);
                if(feedbackData.isComplete()) {
                    _lastFeedbacks.RemoveAt(i);
                }
            }
        }
#endif
    }

    class HapticFeedbackData {
        public HapticFeedbackType type;
        public float startTime;
        public float lifeTime;
        public Color color;

        public HapticFeedbackData(HapticFeedbackType type) {
            this.type = type;
            startTime = Time.time;
            lifeTime = 0.5f;

            if(type == HapticFeedbackType.FeedbackLight) {
                color = Color.green;
                lifeTime = 0.25f;
            }
            else if(type == HapticFeedbackType.FeedbackMedium) {
                color = Color.yellow;
                lifeTime = 0.35f;
            }
            else if(type == HapticFeedbackType.FeedbackHeavy) {
                color = Color.red;
                lifeTime = 0.45f;
            }
            else {
                color = Color.white;
                lifeTime = 1;
            }
        }

        public void debug(int index, Texture debugTexture) {
            GUI.color = color;
            float sizeFactor = Mathf.Clamp01((Time.time - startTime) / lifeTime);
            Vector2 offset = Vector2.one * sizeFactor * 50;

            GUI.Box(new Rect(100 * index + offset.x, offset.y, 100 - offset.x * 2, 100 - offset.y * 2), debugTexture);
        }

        public bool isComplete() {
            return Time.time - startTime > lifeTime;
        }
    }

}