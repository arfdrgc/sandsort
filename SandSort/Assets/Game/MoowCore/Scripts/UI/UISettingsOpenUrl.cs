using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsOpenUrl : MonoBehaviour {
    [SerializeField] Button _button;
    [SerializeField] string _url;

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
    }

    void onClick() {
        Application.OpenURL(_url);
    }
}
