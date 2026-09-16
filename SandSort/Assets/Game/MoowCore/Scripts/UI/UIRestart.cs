using DG.Tweening;
using Moow;
using UnityEngine;
using UnityEngine.UI;

public class UIRestart : MonoBehaviour {
    [SerializeField] Button _button;
        [SerializeField] private CanvasGroup _canvas;
    [Tooltip("Seconds after the player's first drag in a level before the restart button appears. Every level load (first load, restart, next level) hides it again until the next first drag.")]
    [SerializeField, Min(0f)] float _showDelayAfterFirstDrag = 5f;

    Tween _showTween;

    private void Awake() {
        hide();
    }

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<object>(Events.LEVEL_FIRST_DRAG, onFirstDrag);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<object>(Events.LEVEL_FIRST_DRAG, onFirstDrag);
        _showTween?.Kill();
    }

    void onClick() {
        AudioPlayer.PlaySFX(AudioFX.UI_BUTTON_CLICK);
        _canvas.alpha = 0;
        _button.enabled = false;
        this.dispatchEvent<object>(Events.UI_RETRY_CLICKED, null);
    }
    private void onLevelCompleted(Object sender, Event<object> eventData)
    {
       _showTween?.Kill();
       _canvas.alpha = 0;
    }

    private void onLevelLoaded(Object sender, Event<object> eventData) {
        hide();
    }

    // Timed on game time (not ignoring time scale), so it holds while the app is paused.
    private void onFirstDrag(Object sender, Event<object> eventData) {
        _showTween?.Kill();
        _showTween = DOVirtual.DelayedCall(_showDelayAfterFirstDrag, show, false).SetLink(gameObject);
    }

    void hide() {
        _showTween?.Kill();
        _canvas.alpha = 0;
        _button.interactable = false;
        _button.enabled = false;
    }

    void show() {
        _canvas.alpha = 1;
        _button.enabled = true;
        _button.interactable = true;
    }
}
