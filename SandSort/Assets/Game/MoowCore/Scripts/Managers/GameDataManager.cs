using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : BaseSingleton<GameDataManager> {
    [SerializeField] GameDataSO _gameDataSO;

    // Revives used in the current level. A level load (first load, restart or next level) starts over.
    int _reviveCount;

    void OnEnable() => this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
    void OnDisable() => this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);

    void onLevelLoaded(Object sender, Event<object> e) => _reviveCount = 0;

    public bool boostersEnabled => _gameDataSO.boostersEnabled;
    public bool coinsEnabled => _gameDataSO.coinsEnabled;
    public float comboDuration => _gameDataSO.comboDuration;
    public float skipItemIfNotRequiredPercent => _gameDataSO.skipItemIfNotRequiredPercent;
    public int currentReviveCost => _gameDataSO.getReviveCost(_reviveCount);
    public float reviveTimeSeconds => _gameDataSO.reviveTimeSeconds;
    public int reviveCount => _reviveCount;
    public int addSlotCost => _gameDataSO.addSlotCost;
    public int addSlotLevel => _gameDataSO.addSlotLevel;
    public int reviveMoveCount => _gameDataSO.reviveMoveCount;
    public int levelCompleteReward => _gameDataSO.levelCompleteReward;
    public float icePowerUpDuration => _gameDataSO.icePowerUpDuration;

    public void registerRevive() => _reviveCount++;
}
