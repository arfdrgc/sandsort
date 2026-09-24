using DG.Tweening;
using Moow;
using UnityEngine;
using UnityEngine.UI;

// Freeze Time's activation, between the tap and the freeze becoming active (PowerUp_1 ANIMATING):
// the screen dims, the booster icon leaves its button, grows on the way to the upper board, holds,
// then shrinks into the timer pill trailing frost; an ice burst there hands over to UIFreezeTimeHUD.
//
// It follows PowerUp_1 rather than running its own clock: it starts on POWER_UP_1_ACTIVATED, lasts
// the same freezeTimeActivationSeconds (GameplayTunables), pauses and resumes with Settings exactly as
// PowerUp_1 does, and bursts on the first FREEZE_TIME_CHANGED — the moment the freeze really becomes
// active and the HUD fades in. A freeze cut short before that (win, lose, retry, level load) sends
// POWER_UP_1_DEACTIVATED / LEVEL_LOADED instead, and everything is cleared on the spot.
//
// Also owns the activation's feedback, so it can't drift from the animation: a whoosh + light haptic
// on the accepted tap, the ice crack + medium haptic at the burst. Both are one-shots fired once per
// activation (Settings pauses the flight but never re-fires them); a freeze cut short plays neither
// burst sound nor haptic.
//
// Purely visual: nothing here blocks input (no raycast targets), and the timer is already stopped by
// Level from the tap on. Listeners live for the component's whole life (Awake/OnDestroy).
public class UIFreezeTimeActivation : MonoBehaviour {
    [Header("References")]
    [SerializeField] Image _dim;
    [SerializeField] RectTransform _icon;
    [SerializeField] Image _iconImage;
    [Tooltip("Frost trail: world-space particle systems moved onto the icon every frame, so what they emit stays behind. Not children of the icon — hiding the icon at the burst must not delete the trail still fading out.")]
    [SerializeField] ParticleSystem[] _trail;
    [SerializeField] RectTransform _burst;
    [Tooltip("One-shot systems at the timer, each emitted with its own count. Emitted directly: UIParticleSystem drives the simulation itself, and scheduled emission bursts on a one-shot system never fire under it.")]
    [SerializeField] BurstEmitter[] _burstParticles;
    [Tooltip("Where the icon starts: the Freeze Time button's icon.")]
    [SerializeField] RectTransform _from;
    [Tooltip("Where the icon lands and the burst plays: the level timer pill.")]
    [SerializeField] RectTransform _to;
    [Tooltip("Source of the flying icon's sprite, so it always matches the button.")]
    [SerializeField] PowerUpSO _powerUp;

    [Header("Path")]
    [Tooltip("Where the icon holds, as a fraction of this (full-screen) rect: 0,0 bottom-left, 1,1 top-right.")]
    [SerializeField] Vector2 _holdPoint = new Vector2(0.5f, 0.68f);
    [Tooltip("Icon size at the hold, relative to its size on the button.")]
    [SerializeField] float _holdScale = 2.4f;
    [Tooltip("Icon size on arrival at the timer, relative to its size on the button.")]
    [SerializeField] float _endScale = 0.55f;
    [Tooltip("End of the rise-and-grow, as a fraction of the activation time.")]
    [SerializeField, Range(0f, 1f)] float _riseEnd = 0.3f;
    [Tooltip("End of the hold (start of the flight to the timer), as a fraction of the activation time.")]
    [SerializeField, Range(0f, 1f)] float _holdEnd = 0.55f;
    [Tooltip("Move + grow from the button to the hold. An ease-in keeps the icon small and slow as it leaves the button, like the reference.")]
    [SerializeField] Ease _riseEase = Ease.InQuad;
    [Tooltip("Move + shrink from the hold into the timer.")]
    [SerializeField] Ease _flightEase = Ease.InQuad;

    [Header("Dim")]
    [SerializeField, Range(0f, 1f)] float _dimAlpha = 0.5f;
    [SerializeField] float _dimFadeIn = 0.2f;
    [SerializeField] float _dimFadeOut = 0.3f;

    Sequence _flight;
    Tween _dimTween;
    bool _playing;
    // Whether the trail should be emitting right now: snowflake crystals are left behind the MOVING icon
    // (button → hold, hold → timer) and pause while it holds still, so they don't pile up on it.
    bool _trailOn;

    void Awake() {
        this.addListener<object>(Events.POWER_UP_1_ACTIVATED, onActivated);
        this.addListener<float>(Events.FREEZE_TIME_CHANGED, onFreezeTimeChanged);
        this.addListener<object>(Events.POWER_UP_1_DEACTIVATED, onCutShort);
        this.addListener<object>(Events.LEVEL_LOADED, onCutShort);
        this.addListener<object>(Events.UI_OPEN_SETTINGS, onSettingsOpened);
        this.addListener<object>(Events.UI_CLOSE_SETTINGS, onSettingsClosed);
        clear();
    }

