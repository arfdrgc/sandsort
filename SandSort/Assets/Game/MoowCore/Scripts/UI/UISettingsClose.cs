using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsClose : MonoBehaviour {
    [SerializeField] Button _button;

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
    }

    void onClick() {
        AudioPlayer.PlaySFX(AudioFX.UI_BUTTON_CLICK);
        this.dispatchEvent<object>(Events.UI_CLOSE_SETTINGS, null);
    }
}
