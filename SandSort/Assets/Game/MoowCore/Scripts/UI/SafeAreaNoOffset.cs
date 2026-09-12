using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SafeAreaNoOffset : MonoBehaviour {
    [SerializeField] RectTransform _targetTransform;
    float _anchorMinY;
    float _anchorMaxY;

    private void Start() {
        _anchorMinY = _targetTransform.anchorMin.y;
        _anchorMaxY = _targetTransform.anchorMax.y;
        calculateUIPosition();
    }

    private void Update() {
#if UNITY_EDITOR
        calculateUIPosition();
#endif
    }

    private void calculateUIPosition() {
        float move = Screen.safeArea.y > 0 ? (1 - _anchorMaxY) : 0;

        _targetTransform.anchorMin = new Vector2(_targetTransform.anchorMin.x, _anchorMinY + move);
        _targetTransform.anchorMax = new Vector2(_targetTransform.anchorMax.x, _anchorMaxY + move);

    }
}
