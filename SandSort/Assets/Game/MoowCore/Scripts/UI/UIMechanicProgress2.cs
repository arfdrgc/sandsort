using Moow;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMechanicProgress2 : MonoBehaviour {
    [SerializeField] NewMechanicsSO _newMechanicsSO;
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] RectTransform _animationTransform;
    [SerializeField] RectTransform _startPosition;
    [SerializeField] RectTransform _finalPosition;
    [SerializeField] Image _mechanicIcon;
    [SerializeField] Image _mechanicIconBlack;
    [SerializeField] Image _progress;
    [SerializeField] TextMeshProUGUI _progressText;
    [SerializeField] float _animationDuration;
    [SerializeField] float _animationDelay;
    [SerializeField] float _delayBeforeReposition;

    [Header("Punch Animation")]
    [SerializeField] float _punchScaleDuration;
    [SerializeField] float _punchScaleStrength;
    [SerializeField] int _punchScaleLoops;

    [SerializeField] Transform _youWinImg;

    [SerializeField] UIRewardGold _rewardGold;

    System.Action _animationComplete;

    public void show(float globalDelay, System.Action animationComplete) {

        _animationComplete = animationComplete;
        MechanicProgressData data = _newMechanicsSO.getMechanicProgress(LevelManager.instance.levelIndex + 1);

        if(data == null) {
            //_youWinImg.DOScale(1, 0.6f).SetEase(Ease.OutBack);
            AudioPlayer.instance.playSFX(AudioFX.ITEM_LOADED);
            _rewardGold.show(GameDataManager.instance.levelCompleteReward);
            _animationComplete?.Invoke();
            gameObject.SetActive(false);
            return;
        }

        _animationTransform.localScale = Vector3.zero;
        _animationTransform.position = _startPosition.position;

        _animationTransform.DOScale(0.8f, 0.5f).SetDelay(globalDelay).SetEase(Ease.OutBack);


        _progress.fillAmount = data.percentageStart;
        _mechanicIcon.sprite = data.mechanic.mechanicSprite;
        _mechanicIconBlack.sprite = data.mechanic.mechanicUnrevealedSprite;

        startTextAnimation(globalDelay, data.percentageStart, data.percentageEnd);

        DOVirtual.DelayedCall(globalDelay + _animationDelay, () => {
            _progress.DOFillAmount(data.percentageEnd, _animationDuration); //_animationDuration
            punchAnimation();

            DOVirtual.DelayedCall(_animationDuration, () => {
                if(data.percentageEnd == 1) {
                    showUnlockedAnimation();
                }
            });
        });

        return;
    }

    void startTextAnimation(float globalDelay, float start, float end) {
        void onComplete() {
            float duration = 0.33f;
            _animationTransform.DOMove(_finalPosition.position, duration).SetDelay(_delayBeforeReposition);
            _animationTransform.DOScale(Vector3.one * 0.75f, duration).SetDelay(_delayBeforeReposition).OnComplete(() => {
                _animationComplete?.Invoke();
            });
        }

        _progressText.text = $"{(int)(start * 100)}%";
        DOVirtual.Float(start, end, _animationDuration, (t) => {
            _progressText.text = $"{(int)(t * 100)}%";
            AudioPlayer.instance.playSFX(AudioFX.ITEM_COMPLETE_1);
        }).SetDelay(globalDelay + _animationDelay).SetLink(gameObject).OnComplete(onComplete);
    }

    public void hide() {
        _animationTransform.localScale = Vector3.zero;
        _animationTransform.position = _startPosition.position;
        //_canvasGroup.alpha = 0;
    }

    void punchAnimation() {

        //transform.localScale = Vector3.one;
        //transform.DOPunchScale(Vector3.one * _punchScaleStrength, _punchScaleDuration).SetLoops(_punchScaleLoops, LoopType.Restart);
    }

    void showUnlockedAnimation() {

    }
}
