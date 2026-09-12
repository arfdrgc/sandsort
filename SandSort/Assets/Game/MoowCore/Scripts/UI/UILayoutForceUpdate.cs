using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UILayoutForceUpdate : MonoBehaviour
{
    [SerializeField] RectTransform _layoutGroup;
    
    void Start() {
        ForceUpdateLayout();
    }

    public void ForceUpdateLayout() {
        LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutGroup);
    }
}
