using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicSafeArea : MonoBehaviour {
    RectTransform _rectTransform;
    float _anchorMinY;
    float _anchorMaxY;

    private void Start() {
        _rectTransform = (RectTransform)transform;
        _anchorMinY = _rectTransform.anchorMin.y;
        _anchorMaxY = _rectTransform.anchorMax.y;
        calculateUIPosition();
    }

    private void Update() {
#if UNITY_EDITOR
        calculateUIPosition();
#endif
    }

    private void calculateUIPosition() {
        if(Screen.safeArea.y > 0) {
            float offsetY = _anchorMaxY - _anchorMinY;
            float safeAreaTop = (Screen.safeArea.yMax) / Screen.height;

            _rectTransform.anchorMin = new Vector2(_rectTransform.anchorMin.x, safeAreaTop - offsetY);
            _rectTransform.anchorMax = new Vector2(_rectTransform.anchorMax.x, safeAreaTop);
        } else {
            _rectTransform.anchorMin = new Vector2(_rectTransform.anchorMin.x, _anchorMinY);
            _rectTransform.anchorMax = new Vector2(_rectTransform.anchorMax.x, _anchorMaxY);
        }
    }
}
