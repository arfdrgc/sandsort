using System.Collections.Generic;
using UnityEngine;

// A dense per-column sand pool sitting directly above the Board (column x sits above board
// column x). Internally a flat grid of layered cells (see _cells below) — but gameplay-wise each
// column behaves as a bottom(index 0)-to-top stack, not a 1:1 board-row mapping: see
// SandLevelSO's BOTTOM-UP EXTRACTION rule and tryConsumeFromColumn()/canExtractColumn().
//
// VISUALS (2026-09-11, revised): zero per-grain GameObjects. The static picture is a single
// procedurally-painted texture (see SandCanvasRenderer, ported from SandCylinderDemo's
// SandCylinderRenderer texture-painting architecture) and the pour/erosion burst reuses
// SandCylinderDemo's actual SandExtractionParticleEffect + SandCylinderTunables classes directly
// (not a reimplementation) — see setupPourEffect().
//
// Uses the same flat XY-facing-camera layout convention as Board.cs: column x, layer z -> local
// (x * CELL_SIZE, z * CELL_SIZE, 0). The whole SandCanvas GameObject is positioned by Level.cs
// with a Y offset so it renders directly above the Board, matching the reference concept art.
public class SandCanvas : MonoBehaviour {

    Vector2Int _size;
    // Flat 1D array (index = x + z * _size.x), NOT ColorSO.ItemColor[,] — Unity cannot serialize
    // rectangular arrays, so a 2D array silently resets to null on a domain reload triggered
    // mid-Play (e.g. a script recompile), even though this field is never meant to be inspector-visible.
    ColorSO.ItemColor[] _cells;

    SandCanvasRenderer _renderer;

    // SandExtractionParticleEffect.SpawnGrain addresses colors by a 1-based byte index into
    // SandCylinderTunables.sandColors — _paletteColors[i] is the ColorSO.ItemColor that index i
    // represents (index 0 unused, matching that class's own convention). A plain array, not a
    // Dictionary: Dictionaries aren't Unity-serializable either, hitting the exact same
    // domain-reload-reset bug a 2D array would (see the field comment above) — a flat array of a
    // small, fixed palette avoids that risk and a linear scan over a handful of colors is fine.
    ColorSO.ItemColor[] _paletteColors;
    SandCylinderTunables _tunables;
    SandExtractionParticleEffect _particleEffect;

    public void initialize(SandCanvasData data, Vector2Int boardSize) {
        _size = boardSize;
        _cells = new ColorSO.ItemColor[boardSize.x * boardSize.y];

        for (int x = 0; x < boardSize.x; x++) {
            SandColumn column = (data != null && x < data.columns.Count) ? data.columns[x] : null;

            for (int z = 0; z < boardSize.y; z++) {
                ColorSO.ItemColor color = (column != null && z < column.cells.Count) ? column.cells[z] : ColorSO.ItemColor.NONE;
                _cells[cellIndex(x, z)] = color;
            }
        }

        GameObject rendererObject = new GameObject("SandCanvasRenderer");
        rendererObject.transform.SetParent(transform, false);
        _renderer = rendererObject.AddComponent<SandCanvasRenderer>();
        _renderer.initialize(boardSize);

        for (int x = 0; x < boardSize.x; x++) {
            for (int z = 0; z < boardSize.y; z++) {
                _renderer.setCell(x, z, _cells[cellIndex(x, z)]);
            }
        }

        setupPourEffect();
    }

    // Builds the actual SandCylinderDemo particle pipeline (SandCylinderTunables +
    // SandExtractionParticleEffect), not a reimplementation of it. cubeMovementSpeed is forced to
    // 0 because that class predicts a grain's landing X from a constant target velocity (built
    // for cubes riding a moving conveyor) — our Containers are stationary while pouring, so 0
    // makes it correctly predict the Container's current position instead.
    void setupPourEffect() {
        List<ColorSO.ItemColor> distinctColors = new();
        for (int i = 0; i < _cells.Length; i++) {
            ColorSO.ItemColor color = _cells[i];
            if (color != ColorSO.ItemColor.NONE && !distinctColors.Contains(color)) {
                distinctColors.Add(color);
            }
        }

        _paletteColors = new ColorSO.ItemColor[distinctColors.Count + 1];
        Color[] paletteRGB = new Color[distinctColors.Count];
        for (int i = 0; i < distinctColors.Count; i++) {
            _paletteColors[i + 1] = distinctColors[i];
            paletteRGB[i] = SandColorUtility.toUnityColor(distinctColors[i]);
        }

        GameObject tunablesObject = new GameObject("SandCylinderTunables");
        tunablesObject.transform.SetParent(transform, false);
        _tunables = tunablesObject.AddComponent<SandCylinderTunables>();
        _tunables.sandColors = paletteRGB;
        _tunables.cubeMovementSpeed = 0f;

        GameObject particleObject = new GameObject("SandPourParticles");
        particleObject.transform.SetParent(transform, false);
        _particleEffect = particleObject.AddComponent<SandExtractionParticleEffect>();
        _particleEffect.Init(_tunables, new Material(Shader.Find("Sprites/Default")), 1f);
    }

