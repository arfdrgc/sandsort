using DG.Tweening;
using Moow;
using TMPro;
using UnityEngine;

// Freeze Time's HUD while the freeze counts down (PowerUp_1 ACTIVE): the timer pill turns to ice, a
// "❄ 10s" bar drains under it, and an ice frame with light snow covers the screen edges. Pure view —
// PowerUp_1 owns the countdown. Shown on the first FREEZE_TIME_CHANGED, updated by the rest (so a
// countdown held by Settings simply holds still here too), faded out on POWER_UP_1_DEACTIVATED (freeze
// over, win, lose, retry, level load) and snapped hidden on LEVEL_LOADED so nothing carries over.
//
// A freeze that runs out (its last FREEZE_TIME_CHANGED was 0) thaws with a soft drip + soft haptic; one
// cut short by a win, lose, retry or level load ends silently.
//
// Listeners live for the component's whole life (Awake/OnDestroy), not OnEnable: a hidden HUD object
// must still hear the freeze end.
public class UIFreezeTimeHUD : MonoBehaviour {
    [Header("Screen (ice frame + snow)")]
    [SerializeField] CanvasGroup _screenGroup;
    [Tooltip("Kept OUTSIDE _screenGroup: when a freeze ends it only stops emitting, so the flakes already falling drift on and fade out on their own after the frame is gone.")]
    [SerializeField] ParticleSystem _snow;

    [Header("Timer pill")]
    [Tooltip("Icy skin laid over the normal timer pill, behind the time text.")]
    [SerializeField] CanvasGroup _pillSkin;

    [Header("Countdown")]
    [SerializeField] CanvasGroup _countdownGroup;
    [SerializeField] TextMeshProUGUI _countdownText;
    [Tooltip("Drains from right to left: its anchorMax.x is the fraction of the freeze left.")]
    [SerializeField] RectTransform _countdownFill;

    [Header("Timing")]
    [SerializeField] float _fadeInDuration = 0.3f;
    [SerializeField] float _fadeOutDuration = 0.4f;
    [Tooltip("An ease-in keeps the \"0s\" state readable for a moment before it melts away.")]
    [SerializeField] Ease _fadeOutEase = Ease.OutQuad;

    bool _shown;
    float _fullSeconds;
    float _remaining;
    int _shownSeconds = -1;
    Sequence _fade;

    void Awake() {
        this.addListener<float>(Events.FREEZE_TIME_CHANGED, onFreezeTimeChanged);
        this.addListener<object>(Events.POWER_UP_1_DEACTIVATED, onFreezeTimeEnded);
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        hide(instant: true);
    }

    void OnDestroy() {
        this.removeListener<float>(Events.FREEZE_TIME_CHANGED, onFreezeTimeChanged);
        this.removeListener<object>(Events.POWER_UP_1_DEACTIVATED, onFreezeTimeEnded);
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
    }

    void onFreezeTimeChanged(Object sender, Event<float> e) {
        float remaining = Mathf.Max(0f, e.data);
        if (!_shown) show(remaining);
        _remaining = remaining;

        _countdownFill.anchorMax = new Vector2(_fullSeconds > 0f ? Mathf.Clamp01(remaining / _fullSeconds) : 0f, _countdownFill.anchorMax.y);

        // Whole seconds rounded up, like the level timer: 10 → 9 … → 1 → 0.
        int seconds = Mathf.CeilToInt(remaining);
        if (seconds == _shownSeconds) return;
        _shownSeconds = seconds;
        _countdownText.text = seconds + "s";
    }

    void onFreezeTimeEnded(Object sender, Event<object> e) {
        if (!_shown) return;
        hide(instant: false);
        if (_remaining <= 0f) {
            AudioPlayer.PlaySFX(AudioFX.WATER_DROP_1);
            HapticManager.Feedback(HapticFeedbackType.FeedbackSoft);
        }
    }

    void onLevelLoaded(Object sender, Event<object> e) => hide(instant: true);

    // The first value of a freeze is its full length (PowerUp_1 sends it the moment the freeze
    // becomes active), which is what the bar drains from.
    void show(float fullSeconds) {
        _shown = true;
        _fullSeconds = fullSeconds;
        _shownSeconds = -1;

        fitSnowToScreen();
        _snow.Clear();
        _snow.Play();

        fadeTo(1f, _fadeInDuration, Ease.OutQuad);
    }

    void hide(bool instant) {
        _shown = false;
        _snow.Stop(true, instant ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);

        if (instant) {
            _fade?.Kill();
            _fade = null;
            _screenGroup.alpha = 0f;
            _pillSkin.alpha = 0f;
            _countdownGroup.alpha = 0f;
        } else {
            fadeTo(0f, _fadeOutDuration, _fadeOutEase);
        }
    }

    void fadeTo(float alpha, float duration, Ease ease) {
        _fade?.Kill();
        _fade = DOTween.Sequence()
            .Join(_screenGroup.DOFade(alpha, duration))
            .Join(_pillSkin.DOFade(alpha, duration))
            .Join(_countdownGroup.DOFade(alpha, duration))
            .SetEase(ease)
            .SetLink(gameObject);
    }

    // Snow falls over the whole screen group, whatever the device's aspect: the emitter box is sized
    // to the group's rect (in the particle system's own, scaled, units).
    void fitSnowToScreen() {
        Rect rect = ((RectTransform)_screenGroup.transform).rect;
        float scale = Mathf.Max(0.0001f, _snow.transform.localScale.x);
        ParticleSystem.ShapeModule shape = _snow.shape;
        shape.scale = new Vector3(rect.width / scale, rect.height / scale, 1f);
    }
}
