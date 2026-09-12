#define GAME_ANALYTICS_MOOOOOOOW
//todo moow #define GAME_ANALYTICS_MOOOOOOOW
using Moow;
using MoowCore;
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Globalization;
//using Moow.Ads.Providers.Max;


#if VOODOO_MOOW || HOMA_MOOW || GAME_ANALYTICS_MOOW
using GameAnalyticsSDK;
#endif

public class MoowAnalyticsManager : BaseSingleton<MoowAnalyticsManager> {
    [SerializeField] DataSO _dataSO;
    [SerializeField] InventoryDataSO _inventory;

    float _levelStartTime;
    float _levelLoadTime;
    float _powerUpUseCount;
    public int _keepPlayCount;
    float _itemCollected;
    int levelNumber => _dataSO.level + 1;

    void OnEnable() {
        Debug.Log($"Analytics OnEnable {Time.frameCount}");
        this.addListener<object>(Events.LEVEL_LOADED, levelLoaded);
        this.addListener<object>(Events.FAIL_CONDITION_MET, levelFailed);
        this.addListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, levelComplete);
        //this.addListener<object>(Events.UI_RETRY_CLICKED, levelFailed);
        this.addListener<object>(Events.UI_REVIVE_CLICKED, reviveClicked);
        this.addListener<object>(Events.ADDED_SLOT, onSlotAdded);
        this.addListener<object>(Events.SHAPE_FILLED, onShapeFilled);
        this.addListener<PowerUpSO>(Events.UI_POWER_UP_PRESSED, powerUpPressed);
        this.addListener<PowerUpSO>(Events.POWER_UP_USED, powerUpUsed);
        this.addListener<PowerUpSO>(Events.POWER_UP_FAILED_TO_USE, powerUpFailedToUse);
    }

    void OnDisable() {
        this.removeListener<object>(Events.LEVEL_LOADED, levelLoaded);
        this.removeListener<object>(Events.FAIL_CONDITION_MET, levelFailed);
        this.removeListener<object>(Events.LEVEL_OBJECTIVE_COMPLETE, levelComplete);
        //this.removeListener<object>(Events.UI_RETRY_CLICKED, levelFailed);
        this.removeListener<object>(Events.UI_REVIVE_CLICKED, reviveClicked);
        this.removeListener<object>(Events.ADDED_SLOT, onSlotAdded);
        this.removeListener<object>(Events.SHAPE_FILLED, onShapeFilled);
        this.removeListener<PowerUpSO>(Events.UI_POWER_UP_PRESSED, powerUpPressed);
        this.removeListener<PowerUpSO>(Events.POWER_UP_USED, powerUpUsed);
        this.removeListener<PowerUpSO>(Events.POWER_UP_FAILED_TO_USE, powerUpFailedToUse);
    }

    protected override void Awake()
    {
        base.Awake();
        Debug.Log($"Analytics Awake {GetInstanceID()}");
    }

    private void reviveClicked(Object sender, Event<object> eventData) {
        designEvent("ECONOMY:REVIVE_USED");
        
        //AZUR_SDK
        //AnalyticsService.ReviveUse(levelNumber);
        _keepPlayCount++;

        Debug.Log("_keepPlayCount: " + _keepPlayCount + " - sender : " + sender.name);
    }
    
    void onSlotAdded(Object sender, Event<object> eventdata) {
        _powerUpUseCount++;
    }
    
    void onShapeFilled(Object sender, Event<object> eventdata) {
        _itemCollected++;
    }

    private void levelLoaded(Object sender, Event<object> eventData) {

        Debug.Log($"[ANALYTICS EVENT] : LevelStarted: {levelNumber} | Time: {Time.time:F2} | Frame: {Time.frameCount}");
        //levelStarted();

        _levelLoadTime = Time.time;
        _powerUpUseCount = 0;
        _keepPlayCount = 0;
        _itemCollected = 0;

        _dataSO.incrementalLevelCount++;
        Database.SaveGame();

        designEvent("ECONOMY:MONEY_LEVEL_START", _inventory.money);

        //AZUR_SDK
        //AnalyticsService.LevelStart(levelNumber);

        int totalLevelCount = LevelManager.instance.totalLevelCount;

        LevelSO levelSO = LevelManager.instance.currentLevel;
        string levelName = levelSO.name;
        int levelCount = _dataSO.incrementalLevelCount;
        bool random = levelNumber > totalLevelCount ? true : false; 
        string type = "normal";
        string gameMode = levelSO.difficulty.ToString();

        //AnalyticsService.LevelStart(levelNumber, levelName, levelCount, random, type, gameMode);
    }

    private void levelFailed(Object sender, Event<object> eventData) {
        Debug.Log("[ANALYTICS EVENT]: LevelFailed: " + levelNumber);
        //levelFailed_Priv();

        float fullLevelTime = (int)(Time.time - _levelLoadTime);
        float playTime = (int)(Time.time - _levelStartTime);

        designEvent("PROGRESS:FAIL:TIME", fullLevelTime);
        designEvent("PROGRESS:FAIL:POWER_USE", _powerUpUseCount);
        designEvent("PROGRESS:FAIL:ITEM_COLLECTED", _itemCollected);
        designEvent("ECONOMY:MONEY_LEVEL_FAILED", _inventory.money);

        //AZUR_SDK
        //AnalyticsService.LevelFail(levelNumber);
        LevelSO levelSO = LevelManager.instance.currentLevel;
        int totalLevelCount = LevelManager.instance.totalLevelCount;
        
        string levelName = levelSO.name;
        int levelCount = _dataSO.incrementalLevelCount;
        bool random = levelNumber > totalLevelCount ? true : false; 
        string type = "normal";
        string gameMode = levelSO.difficulty.ToString();
        string result = "lose";
        int time = (int)playTime;
        int progress = LevelManager.instance.progress;
        int continueCount = _keepPlayCount;
        //AnalyticsService.LevelFinish(levelNumber, levelName, levelCount, random, type, gameMode, result, time, progress, continueCount);
        Debug.Log("Level Fail");

    }

    private void levelComplete(Object sender, Event<object> eventData) {
        Debug.Log("[ANALYTICS EVENT]: LevelComplete: " + levelNumber);
        //levelComplete();

        float fullLevelTime = (int)(Time.time - _levelLoadTime);
        float playTime = (int)(Time.time - _levelStartTime);

        designEvent("PROGRESS:COMPLETE:TIME", fullLevelTime);
        designEvent("PROGRESS:COMPLETE:POWER_USE", _powerUpUseCount);
        designEvent("PROGRESS:COMPLETE:ITEM_COLLECTED", _itemCollected);
        designEvent("ECONOMY:MONEY_LEVEL_COMPLETE", _inventory.money);

        //AZUR_SDK
        //AnalyticsService.LevelComplete(levelNumber);
        LevelSO levelSO = LevelManager.instance.currentLevel;
        int totalLevelCount = LevelManager.instance.totalLevelCount;

        string levelName = levelSO.name;
        int levelCount = _dataSO.incrementalLevelCount;
        bool random = levelNumber > totalLevelCount ? true : false; 
        string type = "normal";
        string gameMode = levelSO.difficulty.ToString();
        string result = "win";
        int time = (int)playTime;
        int progress = 100;
        int continueCount = _keepPlayCount;
        
        //AnalyticsService.LevelFinish(levelNumber, levelName, levelCount, random, type, gameMode, result, time, progress, continueCount);
        Debug.Log("Level Completed");
    }

    // private void failConditionMet(Object sender, Event<object> eventData) {
    //     designEvent("ECONOMY:ALL_SLOTS_FILLED");
    // }

    // private void noTimeLeft(Object sender, Event<object> eventData) {
    //     designEvent("ECONOMY:NO_TIME_LEFT");
    // }

    private void powerUpPressed(Object sender, Event<PowerUpSO> eventData) {
        designEvent("ECONOMY:POWER_UP_CLICKED:" + eventData.data.name);
    }

    private void powerUpFailedToUse(Object sender, Event<PowerUpSO> eventData) {
        designEvent("ECONOMY:POWER_UP_FAILED:" + eventData.data.name);
    }

    private void powerUpUsed(Object sender, Event<PowerUpSO> eventData) {
        designEvent("ECONOMY:POWER_UP_USED:" + eventData.data.name);
        _powerUpUseCount++;
    }

    // private void needHeartToPlay(Object sender, Event<object> eventData) {
    //     designEvent("ECONOMY:NEED_HEART");
    // }

    // private void needGold(Object sender, Event<object> eventData) {
    //     designEvent("ECONOMY:NEED_GOLD");
    // }

    // private void RECEIVED_LEVEL_GOLD_REWARDED(Object sender, Event<object> eventData) {
    //     designEvent("ECONOMY:RECEIVED_LEVEL_GOLD_REWARDED");
    // }

    // private void receivedLevelGoldNormal(Object sender, Event<object> eventData) {
    //     designEvent("ECONOMY:RECEIVED_LEVEL_GOLD_NORMAL");
    // }

    string formatLevel(int lvl) {
        return lvl.ToString("D4");
    }

    void designEvent(string eventName, float eventValue = 0) {
        eventName = eventName + ":" + formatLevel(levelNumber);

#if VOODOO_MOOW || HOMA_MOOW || GAME_ANALYTICS_MOOW
        GameAnalytics.NewDesignEvent("MOOW_" + eventName, eventValue);
#endif
        Debug.Log("[ANALYTICS EVENT]: Design Event:" + eventName + "-" + eventValue);
    }


//     void levelStarted() {
// #if VOODOO_MOOW
//         TinySauce.OnGameStarted(levelNumber);
// #elif HOMA_MOOW
//         HomaGames.HomaBelly.Analytics.LevelStarted(_dataSO.level);
// #elif GAME_ANALYTICS_MOOW
//         GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, formatLevel(levelNumber));
// #else 
        
// #endif
//     }

    
//     void levelFailed_Priv() {
// #if VOODOO_MOOW
//         TinySauce.OnGameFinished(false, _itemCollected, levelNumber);
// #elif HOMA_MOOW
//         HomaGames.HomaBelly.Analytics.LevelFailed("");
// #elif GAME_ANALYTICS_MOOW
//         GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, formatLevel(levelNumber));
// #else
        
// #endif
//     }   
    
//     void levelComplete() {
// #if VOODOO_MOOW
//         TinySauce.OnGameFinished(true, _itemCollected, levelNumber);
// #elif HOMA_MOOW
//         HomaGames.HomaBelly.Analytics.LevelCompleted();
// #elif GAME_ANALYTICS_MOOW
//         GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, formatLevel(levelNumber));
// #else
        
// #endif
//     }
}