using Moow;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPowerUpPopUp : MonoBehaviour {
    [SerializeField] GameObject _container;
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _infoText;
    [SerializeField] Image _powerUpImage;
    [SerializeField] Button _closeButton;

    private void OnEnable() {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<PowerUpSO>(Events.UI_POWER_UP_TUTORIAL_REQUIRED, onPowerUpTutorialRequired);
        this.addListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
        this.addListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        //this.addListener<Snake>(Events.SNAKE_READY_TO_BEAM, onSnakeReadyToBeam);
        this.addListener<object>(Events.UI_RETRY_CLICKED, onUiRetryClicked);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onPowerUpCancel);
        this.addListener<object>(Events.FAIL_CONDITION_MET, onPowerUpCancel);
        _closeButton.onClick.AddListener(onCloseClicked);
    }

    private void OnDisable() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<PowerUpSO>(Events.UI_POWER_UP_TUTORIAL_REQUIRED, onPowerUpTutorialRequired);
        this.removeListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
        this.removeListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        //this.removeListener<Snake>(Events.SNAKE_READY_TO_BEAM, onSnakeReadyToBeam);
        this.removeListener<object>(Events.UI_RETRY_CLICKED, onUiRetryClicked);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onPowerUpCancel);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, onPowerUpCancel);
        _closeButton.onClick.RemoveListener(onCloseClicked);
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
        hide();
    }

    private void onPowerUpTutorialRequired(UnityEngine.Object sender, Event<PowerUpSO> eventData) {
        Debug.Log("UI PowerUp Popup");
        show(eventData.data);
    }

    private void onPowerUpUsed(UnityEngine.Object sender, Event<PowerUpSO> eventData) {
        hide();
    }

    private void onPowerUpCancel(UnityEngine.Object sender, Event<object> eventData) {
        hide();
    }

    private void onCloseClicked() {
        this.dispatchEvent<object>(Events.UI_CANCEL_POWER_UP, null);
        AudioPlayer.instance.playSFX(AudioFX.UI_BUTTON_CLICK);
    }

    private void onUiRetryClicked(UnityEngine.Object sender, Event<object> eventData)
    {
        this.dispatchEvent<object>(Events.UI_CANCEL_POWER_UP, null);
        hide();
    }

    public void show(PowerUpSO powerUpData) {

        Debug.Log("show popup");

        _container.SetActive(true);
        _titleText.text = powerUpData.popupTitle;
        _infoText.text = powerUpData.popupText;
        _powerUpImage.sprite = powerUpData.sprite;
    }

    public void hide() {
        _container.SetActive(false);
    }
}
