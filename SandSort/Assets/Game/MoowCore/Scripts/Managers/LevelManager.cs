using Moow;
using UnityEngine;
using UnityEngine.Analytics;

public class LevelManager : BaseSingleton<LevelManager> {

    [SerializeField] LevelListSO _levels;
    [SerializeField] DataSO _dataSO;

    private Database _database;

    private int _levelIndex;

    #region BASE
    #endregion

    #region METHODS
    public void initialize()
    {

    }

    #endregion

    #region HELPER
    public LevelSO currentLevel => _levels.get(levelIndex);
    public LevelSO getLevelInfo(int index) => _levels.get(index);
    public int level => _dataSO.level + 1;
    public int levelIndex => _dataSO.level;

    public int totalLevelCount => _levels.Levels.Count;

    public int progress;
    #endregion

}