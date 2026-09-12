using System.Collections.Generic;
using UnityEngine;

// Prototype defaults (2026-09-11, revised same day) — resolves game_mechanics.md's Open Design
// Questions with the simplest workable choices, NOT final design decisions:
//  - Board is a discrete grid (not continuous free movement).
//  - Containers occupy an arbitrary set of grid cells (a "shape" — 1x1, 2x1, L, T, ...), not
//    just rectangles. See ContainerData.cells.
//
// SAND (revised 2026-09-11): the sand is SandCylinderDemo's approved SandCylinderSandGrid, reused
// as-is. A level only supplies its starting picture — a SandCylinderPatternData painted with that
// demo's own pattern editor. The pattern's width (in blocks) must equal boardSize.x, because each
// board column sits under exactly one sand block (see Level.buildBoard).
//
// EXTRACTION RULE (2026-09-11): a Container only pulls sand through cells sitting in the Board's
// top row — the row closest to the sand. The pulling itself is the demo's own extraction. See
// ExtractionGrid.
//
// AREA-BASED CAPACITY MODEL (2026-09-11): a Container's 100%-fill Capacity is derived, not
// hand-authored. For each color, the sand grid's starting cell count of that color is split among
// every Container of that color in proportion to each Container's areaUnits (its shape's cell
// count): Capacity(container) = totalCells * areaUnits(container) / totalAreaUnitsOfThatColor,
// with largest-remainder rounding — see Level.buildContainers(). This guarantees total Container
// capacity for a color always equals total available sand of that color, by construction; see
// validateLevel() below and Container.forceComplete() for the edge-case safeguard.
[CreateAssetMenu(fileName = "SandLevel", menuName = "_ScriptableObjects/SandLevel", order = 2)]
public class SandLevelSO : LevelSO {

    [Header("Board")]
    [SerializeField] Vector2Int _boardSize = new Vector2Int(5, 5);
    public Vector2Int boardSize => _boardSize;

    [Header("Containers")]
    [SerializeField] List<ContainerData> _containers = new();
    public List<ContainerData> containers => _containers;

    [Header("Sand")]
    [SerializeField] SandCylinderPatternData _sandPattern;
    public SandCylinderPatternData sandPattern => _sandPattern;

    [Header("Timer")]
    [SerializeField, Min(1f)] float _timerSeconds = 60f;
    public float timerSeconds => _timerSeconds;

    // Pure-data sanity check (no runtime GameObjects needed). paletteColors[i] is the gameplay
    // ItemColor of pattern color slot i + 1 (see Level._sandPaletteColors). Flags: a missing
    // pattern, a pattern whose width doesn't match the board, pattern slots with no ItemColor
    // mapping, sand colors with no matching Container (that sand can never be collected), and
    // Container colors with no matching sand (auto-complete immediately at runtime — see
    // Container.forceComplete()). None are fatal; all are worth flagging.
    public bool validateLevel(IReadOnlyList<ColorSO.ItemColor> paletteColors, out string message) {
        if (_sandPattern == null) {
            message = "No sand pattern assigned.";
            return false;
        }

        List<string> issues = new();

        if (_sandPattern.width != _boardSize.x) {
            issues.Add($"Sand pattern is {_sandPattern.width} block(s) wide but the board has {_boardSize.x} column(s) — each board column must sit under exactly one sand block.");
        }

        HashSet<ColorSO.ItemColor> sandColors = new();
        HashSet<byte> unmappedSlots = new();
        for (int y = 0; y < _sandPattern.PaintHeight; y++) {
            for (int x = 0; x < _sandPattern.PaintWidth; x++) {
                byte slot = _sandPattern.GetCell(x, y);
                if (slot == SandCylinderSandGrid.EMPTY) continue;

                if (slot > paletteColors.Count) {
                    unmappedSlots.Add(slot);
                } else {
                    sandColors.Add(paletteColors[slot - 1]);
                }
            }
        }

        foreach (byte slot in unmappedSlots) {
            issues.Add($"Sand pattern color slot {slot} has no ItemColor mapping on the Level prefab.");
        }

        HashSet<ColorSO.ItemColor> containerColors = new();
        foreach (ContainerData data in _containers) containerColors.Add(data.color);

        foreach (ColorSO.ItemColor color in sandColors) {
            if (!containerColors.Contains(color)) {
                issues.Add($"{color} sand has no matching Container — that sand can never be collected.");
            }
        }

        foreach (ColorSO.ItemColor color in containerColors) {
            if (!sandColors.Contains(color)) {
                issues.Add($"{color} Container(s) have no matching sand at all — will auto-complete immediately.");
            }
        }

        message = issues.Count == 0
            ? $"OK — {sandColors.Count} sand color(s), matched to {_containers.Count} Container(s)."
            : string.Join(" ", issues);

        return issues.Count == 0;
    }
}

[System.Serializable]
public class ContainerData {
    // Anchor cell of the shape, in board grid space. cells[i] offsets are relative to this.
    public Vector2Int position;
    // Shape: occupied cells relative to position. {(0,0)} = 1x1. {(0,0),(1,0)} = 2x1.
    // areaUnits (used by the capacity model) is simply cells.Count.
    public List<Vector2Int> cells = new() { Vector2Int.zero };
    public ColorSO.ItemColor color;
    // No capacityUnits field here anymore — capacity is computed at build time, see class header.
}

// Retired: SandLevelSO no longer holds per-column sand data (the sand is now SandCylinderSandGrid,
// fed by _sandPattern). Only the old SandCanvas.cs — no longer built by Level, pending deletion —
// still references these two types; delete them together with it.
[System.Serializable]
public class SandCanvasData {
    [SerializeField] List<SandColumn> _columns = new();
    public List<SandColumn> columns => _columns;
}

[System.Serializable]
public class SandColumn {
    public List<ColorSO.ItemColor> cells = new();
}
