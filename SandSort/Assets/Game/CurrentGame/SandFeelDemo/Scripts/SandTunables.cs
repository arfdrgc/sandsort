using UnityEngine;

[System.Serializable]
public class SandTunables {

    [Header("Grid / Grain Density")]
    [Range(20, 400)] public int gridWidth = 250;
    [Range(20, 500)] public int gridHeight = 250;

    [Header("Motion")]
    [Range(1, 60)] public int simulationStepsPerSecond = 36;
    [Range(1f, 6f)] public float maxFallCellsPerTick = 2f;
    [Tooltip("Chance, per tick, that a grain with a free cell directly below it spills into a free diagonal cell instead of falling straight down. 0 keeps falls perfectly vertical (old behavior); higher values make a collapsing column mix into its neighbors as it drops instead of dropping as a rigid straight shaft.")]
    [Range(0f, 1f)] public float fallSidewaysMixChance = 0f;
    [Range(0f, 1f)] public float lateralSpreadChance = 0.9f;
    [Range(0f, 1f)] public float pileStability = 0.28f;
    [Range(1, 4)] public int minGapForFreeCascade = 2;
    [Range(0f, 1f)] public float settleJitterChance = 0.015f;
    [Range(1, 60)] public int settleJitterWindowTicks = 3;

    [Header("Pour / Interaction")]
    [Range(1, 40)] public int pourRatePerStep = 40;
    [Range(1, 20)] public int streamWidth = 20;

    [Header("Visuals")]
    [Range(0f, 1f)] public float colorNoiseAmount = 0.14f;
}
