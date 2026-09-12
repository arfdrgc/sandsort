using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "MechanicUnlocks", menuName = "_ScriptableObjects/MechanicUnlocks")]
public class NewMechanicsSO : ScriptableObject {
    [SerializeField] List<MechanicUnlockData> _mechanicUnlocks;
    [SerializeField] LevelListSO _levels;

    public MechanicProgressData getMechanicProgress(int level) {
        _mechanicUnlocks.Sort((x, y) => x.unlockLevel - y.unlockLevel);

        MechanicUnlockData previousUnlocked = null;
        MechanicUnlockData nextUnlock = null;

        for(int i = _mechanicUnlocks.Count - 1; i >= 0; i--) {
            MechanicUnlockData mechanic = _mechanicUnlocks[i];
            if(level >= mechanic.unlockLevel) {
                previousUnlocked = mechanic;
                if(_mechanicUnlocks.Count > i + 1) {
                    nextUnlock = _mechanicUnlocks[i + 1];
                }

                break;
            }
        }

        if(previousUnlocked == null) {
            nextUnlock = _mechanicUnlocks[0];
        }

        int previousUnlockLevel = previousUnlocked == null ? 0 : previousUnlocked.unlockLevel - 1;
        int nextUnlockLevel = nextUnlock == null ? 0 : nextUnlock.unlockLevel;
        int requiredLevelCount = nextUnlockLevel - previousUnlockLevel - 1;
        int passedLevel = (level) - previousUnlockLevel;

        if(nextUnlock == null) {
            return null;
        }

        MechanicProgressData result = new MechanicProgressData {
            mechanic = nextUnlock,
            percentageStart = (passedLevel - 1) / (float)requiredLevelCount,
            percentageEnd = (passedLevel) / (float)requiredLevelCount,
            requiredLevel = requiredLevelCount,
            completedLevel = passedLevel
        };

        return result;
    }

    [Button]
    public void UpdateUnlockLevelNums()
    {
        if (_levels == null)
            return;

        if (_levels.Levels.Count == 0)
            return;

        foreach (var mechanic in _mechanicUnlocks)
        {
            if (mechanic == null || mechanic.unlockLevelData == null)
            {
                Debug.LogError($"Mechanic {mechanic?.unlockedTitle ?? "Unnamed"} is missing _unlockLevelData");
                continue;
            }

            int index = _levels.Levels.IndexOf(mechanic.unlockLevelData);

            if (index == -1)
            {
                Debug.LogError($"Level '{mechanic.unlockLevelData.name}' was not found in level list!");
                continue;
            }

            mechanic.SetUnlockLevel(index + 1);
        }
    }

    [Button]
    public void debug() {
        for(int i = 1; i < 50; i++) {
            MechanicProgressData progress = getMechanicProgress(i);
            if(progress == null) {
                Debug.Log($"{i} - null");
                continue;
            }

            Debug.Log($"Level {i} --- {progress.mechanic.name} - {(int)(progress.percentageStart * 100)}% - {(int)(progress.percentageEnd * 100)}%");
        }
    }
}

[System.Serializable]
public class MechanicUnlockData {
    [SerializeField] Sprite _mechanicSprite;
    [SerializeField] Sprite _mechanicUnrevealedSprite;
    [SerializeField] LevelSO _unlockLevelData;
    [SerializeField] int _unlockLevel;
    [SerializeField] string _name;
    [SerializeField] string _unlockedTitle;
    [SerializeField] string _unlockedText;

    public LevelSO unlockLevelData => _unlockLevelData;
    public Sprite mechanicSprite => _mechanicSprite;
    public Sprite mechanicUnrevealedSprite => _mechanicUnrevealedSprite;
    public int unlockLevel => _unlockLevel;
    public string name => _name;
    public string unlockedTitle => _unlockedTitle;
    public string unlockedText => _unlockedText;

    public void SetUnlockLevel(int level)
    {
        _unlockLevel = level;
    }
}

public class MechanicProgressData {
    public MechanicUnlockData mechanic;
    public float percentageStart;
    public float percentageEnd;
    public int requiredLevel;
    public int completedLevel;
}