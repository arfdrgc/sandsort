using DG.Tweening;
using Moow;
using MoowCore;
using UnityEngine;
using UnityEngine.UI;

// First-time booster unlock + usage tutorial, for any booster in the bar. When a level loads whose
// number is a booster's PowerUpSO.unlockLevel and that booster's inventory item is not yet isShowed:
//   1. the Feature Unlock popup (UIMechanicUnlockedPopup) shows the booster's icon, name and purpose;
//   2. on its tap the gameplay fades out behind a dark overlay, and only that booster's button stays
//      above it (UIPowerUpButton.setTutorialFocus), with the hand gliding onto it;
//   3. pressing it fades the overlay back out, marks the booster shown (setItemShowed, saved), and the
//      press carries on through the booster's normal activation.
// Gameplay input is locked from the popup to the press (Level, via BOOSTER_TUTORIAL_*); the overlay
// blocks every other UI tap (Settings, restart, other boosters). A level load, retry, win or lose cuts
// the tutorial short: everything is restored on the spot and the booster is NOT marked shown, so it
// comes back next time.
//
// Listeners live for the component's whole life (Awake/OnDestroy).
public class UIBoosterTutorial : MonoBehaviour {
    [Header("References")]
    [SerializeField] UIPowerUpManager _powerUps;
    [SerializeField] UIMechanicUnlockedPopup _unlockPopup;
    [Tooltip("Full-screen dark overlay behind the highlighted booster. Its color's alpha is ignored: see Overlay Alpha.")]
    [SerializeField] Image _overlay;

    [Header("Unlock popup")]
    [SerializeField] string _unlockTitle = "New Booster Unlocked";
    [Tooltip("Size of the booster's purpose line (PowerUpSO.popupText) under its name, relative to the name.")]
    [SerializeField, Range(0.3f, 1f)] float _purposeTextScale = 0.6f;

    [Header("Overlay")]
    [SerializeField, Range(0f, 1f)] float _overlayAlpha = 0.9f;
    [SerializeField] float _fadeInDuration = 0.8f;
    [SerializeField] float _fadeOutDuration = 0.3f;

    PowerUpSO _data;
    UIPowerUpButton _button;
    Tween _fade;

    void Awake() {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<PowerUpSO>(Events.BOOSTER_TUTORIAL_COMPLETED, onCompleted);
        this.addListener<object>(Events.UI_RETRY_CLICKED, onCutShort);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onCutShort);
        this.addListener<object>(Events.FAIL_CONDITION_MET, onCutShort);
        setOverlay(0f);
    }

    void OnDestroy() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<PowerUpSO>(Events.BOOSTER_TUTORIAL_COMPLETED, onCompleted);
        this.removeListener<object>(Events.UI_RETRY_CLICKED, onCutShort);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onCutShort);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, onCutShort);
    }

    // One frame later, so the booster bar has been set up for the new level (UIPowerUpManager) and a
    // Feature Unlock popup for this level, if any, is already up: the booster's popup follows it.
    void onLevelLoaded(Object sender, Event<object> e) {
        cancel();
        StopAllCoroutines();
        StartCoroutine(startWhenReady());
    }

    System.Collections.IEnumerator startWhenReady() {
        yield return null;
        while (_unlockPopup.isShowing) yield return null;

        PowerUpSO data = findUnlockedNow();
        if (data == null) yield break;
        UIPowerUpButton button = _powerUps.buttonFor(data);
        if (button == null || !button.gameObject.activeInHierarchy) yield break;

        _data = data;
        _button = button;
        this.dispatchEvent(Events.BOOSTER_TUTORIAL_STARTED, _data);

        string name = string.IsNullOrEmpty(_data.popupText)
            ? _data.popupTitle
            : $"{_data.popupTitle}\n<size={Mathf.RoundToInt(_purposeTextScale * 100f)}%>{_data.popupText}</size>";
        _unlockPopup.show(_unlockTitle, name, _data.sprite, highlight);
    }

    // The first booster that unlocks on this very level and hasn't had its tutorial yet.
    PowerUpSO findUnlockedNow() {
        int level = LevelManager.instance.level;
        foreach (PowerUpSO data in _powerUps.powerUps) {
            if (data == null || data.unlockLevel != level) continue;
            ItemSaveData save = InventoryManager.instance.getItemSaveData(data.itemID);
            if (save != null && !save.isShowed) return data;
        }
        return null;
    }

    void highlight() {
        if (_data == null) return;
        _button.setTutorialFocus(true);
        _overlay.raycastTarget = true;
        fadeOverlay(_overlayAlpha, _fadeInDuration, Ease.InOutSine);
    }

    // Sent by the highlighted button just before its press is handled like any other.
    void onCompleted(Object sender, Event<PowerUpSO> e) {
        if (_data == null || e.data != _data) return;
        InventoryManager.instance.setItemShowed(_data.itemID);
        _button.setTutorialFocus(false);
        _overlay.raycastTarget = false;
        fadeOverlay(0f, _fadeOutDuration, Ease.OutQuad);
        _data = null;
        _button = null;
    }

    void onCutShort(Object sender, Event<object> e) => cancel();

    void cancel() {
        StopAllCoroutines();
        if (_data == null) return;
        PowerUpSO data = _data;
        _unlockPopup.cancel();
        _button.setTutorialFocus(false);
        setOverlay(0f);
        _data = null;
        _button = null;
        this.dispatchEvent(Events.BOOSTER_TUTORIAL_CANCELLED, data);
    }

    void fadeOverlay(float alpha, float duration, Ease ease) {
        _fade?.Kill();
        _fade = _overlay.DOFade(alpha, duration).SetEase(ease).SetLink(gameObject);
    }

    void setOverlay(float alpha) {
        _fade?.Kill();
        _fade = null;
        Color c = _overlay.color;
        c.a = alpha;
        _overlay.color = c;
        _overlay.raycastTarget = false;
    }
}
