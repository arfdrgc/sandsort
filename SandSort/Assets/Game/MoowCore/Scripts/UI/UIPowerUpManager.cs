using DG.Tweening;
using Moow;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIPowerUpManager : MonoBehaviour {
    [SerializeField] List<PowerUpSO> _powerUpDatas;
    [SerializeField] List<UIPowerUpButton> _powerUpButtons;

    [SerializeField] private CanvasGroup _canvasGroup;

    public IReadOnlyList<PowerUpSO> powerUps => _powerUpDatas;

    // The bar's button for a booster (same index as its data), or null.
    public UIPowerUpButton buttonFor(PowerUpSO data) {
        int index = _powerUpDatas.IndexOf(data);
        return index >= 0 && index < _powerUpButtons.Count ? _powerUpButtons[index] : null;
    }

    private void Awake() {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<object>(Events.UI_CANCEL_POWER_UP, onCancelPowerUp);
        this.addListener<PowerUpSO>(Events.UI_POWER_UP_PRESSED, onPowerUpPressed);
        this.addListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
        this.addListener<object>(Events.UI_RETRY_CLICKED, onCancelPowerUp);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
        this.addListener<object>(Events.FAIL_CONDITION_MET, onLevelFailed);
        this.addListener<object>(Events.MECHANIC_UNLOCK_DISPLAYED, onMechanicUnlockDisplayed);
        this.addListener<object>(Events.MECHANIC_UNLOCK_CLOSED, onMechanicUnlockClosed);
        this.addListener<object>(Events.INCREASE_DOCK, onIncreaseDock);
    }

    private void Start() {
        updateButtons();
    }

    private void OnDestroy() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<object>(Events.UI_CANCEL_POWER_UP, onCancelPowerUp);
        this.removeListener<PowerUpSO>(Events.UI_POWER_UP_PRESSED, onPowerUpPressed);
        this.removeListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
        this.removeListener<object>(Events.UI_RETRY_CLICKED, onCancelPowerUp);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelCompleted);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, onLevelFailed);
        this.removeListener<object>(Events.MECHANIC_UNLOCK_DISPLAYED, onMechanicUnlockDisplayed);
        this.removeListener<object>(Events.MECHANIC_UNLOCK_CLOSED, onMechanicUnlockClosed);
        this.removeListener<object>(Events.INCREASE_DOCK, onIncreaseDock);
    }

    private void onIncreaseDock(UnityEngine.Object sender, Event<object> eventData)
    {
        showButtons();
    }

    private void onMechanicUnlockDisplayed(UnityEngine.Object sender, Event<object> eventData)
    {
        _canvasGroup.DOFade(0, 0.3f).From(1);
    }

    private void onMechanicUnlockClosed(UnityEngine.Object sender, Event<object> eventData)
    {
        _canvasGroup.DOFade(1, 0.3f).From(0);
    }

    private void onCancelPowerUp(UnityEngine.Object sender, Event<object> eventData)
    {
        showButtons();
    }

    private void onCancelPowerUp(UnityEngine.Object sender, Event<PowerUpSO> eventData)
    {
        showButtons();
    }

    private void onPowerUpUsed(UnityEngine.Object sender, Event<PowerUpSO> eventData)
    {        
        updateButtons();
    }

    private void onPowerUpPressed(UnityEngine.Object sender, Event<PowerUpSO> eventData)
    {
        // if(eventData.data.type == PowerUpType.PUT_2)
        //     hideButtons();
    }

    private void onLevelCompleted(UnityEngine.Object sender, Event<object> eventData)
    {
        hideButtons();
    }

    private void onLevelFailed(UnityEngine.Object sender, Event<object> eventData)
    {
        hideButtons();
    }

    private void hideButtons()
    {
        for (int i = 0; i < _powerUpButtons.Count; i++)
        {
            _powerUpButtons[i].gameObject.SetActive(false);
        }
    }

    private void showButtons()
    {
        // Boosters switched off game-wide (GameDataSO.boostersEnabled): the bar stays hidden.
        if (!GameDataManager.instance.boostersEnabled) {
            hideButtons();
            return;
        }

        if(LevelManager.instance.level == 1)
            return;
            
        for (int i = 0; i < _powerUpButtons.Count; i++)
        {
            _powerUpButtons[i].gameObject.SetActive(true);
        }
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
        showButtons();
        updateButtons();
    }

    void updateButtons() {
        for(int i = 0; i < _powerUpDatas.Count; i++) {
            _powerUpButtons[i].init(_powerUpDatas[i]);
        }

        showButtons();
    }
}
