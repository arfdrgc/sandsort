using Moow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : BaseSingleton<GameDataManager> {
    [SerializeField] GameDataSO _gameDataSO;

    public float comboDuration => _gameDataSO.comboDuration;
    public float skipItemIfNotRequiredPercent => _gameDataSO.skipItemIfNotRequiredPercent;
    public int reviveCost => _gameDataSO.reviveCost;
    public int addSlotCost => _gameDataSO.addSlotCost;
    public int addSlotLevel => _gameDataSO.addSlotLevel;
    public int reviveMoveCount => _gameDataSO.reviveMoveCount;
    public int levelCompleteReward => _gameDataSO.levelCompleteReward;
    public float icePowerUpDuration => _gameDataSO.icePowerUpDuration;
}
