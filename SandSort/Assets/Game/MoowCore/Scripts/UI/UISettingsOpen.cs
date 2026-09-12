using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsOpen : MonoBehaviour {
    [SerializeField] Button _button;
    [SerializeField] private CanvasGroup _canvas;

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
    }

    void onClick() {
        AudioPlayer.PlaySFX(AudioFX.UI_BUTTON_CLICK);
        this.dispatchEvent<object>(Events.UI_OPEN_SETTINGS, null);
    }

    private void onLevelCompleted(UnityEngine.Object sender, Event<object> eventData)
    {
       _canvas.alpha = 0;
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
         _canvas.alpha = 1;
    }
}
