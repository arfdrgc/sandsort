using DG.Tweening;
using Moow;
using MoowCore;
//using Spine;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : BaseSingleton<GameManager> {
    [SerializeField] DataSO _dataSO;
    [SerializeField] LevelManager _levelManager;
    [SerializeField] LevelGenerator _levelGenerator;
    [SerializeField] InventoryManager _inventoryManager;

    //todo Moow PopupManager
    //[SerializeField] PopupManager _popupManager;

    #region BASE
    private void OnEnable() {
        this.addListener<object>(Events.UI_NEXT_LEVEL_CLICK, onNextLevelClick);
        this.addListener<object>(Events.UI_RETRY_CLICKED, onRetryClick);
    }

    private void OnDisable() {
        this.removeListener<object>(Events.UI_NEXT_LEVEL_CLICK, onNextLevelClick);
        this.removeListener<object>(Events.UI_RETRY_CLICKED, onRetryClick);
    }

    private void onNextLevelClick(UnityEngine.Object sender, Event<object> eventData) {
        this.dispatchEvent<object>(Events.LEVEL_COMPLETED, null);
        MusicPlayer.instance.continueMusic();
        _dataSO.level += 1;
        _dataSO.levelAttemptCount = 0;
        Database.SaveGame();
        LevelSO levelSO = _levelManager.currentLevel;
        loadLevel(levelSO, false);
    }

    private void onRetryClick(UnityEngine.Object sender, Event<object> eventData) {
        _dataSO.levelAttemptCount++;
        Database.SaveGame();

        LevelSO levelSO = _levelManager.currentLevel;
        loadLevel(levelSO, false);

        // UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
    }

    void Start() {
        initialize();
    }
    #endregion

    #region METHODS
    void initialize() {
        _levelGenerator.initialize();
        _levelManager.initialize();
        _inventoryManager?.initialize();

        LevelSO levelSO = _levelManager.currentLevel;

        if(levelSO != null) {
            loadLevel(levelSO, true);
        } else {
            Debug.Log($"[GameManager::initialize] levelSO data is null! Cannot generate level.");
        }
    }

    ILevel loadLevel(LevelSO levelSO, bool tryToLoadLevelState) {
        ILevel level = _levelGenerator.loadLevel(levelSO);
        Debug.Log("dispatchEvent ---- LEVEL_LOADED call");
        this.dispatchEvent(new Event<object>(Events.LEVEL_LOADED, level));
        return level;
    }

    public void retryLevel() {
        // LevelSO currentLevel = _levelManager.currentLevel;
        // loadLevel(currentLevel, false);
    }
    #endregion

    #region ACTIONS
    #endregion

    #region COROUTINE
    #endregion

    #region HELPER
    #endregion
}
