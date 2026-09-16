using Moow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Moow.Ads.Core;

namespace MoowCore {
    public class GameSuccessPopup : BasePopup<Object> {
        [SerializeField] UIMechanicProgress2 _mechanicProgress;
        [SerializeField] Button _claimButton, _doubleButton;
        [SerializeField] TextMeshProUGUI _doubleButtonText, _claimButtonText;
        [SerializeField] CanvasGroup _claimButtonCanvasGroup, _doubleButtonCanvasGroup;
        [SerializeField] UIRewardGold _rewardGold;
        [SerializeField] Transform banner, lineMask, multiplyScore;

        [SerializeField] UIGoldContainer _gameSceneGoldContainer;

        private float multiplyScoreNum;
        [SerializeField] Animator multiplyScoreAnimator;

        // One reward per shown popup: Continue and the rewarded-ad callback both go through
        // claimReward, so a double tap or a late ad callback cannot pay or advance the level twice.
        bool _rewardClaimed;

        protected override void OnEnable() {
            _claimButton.onClick.AddListener(onClaimClick);
            _doubleButton.onClick.AddListener(onDoubleClaimClick);
        }

        protected override void OnDisable() {
            _claimButton.onClick.RemoveListener(onClaimClick);
            _doubleButton.onClick.RemoveListener(onDoubleClaimClick);
        }

        public override void initialize() {

        }

        private void SetDefault()
        {
            _rewardGold.gameObject.SetActive(false);
            lineMask.gameObject.SetActive(false);

            _claimButton.gameObject.SetActive(false);
            _claimButtonCanvasGroup.alpha = 0;
            _claimButton.interactable = false;

            _doubleButton.gameObject.SetActive(false);
            _doubleButtonCanvasGroup.alpha = 0;
            _doubleButton.transform.localScale = Vector3.one;
            _doubleButton.transform.DOKill();
            _doubleButton.interactable = false;

            _mechanicProgress.hide();

            multiplyScore.localScale = Vector3.zero;
            multiplyScore.gameObject.SetActive(false);
        }


        public override void show(Object data) {
            MusicPlayer.instance.lowerMusic();
            AudioPlayer.instance.playSFX(AudioFX.POSITIVE_1);
            base.show(data);
            SetDefault();
            _rewardClaimed = false;

            banner.transform.localScale = Vector3.zero;
            banner.transform.localPosition = Vector3.zero;

            _claimButtonText.text = GameDataManager.instance.levelCompleteReward + "";
            multiplyScoreAnimator.enabled = true;

            banner.transform.DOScale(1, 1).SetEase(Ease.OutBack).OnComplete(() => {
                AudioPlayer.instance.playSFX(AudioFX.WHOOSH_SHORT_2);
                lineMask.gameObject.SetActive(true);
            });
            banner.transform.DOLocalMoveY(650, 0.5f).SetEase(Ease.OutBack).SetDelay(1f).OnComplete(() => {
            _mechanicProgress.show(0, () => {

                    if (LevelManager.instance.level == 1)
                    {
                        _claimButton.gameObject.SetActive(true);
                        _claimButtonCanvasGroup.DOFade(1, 0.3f).From(0);
                        _claimButton.interactable = true;
                    }
                    else
                    {
                        multiplyScore.gameObject.SetActive(true);
                        multiplyScore.DOScale(0.6f, 1).SetEase(Ease.OutBack);

                        _doubleButton.gameObject.SetActive(true);
                        _doubleButtonCanvasGroup.DOFade(1, 0.3f).From(0);
                        _doubleButton.interactable = true;
                        _doubleButton.transform.DOScale(0.9f, 1.5f).SetLoops(-1, LoopType.Yoyo);

                        DOVirtual.DelayedCall(1.5f, () =>
                        {
                                _claimButton.gameObject.SetActive(true);
                                _claimButtonCanvasGroup.DOFade(1, 0.3f).From(0);
                                _claimButton.interactable = true;
                        });
                    }
                });
            });
        }

