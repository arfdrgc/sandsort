using Moow;
using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.EventTrigger;

namespace MoowCore {
    public class GameFailurePopup : BasePopup<Object> {
        [SerializeField] TextMeshProUGUI _reviveCost;
        [SerializeField] Button _retryButton;
        [SerializeField] Button _reviveButton;
        [SerializeField] Transform _popupContainerArea;
        [Tooltip("The revive offer text (\"Get 20 seconds to keep playing!\"); hidden with the Revive button when coins are off.")]
        [SerializeField] GameObject _reviveDescText;

        // Retry's authored anchors (left half, next to Revive), captured before it is ever moved.
        Vector2 _retryAnchorMin, _retryAnchorMax;
        bool _retryAnchorsStored;

        protected override void OnEnable() {
            _retryButton.onClick.AddListener(onRetryClicked);
            _reviveButton.onClick.AddListener(onReviveClicked);
        }

        protected override void OnDisable() {
            _retryButton.onClick.RemoveListener(onRetryClicked);
            _reviveButton.onClick.RemoveListener(onReviveClicked);
        }

        protected override void Start() {
            _reviveCost.text = "" + GameDataManager.instance.currentReviveCost;
        }

        private void onRetryClicked() {
            _retryButton.interactable = false;
            AudioPlayer.instance.playSFX(AudioFX.UI_BUTTON_CLICK);
            this.dispatchEvent<object>(Events.UI_RETRY_CLICKED, null);
        }

        private void onReviveClicked() {
            _reviveButton.interactable = false;
            AudioPlayer.instance.playSFX(AudioFX.UI_BUTTON_CLICK);
            // Price this revive BEFORE counting it, and send the price with the event, so the charge
            // does not depend on which UI_REVIVE_CLICKED listener runs first.
            int cost = GameDataManager.instance.currentReviveCost;
            GameDataManager.instance.registerRevive();
            this.dispatchEvent<object>(Events.UI_REVIVE_CLICKED, cost);
            hide();
        }

        public override void initialize() {

        }

        public override void show(Object data) {
            MusicPlayer.instance.pauseMusic();
            base.show(data);
            _popupContainerArea.DOScale(1, _fadeInDuration).SetEase(Ease.OutBack);
            float currentGold = InventoryManager.instance.money;
            int reviveCost = GameDataManager.instance.currentReviveCost;
            _reviveCost.text = "" + reviveCost;
            _retryButton.interactable = true;
            _reviveButton.interactable = currentGold >= reviveCost;
            applyCoinLayout(GameDataManager.instance.coinsEnabled);
            AudioPlayer.instance.playSFX(AudioFX.LEVEL_FAILED);
        }

        // Coins off (GameDataSO.coinsEnabled): no coin revive (button and offer text), and Retry moves to the middle keeping its
        // width. Coins on: the authored layout, Retry left and Revive right.
        void applyCoinLayout(bool coinsEnabled) {
            RectTransform retry = (RectTransform)_retryButton.transform;
            if (!_retryAnchorsStored) {
                _retryAnchorMin = retry.anchorMin;
                _retryAnchorMax = retry.anchorMax;
                _retryAnchorsStored = true;
            }

            _reviveButton.gameObject.SetActive(coinsEnabled);
            _reviveDescText.SetActive(coinsEnabled);
            if (coinsEnabled) {
                retry.anchorMin = _retryAnchorMin;
                retry.anchorMax = _retryAnchorMax;
            } else {
                float halfWidth = (_retryAnchorMax.x - _retryAnchorMin.x) * 0.5f;
                retry.anchorMin = new Vector2(0.5f - halfWidth, _retryAnchorMin.y);
                retry.anchorMax = new Vector2(0.5f + halfWidth, _retryAnchorMax.y);
            }
        }

        public override void hide(float duration = 0.3F, bool initialize = false) {
            _retryButton.interactable = false;
            _retryButton.interactable = false;
            _popupContainerArea.DOScale(0, duration).SetEase(Ease.InBack).OnComplete(()=>  base.hide(duration, initialize));
        }
    }
}