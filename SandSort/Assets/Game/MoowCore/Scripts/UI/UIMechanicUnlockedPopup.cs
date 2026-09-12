using Moow;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMechanicUnlockedPopup : MonoBehaviour {
    [SerializeField] Transform _container;
    [SerializeField] NewMechanicsSO _newMechanicsSO;
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _itemNameText;
    [SerializeField] GameObject _tapToContinue;
    [SerializeField] Image _itemIcon;
    [SerializeField] GameObject _vfx;

    private void OnEnable() {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
    }

    private void OnDisable() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
        checkAndShow();
    }

    void checkAndShow() {
        MechanicProgressData data = _newMechanicsSO.getMechanicProgress(LevelManager.instance.levelIndex);
        
        if(data == null || data.percentageEnd != 1) {
            _container.gameObject.SetActive(false);
            return;
        }

        this.dispatchEvent<object>(Events.MECHANIC_UNLOCK_DISPLAYED, null);

        _itemNameText.text = data.mechanic.name;
        _titleText.text = data.mechanic.unlockedTitle;
        _itemIcon.sprite = data.mechanic.mechanicSprite;
        StartCoroutine(playItemUnlockAnimationCorouine());
    }

    private IEnumerator playItemUnlockAnimationCorouine() {
        AudioPlayer.PlaySFX(AudioFX.POSITIVE_2);
        _tapToContinue.SetActive(false);
        _container.gameObject.SetActive(true);
        _canvasGroup.DOFade(1, 0.3f).From(0);
        _vfx.transform.DOScale(675f, 0.3f).From(0);

        float duration = .5f;
        float tapToContinueDuration = 0.5f;
        float tapToContinueDelay = 0.5f;
        Ease animation = Ease.OutBack;
        _titleText.transform.DOScale(0, duration).From().SetEase(animation);
        _itemNameText.transform.DOScale(0, duration).From().SetEase(animation).SetDelay(0.2f);

        yield return _itemIcon.transform.DOScale(0, duration).From().SetEase(animation).WaitForCompletion();
        yield return new WaitForSeconds(tapToContinueDelay);
        _tapToContinue.SetActive(true);
        _tapToContinue.transform.DOScale(1, tapToContinueDuration).From(0).SetEase(animation);

        yield return new WaitUntil(() => Input.GetMouseButton(0));

        AudioPlayer.PlaySFX(AudioFX.UI_BUTTON_CLICK);
        _vfx.transform.DOScale(0, 0.25f).From(675f);
        this.dispatchEvent<object>(Events.MECHANIC_UNLOCK_CLOSED, null);
        yield return _canvasGroup.DOFade(0, 0.5f).WaitForCompletion();
        _container.gameObject.SetActive(false);

    }
}