        public override void hide(float duration = 0.3F, bool initialize = false) {
            base.hide(duration, initialize);
        }

        private void onClaimClick() {
            if (_rewardClaimed) return;
            AudioPlayer.PlaySFX(AudioFX.UI_BUTTON_CLICK);
            claimReward(GameDataManager.instance.levelCompleteReward);
        }

        // The coin burst goes first: UIGoldContainer registers the amount as "still flying" before
        // MONEY_CHANGED arrives, so its counter rises with the coins instead of jumping. The money is
        // added (and saved) on the claim itself, then the existing next-level flow runs.
        void claimReward(float value) {
            if (_rewardClaimed) return;
            _rewardClaimed = true;
            _claimButton.interactable = false;
            _doubleButton.interactable = false;

            int amount = Mathf.RoundToInt(value);
            this.dispatchEvent<int>(Events.GIVE_COIN_ANIMATION, amount);
            InventoryManager.instance.increase(amount);
            this.dispatchEvent<object>(Events.UI_NEXT_LEVEL_CLICK, null);
        }

        // private void onDoubleClaimClick() {
        //     _doubleButton.interactable = false;
        //     multiplyScoreAnimator.enabled = false;

        //     AdsService.Instance.ShowRewarded(result =>
        //     {
        //         Debug.Log("result : " + result);

        //         if (result == AdResult.Rewarded)
        //         {
        //             Debug.Log("User rewarded → reward ver");
        //             float amount = GameDataManager.instance.levelCompleteReward + multiplyScoreNum;
        //             this.dispatchEvent<int>(Events.GIVE_COIN_ANIMATION, (int)amount);
        //             this.dispatchEvent<object>(Events.UI_NEXT_LEVEL_CLICK, null);
        //             DOVirtual.DelayedCall(0.1f, ()=>{ InventoryManager.instance.increase(amount); });

        //         }
        //         else if (result == AdResult.Displayed)
        //         {
        //             this.dispatchEvent<int>(Events.GIVE_COIN_ANIMATION, GameDataManager.instance.levelCompleteReward);
        //             this.dispatchEvent<object>(Events.UI_NEXT_LEVEL_CLICK, null);
        //             DOVirtual.DelayedCall(0.1f, ()=>{ InventoryManager.instance.increase(GameDataManager.instance.levelCompleteReward); });
        //             Debug.Log("Ad shown but not rewarded");
        //         }
        //         else if (result == AdResult.Failed)
        //         {
        //             _doubleButton.interactable = true;
        //             multiplyScoreAnimator.enabled = true;
        //             Debug.Log("Rewarded ad failed");
        //         }
        //     });
        // }

        private void onDoubleClaimClick()
    {
        if (_rewardClaimed) return;
        _doubleButton.interactable = false;
        multiplyScoreAnimator.enabled = false;

        AdsService.Instance.ShowRewarded(result =>
        {
            Debug.Log($"Reward Result : {result}");

            switch (result)
            {
                case AdResult.Rewarded:

                    claimReward(
                        GameDataManager.instance.levelCompleteReward +
                        multiplyScoreNum);

                    break;

                case AdResult.Skipped:

                    Debug.Log("User skipped rewarded ad");

                    _doubleButton.interactable = true;
                    multiplyScoreAnimator.enabled = true;

                    break;

                case AdResult.Failed:

                    Debug.Log("Rewarded ad failed");

                    _doubleButton.interactable = true;
                    multiplyScoreAnimator.enabled = true;

                    break;

                case AdResult.NotReady:

                    Debug.Log("Rewarded ad not ready");

                    _doubleButton.interactable = true;
                    multiplyScoreAnimator.enabled = true;

                    break;
            }
        });
    }

        public void UpdateDoubleButtonAmount(float multiplyScore)
        {
            HapticManager.instance.feedback(HapticFeedbackType.FeedbackLight);
            multiplyScoreNum = multiplyScore;
            float amount = GameDataManager.instance.levelCompleteReward + multiplyScoreNum;
            _doubleButtonText.text = amount +"";
        }
    }
}