using System.Collections.Generic;
using UnityEngine;

// Isolated falling-sand cellular automaton for SandCylinderDemo: a
// Tetris-piece-shaped block-grid initial fill, a color-blind hole-centric
// simulation (SimulatePass / ChooseSource) and mouth-only extraction
// (ExtractAtMouth). Sand enters, moves and leaves only through the
// Place / Move / Remove primitives.
public class SandCylinderSandGrid : MonoBehaviour {

    public const byte EMPTY = 0;

    SandCylinderTunables tunables;
    public SandCylinderTunables Tunables => tunables;

    byte[] cells;
    int[] colorCounts; // index 0 unused (EMPTY), 1..N per palette color

    // Persistent visual identity, one byte per cell, parallel to cells[]. A
    // grain gets its tint once, when Place creates it, and carries it for
    // life: Move copies it along with the color, Remove clears it. It is never
    // recomputed from a grid position, and no movement or extraction rule may
    // read it — it exists for the renderer only (see GetTint).
    byte[] tint;

    // Sand cells per row, kept in sync by Place/Move/Remove so a simulation
    // pass can skip rows with nothing in them.
    int[] rowCount;

    // Two independent xorshift32 streams. visualRng is drawn ONLY by NextTint
    // (a new grain's tint, from Place); physicsRng ONLY by NextPhysicsRandom
    // (movement decisions). Neither is derived from the other, from
    // UnityEngine.Random, or from any cell's position/color/tint, so drawing
    // from one can never shift the other's sequence. Both restart from their
    // own seed in Resize, so the same fill always produces the same tints.
    // 0 is xorshift's fixed point and also what a mid-Play domain reload
    // leaves in a non-serialized field — hence the reseed guards.
    const uint VisualRngSeed = 0x9E3779B9u;
    const uint PhysicsRngSeed = 0x85EBCA6Bu;
    uint visualRng;
    uint physicsRng;

    int width, height;
    float passAccumulator;
    int passParity;

    // The awake region: what the NEXT SimulatePass has to look at — columns
    // [wakeMinX, wakeMaxX], rows wakeMinY up to the top of the grid (a hole
    // only ever travels upward, so there is no upper bound to keep). Grown by
    // Wake, which every primitive calls; handed to a pass and emptied by
    // ClearWake. Empty (wakeMaxX < wakeMinX) means the sand is at rest and the
    // simulation does no work at all.
    int wakeMinX, wakeMaxX, wakeMinY;
    bool IsAwake => wakeMaxX >= wakeMinX;

    // ExtractAtMouth scratch (support rule), one flag per mouth column. Rebuilt
    // on demand, so a mid-Play domain reload leaving it null is harmless.
    bool[] mouthColumnOpen;

    // Monotonic "the grid changed" counter. The Place/Move/Remove primitives
    // below are the ONLY `cells[...] =` in this file and each bumps it, so
    // every mutation path — fill, spawn, simulation, extraction — bumps this
    // for free; Resize bumps it separately because it replaces the array
    // outright rather than writing through a primitive. A reader
    // can therefore tell whether anything moved since it last looked without
    // diffing the grid. SandCylinderRenderer uses it to skip its full texture
    // rebuild + upload on frames where nothing changed: measured on device
    // (Mi 9T, IL2CPP) that rebuild cost 11.3 ms/frame on the 306x306 9-colour
    // grid and ran even while the sand was completely static.
    ulong cellsVersion;
    public ulong CellsVersion => cellsVersion;

    public int Width => width;
    public int Height => height;
    public int TotalSandCount { get; private set; }

    // The cylinder's original total cell count, captured once when the fill
    // finishes and never touched again — the fixed denominator per-cube
    // collection percentages are computed against, as opposed to
    // TotalSandCount which keeps decreasing as extraction proceeds.
    public int InitialSandCount { get; private set; }

    // Bound to the shared SandCylinderTunables instance by SandCylinderDemoBootstrap
    // before any other method on this component is used.
    public void Init(SandCylinderTunables tunables) {
        this.tunables = tunables;
        Resize(tunables.GridWidth, tunables.GridHeight);
    }

    public void Resize(int w, int h) {
        width = Mathf.Max(1, w);
        height = Mathf.Max(1, h);
        cells = new byte[width * height];
        cellsVersion++; // a fresh (all-EMPTY) array is a content change no primitive sees
        colorCounts = new int[tunables.sandColors.Length + 1];
        TotalSandCount = 0;
        tint = new byte[width * height];
        rowCount = new int[height];
        visualRng = VisualRngSeed;
        physicsRng = PhysicsRngSeed;

        passAccumulator = 0f;
        passParity = 0;
        ClearWake();
    }

    public byte GetCell(int x, int y) => cells[y * width + x];

    // The renderer's read of a grain's persistent tint (0..255); meaningless
    // for an EMPTY cell. Read-only on purpose — see tint's own comment.
    public byte GetTint(int x, int y) => tint[y * width + x];

