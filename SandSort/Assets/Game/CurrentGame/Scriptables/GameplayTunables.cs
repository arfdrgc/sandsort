using UnityEngine;

// Single Inspector panel for the GAMEPLAY feel/balance knobs — shape movement, drag, collision,
// snapping, extraction thresholds. One asset, assigned on the Level prefab, so these can be tuned
// without touching code.
//
// DELIBERATELY SEPARATE FROM THE SAND (2026-09-12). SandCylinderDemo's sand simulation is meant to
// stay reusable on its own: drop SandCylinderDemo/ into another project and it must compile and run
// without a single gameplay class or gameplay asset coming along. So:
//  - SandCylinderTunables (a scene component, not this asset) stays sand physics ONLY: sand area
//    size, density, grid blocks, colors, conveyor/extraction rates. Nothing about shapes or the
//    board belongs there.
//  - This asset stays gameplay ONLY. It must never reference a SandCylinder* type, and no
//    SandCylinderDemo script may ever reference it.
// The dependency runs one way and only through the gameplay-side interaction layer: ExtractionGrid
// and Level call INTO the sand system, never the reverse.
[CreateAssetMenu(fileName = "GameplayTunables", menuName = "_ScriptableObjects/GameplayTunables", order = 3)]
public class GameplayTunables : ScriptableObject {

    // Used when the Level prefab has no asset assigned, so one missing reference can't silently
    // change how the game feels. Kept here rather than in the gameplay classes so each number has a
    // single home.
    public const float DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS = 0.035f;
    public const float DEFAULT_GRID_SAND_GAP_CELLS = 0.5f;
    public const float DEFAULT_DRAG_FOLLOW_SHARPNESS = 30f;
    public const float DEFAULT_DRAG_MAX_SPEED_CELLS = 40f;
    public const float DEFAULT_RELEASE_SNAP_SHARPNESS = 22f;
    public const float DEFAULT_RELEASE_SNAP_MIN_SPEED_CELLS = 4f;

    [Header("Drag")]
    [Tooltip("How hard the shape is pulled toward the pointer, per second. The visual catches up exponentially, so this is really a time constant: 1/sharpness is roughly the lag (30 => ~33 ms). Higher feels locked to the finger, lower feels heavy and smooths out a shaky pointer.")]
    [Range(5f, 60f)]
    [SerializeField] float _dragFollowSharpness = DEFAULT_DRAG_FOLLOW_SHARPNESS;
    [Tooltip("Speed cap while dragging, in cells per second. Keeps a pointer JUMP (alt-tab, a teleporting touch) reading as fast motion instead of a teleport. High enough that normal dragging never hits it.")]
    [Range(5f, 80f)]
    [SerializeField] float _dragMaxSpeedCells = DEFAULT_DRAG_MAX_SPEED_CELLS;

    [Header("Release Snap")]
    [Tooltip("How hard the shape is pulled onto its committed cell after release, per second — the only snap in the game. Same exponential shape as Drag Follow Sharpness: higher snaps faster.")]
    [Range(5f, 60f)]
    [SerializeField] float _releaseSnapSharpness = DEFAULT_RELEASE_SNAP_SHARPNESS;
    [Tooltip("Floor on the release snap speed, in cells per second, so the last fraction of a cell does not crawl in (an exponential alone never quite arrives).")]
    [Range(0.5f, 20f)]
    [SerializeField] float _releaseSnapMinSpeedCells = DEFAULT_RELEASE_SNAP_MIN_SPEED_CELLS;
    public float dragFollowSharpness => _dragFollowSharpness;
    public float dragMaxSpeedCells => _dragMaxSpeedCells;
    public float releaseSnapSharpness => _releaseSnapSharpness;
    public float releaseSnapMinSpeedCells => _releaseSnapMinSpeedCells;

    [Header("Extraction")]
    [Tooltip("How close to the top row a shape's cell must be DRAWN before it may extract, in cells. Container.gridPosition rounds to the nearest cell, so it reads as \"top row\" while the shape is still drawn up to half a cell short of it — without this gate sand pours across a visible gap. Keep it small: large values re-introduce that gap, and 0 risks a shape held just under the row never starting at all.")]
    [Range(0f, 0.25f)]
    [SerializeField] float _extractionArrivalToleranceCells = DEFAULT_EXTRACTION_ARRIVAL_TOLERANCE_CELLS;
    public float extractionArrivalToleranceCells => _extractionArrivalToleranceCells;

    [Header("Board Placement")]
    [Tooltip("Empty space between the board's top edge and the sand area's bottom edge, measured in CELLS: 0.5 = half a cell, 0.25 = a quarter, 0 = the board's top edge sits flush against the sand. The cell's world size still comes from the sand (SandCylinderTunables.CubeWorldSize), so this stays a pure gameplay/layout knob and board and sand can never drift out of scale. Read by Level.buildBoard at level build time only — changing it during Play does nothing until the level is rebuilt. PURELY VISUAL: extraction does not depend on it. ExtractionGrid anchors its vertical extraction band to the sand's own bottom edge, so the sand a shape can reach is the same at every gap — see ExtractionGrid's EXTRACTION BAND note. The 1-cell cap is just to keep the board from drifting absurdly far from the sand.")]
    [Range(0f, 1f)]
    [SerializeField] float _gridSandGapCells = DEFAULT_GRID_SAND_GAP_CELLS;
    public float gridSandGapCells => _gridSandGapCells;
}
