using System.Collections;
using System.Collections.Generic;
using Moow;
using DG.Tweening;
using UnityEngine.Splines;
using UnityEngine;

public class PowerUp_3 : BasePowerUp
{
    public PowerUp_3_State state { get; private set; }

    private void OnEnable()
    {
        this.addListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onPowerUpCancel);
        this.addListener<object>(Events.FAIL_CONDITION_MET, onPowerUpCancel);
    }

    private void OnDisable()
    {
        this.removeListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onPowerUpCancel);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, onPowerUpCancel);
    }

    private void onPowerUpCancel(UnityEngine.Object sender, Event<object> eventData)
    {
        hide();
    }

    public override void useIfAvailable()
    {
        if (state == PowerUp_3_State.ACTIVE)
        {
            this.dispatchEvent<object>(Events.UI_CANCEL_POWER_UP, null);
            return;
        }

        this.dispatchEvent(Events.UI_POWER_UP_TUTORIAL_REQUIRED, powerUpSO);
        state = PowerUp_3_State.ACTIVE;
        show();
    }

    void show()
    {
        this.dispatchEvent<object>(Events.POWER_UP_3_ACTIVATED, null);
    }
    void hide()
    {
        state = PowerUp_3_State.DEACTIVE;
        this.dispatchEvent<object>(Events.POWER_UP_3_DEACTIVATED, null);
    }

    public enum PowerUp_3_State
    {
        DEACTIVE,
        ANIMATING,
        ACTIVE
    }
}
