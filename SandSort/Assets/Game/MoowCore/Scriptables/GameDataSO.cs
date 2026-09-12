using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "GameData", menuName = "_ScriptableObjects/GameData")]
public class GameDataSO : ScriptableObject {
    [SerializeField, Range(0, 100f)] public float skipItemIfNotRequiredPercent;
    [SerializeField] public int reviveCost;
    [SerializeField] public int addSlotCost;
    [SerializeField] public int addSlotLevel;
    [SerializeField] public int levelCompleteReward;
    [SerializeField] public int reviveMoveCount;
    [SerializeField] public float comboDuration;

    [SerializeField] public float icePowerUpDuration;
}