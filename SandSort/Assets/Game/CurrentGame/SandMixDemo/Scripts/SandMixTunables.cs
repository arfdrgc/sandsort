using UnityEngine;

// SandMixDemo's tunables: everything SandCylinderTunables already defines
// (grid/palette/extraction/cube/conveyor knobs — see that class for all of
// those) plus the handful of extra knobs this demo's pour interaction needs.
// A subclass rather than a second full copy, since SandCylinderSandGrid.Init
// takes a SandCylinderTunables and this demo reuses that grid/extraction
// stack completely unmodified — only the top of the sand area behaves
// differently here (poured in by the player instead of FillInitialLayers'
// one-shot Tetris-piece fill).
public class SandMixTunables : SandCylinderTunables {

    [Header("Pour (SandMixDemo only)")]
    [Tooltip("Grains poured into the grid per Update while the pointer is held down.")]
    [Range(1, 200)] public int pourRatePerStep = 40;
    [Tooltip("Width, in grid columns, of the pour stream around the pointer's grid column.")]
    [Range(1, 40)] public int streamWidth = 10;
    [Tooltip("How many rows below the very top of the grid a poured grain can land, so consecutive pours don't all target the exact same top row.")]
    [Range(1, 10)] public int pourSpawnRowJitter = 3;
}
