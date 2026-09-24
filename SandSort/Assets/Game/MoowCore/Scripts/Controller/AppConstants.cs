using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Moow {
    static public class AppConstants {
        const float REF_WIDTH_RES = 1080.0f;
        const float REF_HEIGHT_RES = 1920.0f;
        static public float DISTANCE_THRESHOLD = 0.0001f;
        public static float RES_HEIGHT_CHANGE_RATIO => (float)Screen.height / REF_HEIGHT_RES;
        public static float RES_WIDTH_CHANGE_RATIO => (float)Screen.width / REF_WIDTH_RES;

        public static string Offset = "offset";
        public static string Speed = "speed";
    }
    public class RemainingTimeChangedEventData {
        public float passedTime { get; private set; }
        public float remainingTime { get; private set; }

        public RemainingTimeChangedEventData(float passedTime, float remainingTime) {
            this.passedTime = passedTime;
            this.remainingTime = remainingTime;
        }
    }

    sealed public class Events {
        static public string CPI_ACTIVE = "CPI_ACTIVE";
        static public string UNLOCK_ALL_FEATURE = "UNLOCK_ALL_FEATURE";
        static public string INCREASE_DOCK = "INCREASE_DOCK";
        static public string INCREASE_DOCK_WITH_POWER_UP = "INCREASE_DOCK_WITH_POWER_UP";

        static public string INIT_BUNDLE = "INIT_BUNDLE";



        static public string MORE_SLOT_POWER_USED = "MOVE_SLOT_POWER_USED";
        static public string MAX_SLOT_COUNT_CHANGED = "MAX_SLOT_COUNT_CHANGED";

        static public string ADD_SLOT_BUTTON_PRESSED = "ADD_SLOT_BUTTON_PRESSED";
        static public string ADDED_SLOT = "ADDED_SLOT"; //TODO MoowAnalytics
        static public string SHAPE_FILLED = "SHAPE_FILLED";

        static public string OUT_OF_SLOT_ACTIVATED = "OUT_OF_SLOT_ACTIVATED";
        static public string OUT_OF_SLOT_DEACTIVATED = "OUT_OF_SLOT_DEACTIVATED";

        static public string POWER_UP_1_ACTIVATED = "POWER_UP_1_ACTIVATED";
        static public string POWER_UP_2_ACTIVATED = "POWER_UP_2_ACTIVATED";
        static public string POWER_UP_3_ACTIVATED = "POWER_UP_3_ACTIVATED";
        static public string POWER_UP_4_ACTIVATED = "POWER_UP_4_ACTIVATED";

        static public string POWER_UP_1_DEACTIVATED = "POWER_UP_1_DEACTIVATED";
        static public string POWER_UP_2_DEACTIVATED = "POWER_UP_2_DEACTIVATED";
        static public string POWER_UP_3_DEACTIVATED = "POWER_UP_3_DEACTIVATED";
        static public string POWER_UP_4_DEACTIVATED = "POWER_UP_4_DEACTIVATED";

        static public string ON_POWER_UP_TUTORIAL_READY = "ON_POWER_UP_TUTORIAL_READY";
        static public string TUTORIAL_POPUP_STARTED = "TUTORIAL_POPUP_STARTED";
        static public string TUTORIAL_POPUP_COMPLETE = "TUTORIAL_POPUP_COMPLETE";

        static public string SET_LIGHT_DAY = "SET_LIGHT_DAY";
        static public string SET_LIGHT_NIGHT = "SET_LIGHT_NIGHT";

        static public string RECORD_IMPOSSIBLE_LEVEL = "RECORD_IMPOSSIBLE_LEVEL";
        static public string RESET_RECORD_DATA = "RESET_RECORD_DATA";

        static public string SET_DARK_THEME = "SET_DARK_THEME";
        static public string SET_EDIT_ACTIVE = "SET_EDIT_ACTIVE";
        static public string LEVEL_OBJECTIVE_COMPLETE = "LEVEL_OBJECTIVE_COMPLETE";
        static public string FAIL_CONDITION_MET = "NO_SPACE_REMAINING";


        static public string PLAY_MUSIC_STATE_CHANGED = "PLAY_MUSIC_STATE_CHANGED";
        static public string SETTINGS_SOUND_STATE_CHANED = "SETTINGS_SOUND_STATE_CHANED";
        static public string SETTINGS_HAPTIC_STATE_CHANED = "SETTINGS_HAPTIC_STATE_CHANED";
        static public string SETTINGS_MUSIC_STATE_CHANED = "SETTINGS_MUSIC_STATE_CHANED";

        static public string GIVE_COIN_ANIMATION = "GIVE_COIN_ANIMATION";
        static public string UI_OPEN_SETTINGS = "UI_OPEN_SETTINGS";
        static public string UI_CLOSE_SETTINGS = "UI_CLOSE_SETTINGS";
        static public string UI_RETRY_CLICKED = "UI_RETRY_CLICKED";
        static public string UI_GOLD_ANIMATION_PROGRESS = "UI_GOLD_ANIMATION_PROGRESS";
        static public string UI_REVIVE_CLICKED = "UI_REVIVE_CLICKED";
        static public string UI_NEXT_LEVEL_CLICK = "UI_NEXT_LEVEL_CLICK";
        static public string UI_CANCEL_POWER_UP = "UI_CANCEL_POWER_UP";
        static public string UI_POWER_UP_TUTORIAL_REQUIRED = "UI_POWER_UP_TUTORIAL_REQUIRED";

        static public string FREE_POWER_UP_CHANGED = "FREE_POWER_UP_CHANGED";
        static public string POWER_UP_USED = "POWER_UP_USED";
        static public string POWER_UP_FAILED_TO_USE = "POWER_UP_FAILED_TO_USE";
        static public string UI_POWER_UP_PRESSED = "UI_POWER_UP_PRESSED";
        static public string FREEZE_TIME_CHANGED = "FREEZE_TIME_CHANGED"; // float: freeze seconds left, every frame while Freeze Time counts down
        static public string BOOSTER_TUTORIAL_STARTED = "BOOSTER_TUTORIAL_STARTED"; // PowerUpSO: first-unlock popup + tutorial shown, gameplay input locked
        static public string BOOSTER_TUTORIAL_COMPLETED = "BOOSTER_TUTORIAL_COMPLETED"; // PowerUpSO: highlighted booster pressed (sent before UI_POWER_UP_PRESSED); starts the level
        static public string BOOSTER_TUTORIAL_CANCELLED = "BOOSTER_TUTORIAL_CANCELLED"; // PowerUpSO: tutorial cut short (level load, retry, win, lose); not marked shown

        static public string LEVEL_LOADED = "LEVEL_LOADED";
        static public string LEVEL_READY_TO_PLAY = "LEVEL_READY_TO_PLAY";
        static public string LEVEL_BOUNDS_CHANGED = "LEVEL_BOUNDS_CHANGED";
        static public string LEVEL_FAILED = "LEVEL_FAILED";
        static public string LEVEL_COMPLETED = "LEVEL_COMPLETED";
        static public string LEVEL_TIMER_CHANGED = "LEVEL_TIMER_CHANGED";
        static public string LEVEL_FIRST_DRAG = "LEVEL_FIRST_DRAG";
        static public string LEVEL_MONEY_EARNED = "LEVEL_MONEY_EARNED";
        static public string MONEY_CHANGED = "MONEY_CHANGED";

        static public string MECHANIC_UNLOCK_DISPLAYED = "MECHANIC_UNLOCK_DISPLAYED";
        static public string MECHANIC_UNLOCK_CLOSED = "MECHANIC_UNLOCK_CLOSED";

        static public string RATE_US_SHOWED = "RATE_US_SHOWED";
        static public string RATE_US_RATED = "RATE_US_RATED";
        static public string RATE_US_NOT_RATED = "RATE_US_NOT_RATED";

        static public string REWARDED_SHOW_REQUEST = "REWARDED_SHOW_REQUEST";
        static public string REWARDED_SDK_NOT_INITIALIZED = "REWARDED_SDK_NOT_INITIALIZED";
        static public string REWARDED_DISPLAYED = "REWARDED_DISPLAYED";
        static public string REWARDED_IS_NOT_READY = "REWARDED_IS_NOT_READY";
        static public string ENABLE_INPUT = "ENABLE_INPUT";
        static public string DISABLE_INPUT = "DISABLE_INPUT";
        static public string LEVEL_PAUSE_STATE_CHANGED = "LEVEL_PAUSE_STATE_CHANGED";

    }
    sealed public class Scenes {
        static public string MainScene = "Main Scene";
        static public string GameScene = "Game Scene";
    }
    public enum UIMenuType {
        Menu,
        Game,
    }

    public enum Direction
    {
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft
    }
    sealed public class RemoteKeys {
        /// <summary>
        /// Game Start
        /// </summary>

        // Aşağıdaki tüm değerler, upgrade cost = initial currency + (incremental_currency* level ) olarak hesaplanan matematiği etkileyecektir.
        //INTERSTITIAL FIRST LEVEL
        static public string interstitial_level_first_integer = "interstitial_level_first_integer";
        static public string interstitial_timer_float = "interstitial_timer_float";

        // REWARD BAR REMOTES
        static public string reward_bar_rotation_speed_float = "reward_bar_rotation_speed_float";
        static public string reward_bar_multiplier_low_float = "reward_bar_multiplier_low_float";
        static public string reward_bar_multiplier_mid_float = "reward_bar_multiplier_mid_float";
        static public string reward_bar_multiplier_high_float = "reward_bar_multiplier_high_float";

        // INCOME CALCULATION
        static public string income_upgrade_constantA_integer = "income_upgrade_constantA_integer";
        static public string income_upgrade_constantB_integer = "income_upgrade_constantB_integer";

        // OTHERS
        static public string rate_us_popup_level_first_integer = "rate_us_popup_level_first_integer";
        static public string ios_fake_rate_us_availability_boolean = "ios_fake_rate_us_availability_boolean";
    }

    public class AppUtility {

        public static GUISkin GUISkin => Resources.Load<GUISkin>("Skins/AdenGUISkin");

        const float REF_WIDTH_RES = 1080.0f;
        public static float MOVEMENT_FACTOR => REF_WIDTH_RES / (float)Screen.width;
    }

}