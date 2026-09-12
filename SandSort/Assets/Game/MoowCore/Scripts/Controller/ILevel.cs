using UnityEngine;

public interface ILevel {
    LevelSO levelSO { get; }
    Bounds levelBounds { get; }
    void initialize(LevelSO levelSO);
}
