using Moow;
using MoowCore;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPowerUpButton : MonoBehaviour {
    [Header("Background")]
    [SerializeField] Image _backgroundImage;
    [SerializeField] Sprite _lockedBackgroundSprite;
    [SerializeField] Sprite _unlockedBackgroundSprite;
    [SerializeField] Color _lockedColor;

    [Header("Other References")]
    [SerializeField] Image _powerUpImage;
    [SerializeField] Button _button;
    [SerializeField] GameObject _locked;
    [SerializeField] GameObject _freeCount;
    [SerializeField] GameObject _moneyCost;
    [SerializeField] TextMeshProUGUI _unlockedText;
    [SerializeField] TextMeshProUGUI _freeCountText;
    [SerializeField] TextMeshProUGUI _moneyCostText;
    [SerializeField] TextMeshProUGUI _unlockedAt;
    [SerializeField] GameObject _handPoint;

    [Header("Tutorial Highlight")]
    [Tooltip("Sorting order the button is lifted to while the booster tutorial highlights it, so it draws and takes taps above the tutorial overlay.")]
    [SerializeField] int _tutorialSortingOrder = 10;
    [Tooltip("Where the hand starts each loop, relative to its resting spot on the button; it glides in from there.")]
    [SerializeField] Vector2 _handApproachOffset = new Vector2(150f, 190f);
    [SerializeField] float _handApproachDuration = 0.55f;
    [Tooltip("How long the hand rests on the button (its tap animation playing) before the next approach.")]
    [SerializeField] float _handRestDuration = 1.3f;

    [Header("Failed Animation")]
    [SerializeField] float _failedAnimationRotation;
    [SerializeField] float _failedAnimationDuration;
    [SerializeField] int _failedAnimationVibrate;
    [SerializeField] float _failedAnimationElasticity;

    PowerUpSO _data;
    bool _isLocked;
    bool _tutorialFocus;
    Canvas _tutorialCanvas;
    Vector2 _handRest;
    Sequence _handLoop;

    // Freeze Time can't be stacked, so its button can't be pressed from the tap until the freeze ends.
    // Registered for the component's whole life, not in OnEnable: the buttons are hidden on win/lose,
    // and the freeze ending then must still re-enable this one.
    private void Awake() {
        this.addListener<object>(Events.POWER_UP_1_ACTIVATED, onFreezeTimeStarted);
        this.addListener<object>(Events.POWER_UP_1_DEACTIVATED, onFreezeTimeEnded);
    }

    private void OnDestroy() {
        this.removeListener<object>(Events.POWER_UP_1_ACTIVATED, onFreezeTimeStarted);
        this.removeListener<object>(Events.POWER_UP_1_DEACTIVATED, onFreezeTimeEnded);
    }

    private void onFreezeTimeStarted(UnityEngine.Object sender, Event<object> eventData) {
        if (_data != null && _data.type == PowerUpType.PUT_1) _button.interactable = false;
    }

    private void onFreezeTimeEnded(UnityEngine.Object sender, Event<object> eventData) {
        if (_data != null && _data.type == PowerUpType.PUT_1) _button.interactable = true;
    }

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
        this.addListener<PowerUpSO>(Events.POWER_UP_FAILED_TO_USE, onFailedToUse);
        this.addListener<object>(Events.FREE_POWER_UP_CHANGED, onFreePowerUpChanged);

        _unlockedAt.gameObject.SetActive(false);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
        this.removeListener<PowerUpSO>(Events.POWER_UP_FAILED_TO_USE, onFailedToUse);
        this.removeListener<object>(Events.FREE_POWER_UP_CHANGED, onFreePowerUpChanged);
    }

    // The booster tutorial (UIBoosterTutorial) highlights this button: it is lifted above the tutorial
    // overlay (its own override-sorted canvas, with a raycaster so it still takes the tap) and the hand
    // keeps gliding onto it. Off puts it back in the bar and hides the hand.
    public void setTutorialFocus(bool on) {
        if (on == _tutorialFocus) return;
        _tutorialFocus = on;

        if (_tutorialCanvas == null) {
            // Added once and kept: a nested canvas needs its own raycaster, since the root one no longer
            // sees graphics under it.
            _tutorialCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
            _handRest = ((RectTransform)_handPoint.transform).anchoredPosition;
        }
        _tutorialCanvas.overrideSorting = on;
        _tutorialCanvas.sortingOrder = on ? _tutorialSortingOrder : 0;

        _handLoop?.Kill();
        _handLoop = null;
        RectTransform hand = (RectTransform)_handPoint.transform;
        Image handImage = _handPoint.GetComponent<Image>();
        hand.anchoredPosition = _handRest;
        handImage.color = Color.white;
        _handPoint.SetActive(on);
        if (!on) return;

        _handLoop = DOTween.Sequence()
            .AppendCallback(() => {
                hand.anchoredPosition = _handRest + _handApproachOffset;
                handImage.color = new Color(1f, 1f, 1f, 0f);
            })
            .Append(hand.DOAnchorPos(_handRest, _handApproachDuration).SetEase(Ease.OutCubic))
            .Join(handImage.DOFade(1f, _handApproachDuration * 0.6f))
            .AppendInterval(_handRestDuration)
            .Append(handImage.DOFade(0f, 0.2f))
            .SetLoops(-1)
            .SetLink(gameObject);
    }

    void onClick() {
        if(_isLocked) {
            HapticManager.instance.feedback(HapticFeedbackType.FeedbackLight);
            failedToUseAnimation();
            failedPopupInfo($"Unlocked At Level {_data.unlockLevel}");
            
        } else {
            // A highlighted booster ends its tutorial first: that starts the level (Level), which the
            // booster may need (Freeze Time does) before it is pressed below.
            if (_tutorialFocus) this.dispatchEvent(Events.BOOSTER_TUTORIAL_COMPLETED, _data);
            this.dispatchEvent(Events.UI_POWER_UP_PRESSED, _data);
        }
    }

    private void onFailedToUse(UnityEngine.Object sender, Event<PowerUpSO> eventData) {
        if(eventData.data.type == _data.type) {
            failedToUseAnimation();
            if(InventoryManager.instance.canUsePowerUp(_data) == false) {
                failedPopupInfo($"Not Enough Gold");
            }
        }
    }

    void failedToUseAnimation() {
        transform.DOBlendablePunchRotation(Vector3.forward * _failedAnimationRotation, _failedAnimationDuration, _failedAnimationVibrate, _failedAnimationElasticity);
    }

    void failedPopupInfo(string text) {
        _unlockedAt.text = text;
        _unlockedAt.gameObject.SetActive(true);
        _unlockedAt.transform.DOKill();


        _unlockedAt.rectTransform.offsetMin = Vector2.zero; // Left, Bottom
        _unlockedAt.rectTransform.offsetMax = Vector2.zero;

        _unlockedAt.transform.localScale = Vector3.one;
        _unlockedAt.transform.DOBlendableLocalMoveBy(Vector3.up * 100, 1f);
        _unlockedAt.transform.DOScale(0, 0.15f).SetDelay(0.85f);
    }

    private void onFreePowerUpChanged(UnityEngine.Object sender, Event<object> eventData) {
        init(_data);
    }

    public void init(PowerUpSO data) {

        if(_handPoint.activeSelf && !_tutorialFocus)
            _handPoint.SetActive(false);

        _data = data;
        _isLocked = LevelManager.instance.level < _data.unlockLevel;
        int freeCount = (int)InventoryManager.instance.getItemCount(_data.itemID);

        _powerUpImage.sprite = _isLocked ? data.spriteLocked : data.sprite;
        _powerUpImage.enabled = !_isLocked;
        _backgroundImage.sprite = _isLocked ? _lockedBackgroundSprite : _unlockedBackgroundSprite;
        _backgroundImage.color = _isLocked ? _lockedColor : Color.white;
        _powerUpImage.color = _isLocked ? _lockedColor : Color.white;
        _locked.gameObject.SetActive(_isLocked);

        if(_freeCount != null) {
            _freeCount.gameObject.SetActive(_isLocked == false && freeCount > 0);
        }
        if(_moneyCost != null) {
            _moneyCost.gameObject.SetActive(_isLocked == false && freeCount <= 0);
        }

        _unlockedText.text = "LVL " + data.unlockLevel;

        if(_freeCountText != null) {
            _freeCountText.text = freeCount.ToString();
        }
        if(_moneyCostText != null) {
            _moneyCostText.text = data.goldCost.ToString();
        }
    }
}
