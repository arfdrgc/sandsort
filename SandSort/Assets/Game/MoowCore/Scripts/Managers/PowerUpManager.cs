using Moow;
using MoowCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour {
    [SerializeField] PowerUp_1 _powerUp_1;
    public bool preventRaycast { get; private set; }


    private void OnEnable()
    {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<PowerUpSO>(Events.UI_POWER_UP_PRESSED, onPowerUpPressed);
        this.addListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        this.addListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
    }

    private void OnDisable() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<PowerUpSO>(Events.UI_POWER_UP_PRESSED, onPowerUpPressed);
        this.removeListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        this.removeListener<PowerUpSO>(Events.POWER_UP_USED, onPowerUpUsed);
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
        preventRaycast = false;
    }

    private void onPowerUpPressed(UnityEngine.Object sender, Event<PowerUpSO> eventData) {

        // Boosters switched off game-wide (GameDataSO.boostersEnabled): no press is handled.
        if (!GameDataManager.instance.boostersEnabled) return;

        PowerUpSO powerUp = eventData.data;

        // Only Freeze Time (PUT_1) exists in this game so far. PUT_2–4 are the reference game's
        // boosters and stay no-ops until they are designed.
        if (powerUp.type != PowerUpType.PUT_1) return;

        if(InventoryManager.instance.canUsePowerUp(powerUp) == false) {
            this.dispatchEvent(Events.POWER_UP_FAILED_TO_USE, powerUp);
            return;
        }


        // No tap feedback here: PowerUp_1 may still refuse (before the first drag, while a freeze runs).
        // The accepted tap's sound + haptic play with the activation (UIFreezeTimeActivation).
        _powerUp_1.useIfAvailable();
    }

    private void onPowerUpCancel(UnityEngine.Object sender, Event<object> eventData) {
        preventRaycast = false;
    }

    private void onPowerUpUsed(UnityEngine.Object sender, Event<PowerUpSO> eventData) {
        preventRaycast = false;
    }
}