    // WRITE PRIMITIVES. Place / Move / Remove are the only three ways sand
    // enters, travels through or leaves the grid: each keeps cells, tint,
    // rowCount, colorCounts, TotalSandCount and cellsVersion in step and wakes
    // the part of the grid its change can set in motion (see Wake), so no
    // caller does any of that bookkeeping itself. Callers guarantee in-bounds
    // coordinates. All three refuse (and change nothing) rather than corrupt
    // the counts when their precondition does not hold.

    // Creates a new grain of colorIndex in the empty cell (x,y) and gives it
    // its lifelong tint from the visual stream. False if the cell is occupied.
    bool Place(int x, int y, byte colorIndex) {
        int idx = y * width + x;
        if (colorIndex == EMPTY || cells[idx] != EMPTY) return false;
        cells[idx] = colorIndex;
        tint[idx] = NextTint();
        rowCount[y]++;
        colorCounts[colorIndex]++;
        TotalSandCount++;
        cellsVersion++;
        Wake(x - 1, x + 1, y - 1);
        return true;
    }

    // Moves the grain at (srcX,srcY) into the empty cell (dstX,dstY); color
    // and tint travel together, which is what makes the grain pattern move
    // with the sand. False if there is no grain to move or the destination is
    // occupied.
    bool Move(int srcX, int srcY, int dstX, int dstY) {
        int src = srcY * width + srcX;
        int dst = dstY * width + dstX;
        if (cells[src] == EMPTY || cells[dst] != EMPTY) return false;
        cells[dst] = cells[src];
        tint[dst] = tint[src];
        cells[src] = EMPTY;
        tint[src] = 0;
        rowCount[srcY]--;
        rowCount[dstY]++;
        cellsVersion++;
        Wake(Mathf.Min(srcX, dstX) - 1, Mathf.Max(srcX, dstX) + 1, Mathf.Min(srcY, dstY) - 1);
        return true;
    }

    // Takes the grain at (x,y) out of the grid and clears its tint. Returns
    // the color removed, or EMPTY if there was no grain.
    byte Remove(int x, int y) {
        int idx = y * width + x;
        byte c = cells[idx];
        if (c == EMPTY) return EMPTY;
        cells[idx] = EMPTY;
        tint[idx] = 0;
        rowCount[y]--;
        colorCounts[c]--;
        TotalSandCount--;
        cellsVersion++;
        Wake(x - 1, x + 1, y);
        return c;
    }

    // Grows the awake region to cover columns [xLo, xHi] from row yLo up.
    // Unclamped on purpose (SimulatePass clamps once per pass). What each
    // primitive wakes is exactly what its change can enable: a cell that
    // became EMPTY is a hole to fill (its own row); a cell that became
    // occupied is a new source for the three holes under it and new support
    // for the grain above it, which can then slide into the holes beside it
    // (its own row and the one below, one column to each side).
    void Wake(int xLo, int xHi, int yLo) {
        if (xLo < wakeMinX) wakeMinX = xLo;
        if (xHi > wakeMaxX) wakeMaxX = xHi;
        if (yLo < wakeMinY) wakeMinY = yLo;
    }

    void ClearWake() {
        wakeMinX = int.MaxValue;
        wakeMaxX = int.MinValue;
        wakeMinY = int.MaxValue;
    }

    byte NextTint() {
        uint s = visualRng;
        if (s == 0) s = VisualRngSeed;
        s ^= s << 13;
        s ^= s >> 17;
        s ^= s << 5;
        visualRng = s;
        return (byte)(s >> 24);
    }

