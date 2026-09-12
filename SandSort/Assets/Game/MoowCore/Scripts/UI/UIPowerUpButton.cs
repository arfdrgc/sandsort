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

    [Header("Failed Animation")]
    [SerializeField] float _failedAnimationRotation;
    [SerializeField] float _failedAnimationDuration;
    [SerializeField] int _failedAnimationVibrate;
    [SerializeField] float _failedAnimationElasticity;

    PowerUpSO _data;
    bool _isLocked;

    private void OnEnable() {
        _button.onClick.AddListener(onClick);
        this.addListener<PowerUpSO>(Events.POWER_UP_FAILED_TO_USE, onFailedToUse);
        this.addListener<object>(Events.FREE_POWER_UP_CHANGED, onFreePowerUpChanged);
        this.addListener<object>(Events.MECHANIC_UNLOCK_CLOSED, onMechanicUnlockClosed);
        this.addListener<object>(Events.ON_POWER_UP_TUTORIAL_READY, onTutorialReady);
        

        _unlockedAt.gameObject.SetActive(false);
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(onClick);
        this.removeListener<PowerUpSO>(Events.POWER_UP_FAILED_TO_USE, onFailedToUse);
        this.removeListener<object>(Events.FREE_POWER_UP_CHANGED, onFreePowerUpChanged);
        this.removeListener<object>(Events.MECHANIC_UNLOCK_CLOSED, onMechanicUnlockClosed);
        this.removeListener<object>(Events.ON_POWER_UP_TUTORIAL_READY, onTutorialReady);
    }

    private void onMechanicUnlockClosed(UnityEngine.Object sender, Event<object> eventData)
    {
        ItemSaveData saveData = InventoryManager.instance.getItemSaveData(_data.itemID);

        if(saveData != null && !saveData.isShowed && _data.unlockLevel == LevelManager.instance.level)
        {
            LevelTutorial tutorial = LevelManager.instance.currentLevel.levelTutorial;

            if(tutorial == null)
            {
                _handPoint.SetActive(true);
            }

            InventoryManager.instance.setItemShowed(_data.itemID);
        }
    }

    private void onTutorialReady(UnityEngine.Object sender, Event<object> eventData)
    {
        if(_data.unlockLevel == LevelManager.instance.level)
        {
            _handPoint.SetActive(true);
        }
    }

    void onClick() {
        if(_isLocked) {
            HapticManager.instance.feedback(HapticFeedbackType.FeedbackLight);
            failedToUseAnimation();
            failedPopupInfo($"Unlocked At Level {_data.unlockLevel}");
            
        } else {
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

        if(_handPoint.activeSelf)
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
