using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "GameData", menuName = "_ScriptableObjects/GameData")]
public class GameDataSO : ScriptableObject {
    [SerializeField, Range(0, 100f)] public float skipItemIfNotRequiredPercent;
    [Header("Revive")]
    [Tooltip("Cost of each revive within one level, in order (1st, 2nd, 3rd...). Revives past the end of the list reuse the last entry.")]
    [SerializeField] public int[] reviveCosts = { 900, 1900, 2900 };
    [Tooltip("Time, in seconds, the player gets back after a revive.")]
    [SerializeField, Min(0f)] public float reviveTimeSeconds = 20f;

    [SerializeField] public int addSlotCost;
    [SerializeField] public int addSlotLevel;
    [SerializeField] public int levelCompleteReward;
    [SerializeField] public int reviveMoveCount;
    [SerializeField] public float comboDuration;

    [SerializeField] public float icePowerUpDuration;

    // reviveIndex is 0-based: 0 = the level's first revive.
    public int getReviveCost(int reviveIndex) {
        if (reviveCosts == null || reviveCosts.Length == 0) return 0;
        return reviveCosts[Mathf.Clamp(reviveIndex, 0, reviveCosts.Length - 1)];
    }
}