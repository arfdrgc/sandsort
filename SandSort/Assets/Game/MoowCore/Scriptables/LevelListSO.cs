using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;


#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "LevelListSO", menuName = "_ScriptableObjects/LevelListSO")]
public class LevelListSO : ScriptableObject
{
    [SerializeField] List<LevelSO> _levels;
    [SerializeField] List<LevelSO> _repeatingLevels;
    [SerializeField] NewMechanicsSO mechanics;

    public List<LevelSO> Levels => _levels;

    [SerializeField] LevelSO _testLevel;

    public LevelSO get(int index)
    {
#if UNITY_EDITOR
        if (_testLevel != null)
        {
            return _testLevel;
        }
#endif

        if (index < _levels.Count)
        {
            return _levels[index];
        }

        return _repeatingLevels[(index - _levels.Count) % _repeatingLevels.Count];
    }

    public bool isRepeating(int index)
    {
        return index >= _levels.Count;
    }

    [Button]
    public void showMechanicTable()
    {
#if UNITY_EDITOR
        //LevelListEditorWindow.ShowWindow(this);
#endif
    }

    private void OnLevelsChanged()
    {
        Debug.Log("_levels List Changed!");
        mechanics.UpdateUnlockLevelNums();
    }
}
