using System.Collections.Generic;
using UnityEngine;
using static ColorSO;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Level", menuName = "_ScriptableObjects/Level", order = 1)]
public class LevelSO : ScriptableObject {

    // ── Level Prefab ──────────────────────────────────────────────────────────
    [SerializeField] MonoBehaviour _levelPrefab;
    public ILevel levelPrefab => _levelPrefab as ILevel;

    // ── Difficulty ────────────────────────────────────────────────────────────
    [SerializeField] LevelDifficulty _difficulty;
    public LevelDifficulty difficulty => _difficulty;

    // ── Tutorial ──────────────────────────────────────────────────────────────
    [SerializeField] LevelTutorial _levelTutorial;
    public LevelTutorial levelTutorial => _levelTutorial;

    // ── Layout / Camera ───────────────────────────────────────────────────────
    [SerializeField, Range(-15f, 15f)] float _expandBottom;
    public float expandBottom => _expandBottom;

    [SerializeField, Range(-25f, 25f)] float _centerFactor;
    public float centerFactor => _centerFactor;

    // ── Color Lookup ──────────────────────────────────────────────────────────
    [SerializeField] ColorPaletteSO _colorPalette;

    public void init() { }

    public Color GetColor(ItemColor c) {
        if (_colorPalette == null)
            _colorPalette = Resources.Load<ColorPaletteSO>("ColorPalette");

        return _colorPalette != null ? _colorPalette.GetColor(c) : Color.magenta;
    }

    public void forceSave() {
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
#endif
    }
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum LevelDifficulty {
    EASY,
    HARD,
    VERY_HARD
}