    // Uniform in [0,1). The only source of randomness ChooseSource and
    // ExtractAtMouth use — nothing in the simulation draws from
    // UnityEngine.Random.
    float NextPhysicsRandom() {
        uint s = physicsRng;
        if (s == 0) s = PhysicsRngSeed;
        s ^= s << 13;
        s ^= s >> 17;
        s ^= s << 5;
        physicsRng = s;
        return (s >> 8) * (1f / 16777216f);
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public int GetColorCount(byte colorIndex) {
        if (colorIndex <= 0 || colorIndex >= colorCounts.Length) return 0;
        return colorCounts[colorIndex];
    }

    // Adds one grain of colorIndex at (x,y) if that cell is currently empty —
    // SandMixDemo's pour interaction (SandMixSpout) uses this to add sand
    // from the top at runtime, instead of the one-shot FillInitialLayers path
    // SandCylinderDemo uses. Goes through Place like every other birth path,
    // so GetColorCount, TotalSandCount and the cube-collection percent math all
    // stay correct regardless of how a grain entered the grid.
    public void SpawnCell(int x, int y, byte colorIndex) {
        if (!InBounds(x, y)) return;
        if (colorIndex == EMPTY || colorIndex >= colorCounts.Length) return;
        Place(x, y, colorIndex); // refuses an occupied cell itself
    }

    // Tetromino-like piece shapes (plus two 2x3/3x2 rectangles, which aren't
    // classic tetrominoes but were explicitly asked for alongside them) used
    // to tile the initial block-grid — see FillInitialLayers. Each shape is
    // a list of (dx, dy) cell offsets from its own anchor cell, and EVERY
    // shape MUST include (0,0) as one of its own offsets — anchoring it at
    // the first uncovered block otherwise never actually covers that block
    // (the placement loop below only checks "did any offset land here", so
    // an anchor cell not in the shape's own offset list is left at -1
    // forever, which used to crash ColorPieces/MarkNeighborColor with an
    // IndexOutOfRangeException once it hit that -1 as a "neighbor" — this
    // bit three shapes below before they were corrected to anchor on one of
    // their own real cells instead of an implied bounding-box corner).
    // Offsets do NOT need to be non-negative — BuildPieceIdGrid's bounds
    // check handles negative dx/dy correctly — only (0,0)'s presence
    // matters.
    // Public: SandCylinderPatternDataEditor's "Randomize (Tetris pieces)"
    // button reuses this (and BuildPieceIdGrid/ColorPieces below) so a
    // hand-painted pattern's randomize preview always matches exactly what
    // the runtime generator itself would produce, without duplicating this
    // logic in editor-only code.
    public static readonly Vector2Int[][] PieceShapes = {
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0) }, // I horizontal
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(0,3) }, // I vertical
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }, // O square
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(1,1) }, // T, bar bottom
        new[] { new Vector2Int(0,0), new Vector2Int(-1,1), new Vector2Int(0,1), new Vector2Int(1,1) }, // T, bar top (anchored on the nub)
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(1,1) }, // T, nub right
        new[] { new Vector2Int(0,0), new Vector2Int(-1,1), new Vector2Int(0,1), new Vector2Int(0,2) }, // T, nub left (anchored on the nub)
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(1,2) }, // L
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1) }, // L rotated
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(1,2) }, // J (mirrored L)
        new[] { new Vector2Int(0,0), new Vector2Int(-2,1), new Vector2Int(-1,1), new Vector2Int(0,1) }, // J rotated (anchored on its own corner cell)
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(0,2), new Vector2Int(1,2) }, // 2x3 rect (tall)
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }, // 3x2 rect (wide)
    };

    // Reference: "Sand Blocks: Drop Puzzle" (Rollic Games) lays its initial
    // sand out as a grid of solid-color blocks — one fixed cell size
    // (tunables.blockCellSize) per block, tunables.EffectiveBlockGridWidth x
    // tunables.EffectiveBlockGridHeight blocks total (blockGridWidth/
    // blockGridHeight normally, or a customPattern's own Width/Height when
    // one is assigned — see EffectiveBlockGridWidth/Height's own doc
    // comment) — but grouped into Tetris-like multi-block piece shapes (see
    // PieceShapes) rather than one color per single block. tunables.GridWidth
    // is DEFINED as EffectiveBlockGridWidth * blockCellSize, so width is
    // always an exact multiple of blockCellSize — block column boundaries
    // are exact (blockCol * blockCellSize), no rounding remainder to worry
    // about.
    //
    // Public: SandExtractionController reads GetBlockColumnRange to snap a
    // cube's extraction window to whichever whole single block (not the
    // larger multi-block piece it's part of — a cube's reach is still
    // exactly one block wide) its current column falls within.
    //
    // If tunables.customPattern is assigned, its hand-painted colors are
    // used verbatim (via PaintFromPattern below) instead of running the
    // randomized piece-tiling/coloring generator — see
    // SandCylinderPatternDataEditor for how it's painted, and
    // PaintFromPattern's own doc comment for how a block can be painted at
    // finer-than-one-color-per-block resolution (pattern.subdivisionsPerBlock).
    public void FillInitialLayers() {
        int colorCount = tunables.sandColors.Length;
        if (colorCount <= 0) return;

        int blockSize = Mathf.Max(1, tunables.blockCellSize);
        int blockGridWidth = tunables.EffectiveBlockGridWidth;
        int blockGridHeight = Mathf.Max(1, Mathf.Min(tunables.EffectiveBlockGridHeight, height / blockSize));
        if (blockGridWidth <= 0 || blockGridHeight <= 0) return;

        SandCylinderPatternData pattern = tunables.customPattern;
        if (pattern != null) {
            // Rounded UP for a pattern: a grid sized to a picture's exact height can end mid-block,
            // and PaintFromPattern already clips that partial top block to `height`. The random
            // generator below keeps the floored blockGridHeight.
            int patternBlockGridHeight = Mathf.Max(1, Mathf.Min(tunables.EffectiveBlockGridHeight, (height + blockSize - 1) / blockSize));
            PaintFromPattern(pattern, blockSize, blockGridWidth, patternBlockGridHeight, colorCount);
        } else {
            int[,] pieceIdGrid = BuildPieceIdGrid(blockGridWidth, blockGridHeight, out List<List<Vector2Int>> pieces);
            int[] pieceColors = ColorPieces(pieceIdGrid, pieces, blockGridWidth, blockGridHeight, colorCount);

            for (int by = 0; by < blockGridHeight; by++) {
                int rowStart = by * blockSize;
                int rowEnd = Mathf.Min(rowStart + blockSize, height);

                for (int bx = 0; bx < blockGridWidth; bx++) {
                    byte colorIndex = (byte)(pieceColors[pieceIdGrid[bx, by]] + 1);

                    int colStart = bx * blockSize;
                    int colEnd = Mathf.Min(colStart + blockSize, width);

                    for (int y = rowStart; y < rowEnd; y++) {
                        for (int x = colStart; x < colEnd; x++) {
                            Place(x, y, colorIndex);
                        }
                    }
                }
            }
        }

        InitialSandCount = TotalSandCount;
    }

    // Paints from a hand-authored pattern at its own PAINT resolution — each
    // block is subdivided into pattern.subdivisionsPerBlock x
    // subdivisionsPerBlock equal-sized sub-cells (e.g. 2 -> a 2x2 grid of 4
    // quadrants per block), each independently colorable, rather than one
    // solid color per whole block. subdivisionsPerBlock == 1 (the default)
    // makes this identical to the old one-color-per-block behavior — PaintWidth/
    // PaintHeight then equal blockGridWidth/blockGridHeight exactly. A
    // paint cell left at slot 0 ("empty") is skipped entirely (no sand
    // placed there), unlike the randomized generator's leftover-sliver
    // fallback, which always covers every block with SOME color.
    //
    // Block-level concepts elsewhere (GetBlockColumnRange, cube extraction
    // reach/capacity) are unaffected by subdivisionsPerBlock — a cube's
    // reach and capacity still span a whole block's blockCellSize x
    // blockCellSize cells regardless of how many differently-colored
    // sub-cells happen to be painted inside it; ExtractAtMouth simply takes
    // whatever matching grains stand in the mouth.
    void PaintFromPattern(SandCylinderPatternData pattern, int blockSize, int blockGridWidth, int blockGridHeight, int colorCount) {
        int subdivisions = Mathf.Max(1, pattern.subdivisionsPerBlock);
        int subCellSize = Mathf.Max(1, blockSize / subdivisions);
        int paintWidth = blockGridWidth * subdivisions;
        int paintHeight = blockGridHeight * subdivisions;

        for (int py = 0; py < paintHeight; py++) {
            int rowStart = py * subCellSize;
            int rowEnd = Mathf.Min(rowStart + subCellSize, height);
            if (rowStart >= rowEnd) continue;

            for (int px = 0; px < paintWidth; px++) {
                byte colorIndex = pattern.GetCell(px, py);
                if (colorIndex == EMPTY || colorIndex > colorCount) continue;

                int colStart = px * subCellSize;
                int colEnd = Mathf.Min(colStart + subCellSize, width);
                if (colStart >= colEnd) continue;

                for (int y = rowStart; y < rowEnd; y++) {
                    for (int x = colStart; x < colEnd; x++) {
                        Place(x, y, colorIndex);
                    }
                }
            }
        }
    }

    // Greedily tiles a blockGridWidth x blockGridHeight grid with randomly-
    // ordered PieceShapes: scans block cells in row-major order, and at the
    // first uncovered one tries a shuffled list of shapes anchored there
    // (shape offset (0,0) == that cell), taking the first that fits fully
    // in-bounds without overlapping an already-placed piece. Falls back to a
    // single-cell piece when nothing fits — this is what guarantees full
    // coverage even though 4- and 6-cell pieces alone can't exactly tile
    // every possible grid size (parity/remainder gaps land as small filler
    // pieces instead of blocking completion).
    public static int[,] BuildPieceIdGrid(int blockGridWidth, int blockGridHeight, out List<List<Vector2Int>> pieces) {
        int[,] pieceIdGrid = new int[blockGridWidth, blockGridHeight];
        for (int by = 0; by < blockGridHeight; by++)
            for (int bx = 0; bx < blockGridWidth; bx++)
                pieceIdGrid[bx, by] = -1;

        pieces = new List<List<Vector2Int>>();
        var shapeOrder = new List<int>(PieceShapes.Length);
        for (int i = 0; i < PieceShapes.Length; i++) shapeOrder.Add(i);

        for (int by = 0; by < blockGridHeight; by++) {
            for (int bx = 0; bx < blockGridWidth; bx++) {
                if (pieceIdGrid[bx, by] != -1) continue;

                ShuffleInPlace(shapeOrder);
                List<Vector2Int> placedCells = null;

                for (int s = 0; s < shapeOrder.Count && placedCells == null; s++) {
                    Vector2Int[] shape = PieceShapes[shapeOrder[s]];
                    bool fits = true;
                    for (int i = 0; i < shape.Length; i++) {
                        int cx = bx + shape[i].x;
                        int cy = by + shape[i].y;
                        if (cx < 0 || cx >= blockGridWidth || cy < 0 || cy >= blockGridHeight || pieceIdGrid[cx, cy] != -1) {
                            fits = false;
                            break;
                        }
                    }
                    if (!fits) continue;

                    placedCells = new List<Vector2Int>(shape.Length);
                    for (int i = 0; i < shape.Length; i++) {
                        int cx = bx + shape[i].x;
                        int cy = by + shape[i].y;
                        pieceIdGrid[cx, cy] = pieces.Count;
                        placedCells.Add(new Vector2Int(cx, cy));
                    }
                }

                if (placedCells == null) {
                    // Nothing fit (a leftover sliver near the grid's edge) —
                    // a lone single block still needs a piece of its own.
                    placedCells = new List<Vector2Int> { new Vector2Int(bx, by) };
                    pieceIdGrid[bx, by] = pieces.Count;
                }

                pieces.Add(placedCells);
            }
        }

        // Defensive final sweep: every cell SHOULD already be covered by
        // the loop above, but this guarantees it even if some future
        // PieceShapes entry has a bug (e.g. missing the (0,0) offset a
        // shape must include — see PieceShapes' doc comment) instead of
        // leaving a -1 that later crashes ColorPieces/MarkNeighborColor
        // with an IndexOutOfRangeException. Cheap no-op in the normal case.
        for (int by = 0; by < blockGridHeight; by++) {
            for (int bx = 0; bx < blockGridWidth; bx++) {
                if (pieceIdGrid[bx, by] != -1) continue;
                pieceIdGrid[bx, by] = pieces.Count;
                pieces.Add(new List<Vector2Int> { new Vector2Int(bx, by) });
            }
        }

        return pieceIdGrid;
    }

    // Greedy graph coloring over the pieces' orthogonal-adjacency graph:
    // each piece picks the first palette color not already used by any
    // EARLIER-processed neighboring piece. Standard greedy coloring — since
    // every later piece also avoids whatever color an earlier one took, no
    // two adjacent pieces ever end up sharing a color, as long as no single
    // piece ends up boxed in by 5+ differently-colored neighbors (rare for
    // this grid size; if it happens, that one piece just reuses a color
    // rather than the fill failing outright).
    public static int[] ColorPieces(int[,] pieceIdGrid, List<List<Vector2Int>> pieces, int blockGridWidth, int blockGridHeight, int colorCount) {
        var pieceColors = new int[pieces.Count];
        for (int i = 0; i < pieceColors.Length; i++) pieceColors[i] = -1;

        var usedByNeighbor = new bool[colorCount];
        for (int p = 0; p < pieces.Count; p++) {
            for (int c = 0; c < colorCount; c++) usedByNeighbor[c] = false;

            foreach (Vector2Int cell in pieces[p]) {
                MarkNeighborColor(pieceIdGrid, pieceColors, cell.x - 1, cell.y, blockGridWidth, blockGridHeight, usedByNeighbor);
                MarkNeighborColor(pieceIdGrid, pieceColors, cell.x + 1, cell.y, blockGridWidth, blockGridHeight, usedByNeighbor);
                MarkNeighborColor(pieceIdGrid, pieceColors, cell.x, cell.y - 1, blockGridWidth, blockGridHeight, usedByNeighbor);
                MarkNeighborColor(pieceIdGrid, pieceColors, cell.x, cell.y + 1, blockGridWidth, blockGridHeight, usedByNeighbor);
            }

            int chosen = 0;
            for (int c = 0; c < colorCount; c++) {
                if (!usedByNeighbor[c]) { chosen = c; break; }
            }
            pieceColors[p] = chosen;
        }

        return pieceColors;
    }

    static void MarkNeighborColor(int[,] pieceIdGrid, int[] pieceColors, int nx, int ny, int blockGridWidth, int blockGridHeight, bool[] usedByNeighbor) {
        if (nx < 0 || nx >= blockGridWidth || ny < 0 || ny >= blockGridHeight) return;
        int neighborPieceId = pieceIdGrid[nx, ny];
        // Defensive: BuildPieceIdGrid's final sweep guarantees this is never
        // -1 in practice, but skip rather than let a stray -1 index
        // negatively into pieceColors and throw.
        if (neighborPieceId < 0) return;
        int neighborColor = pieceColors[neighborPieceId];
        if (neighborColor >= 0) usedByNeighbor[neighborColor] = true;
    }

    static void ShuffleInPlace(List<int> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Returns the [colStart, colEnd) column range of the single block that
    // gridColumn falls within — the exact same boundaries FillInitialLayers
    // used, computed the same way (blockCol * blockCellSize) so the two can
    // never drift apart. Note this is one BLOCK's width, not the width of
    // whatever larger multi-block piece that block is part of — a cube's
    // extraction reach is still exactly one block, same as before the
    // Tetris-shaped fill (see SandExtractionController.BlockColumnRangeForCube).
    // Lets a caller snap a cube's extraction window to an entire block at
    // once — every column of that block becomes simultaneously reachable
    // for as long as the cube's world X stays anywhere inside it — instead
    // of a single column at a time, which never gave a fast-moving cube
    // enough time in any one column to drain that column's full depth
    // before its window moved on.
    public (int colStart, int colEnd) GetBlockColumnRange(int gridColumn) {
        int blockSize = Mathf.Max(1, tunables.blockCellSize);
        int blockGridWidth = tunables.EffectiveBlockGridWidth;
        int blockCol = Mathf.Clamp(gridColumn / blockSize, 0, blockGridWidth - 1);
        int colStart = blockCol * blockSize;
        int colEnd = colStart + blockSize;
        return (colStart, colEnd);
    }

    // MOUTH EXTRACTION. The mouth is the ONLY place sand leaves the grid: the
    // caller's block columns [xStart, xEnd) x the bottom
    // tunables.extractionMouthRows rows. No reach above it, no wings, no
    // climbing past other colors, no refill rule — sand that is not standing
    // in the mouth is not extractable, and everything that happens above the
    // mouth afterwards is SimulatePass's business alone. Matching colorIndex is
    // a gameplay rule and lives here only; the simulation never looks at a
    // cell's color.
    //
    // Removes up to maxCells matching grains, bottom row first; within a row
    // the scan starts at a column drawn from the physics stream and wraps, so
    // a budget smaller than the mouth's matching sand does not always drain
    // the same side.
    //
    // SUPPORT RULE: a matching grain is extractable only while every cell under
    // it in its own column, down to the floor, is occupied. A grain with a hole
    // anywhere beneath it is on its way down and is left to SimulatePass — so
    // extraction never eats falling sand over an empty space (which is what
    // kept a visible pocket open under the descending sand), and one call takes
    // at most one grain per column, which a single pass refills. Occupancy
    // only: what the cells below hold does not matter, nor does anything above.
    // HasColorAtMouth applies the same rule.
    //
    // When removedCellsOut is non-null the grid (x, y) of every
    // removed cell is appended to it (not cleared first — caller's
    // responsibility), so a caller can place visuals at the exact cells removed.
    //
    // This overload is the whole-block mouth (the demo conveyor's): a contact
    // range of the entire grid width has both edges on the sand's walls, so
    // MouthRowLimit never trims a column and every block column keeps all
    // extractionMouthRows rows.
    public int ExtractAtMouth(int xStart, int xEnd, byte colorIndex, int maxCells, List<Vector2Int> removedCellsOut = null) {
        return ExtractAtMouth(xStart, xEnd, colorIndex, maxCells, 0, width, removedCellsOut);
    }

    // CONTACT-LIMITED MOUTH. [contactStart, contactEnd) are the grid columns the
    // collector physically covers (their centres lie inside its drawn
    // footprint). Only block columns inside that range are scanned, so a
    // collector that only just touches a block exposes only the columns it
    // actually covers, never the whole block. Within the range each column's
    // top eligible row comes from MouthRowLimit (45° from the contact edges).
    // Support rule, colour match, one grain per column per call and the random
    // start column are unchanged.
    public int ExtractAtMouth(int xStart, int xEnd, byte colorIndex, int maxCells, int contactStart, int contactEnd, List<Vector2Int> removedCellsOut = null) {
        if (maxCells <= 0 || colorIndex == EMPTY) return 0;
        contactStart = Mathf.Clamp(contactStart, 0, width);
        contactEnd = Mathf.Clamp(contactEnd, 0, width);
        xStart = Mathf.Max(Mathf.Clamp(xStart, 0, width - 1), contactStart);
        xEnd = Mathf.Min(Mathf.Clamp(xEnd, xStart + 1, width), contactEnd);
        if (xEnd <= xStart) return 0;
        int mouthRows = Mathf.Clamp(tunables.extractionMouthRows, 1, height);
        int span = xEnd - xStart;
        int startOffset = Mathf.Min((int)(NextPhysicsRandom() * span), span - 1);

        // Support rule: mouthColumnOpen[i] = column xStart + i already has an
        // EMPTY cell at or below the row being scanned (found, or just made).
        if (mouthColumnOpen == null || mouthColumnOpen.Length < span) mouthColumnOpen = new bool[width];
        System.Array.Clear(mouthColumnOpen, 0, span);

        int removed = 0;
        for (int y = 0; y < mouthRows && removed < maxCells; y++) {
            int row = y * width;
            for (int k = 0; k < span && removed < maxCells; k++) {
                int i = (startOffset + k) % span;
                if (mouthColumnOpen[i]) continue;
                int x = xStart + i;
                if (y > MouthRowLimit(x, contactStart, contactEnd, mouthRows)) continue;
                byte c = cells[row + x];
                if (c == EMPTY) {
                    mouthColumnOpen[i] = true;
                    continue;
                }
                if (c != colorIndex) continue;
                Remove(x, y);
                mouthColumnOpen[i] = true;
                removed++;
                removedCellsOut?.Add(new Vector2Int(x, y));
            }
        }
        return removed;
    }

    // Non-mutating twin of ExtractAtMouth: is any colorIndex grain standing in
    // the mouth right now. Same columns, same rows, nothing else.
    public bool HasColorAtMouth(int xStart, int xEnd, byte colorIndex) {
        return HasColorAtMouth(xStart, xEnd, colorIndex, 0, width);
    }

    // Twin of the contact-limited ExtractAtMouth: same columns, same per-column
    // row limit.
    public bool HasColorAtMouth(int xStart, int xEnd, byte colorIndex, int contactStart, int contactEnd) {
        if (colorIndex == EMPTY) return false;
        contactStart = Mathf.Clamp(contactStart, 0, width);
        contactEnd = Mathf.Clamp(contactEnd, 0, width);
        xStart = Mathf.Max(Mathf.Clamp(xStart, 0, width - 1), contactStart);
        xEnd = Mathf.Min(Mathf.Clamp(xEnd, xStart + 1, width), contactEnd);
        int mouthRows = Mathf.Clamp(tunables.extractionMouthRows, 1, height);

        // Support rule (see ExtractAtMouth): walk each column up from the floor
        // and stop at its first EMPTY cell — nothing above a hole is extractable.
        for (int x = xStart; x < xEnd; x++) {
            int topRow = MouthRowLimit(x, contactStart, contactEnd, mouthRows);
            for (int y = 0; y <= topRow; y++) {
                byte c = cells[y * width + x];
                if (c == EMPTY) break;
                if (c == colorIndex) return true;
            }
        }
        return false;
    }

    // Top eligible mouth row of column x (inside [contactStart, contactEnd)):
    // 45° from the contact edges. The edge column reaches row
    // mouthRows - 1 - reach, each column further in one row more, and from
    // `reach` columns in the full mouth. reach = mouthRows / 2, so with 15
    // mouth rows: edge column rows 0-7, then 0-8 ... 0-14 from 7 columns in.
    // An edge lying on the sand's outer wall is not an open edge (there is no
    // uncovered sand beyond it), so it trims nothing.
    int MouthRowLimit(int x, int contactStart, int contactEnd, int mouthRows) {
        int reach = mouthRows / 2;
        int edgeDistance = int.MaxValue;
        if (contactStart > 0) edgeDistance = x - contactStart;
        if (contactEnd < width) edgeDistance = Mathf.Min(edgeDistance, contactEnd - 1 - x);
        return mouthRows - 1 - reach + Mathf.Min(edgeDistance, reach);
    }

    void Update() {
        if (width != tunables.GridWidth || height != tunables.GridHeight) {
            // Resizing mid-run would discard existing sand state; the prototype
            // only expects radius/height/density to be set before Play.
            Resize(tunables.GridWidth, tunables.GridHeight);
            FillInitialLayers();
        }
    }

    // Hard cap on passes in one frame, whatever the accumulator says — a hitch
    // is dropped, never paid back.
    const int MaxPassesPerFrame = 16;

    // SCHEDULER. The whole simulation runs here, in LateUpdate, never in
    // Update: extraction removes grains during Update, and SandCylinderRenderer
    // draws at DefaultExecutionOrder(100), so running after every Update and
    // before the renderer means a hole opened this frame has had a pass before
    // it is ever drawn.
    //
    // Passes per second = tunables.sandSimulationSpeed. While the region is
    // awake a frame always gets at least one pass (the reason above) and never
    // more than MaxPassesPerFrame or tunables.maxCellsPerFrame's worth of
    // cell visits (passes x awake area). While nothing is awake this costs a
    // single comparison.
    void LateUpdate() {
        if (tunables == null || cells == null) return;
        if (!IsAwake) {
            passAccumulator = 0f;
            return;
        }

        passAccumulator += Time.deltaTime * Mathf.Max(1, tunables.sandSimulationSpeed);
        int passes = Mathf.FloorToInt(passAccumulator);
        passAccumulator -= passes; // debited in full before the clamps below: excess is dropped, not banked
        passes = Mathf.Clamp(passes, 1, MaxPassesPerFrame);

        if (tunables.maxCellsPerFrame > 0) {
            int areaWidth = Mathf.Min(width - 1, wakeMaxX) - Mathf.Max(0, wakeMinX) + 1;
            int areaHeight = height - Mathf.Max(0, wakeMinY);
            int budgetPasses = tunables.maxCellsPerFrame / Mathf.Max(1, areaWidth * areaHeight);
            if (budgetPasses < 1) budgetPasses = 1;
            if (passes > budgetPasses) passes = budgetPasses;
        }

        for (int i = 0; i < passes && IsAwake; i++) SimulatePass();
    }

    // One pass over the awake region, then the region is whatever this pass
    // itself woke (every Move wakes its surroundings, every deferred slide
    // wakes its hole) — so a pass that moves nothing and defers nothing puts
    // the simulation to sleep, and it stays asleep until a Place/Remove wakes it.
    //
    // HOLE-CENTRIC: the pass visits EMPTY cells and asks ChooseSource which
    // grain from the row above, if any, drops into each. This method knows no
    // movement rule — that is ChooseSource's job alone.
    //
    // Rows go bottom-up, so the hole a move leaves in row y+1 is itself visited
    // later in the same pass: a void opened at the mouth travels all the way to
    // the surface within one pass and is never drawn hanging inside the pile.
    // For that to hold the hole must be inside the scanned columns, so the
    // column range widens live as moves happen. Scan direction alternates per
    // row and per pass so no side is systematically served first.
    void SimulatePass() {
        int minX = Mathf.Max(0, wakeMinX);
        int maxX = Mathf.Min(width - 1, wakeMaxX);
        int minY = Mathf.Max(0, wakeMinY);
        ClearWake();
        passParity ^= 1;

        for (int y = minY; y < height - 1; y++) {
            // Nothing above to come down, or no hole in this row to fill.
            if (rowCount[y + 1] == 0 || rowCount[y] == width) continue;

            int row = y * width;
            int x0 = minX;
            int x1 = maxX;
            bool leftToRight = ((y + passParity) & 1) == 0;
            for (int i = x0; i <= x1; i++) {
                int x = leftToRight ? i : x1 - (i - x0);
                if (cells[row + x] != EMPTY) continue;

                int src = ChooseSource(x, y);
                if (src == NoSource) continue;
                if (src == Deferred) {
                    Wake(x - 1, x + 1, y);
                    continue;
                }

                Move(src, y + 1, x, y);
                if (src - 1 < minX) minX = Mathf.Max(0, src - 1);
                if (src + 1 > maxX) maxX = Mathf.Min(width - 1, src + 1);
            }
        }
    }

    const int NoSource = -1;
    const int Deferred = -2;

    // THE MOVEMENT POLICY — the only place a movement rule lives. For the
    // EMPTY cell (x,y), returns the column of the grain in row y+1 that moves
    // into it this pass, NoSource if none can, or Deferred if one could but
    // does not this pass (the hole stays awake and is asked again next pass).
    // Callers guarantee y + 1 < height.
    //
    // Reads occupancy only (cells[...] != EMPTY): never a color, never tint[].
    // Every random decision comes from physicsRng and is re-rolled on every
    // call; nothing is remembered per grain or per cell.
    //
    // Baseline policy v0:
    //  - Candidates are the grain directly above and the two grains diagonally
    //    above. A diagonal grain counts only if it is SUPPORTED (the cell under
    //    it is occupied); an unsupported one is falling on its own account.
    //  - Grain directly above: it drops in — except, with chance
    //    tunables.flowSpread, a supported diagonal grain takes its place, which
    //    is what lets a rising void wander sideways instead of climbing
    //    straight up its own column. Beside open space that chance is raised
    //    by tunables.edgeCollapse (see EDGE COLLAPSE below).
    //  - Only diagonals: one slides in with chance tunables.slideChance this
    //    pass, otherwise Deferred.
    //  - Both diagonals eligible: a fair coin.
    int ChooseSource(int x, int y) {
        int row = y * width;
        int above = row + width;
        bool up = cells[above + x] != EMPTY;
        bool left = x > 0 && cells[above + x - 1] != EMPTY && cells[row + x - 1] != EMPTY;
        bool right = x < width - 1 && cells[above + x + 1] != EMPTY && cells[row + x + 1] != EMPTY;

        if (up) {
            if (left && right) {
                if (NextPhysicsRandom() < tunables.flowSpread) return PickDiagonal(x, true, true);
            } else if (left || right) {
                // EDGE COLLAPSE (off at tunables.edgeCollapse == 0, where this is
                // exactly v0). Only one diagonal is eligible; if the other diagonal
                // cell is open space (in the grid and EMPTY), the hole sits just
                // under a face or crater wall. Taking the grain from above sends it
                // straight up the face column, so a face only ever sheds sideways and
                // translates; taking the sand-side diagonal sends it into the sand
                // body, so the face slumps from its upper part toward the opening.
                // edgeCollapse raises that chance from flowSpread to certainty. The
                // grid's outer wall is not open space (nothing to collapse into).
                int other = left ? x + 1 : x - 1;
                float chance = tunables.flowSpread;
                if (other >= 0 && other < width && cells[above + other] == EMPTY)
                    chance += tunables.edgeCollapse * (1f - chance);
                if (NextPhysicsRandom() < chance) return left ? x - 1 : x + 1;
            }
            return x;
        }

        if (!left && !right) return NoSource;

        // L3 — REST FRICTION (EXPERIMENTAL, off at tunables.restFriction == 0,
        // where this block draws nothing and the policy is exactly v0).
        // Why v0's rest surface is a ruler-straight one-cell-per-column
        // staircase: a slide that loses its slideChance roll is Deferred, so it
        // is asked again until it happens — sand only comes to rest once NO
        // grain anywhere has a free diagonal, and a drained pit pulls every face
        // to exactly that limit. Friction gives the last, smallest kind of slide
        // a way to genuinely not happen: a MARGINAL slide — the hole already
        // rests on sand (or the floor), and the grain is the top of its own
        // column, i.e. a step exactly two cells high, the grain would drop one
        // row and stop — is refused this pass with chance restFriction, and a
        // refused marginal slide is NoSource, not Deferred: it does not keep the
        // region awake. Anything taller than that (a grain with sand on top of
        // it, or a hole with nothing under it) is untouched v0. The roll is
        // fresh on every call and nothing is stored: a resting step is asked
        // again whenever the region is awake for any other reason, so nearby
        // motion can still set it off. No slope is targeted.
        float restFriction = tunables.restFriction;
        if (restFriction > 0f && (y == 0 || cells[row - width + x] != EMPTY)) {
            if (left && IsColumnTop(x - 1, y + 1) && NextPhysicsRandom() < restFriction) left = false;
            if (right && IsColumnTop(x + 1, y + 1) && NextPhysicsRandom() < restFriction) right = false;
            if (!left && !right) return NoSource;
        }

        // slideChance 0 means "never slides", not "asks forever".
        if (tunables.slideChance <= 0f) return NoSource;
        if (NextPhysicsRandom() >= tunables.slideChance) return Deferred;
        return PickDiagonal(x, left, right);
    }

    // True when nothing rests on the grain at (x,y) — occupancy only.
    bool IsColumnTop(int x, int y) {
        return y + 1 >= height || cells[(y + 1) * width + x] == EMPTY;
    }

    int PickDiagonal(int x, bool left, bool right) {
        if (left && right) return NextPhysicsRandom() < 0.5f ? x - 1 : x + 1;
        return left ? x - 1 : x + 1;
    }
}
