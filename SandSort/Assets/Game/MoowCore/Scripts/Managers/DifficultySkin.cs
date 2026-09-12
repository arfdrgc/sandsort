using System;
using System.Collections;
using System.Collections.Generic;
using Moow;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

public class DifficultySkin : MonoBehaviour
{
    [SerializeField] MeshRenderer _renderer;
    [SerializeField] Material _defaultMaterial;
    [SerializeField] Material _hardMaterial;
    [SerializeField] Material _veryHardMaterial;
    [SerializeField] Material _darkThemeMaterial;

    void OnEnable() {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<object>(Events.SET_DARK_THEME, onSetDarkTheme);
    }
    
    void OnDisable() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<object>(Events.SET_DARK_THEME, onSetDarkTheme);
    }

    void onLevelLoaded(Object sender, Event<object> eventdata) {
        // LevelSO level = LevelManager.instance.currentLevel;
        
        // if (level.difficulty == LevelDifficulty.NORMAL) {
        //     _renderer.material = _defaultMaterial;
        // }
        // else if (level.difficulty == LevelDifficulty.HARD) {
        //     _renderer.material = _hardMaterial;
        // }
        // else {
        //     _renderer.material = _veryHardMaterial;
        // }
    }

    void onSetDarkTheme(Object sender, Event<object> eventdata)
    {
        _renderer.material = _darkThemeMaterial;
    }
}