    void OnDestroy() {
        this.removeListener<object>(Events.POWER_UP_1_ACTIVATED, onActivated);
        this.removeListener<float>(Events.FREEZE_TIME_CHANGED, onFreezeTimeChanged);
        this.removeListener<object>(Events.POWER_UP_1_DEACTIVATED, onCutShort);
        this.removeListener<object>(Events.LEVEL_LOADED, onCutShort);
        this.removeListener<object>(Events.UI_OPEN_SETTINGS, onSettingsOpened);
        this.removeListener<object>(Events.UI_CLOSE_SETTINGS, onSettingsClosed);
    }

    void LateUpdate() {
        if (!_playing) return;
        foreach (ParticleSystem ps in _trail) ps.transform.position = _icon.position;
    }

    void onActivated(Object sender, Event<object> e) => play();

    void onFreezeTimeChanged(Object sender, Event<float> e) {
        if (_playing) burst();
    }

    // The launch whoosh (a 0.8 s one-shot) is left to finish: AudioPlayer.StopSFX stops whatever its
    // pooled AudioSource plays now, which after the whoosh ends can be an unrelated sound.
    void onCutShort(Object sender, Event<object> e) {
        if (_playing) clear();
    }

    // The icon holds still with the activation; the trail stops emitting so it doesn't pile up on it,
    // and picks up again on close only if the icon was moving.
    void onSettingsOpened(Object sender, Event<object> e) {
        if (!_playing) return;
        _flight?.Pause();
        _dimTween?.Pause();
        emitTrail(false);
    }

    void onSettingsClosed(Object sender, Event<object> e) {
        if (!_playing) return;
        _flight?.Play();
        _dimTween?.Play();
        emitTrail(_trailOn);
    }

    void setTrail(bool on) {
        _trailOn = on;
        emitTrail(on);
    }

    void emitTrail(bool on) {
        foreach (ParticleSystem ps in _trail) {
            if (on) ps.Play();
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void play() {
        clear();
        _playing = true;

        float duration = activationSeconds();
        Rect area = ((RectTransform)transform).rect;
        Vector3 hold = transform.TransformPoint(new Vector3(
            Mathf.Lerp(area.xMin, area.xMax, _holdPoint.x),
            Mathf.Lerp(area.yMin, area.yMax, _holdPoint.y), 0f));

        if (_powerUp != null) _iconImage.sprite = _powerUp.sprite;
        _icon.sizeDelta = _from.rect.size;
        _icon.position = _from.position;
        _icon.localScale = Vector3.one;
        _icon.gameObject.SetActive(true);
        foreach (ParticleSystem ps in _trail) ps.transform.position = _icon.position;
        setTrail(true);

        _dimTween = _dim.DOFade(_dimAlpha, _dimFadeIn).SetLink(gameObject);

        AudioPlayer.PlaySFX(AudioFX.WHOOSH_SHORT_1);
        HapticManager.Feedback(HapticFeedbackType.FeedbackLight);

        float rise = duration * _riseEnd;
        float holdEnd = duration * _holdEnd;
        float fly = duration - holdEnd;
        _flight = DOTween.Sequence()
            .Insert(0f, _icon.DOMove(hold, rise).SetEase(_riseEase))
            .Insert(0f, _icon.DOScale(_holdScale, rise).SetEase(_riseEase))
            .InsertCallback(rise, () => setTrail(false))
            .Insert(rise, _icon.DOPunchScale(Vector3.one * 0.08f, holdEnd - rise, 2, 0.5f))
            .InsertCallback(holdEnd, () => setTrail(true))
            .Insert(holdEnd, _icon.DOMove(_to.position, fly).SetEase(_flightEase))
            .Insert(holdEnd, _icon.DOScale(_endScale, fly).SetEase(_flightEase))
            .SetLink(gameObject);
    }

    // The freeze is now active (the HUD fades in on this same event): land the icon, burst at the
    // timer, and let the dim go.
    void burst() {
        _playing = false;
        _flight?.Complete();
        _flight = null;
        _icon.gameObject.SetActive(false);
        setTrail(false);

        _burst.position = _to.position;
        foreach (BurstEmitter b in _burstParticles) {
            b.system.Clear();
            b.system.Play();
            b.system.Emit(b.count);
        }
        AudioPlayer.PlaySFX(AudioFX.ICE_CRACK);
        HapticManager.Feedback(HapticFeedbackType.FeedbackMedium);

        _dimTween?.Kill();
        _dimTween = _dim.DOFade(0f, _dimFadeOut).SetLink(gameObject);
    }

    void clear() {
        _playing = false;
        _flight?.Kill();
        _flight = null;
        _dimTween?.Kill();
        _dimTween = null;
        Color c = _dim.color;
        c.a = 0f;
        _dim.color = c;
        _icon.gameObject.SetActive(false);
        _trailOn = false;
        foreach (ParticleSystem ps in _trail) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (BurstEmitter b in _burstParticles) b.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    [System.Serializable]
    struct BurstEmitter {
        public ParticleSystem system;
        public int count;
    }

    static float activationSeconds() {
        Level level = LevelGenerator.instance != null ? LevelGenerator.instance.currentLevel as Level : null;
        return level != null && level.gameplayTunables != null
            ? level.gameplayTunables.freezeTimeActivationSeconds
            : GameplayTunables.DEFAULT_FREEZE_TIME_ACTIVATION_SECONDS;
    }
}