    byte paletteIndexOf(ColorSO.ItemColor color) {
        for (int i = 1; i < _paletteColors.Length; i++) {
            if (_paletteColors[i] == color) return (byte)i;
        }
        return 0;
    }

    int cellIndex(int x, int z) => x + z * _size.x;

    // Index (within the column) of the lowest remaining (non-NONE) layer, or -1 if the column is
    // fully empty. This is the layer BOTTOM-UP EXTRACTION always targets next.
    int lowestRemainingLayer(int column) {
        for (int z = 0; z < _size.y; z++) {
            if (_cells[cellIndex(column, z)] != ColorSO.ItemColor.NONE) return z;
        }
        return -1;
    }

    // True if a Container of `color` could extract from this column right now — i.e. the
    // lowest remaining layer exists AND matches. A mismatched lowest layer blocks the column for
    // this color until whatever DOES match it removes it first (see SandLevelSO's class header).
    public bool canExtractColumn(int column, ColorSO.ItemColor color) {
        int layer = lowestRemainingLayer(column);
        return layer >= 0 && _cells[cellIndex(column, layer)] == color;
    }

    // True if `color` exists ANYWHERE in this column, regardless of what's currently blocking it
    // from the bottom. Used for the deadlock check (Level.canExtractAt) rather than
    // canExtractColumn: a column temporarily blocked by a different color underneath is a normal
    // sequencing dependency (whatever's blocking it can itself be cleared later by another
    // Container) — not a genuine "this Container can never get its sand" deadlock. Only a color
    // that isn't in the column AT ALL rules that column out for good.
    public bool hasAnyRemainingCellInColumn(int column, ColorSO.ItemColor color) {
        for (int z = 0; z < _size.y; z++) {
            if (_cells[cellIndex(column, z)] == color) return true;
        }
        return false;
    }

    // Consumes (erodes) the column's lowest remaining layer if it matches color, repaints that
    // cell transparent on the SandCanvasRenderer texture, and plays a falling-sand grain via the
    // real SandExtractionParticleEffect toward target. Returns false if the column can't
    // currently be extracted for this color (empty, or blocked by a mismatched lower layer) —
    // the caller (Container) treats that as "no pour this tick".
    public bool tryConsumeFromColumn(int column, ColorSO.ItemColor color, Transform target) {
        int layer = lowestRemainingLayer(column);
        if (layer < 0 || _cells[cellIndex(column, layer)] != color) return false;

        _cells[cellIndex(column, layer)] = ColorSO.ItemColor.NONE;
        _renderer.setCell(column, layer, ColorSO.ItemColor.NONE);

        Vector3 cellWorldPosition = transform.TransformPoint(new Vector3(column * Board.CELL_SIZE, layer * Board.CELL_SIZE, 0f));
        _particleEffect.SpawnGrain(cellWorldPosition, target, paletteIndexOf(color));

        return true;
    }

    public bool hasAnyRemainingCellOfColor(ColorSO.ItemColor color) {
        for (int x = 0; x < _size.x; x++) {
            for (int z = 0; z < _size.y; z++) {
                if (_cells[cellIndex(x, z)] == color) return true;
            }
        }
        return false;
    }

    // Total pixels of a color across the whole canvas — used by Level.buildContainers() to derive
    // Container capacity (see SandLevelSO's class header for the capacity model).
    public int countCellsOfColor(ColorSO.ItemColor color) {
        int count = 0;
        for (int x = 0; x < _size.x; x++) {
            for (int z = 0; z < _size.y; z++) {
                if (_cells[cellIndex(x, z)] == color) count++;
            }
        }
        return count;
    }
}
