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

        // A Shape reference that has gone missing, NOT an empty one: an empty one is a legitimate
        // legacy entry that authors its footprint in `cells`, while a broken one silently collapses
        // to that same `cells` list and quietly changes the piece's shape. See
        // ContainerData.hasMissingShapeReference. Deliberately the only footprint check here — board
        // bounds, overlap and rotated-footprint validation are a separate level-authoring task.
        for (int i = 0; i < _containers.Count; i++) {
            if (!_containers[i].hasMissingShapeReference) continue;
            issues.Add($"Container {i} ({_containers[i].color} at {_containers[i].position}) points at a Shape prefab that is missing — it will fall back to its legacy `cells` footprint ({_containers[i].cells.Count} cell(s)), which is almost certainly not the shape this level means.");
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

// What a level says about one piece (2026-09-12): Shape + Position + Rotation + Color. The shape is
// the CANONICAL prefab — Prefabs/Shapes/Shape_L4.prefab, never a per-orientation variant — and the
// rotation is applied to the instance, so "L4 at (3,5) rotated 90" and "L4 at (6,2) rotated 180" are
// the same asset twice. See Shape.cs.
[System.Serializable]
public class ContainerData {
    // Anchor cell of the shape, in board grid space. Every occupied cell offset is relative to this.
    public Vector2Int position;
    // The canonical shape prefab. Leave it empty to author a one-off footprint in `cells` instead —
    // that is what the pre-Shape levels do and they keep working unchanged.
    public Shape shape;
    // Right angles only. Ignored when `shape` is empty (a hand-authored `cells` list is taken as-is).
    public ShapeRotation rotation = ShapeRotation.Deg0;
    // Legacy / one-off footprint: occupied cells relative to position. {(0,0)} = 1x1.
    // {(0,0),(1,0)} = 2x1. Only read when `shape` is empty. NOT authoritative once a Shape is
    // assigned — it is left in place only until every level has been migrated and we have confirmed
    // nothing else authors footprints this way.
    public List<Vector2Int> cells = new() { Vector2Int.zero };
    public ColorSO.ItemColor color;
    // No capacityUnits field here anymore — capacity is computed at build time, see class header.

    // Resolved footprint, cached per entry. NOT serialized and NOT authoritative: it is rebuilt from
    // shape + rotation (or from the legacy cells) whenever either of those changes, so editing the
    // rotation in the Inspector between Play runs can never serve a stale list. A domain reload
    // simply clears it and the next read recomputes.
    [System.NonSerialized] List<Vector2Int> _resolvedCells;
    [System.NonSerialized] Shape _resolvedShape;
    [System.NonSerialized] ShapeRotation _resolvedRotation;
    // Which branch produced _resolvedCells. Needed on its own because _resolvedShape can no longer
    // be told apart from `shape` once the prefab is destroyed mid-session: both sides are then the
    // same fake-null wrapper and `!=` (Unity's operator) calls them equal, which would keep serving
    // the footprint of an asset that is already gone.
    [System.NonSerialized] bool _resolvedFromShape;

    // The footprint this piece actually occupies, and the single source everything gameplay-side
    // uses: Board occupancy, the drag/collision sweep, the extraction footprint, and areaUnits in
    // the capacity model. Never a mesh or a renderer bounds.
    //
    // The legacy branch hands back a COPY, never the serialized list itself: that list lives inside
    // the level ScriptableObject, and in the Editor a runtime mutation of it would be written back
    // into the asset on disk. Nothing mutates it today; the copy makes sure nothing ever can.
    public List<Vector2Int> occupiedCells {
        get {
            bool useShape = shape != null;
            if (_resolvedCells == null || _resolvedFromShape != useShape || _resolvedShape != shape || _resolvedRotation != rotation) {
                _resolvedCells = useShape
                    ? Shape.rotatedCells(shape.canonicalCells, rotation)
                    : new List<Vector2Int>(cells);
                _resolvedShape = shape;
                _resolvedRotation = rotation;
                _resolvedFromShape = useShape;
            }
            return _resolvedCells;
        }
    }

    // A Shape WAS assigned here and its prefab has since gone missing. Unity hands a destroyed or
    // unresolvable object reference back as a "fake null" — a live managed wrapper whose native
    // object is gone — so `shape == null` is true for it just as it is for a field that was never
    // filled in. ReferenceEquals bypasses that operator and tells the two apart: a genuinely empty
    // field is a real null reference, a broken one is not.
    //
    // This matters because the two take the SAME code path above: without the distinction, a level
    // that means "L4 here" silently builds a 1x1 (the default `cells`) the moment the prefab is
    // moved or deleted. See SandLevelSO.validateLevel and Level.buildContainers, which report it.
    public bool hasMissingShapeReference => shape == null && !ReferenceEquals(shape, null);
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
