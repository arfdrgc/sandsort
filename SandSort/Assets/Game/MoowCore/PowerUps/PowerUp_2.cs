using Moow;
using UnityEngine;

public class PowerUp_2 : BasePowerUp {

    public PowerUp_2_State state { get; private set; }

    void OnEnable() {
        this.addListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onPowerUpCancel);
        this.addListener<object>(Events.FAIL_CONDITION_MET, onPowerUpCancel);
    }

    void OnDisable() {
        this.removeListener<object>(Events.UI_CANCEL_POWER_UP, onPowerUpCancel);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onPowerUpCancel);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, onPowerUpCancel);
    }

    void onPowerUpCancel(UnityEngine.Object sender, Event<object> e) => hide();

    public override void useIfAvailable() {
        if (state == PowerUp_2_State.ACTIVE) {
            this.dispatchEvent<object>(Events.UI_CANCEL_POWER_UP, null);
            return;
        }
        this.dispatchEvent(Events.UI_POWER_UP_TUTORIAL_REQUIRED, powerUpSO);
        state = PowerUp_2_State.ACTIVE;
        show();
    }

    void show() => this.dispatchEvent<object>(Events.POWER_UP_2_ACTIVATED, null);

    void hide() {
        state = PowerUp_2_State.DEACTIVE;
        this.dispatchEvent<object>(Events.POWER_UP_2_DEACTIVATED, null);
    }

    public enum PowerUp_2_State { DEACTIVE, ANIMATING, ACTIVE }
}
