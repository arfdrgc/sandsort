using Moow;
using UnityEngine;

// Freeze Time. A tap stops the level timer at once (POWER_UP_1_ACTIVATED) and starts the activation
// (ANIMATING). When the activation has run its course the booster is consumed and the freeze counts
// down (ACTIVE); at 0 the timer resumes (POWER_UP_1_DEACTIVATED). Winning, losing, restarting or
// loading a level ends it early — and an activation cut short that way is never consumed.
//
// Level owns the timer: it stops counting between the two events and says through canFreezeTimer
// whether a freeze may start (timer started, level not won/lost). Both phases run on the level
// timer's clock (Time.deltaTime) and stand still while Settings is open, so a paused game never
// spends freeze time. Durations come from the Level's GameplayTunables.
public class PowerUp_1 : BasePowerUp {

    public PowerUp_1_State state { get; private set; }
    // ANIMATING: activation time left. ACTIVE: freeze time left.
    public float phaseRemaining { get; private set; }

    bool _settingsOpen;

    void OnEnable() {
        this.addListener<object>(Events.UI_OPEN_SETTINGS, onSettingsOpened);
        this.addListener<object>(Events.UI_CLOSE_SETTINGS, onSettingsClosed);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelEnded);
        this.addListener<object>(Events.FAIL_CONDITION_MET, onLevelEnded);
        this.addListener<object>(Events.UI_RETRY_CLICKED, onLevelEnded);
        this.addListener<object>(Events.LEVEL_LOADED, onLevelEnded);
    }

    void OnDisable() {
        this.removeListener<object>(Events.UI_OPEN_SETTINGS, onSettingsOpened);
        this.removeListener<object>(Events.UI_CLOSE_SETTINGS, onSettingsClosed);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, onLevelEnded);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, onLevelEnded);
        this.removeListener<object>(Events.UI_RETRY_CLICKED, onLevelEnded);
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelEnded);
    }

    void onSettingsOpened(Object sender, Event<object> e) => _settingsOpen = true;

    void onSettingsClosed(Object sender, Event<object> e) => _settingsOpen = false;

    void onLevelEnded(Object sender, Event<object> e) {
        if (state != PowerUp_1_State.DEACTIVE) end();
    }

    // Not usable before the first drag, once the level is won/lost, or while a freeze is already
    // running (no stacking): those presses get the normal failed feedback and cost nothing.
    public override void useIfAvailable() {
        Level level = currentLevel;
        if (state != PowerUp_1_State.DEACTIVE || level == null || !level.canFreezeTimer) {
            failedToUse();
            return;
        }

        state = PowerUp_1_State.ANIMATING;
        phaseRemaining = level.gameplayTunables != null
            ? level.gameplayTunables.freezeTimeActivationSeconds
            : GameplayTunables.DEFAULT_FREEZE_TIME_ACTIVATION_SECONDS;
        this.dispatchEvent<object>(Events.POWER_UP_1_ACTIVATED, null);
    }

    void Update() {
        if (state == PowerUp_1_State.DEACTIVE || _settingsOpen) return;

        phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);

        if (state == PowerUp_1_State.ANIMATING) {
            if (phaseRemaining > 0f) return;
            activate();
            return;
        }

        this.dispatchEvent<float>(Events.FREEZE_TIME_CHANGED, phaseRemaining);
        if (phaseRemaining <= 0f) end();
    }

    // The activation has finished: consume the booster and start the countdown. The level may have
    // been won meanwhile (the win is locked in before it is announced) — then end without charging.
    void activate() {
        Level level = currentLevel;
        if (level == null || !level.canFreezeTimer) {
            end();
            return;
        }

        state = PowerUp_1_State.ACTIVE;
        phaseRemaining = level.gameplayTunables != null
            ? level.gameplayTunables.freezeTimeSeconds
            : GameplayTunables.DEFAULT_FREEZE_TIME_SECONDS;
        used();
        this.dispatchEvent<float>(Events.FREEZE_TIME_CHANGED, phaseRemaining);
    }

    void end() {
        state = PowerUp_1_State.DEACTIVE;
        phaseRemaining = 0f;
        this.dispatchEvent<object>(Events.POWER_UP_1_DEACTIVATED, null);
    }

    Level currentLevel => LevelGenerator.instance != null ? LevelGenerator.instance.currentLevel as Level : null;

    public enum PowerUp_1_State { DEACTIVE, ANIMATING, ACTIVE }
}
