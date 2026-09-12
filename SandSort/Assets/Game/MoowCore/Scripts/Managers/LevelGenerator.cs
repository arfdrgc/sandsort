using Moow;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : BaseSingleton<LevelGenerator> {
    private LevelSO _levelSO;
    private MonoBehaviour _generatedLevel;
    public ILevel currentLevel => _generatedLevel as ILevel;

    private bool _isReady;


    public void initialize() {

    }

    public ILevel loadLevel(LevelSO levelSO) {
        _levelSO = levelSO;

        destroyLevel();

        _generatedLevel = null;

        _generatedLevel = Instantiate(levelSO.levelPrefab as MonoBehaviour);
        _generatedLevel.transform.position = Vector3.zero;
        ILevel generatedLevel = _generatedLevel as ILevel;
        generatedLevel.initialize(levelSO);

        if(levelSO.levelTutorial != null) {
            LevelTutorial tutorial = Instantiate(levelSO.levelTutorial, _generatedLevel.transform);
            tutorial.init(generatedLevel);
        }

        return generatedLevel;
    }

    void destroyLevel() {
        if(_generatedLevel) {
            Destroy(_generatedLevel.gameObject);
        }
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.black;
    }
}
