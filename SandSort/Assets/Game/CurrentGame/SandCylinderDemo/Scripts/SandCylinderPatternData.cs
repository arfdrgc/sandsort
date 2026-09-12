using UnityEngine;

// A hand-authored initial sand pattern for SandCylinderDemo — an alternative
// to SandCylinderSandGrid's randomized Tetris-piece generator. Paint it
// visually via the custom Inspector (Scripts/Editor/SandCylinderPatternDataEditor.cs)
// on a Width x Height grid of BLOCKS, then drag the asset onto
// SandCylinderTunables.customPattern to use it instead of a fresh random
// layout every Play.
//
// Width/Height are in BLOCK units — the same "1-unit" block a collector
// cube fully drains in one pass (see SandCylinderTunables.EffectiveBlockGridWidth/
// Height, which read these two fields directly and are unaffected by
// subdivisionsPerBlock below). subdivisionsPerBlock is purely about how
// finely you can PAINT within each block: 1 (default) means one solid color
// per whole block, same as before; 2 lets each block be painted as a 2x2
// grid of 4 independently-colored quadrants for more detailed layouts,
// without changing the block-grid's own size or what a cube can hold/reach.
//
// Runtime-readable (this class itself has no editor-only dependency — only
// its custom Inspector does), so SandCylinderSandGrid.FillInitialLayers can
// read it directly in a build.
[CreateAssetMenu(fileName = "SandCylinderPattern", menuName = "SandCylinderDemo/Sand Pattern")]
public class SandCylinderPatternData : ScriptableObject {

    [Min(1)] public int width = 5;
    [Min(1)] public int height = 7;

    [Tooltip("How many equal sub-cells per side each block is painted as — 1 = one solid color per whole block (default); 2 = each block becomes a 2x2 grid of 4 independently-colored quadrants, for more detailed hand-painted layouts. Purely a painting-resolution knob: the block grid's own Width/Height (and everything gameplay reads from them — cube reach/capacity, extraction) are unaffected.")]
    [Range(1, 4)] public int subdivisionsPerBlock = 1;

    // Flat, row-major (paintY * PaintWidth + paintX), one entry per PAINT
    // cell — a block subdivided subdivisionsPerBlock x subdivisionsPerBlock
    // times, so PaintWidth/PaintHeight (not width/height directly) are the
    // real dimensions this array is laid out at. 0 means "empty" (no sand
    // placed there at all — not "auto-fill", unlike the randomized
    // generator's leftover-slivers); 1..5 is a 1-based index into
    // SandCylinderTunables.sandColors (palette index colorSlot - 1). Kept
    // flat rather than a 2D array purely because Unity can't serialize a 2D
    // array directly in the Inspector.
    [SerializeField] byte[] cells = new byte[0];

    // The actual painted grid's dimensions, in PAINT cells (sub-cells) —
    // what GetCell/SetCell and the custom Inspector's button grid both
    // address. Equal to width/height exactly when subdivisionsPerBlock == 1.
    public int PaintWidth => Mathf.Max(1, width) * Mathf.Max(1, subdivisionsPerBlock);
    public int PaintHeight => Mathf.Max(1, height) * Mathf.Max(1, subdivisionsPerBlock);

    // Re-syncs `cells` to the CURRENT width/height/subdivisionsPerBlock
    // (e.g. right after a freshly-created asset's default field values are
    // set, when there's no "old" size to preserve anything from). For an
    // actual resize where any of those are CHANGING, call
    // Resize(newWidth, newHeight, newSubdivisions) instead — that one still
    // has access to the real old dimensions to remap from; this one does
    // not, since by the time it runs the fields already hold whatever the
    // caller most recently set them to.
    public void EnsureSized() {
        Resize(width, height, subdivisionsPerBlock);
    }

    // Resizes `cells` to the new PAINT dimensions, preserving any existing
    // cell values that still fall within the new bounds (so shrinking/
    // growing the grid, or changing its subdivision level, in the Inspector
    // doesn't discard an in-progress painting). Captures the CURRENT
    // PaintWidth/PaintHeight as the "old" dimensions before overwriting
    // width/height/subdivisionsPerBlock, so the remap uses the actual
    // stride `cells` was laid out with — passing the new values directly
    // here (rather than setting the fields first and calling a no-argument
    // resize after) is what makes that possible.
    public void Resize(int newWidth, int newHeight, int newSubdivisions) {
        int oldPaintWidth = PaintWidth;
        int oldPaintHeight = PaintHeight;

        width = Mathf.Max(1, newWidth);
        height = Mathf.Max(1, newHeight);
        subdivisionsPerBlock = Mathf.Max(1, newSubdivisions);
        int newPaintWidth = PaintWidth;
        int newPaintHeight = PaintHeight;

        byte[] resized = new byte[newPaintWidth * newPaintHeight];
        if (cells != null && oldPaintWidth > 0) {
            for (int y = 0; y < newPaintHeight && y < oldPaintHeight; y++) {
                for (int x = 0; x < newPaintWidth && x < oldPaintWidth; x++) {
                    int oldIndex = y * oldPaintWidth + x;
                    if (oldIndex >= 0 && oldIndex < cells.Length) {
                        resized[y * newPaintWidth + x] = cells[oldIndex];
                    }
                }
            }
        }
        cells = resized;
    }

    // paintX/paintY are PAINT-space (sub-cell) coordinates: 0..PaintWidth-1,
    // 0..PaintHeight-1 — NOT block coordinates, except when
    // subdivisionsPerBlock == 1, where the two coincide exactly.
    public byte GetCell(int paintX, int paintY) {
        int pw = PaintWidth, ph = PaintHeight;
        if (cells == null || paintX < 0 || paintX >= pw || paintY < 0 || paintY >= ph) return 0;
        int index = paintY * pw + paintX;
        return index >= 0 && index < cells.Length ? cells[index] : (byte)0;
    }

    public void SetCell(int paintX, int paintY, byte colorSlot) {
        int pw = PaintWidth, ph = PaintHeight;
        if (cells == null || paintX < 0 || paintX >= pw || paintY < 0 || paintY >= ph) return;
        int index = paintY * pw + paintX;
        if (index >= 0 && index < cells.Length) cells[index] = colorSlot;
    }
}
