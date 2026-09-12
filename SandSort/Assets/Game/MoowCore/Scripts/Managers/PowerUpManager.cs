using Moow;
using MoowCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour {
    [SerializeField] PowerUp_2 _powerUp_2;
    [SerializeField] PowerUp_3 _powerUp_3;
    [SerializeField] PowerUp_4 _powerUp_4;
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

        PowerUpSO powerUp = eventData.data;

        if(InventoryManager.instance.canUsePowerUp(powerUp) == false) {
            this.dispatchEvent(Events.POWER_UP_FAILED_TO_USE, powerUp);
            return;
        }


        HapticManager.instance.feedback(HapticFeedbackType.FeedbackLight);

        if (powerUp.type == PowerUpType.PUT_1)
        {
            AudioPlayer.instance.playSFX(AudioFX.WHOOSH_SHORT_2);
            this.dispatchEvent<PowerUpSO>(Events.INCREASE_DOCK_WITH_POWER_UP, eventData.data);
        }
        else if (powerUp.type == PowerUpType.PUT_2)
        {
            AudioPlayer.instance.playSFX(AudioFX.UI_BUTTON_CLICK);
            preventRaycast = true;
            _powerUp_2.useIfAvailable();
        }
        else if (powerUp.type == PowerUpType.PUT_3)
        {
            AudioPlayer.instance.playSFX(AudioFX.WHOOSH_SHORT_2);
        }
        else if (powerUp.type == PowerUpType.PUT_4)
        {
            AudioPlayer.instance.playSFX(AudioFX.WHOOSH_SHORT_2);
        }
    }

    private void onPowerUpCancel(UnityEngine.Object sender, Event<object> eventData) {
        preventRaycast = false;
    }

    private void onPowerUpUsed(UnityEngine.Object sender, Event<PowerUpSO> eventData) {
        preventRaycast = false;
    }
}
