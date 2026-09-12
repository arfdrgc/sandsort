using Moow;
using UnityEngine;
using UnityEngine.UI;

public class UIRestart : MonoBehaviour {
    [SerializeField] Button _button;
        [SerializeField] private CanvasGroup _canvas;

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
    }

    void onClick() {
        AudioPlayer.PlaySFX(AudioFX.UI_BUTTON_CLICK);
        _canvas.alpha = 0;
        _button.enabled = false;
        this.dispatchEvent<object>(Events.UI_RETRY_CLICKED, null);
    }
    private void onLevelCompleted(Object sender, Event<object> eventData)
    {
       _canvas.alpha = 0;
    }
}
