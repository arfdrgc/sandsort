using DG.Tweening;
using Moow;
using TMPro;
using UnityEngine;

// Shows Level's countdown as mm:ss. Level owns all timer rules (start on first drag, Settings
// pause, stop on Win/Lose, reset on restart) and dispatches LEVEL_TIMER_CHANGED whenever the
// whole-second value changes; this only formats it. At _warningSeconds or less the text turns
// red and pulses; any value above that (restart, new level, revive) restores the authored look.
// While Freeze Time counts down the time can't run out, so the warning is held off and the pill
// wears UIFreezeTimeHUD's ice skin instead; it comes back when the freeze ends if still due.
public class UILevelTimer : MonoBehaviour {
    [SerializeField] TextMeshProUGUI _timerText;
    [SerializeField] int _warningSeconds = 10;
    [SerializeField] Color _warningColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] float _pulseScale = 1.12f;
    [SerializeField] float _pulseHalfPeriod = 0.4f;

    Color _normalColor;
    Vector3 _normalScale;
    Tween _pulseTween;
    bool _warning;
    int _seconds = int.MaxValue;
    bool _frozen;

    void Awake() {
        _normalColor = _timerText.color;
        _normalScale = _timerText.rectTransform.localScale;
    }

    void OnEnable() {
        this.addListener<int>(Events.LEVEL_TIMER_CHANGED, onTimerChanged);
        this.addListener<float>(Events.FREEZE_TIME_CHANGED, onFreezeTimeChanged);
        this.addListener<object>(Events.POWER_UP_1_DEACTIVATED, onFreezeTimeEnded);
    }

    void OnDisable() {
        this.removeListener<int>(Events.LEVEL_TIMER_CHANGED, onTimerChanged);
        this.removeListener<float>(Events.FREEZE_TIME_CHANGED, onFreezeTimeChanged);
        this.removeListener<object>(Events.POWER_UP_1_DEACTIVATED, onFreezeTimeEnded);
        _frozen = false;
        setWarning(false);
    }

    void onTimerChanged(Object sender, Event<int> e) {
        _seconds = Mathf.Max(0, e.data);
        _timerText.text = $"{_seconds / 60:00}:{_seconds % 60:00}";
        refreshWarning();
    }

    void onFreezeTimeChanged(Object sender, Event<float> e) {
        if (_frozen) return;
        _frozen = true;
        refreshWarning();
    }

    void onFreezeTimeEnded(Object sender, Event<object> e) {
        _frozen = false;
        refreshWarning();
    }

    void refreshWarning() => setWarning(!_frozen && _seconds <= _warningSeconds);

    void setWarning(bool warning) {
        if (warning == _warning) return;
        _warning = warning;

        _pulseTween?.Kill();
        _pulseTween = null;
        RectTransform rect = _timerText.rectTransform;
        rect.localScale = _normalScale;

        if (warning) {
            _timerText.color = _warningColor;
            _pulseTween = rect.DOScale(_normalScale * _pulseScale, _pulseHalfPeriod)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        } else {
            _timerText.color = _normalColor;
        }
    }
}
