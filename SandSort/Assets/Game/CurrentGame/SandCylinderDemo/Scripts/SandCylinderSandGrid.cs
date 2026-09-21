using System.Collections.Generic;
using UnityEngine;

// Isolated falling-sand cellular automaton for SandCylinderDemo.
// The core gravity/settle/lateral-spread step is reused logic ported from
// SandFeelDemo/Scripts/SandGrid.cs (that file is untouched) with two additions
// on top: a Tetris-piece-shaped block-grid initial fill, and color-targeted
// extraction from a shaft under the cylinder, both needed for the
// color-cube mechanic.
public class SandCylinderSandGrid : MonoBehaviour {

    public const byte EMPTY = 0;

    SandCylinderTunables tunables;
    public SandCylinderTunables Tunables => tunables;

    byte[] cells;
    byte[] settledStreak;
    float[] fallProgress;
    int[] colorCounts; // index 0 unused (EMPTY), 1..N per palette color
    int width, height;
    float stepAccumulator;
    int frameParity;

    // Incremented once per logical Step() tick — see lastPrimedTickPerColumn
    // for why the priming loop needs this instead of just calling
    // PrimeColumnFalling unconditionally.
    int currentTickId;

    // Reused across ExtractColor calls — the tick ID (currentTickId) a
    // column was last given an instant PrimeColumnFalling pass in. A cube
    // actively extracting calls ExtractColor roughly once per Update() frame
    // (its own extraction-rate accumulator crosses 1 grain almost every
    // frame at typical rates), which is much more often than the grid's own
    // Step() tick (throttled to tunables.sandSimulationSpeed) — so without
    // this guard, the column under an active cube got a FULL extra gravity
    // pass on top of its normal Step() pass every single frame, making sand
    // fall visibly faster right at the extraction point than sand falling
    // anywhere else in the grid (most noticeable in SandMixDemo, where
    // freshly-poured sand at normal speed sits right next to a rapidly
    // draining column). Capping priming to once per tick per column means an
    // actively-extracting column still reacts the same frame a cell is
    // removed (no more waiting a full tick for the visible gap to close) but
    // never gets MORE gravity evaluations per tick than the rest of the grid.
    int[] lastPrimedTickPerColumn;

    // Active-region sub-step state (see RunActiveSubSteps). Plain runtime
    // buffers, rebuilt whenever they are missing or the wrong size, so a grid
    // resize or a mid-Play recompile just starts a fresh history.
    byte[] subStepPreviousCells;
    const int ActiveHistoryLength = 256;
    int[] activeMinX, activeMaxX, activeMinY, activeMaxY;
    float[] activeTime;
    int activeHistoryIndex;
    int subStepParity;
    float subStepAccumulator;

    // Monotonic "the grid changed" counter. SetCell below is the ONLY
    // `cells[...] =` in this file, so every mutation path — SpawnCell,
    // FillInitialLayers, PaintFromPattern, ExtractColor, ResolveCaveInBias and
    // StepCell — bumps this for free; Resize bumps it separately because it
    // replaces the array outright rather than writing through SetCell. A reader
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
        cellsVersion++; // a fresh (all-EMPTY) array is a content change SetCell never sees
        settledStreak = new byte[width * height];
        fallProgress = new float[width * height];
        colorCounts = new int[tunables.sandColors.Length + 1];
        TotalSandCount = 0;

        // -1 (not 0, which would collide with currentTickId's own starting
        // value) so a column that has never been primed doesn't look like it
        // was already primed this tick before Step() has run even once.
        lastPrimedTickPerColumn = new int[width];
        for (int x = 0; x < width; x++) lastPrimedTickPerColumn[x] = -1;

        subStepPreviousCells = null;
        activeMinX = null;
    }

    public byte GetCell(int x, int y) => cells[y * width + x];

    void SetCell(int x, int y, byte v) {
        cells[y * width + x] = v;
        cellsVersion++;
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public int GetColorCount(byte colorIndex) {
        if (colorIndex <= 0 || colorIndex >= colorCounts.Length) return 0;
        return colorCounts[colorIndex];
    }

    // Adds one grain of colorIndex at (x,y) if that cell is currently empty —
    // SandMixDemo's pour interaction (SandMixSpout) uses this to add sand
    // from the top at runtime, instead of the one-shot FillInitialLayers path
    // SandCylinderDemo uses. Keeps colorCounts/TotalSandCount bookkeeping in
    // sync exactly like FillInitialLayers/ExtractColor do, so GetColorCount,
    // TotalSandCount and the cube-collection percent math all stay correct
    // regardless of how a grain entered the grid.
    public void SpawnCell(int x, int y, byte colorIndex) {
        if (!InBounds(x, y)) return;
        if (colorIndex == EMPTY || colorIndex >= colorCounts.Length) return;
        if (GetCell(x, y) != EMPTY) return;
        SetCell(x, y, colorIndex);
        colorCounts[colorIndex]++;
        TotalSandCount++;
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
            PaintFromPattern(pattern, blockSize, blockGridWidth, blockGridHeight, colorCount);
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
                            SetCell(x, y, colorIndex);
                            colorCounts[colorIndex]++;
                            TotalSandCount++;
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
    // sub-cells happen to be painted inside it; ExtractColor already
    // handles a block containing more than one color the same way it
    // handles any other mixed column (see its own doc comment).
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
                        SetCell(x, y, colorIndex);
                        colorCounts[colorIndex]++;
                        TotalSandCount++;
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

    // Sorts extraction candidates by ascending row (smallest Y / lowest in
    // the cylinder first) so ExtractColor always drains the bottom of the
    // reachable window before moving up, regardless of which column happens
    // to come first left-to-right. Cached rather than a fresh lambda per
    // call to avoid a per-frame delegate allocation.
    static readonly System.Comparison<Vector2Int> CompareByAscendingY = (a, b) => a.y.CompareTo(b.y);

    // Reused across ExtractColor calls to avoid a per-frame List allocation.
    readonly List<Vector2Int> extractionCandidatesBuffer = new List<Vector2Int>();

    // Reused across ExtractColor calls — tracks which columns actually had a
    // cell removed this call, so CompactColumn only needs to run once per
    // touched column afterward, never on the rest of the grid.
    readonly HashSet<int> touchedColumnsBuffer = new HashSet<int>();

    // Reused across ExtractColor calls — the actual set of columns primed via
    // PrimeColumnFalling: every touchedColumnsBuffer column PLUS, per
    // tunables.extractionNeighborPrimeRadius, the columns within that radius
    // to either side (see the priming loop at the end of ExtractColor for why
    // a separate set from touchedColumnsBuffer is needed here).
    readonly HashSet<int> primeColumnsBuffer = new HashSet<int>();

    // Reused across ExtractColor calls — for each column that had a cell
    // removed this call, the HIGHEST row index removed from it. Lets the
    // priming loop start a touched column's PrimeColumnFalling pass ABOVE the
    // rows ResolveCaveInBias already placed, instead of re-running StepCell
    // over them too — see the priming loop's own comment for why re-running
    // them would fight the bias resolution instead of respecting it.
    readonly Dictionary<int, int> maxRemovedRowPerColumn = new Dictionary<int, int>();

    // Pulls up to maxCells cells of colorIndex out of the shaft [xStart, xEnd)
    // by removing matching cells anywhere within [yStart, yEnd) — the
    // caller's rectangular extraction-range window (see
    // SandExtractionController's BlockColumnRangeForCube/extractionRangeY
    // handling). Within a column, scanning starts at the true bottom (y = 0,
    // not yStart) and climbs the WHOLE column, SKIPPING (not stopping at)
    // any empty or differently-colored cell in between — sand is no longer
    // opaque to extraction: once colors get interspersed after enough
    // falling/settling, a cube parked over a mixed column can still reach a
    // matching cell buried under other colors instead of being blocked
    // outright by whatever happens to sit at the very bottom. Removing a
    // cell that had other sand resting on top of it leaves a gap; the
    // gravity step then lets that sand fall to fill it in on subsequent
    // ticks, same as it always has for any other removal.
    //
    // maxPerColumn still caps how many cells a single tick can pull from any
    // one column (see maxCellsPerColumnPerTick's doc comment) — that cap is
    // about pacing/visual smoothness, independent of this opacity change.
    //
    // Across columns, candidates are drained in ascending-Y order (smallest
    // Y / lowest position first) rather than left-to-right column order —
    // extraction should always prefer the lowest reachable sand first.
    //
    // When removedCellsOut is non-null, the grid (x, y) of every cell actually
    // removed is appended to it (not cleared first — caller's responsibility),
    // so a caller can place visuals (e.g. extraction particles) at the exact
    // cells this call removed rather than guessing from the shaft range. Purely
    // additive bookkeeping — it does not change which cells are removed.
    //
    // When diagonalSpread is true the window also gets the 45-degree wings
    // described at DiagonalColumnFloor — same budget, same per-column cap, same
    // ascending-Y order; only the set of reachable columns/rows grows.
    public int ExtractColor(int xStart, int xEnd, int yStart, int yEnd, byte colorIndex, int maxCells, List<Vector2Int> removedCellsOut = null, bool diagonalSpread = false) {
        if (maxCells <= 0 || colorIndex == EMPTY) return 0;
        xStart = Mathf.Clamp(xStart, 0, width - 1);
        xEnd = Mathf.Clamp(xEnd, xStart + 1, width);
        yStart = Mathf.Clamp(yStart, 0, height - 1);
        yEnd = Mathf.Clamp(yEnd, yStart + 1, height);

        int maxPerColumn = Mathf.Max(1, tunables.maxCellsPerColumnPerTick);

        int spread = diagonalSpread ? yEnd - yStart : 0;
        int scanXStart = Mathf.Max(0, xStart - spread);
        int scanXEnd = Mathf.Min(width, xEnd + spread);

        extractionCandidatesBuffer.Clear();
        for (int x = scanXStart; x < scanXEnd; x++) {
            int columnYStart = DiagonalColumnFloor(x, xStart, xEnd, yStart);
            if (columnYStart >= yEnd) continue; // wing has not reached this column below the ceiling
            int columnCount = 0;
            for (int y = 0; y < yEnd; y++) {
                byte c = GetCell(x, y);
                if (c != colorIndex) continue; // empty or a different color — skip past it, keep climbing
                if (y >= columnYStart) {
                    extractionCandidatesBuffer.Add(new Vector2Int(x, y));
                    columnCount++;
                    // Caps how many cells a single tick can pull from THIS
                    // column, even if more matching cells sit higher up and
                    // the overall maxCells budget has room — see
                    // maxCellsPerColumnPerTick's doc comment for why.
                    if (columnCount >= maxPerColumn) break;
                }
                // else: matching color but below the window floor — keep
                // climbing, more may still be reachable within [yStart, yEnd).
            }
        }

        extractionCandidatesBuffer.Sort(CompareByAscendingY);

        int removed = 0;
        touchedColumnsBuffer.Clear();
        for (int i = 0; i < extractionCandidatesBuffer.Count && removed < maxCells; i++) {
            Vector2Int cell = extractionCandidatesBuffer[i];
            int idx = cell.y * width + cell.x;
            SetCell(cell.x, cell.y, EMPTY);
            settledStreak[idx] = 0;
            fallProgress[idx] = 0f;
            colorCounts[colorIndex]--;
            TotalSandCount--;
            removed++;
            removedCellsOut?.Add(cell);
            touchedColumnsBuffer.Add(cell.x);
        }

        // Resolve each freshly-emptied cell's immediate left/right cave-in
        // BEFORE the generic column priming below — see ResolveCaveInBias's
        // own doc comment for why this needs to run first (otherwise
        // whichever neighbor column PrimeColumnFalling happens to process
        // first always wins ties, instead of tunables.extractionCaveInLeftBias
        // deciding them). Ascending-Y order (extractionCandidatesBuffer is
        // already sorted that way) resolves the bottom of the extraction
        // window first, matching gravity's own bottom-up preference.
        //
        // Also records the highest row removed per column into
        // maxRemovedRowPerColumn — see the priming loop below for why.
        maxRemovedRowPerColumn.Clear();
        for (int i = 0; i < removed; i++) {
            Vector2Int cell = extractionCandidatesBuffer[i];
            ResolveCaveInBias(cell.x, cell.y);
            int existingMax;
            if (!maxRemovedRowPerColumn.TryGetValue(cell.x, out existingMax) || cell.y > existingMax) {
                maxRemovedRowPerColumn[cell.x] = cell.y;
            }
        }

        // Give every column this call actually removed from — AND, within
        // tunables.extractionNeighborPrimeRadius, its neighbor columns to
        // each side — one immediate gravity pass. See PrimeColumnFalling's
        // doc comment for why this is what closes the "visible empty gap for
        // a tick before sand starts falling into it" window, WITHOUT the sand
        // just teleporting straight to its resting position (an earlier
        // version of this fix repacked the column instantly, which closed
        // the gap but read as sand snapping/teleporting rather than actually
        // falling — this instead reuses the same per-cell StepCell gravity
        // the scheduled Step() loop already uses, just triggered a tick
        // early, so it's a real (if immediate) fall, not a teleport).
        //
        // The neighbor columns matter because a removed cell can also be
        // filled by a SETTLED neighbor's lateral-spread topple (StepCell's
        // leftFree/rightFree branch), not just by its own column's sand
        // falling straight down — without also priming the neighbor, that
        // topple only reacts on the grid's next scheduled Step() tick, which
        // reads as the collapse right at the extraction point lagging a beat
        // behind the extracted column's own (primed) fall.
        int primeRadius = Mathf.Max(0, tunables.extractionNeighborPrimeRadius);
        primeColumnsBuffer.Clear();
        foreach (int x in touchedColumnsBuffer) {
            int loX = Mathf.Max(0, x - primeRadius);
            int hiX = Mathf.Min(width - 1, x + primeRadius);
            for (int nx = loX; nx <= hiX; nx++) primeColumnsBuffer.Add(nx);
        }
        foreach (int x in primeColumnsBuffer) {
            // At most one PrimeColumnFalling pass per column per logical
            // Step() tick — see lastPrimedTickPerColumn's own doc comment for
            // why: ExtractColor can run many times within a single tick
            // (once per Update() frame a cube is actively extracting), and
            // without this guard each of those calls gave the column another
            // full gravity pass on top of its regular Step() pass, making
            // sand fall visibly faster right at the extraction point than
            // anywhere else in the grid.
            if (lastPrimedTickPerColumn[x] == currentTickId) continue;
            lastPrimedTickPerColumn[x] = currentTickId;

            // A column ResolveCaveInBias already placed cells into (i.e. one
            // that had a cell removed from it directly) starts priming ABOVE
            // those rows, not from the bottom — StepCell has no notion of "a
            // cell the bias resolution just deliberately placed", so priming
            // it again from row 1 could immediately re-topple that cell via
            // ordinary lateral-spread (into whichever hole ResolveCaveInBias
            // itself just opened up in a neighbor column), silently undoing
            // the bias-decided color right after deciding it. Neighbor-only
            // columns (reached purely via extractionNeighborPrimeRadius, not
            // touched directly) have no such rows to protect and still prime
            // fully from the bottom, same as before.
            int startY = 1;
            int maxRemovedRow;
            if (maxRemovedRowPerColumn.TryGetValue(x, out maxRemovedRow)) {
                startY = maxRemovedRow + 1;
            }

            // Scans upward from startY until it hits a real stretch of empty
            // rows (tunables.extractionPrimeMaxEmptyGap), rather than either
            // the whole column or a fixed row count — see
            // extractionPrimeMaxEmptyGap's own doc comment for why a fixed
            // count can't work for both demos at once: a dense, solidly
            // packed pile (SandCylinderDemo) needs priming to keep going
            // until it actually runs out of material, however far that is,
            // or the pile visibly hollows out faster than it refills; a
            // sparse trickle of still-falling sand far above the gap
            // (SandMixDemo's pour) needs priming to stop almost immediately,
            // or that distant sand gets an undue bonus gravity tick. Scanning
            // "until a real gap" gives each case exactly what it needs
            // without a magic number tuned per scene.
            PrimeColumnFalling(x, startY, tunables.extractionPrimeMaxEmptyGap);
        }

        return removed;
    }

    // Called once per freshly-extracted cell (x,y), BEFORE any StepCell-based
    // priming runs. Mirrors StepCell's own "settled" lateral-spread condition
    // (see its leftFree/rightFree branch) but evaluated from the EMPTY
    // destination cell's point of view instead of a candidate source cell's:
    // checks whether the settled cell one row up-left (x-1,y+1) and/or one
    // row up-right (x+1,y+1) is eligible to topple down into (x,y) — eligible
    // meaning it's occupied AND itself settled (its own directly-below cell,
    // (x-1,y) or (x+1,y), is occupied, so it isn't already free-falling).
    //
    // Without this, StepCell's normal per-SOURCE-cell scan means whichever
    // neighbor column happens to get processed first (column scan order in
    // PrimeColumnFalling, or the left/right frameParity alternation in the
    // regular Step() loop) always claims a contested empty cell — extracting
    // a middle column between two solid-colored columns would then always
    // cave in from a fixed, scan-order-determined side rather than mixing
    // both colors. When BOTH sides are eligible here, tunables.
    // extractionCaveInLeftBias decides the winner with a weighted coin flip
    // instead, and this method performs that single diagonal move directly
    // (the loser is left untouched — it may still get its own turn on a
    // later extraction or the grid's regular Step() ticks). When only one
    // side is eligible, that side moves in deterministically, same as
    // StepCell would have resolved it anyway.
    void ResolveCaveInBias(int x, int y) {
        if (y + 1 >= height) return;
        if (GetCell(x, y) != EMPTY) return; // already refilled by an earlier call this same pass

        bool leftEligible = x > 0 && GetCell(x - 1, y + 1) != EMPTY && GetCell(x - 1, y) != EMPTY;
        bool rightEligible = x < width - 1 && GetCell(x + 1, y + 1) != EMPTY && GetCell(x + 1, y) != EMPTY;
        if (!leftEligible && !rightEligible) return;

        bool takeLeft = leftEligible && (!rightEligible || Random.value < tunables.extractionCaveInLeftBias);
        int srcX = takeLeft ? x - 1 : x + 1;
        byte c = GetCell(srcX, y + 1);

        SetCell(x, y, c);
        SetCell(srcX, y + 1, EMPTY);
        int destIdx = y * width + x;
        int srcIdx = (y + 1) * width + srcX;
        settledStreak[destIdx] = 0;
        fallProgress[destIdx] = 0f;
        fallProgress[srcIdx] = 0f;
    }

    // Runs one bottom-to-top StepCell pass over column x — the exact same
    // per-cell gravity/lateral-spread/settle logic the scheduled Step()
    // loop uses for the whole grid, just scoped to one column and run
    // immediately instead of waiting for the next tick on
    // tunables.sandSimulationSpeed's own slower clock (decoupled from
    // extraction's every-frame cadence). Called right after ExtractColor
    // empties a cell in column x OR one of its neighbors within
    // tunables.extractionNeighborPrimeRadius (see ExtractColor's priming
    // loop), so whatever cell now has empty space below or diagonally below
    // it — its own column's sand falling straight down, or a neighboring
    // column's settled sand toppling sideways into the gap — gets an instant
    // first nudge to START moving this same frame — via fallProgress, exactly
    // like any other fall, so it still takes multiple ticks to actually
    // descend and settle, it just doesn't sit static for a tick first. Any
    // further descent after that first nudge is picked up by the grid's own
    // regular Step() ticks, same as for sand that fell for any other reason.
    //
    // startY lets a caller skip the lowest rows of the column (see
    // ExtractColor's priming loop, which passes maxRemovedRow+1 for a column
    // ResolveCaveInBias already placed cells into).
    //
    // maxEmptyGap bounds the OTHER end ADAPTIVELY rather than by a fixed row
    // count — see tunables.extractionPrimeMaxEmptyGap's own doc comment for
    // why a fixed count can't serve both a densely-packed column (needs to
    // keep priming until it genuinely runs out of material, however far that
    // is, or the pile visibly hollows out faster than it refills) and a
    // mostly-empty one (needs to stop almost immediately, or distant
    // still-falling sand gets an undue bonus gravity tick). Tracks a run of
    // consecutive EMPTY cells while scanning upward; once that run exceeds
    // maxEmptyGap, whatever's further up (if anything) is disconnected from
    // the gap being closed, so priming stops rather than reaching for it.
    void PrimeColumnFalling(int x, int startY, int maxEmptyGap) {
        int consecutiveEmpty = 0;
        for (int y = Mathf.Max(1, startY); y < height; y++) {
            bool wasEmpty = GetCell(x, y) == EMPTY;
            StepCell(x, y);
            if (wasEmpty) {
                consecutiveEmpty++;
                if (consecutiveEmpty > maxEmptyGap) break;
            } else {
                consecutiveEmpty = 0;
            }
        }
    }

    // The lowest row column x may extract from — the ONE place the extraction
    // window's shape is defined, shared by ExtractColor and HasReachableColor so
    // the two can never disagree. Inside the vertical window [xStart, xEnd) it is
    // simply yStart. Outside it, d = how many columns x lies beyond the window's
    // nearest edge, and the 45-degree wing reaches that column from row
    // yStart + d - 1: one column out already at the bottom row, one more column
    // per row higher up —
    //
    //     A A A H H H A A A    yStart + 2
    //       A A H H H A A      yStart + 1
    //         A H H H A        yStart
    //
    // The ceiling is the caller's: a floor at or above yEnd means the wing does
    // not reach x at all. When the wings are off every scanned column is inside
    // the window, so this always returns yStart and nothing changes.
    static int DiagonalColumnFloor(int x, int xStart, int xEnd, int yStart) {
        if (x < xStart) return yStart + (xStart - x) - 1;
        if (x >= xEnd) return yStart + (x - (xEnd - 1)) - 1;
        return yStart;
    }

    // Non-mutating existence check, mirroring ExtractColor's own reachability
    // rule (any matching cell anywhere within [yStart, yEnd) counts, not
    // just the lowest occupied cell in a column — see ExtractColor's doc
    // comment) without removing anything. Lets a caller test "is there
    // matching sand near this specific position" before committing to
    // extract there — e.g. to decide per-cube eligibility based on the
    // cube's actual world position and extraction range rather than a
    // single fixed column range.
    public bool HasReachableColor(int xStart, int xEnd, int yStart, int yEnd, byte colorIndex, bool diagonalSpread = false) {
        if (colorIndex == EMPTY) return false;
        xStart = Mathf.Clamp(xStart, 0, width - 1);
        xEnd = Mathf.Clamp(xEnd, xStart + 1, width);
        yStart = Mathf.Clamp(yStart, 0, height - 1);
        yEnd = Mathf.Clamp(yEnd, yStart + 1, height);

        // Same window as ExtractColor, wings included — see DiagonalColumnFloor.
        int spread = diagonalSpread ? yEnd - yStart : 0;
        int scanXStart = Mathf.Max(0, xStart - spread);
        int scanXEnd = Mathf.Min(width, xEnd + spread);

        for (int x = scanXStart; x < scanXEnd; x++) {
            for (int y = DiagonalColumnFloor(x, xStart, xEnd, yStart); y < yEnd; y++) {
                if (GetCell(x, y) == colorIndex) return true;
            }
        }
        return false;
    }

    void Update() {
        if (width != tunables.GridWidth || height != tunables.GridHeight) {
            // Resizing mid-run would discard existing sand state; the prototype
            // only expects radius/height/density to be set before Play.
            Resize(tunables.GridWidth, tunables.GridHeight);
            FillInitialLayers();
        }

        stepAccumulator += Time.deltaTime;
        float stepInterval = 1f / Mathf.Max(1, tunables.sandSimulationSpeed);
        int safety = 8;
        while (stepAccumulator >= stepInterval && safety-- > 0) {
            Step();
            stepAccumulator -= stepInterval;
        }
    }

    // Sub-steps run in LateUpdate, not Update: extraction removes cells during
    // Update, and the holes it leaves hang the
    // grains above them for exactly the frame SandCylinderRenderer then draws —
    // short horizontal rows of floating sand. Running the same passes here,
    // after every Update and before the renderer's LateUpdate (it carries a
    // later DefaultExecutionOrder), closes those holes before they are ever
    // drawn. Measured on the 9-colour dig: frames showing a 2+ cell hanging row
    // 14% -> 2%, none left in the extraction band, same pass count and cost.
    void LateUpdate() {
        if (tunables == null || cells == null) return;
        if (tunables.activeSubStepsPerTick > 0) {
            // Spread evenly over frames rather than bunched onto tick frames:
            // extraction runs every frame, and a pocket it empties refills far
            // more slowly when its extra passes arrive in bursts (measured: 90%
            // of the flow at 7.8 s bunched vs 5.3 s spread, same pass count).
            subStepAccumulator += Time.deltaTime * tunables.activeSubStepsPerTick * Mathf.Max(1, tunables.sandSimulationSpeed);
            int passes = Mathf.Min(Mathf.FloorToInt(subStepAccumulator), tunables.activeSubStepsPerTick * safetyTicksPerFrame);
            subStepAccumulator -= passes;
            if (subStepAccumulator > tunables.activeSubStepsPerTick) subStepAccumulator = 0f; // after a hitch, don't try to catch up
            RunActiveSubSteps(passes);
        } else {
            subStepAccumulator = 0f;
        }
    }

    const int safetyTicksPerFrame = 8;

    // ACTIVE-REGION SUB-STEPS (2026-09-15). A steep face only moves through
    // its outermost grains — each StepCell call lets one grain slide one
    // diagonal cell, and the grains behind it wait until it is gone — so a
    // large collapse is limited by how many StepCell evaluations the face gets
    // per second, not by any rule. Raising sandSimulationSpeed buys those
    // evaluations for the whole grid; this buys them only where sand has moved
    // during the last activeSubStepWindowSeconds, plus a margin.
    //
    // The extra passes are the ordinary StepCell rule in Step()'s own
    // row-major, alternating-direction order. That order matters: running the
    // same extra gravity column by column (PrimeColumnFalling-style) was just
    // as fast but drew vertical colour streaks through mixing sand, because
    // each column fully resolved before its neighbour had moved at all.
    //
    // currentTickId is deliberately not advanced here, so extraction priming
    // stays capped at once per real tick.
    void RunActiveSubSteps(int passes) {
        int cellCount = width * height;
        if (subStepPreviousCells == null || subStepPreviousCells.Length != cellCount) {
            subStepPreviousCells = new byte[cellCount];
            System.Array.Copy(cells, subStepPreviousCells, cellCount);
            activeMinX = null;
            return;
        }
        if (activeMinX == null) {
            activeMinX = new int[ActiveHistoryLength];
            activeMaxX = new int[ActiveHistoryLength];
            activeMinY = new int[ActiveHistoryLength];
            activeMaxY = new int[ActiveHistoryLength];
            activeTime = new float[ActiveHistoryLength];
            for (int i = 0; i < ActiveHistoryLength; i++) activeMaxX[i] = -1;
            activeHistoryIndex = 0;
        }

        // Bounding box of everything that changed since the previous call
        // ended: Step() ticks plus any extraction in between.
        int minX = width, maxX = -1, minY = height, maxY = -1;
        for (int y = 0; y < height; y++) {
            int row = y * width;
            for (int x = 0; x < width; x++) {
                byte v = cells[row + x];
                if (v == subStepPreviousCells[row + x]) continue;
                subStepPreviousCells[row + x] = v;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        float now = Time.time;
        if (maxX >= 0) {
            activeMinX[activeHistoryIndex] = minX;
            activeMaxX[activeHistoryIndex] = maxX;
            activeMinY[activeHistoryIndex] = minY;
            activeMaxY[activeHistoryIndex] = maxY;
            activeTime[activeHistoryIndex] = now;
            activeHistoryIndex = (activeHistoryIndex + 1) % ActiveHistoryLength;
        }
        if (passes <= 0) return;

        float oldest = now - Mathf.Max(0.01f, tunables.activeSubStepWindowSeconds);
        minX = width; maxX = -1; minY = height; maxY = -1;
        for (int i = 0; i < ActiveHistoryLength; i++) {
            if (activeMaxX[i] < 0 || activeTime[i] < oldest) continue;
            if (activeMinX[i] < minX) minX = activeMinX[i];
            if (activeMaxX[i] > maxX) maxX = activeMaxX[i];
            if (activeMinY[i] < minY) minY = activeMinY[i];
            if (activeMaxY[i] > maxY) maxY = activeMaxY[i];
        }
        if (maxX < 0) return; // nothing has moved recently: no extra work

        int margin = Mathf.Max(0, tunables.activeSubStepMargin);
        minX = Mathf.Max(0, minX - margin);
        maxX = Mathf.Min(width - 1, maxX + margin);
        minY = Mathf.Max(1, minY - margin);
        maxY = Mathf.Min(height - 1, maxY + margin);

        // WORK CEILING (2026-09-16). The pass count handed in is proportional to
        // Time.deltaTime, so before this clamp every slow frame bought the next
        // one more passes — measured on device, a Canyon collapse escalated
        // 0.4 -> 36 -> 72 -> 107 -> 158 ms over four frames and topped out at
        // ~230 ms, because the only brake was the pass-count cap
        // (activeSubStepsPerTick x safetyTicksPerFrame), which on a full-grid
        // active region is ~12 frame budgets of work. Cost is
        // passes x activeArea StepCell evaluations, so the fix is to bound that
        // product rather than the pass count: this is the first point where the
        // area is known, which is why the clamp lives here and not in
        // LateUpdate.
        //
        // The rate above stays the target, so flow speed remains
        // framerate-independent whenever the budget is not the binding limit;
        // when it is, the excess is dropped rather than banked (the caller has
        // already debited the accumulator) so a hitch cannot be paid back later.
        // Integer arithmetic only — no wall-clock timing, so the pass count
        // stays a pure function of the accumulator and the region's size.
        //
        // The floor of 1 is deliberate: extraction opens its holes during
        // Update and the renderer draws at DefaultExecutionOrder(100), so the
        // region must get at least one pass here every frame or the P1
        // hanging-row fix regresses.
        if (tunables.maxCellsPerFrame > 0) {
            int activeArea = (maxX - minX + 1) * (maxY - minY + 1);
            int budgetPasses = tunables.maxCellsPerFrame / Mathf.Max(1, activeArea);
            if (budgetPasses < 1) budgetPasses = 1;
            if (passes > budgetPasses) passes = budgetPasses;
        }

        for (int pass = 0; pass < passes; pass++) {
            subStepParity ^= 1;
            for (int y = minY; y <= maxY; y++) {
                bool leftToRight = ((y + subStepParity) & 1) == 0;
                if (leftToRight) {
                    for (int x = minX; x <= maxX; x++) StepCell(x, y);
                } else {
                    for (int x = maxX; x >= minX; x--) StepCell(x, y);
                }
            }
        }

        // The sub-steps' own moves are not "new" movement for the next call.
        System.Array.Copy(cells, subStepPreviousCells, cellCount);
    }

    void Step() {
        currentTickId++;
        frameParity ^= 1;
        for (int y = 1; y < height; y++) {
            bool leftToRight = ((y + frameParity) & 1) == 0;
            if (leftToRight) {
                for (int x = 0; x < width; x++) StepCell(x, y);
            } else {
                for (int x = width - 1; x >= 0; x--) StepCell(x, y);
            }
        }
    }

    void StepCell(int x, int y) {
        byte c = GetCell(x, y);
        if (c == EMPTY) return;
        int idx = y * width + x;

        if (GetCell(x, y - 1) == EMPTY) {
            if (tunables.fallSidewaysMixChance > 0f && Random.value < tunables.fallSidewaysMixChance) {
                bool spillLeft = x > 0 && GetCell(x - 1, y - 1) == EMPTY;
                bool spillRight = x < width - 1 && GetCell(x + 1, y - 1) == EMPTY;
                if (spillLeft || spillRight) {
                    bool goLeft = spillLeft && (!spillRight || Random.value < 0.5f);
                    int nx = goLeft ? x - 1 : x + 1;
                    int spillIdx = (y - 1) * width + nx;
                    SetCell(nx, y - 1, c);
                    SetCell(x, y, EMPTY);
                    settledStreak[spillIdx] = 0;
                    fallProgress[idx] = 0f;
                    fallProgress[spillIdx] = 0f;
                    return;
                }
            }

            float progress = fallProgress[idx] + tunables.maxFallCellsPerTick;
            int wantCells = Mathf.FloorToInt(progress);

            int landY = y - 1;
            int moved = 1;
            for (int i = 2; i <= wantCells; i++) {
                int cy = y - i;
                if (cy < 0 || GetCell(x, cy) != EMPTY) break;
                landY = cy;
                moved = i;
            }

            int landIdx = landY * width + x;
            SetCell(x, landY, c);
            SetCell(x, y, EMPTY);
            settledStreak[landIdx] = 0;
            fallProgress[idx] = 0f;
            fallProgress[landIdx] = (moved >= wantCells) ? (progress - wantCells) : 0f;
            return;
        }

        fallProgress[idx] = 0f;

        bool leftFree = x > 0 && GetCell(x - 1, y - 1) == EMPTY;
        bool rightFree = x < width - 1 && GetCell(x + 1, y - 1) == EMPTY;

        if (leftFree || rightFree) {
            bool goLeft = leftFree && (!rightFree || Random.value < 0.5f);
            int nx = goLeft ? x - 1 : x + 1;

            bool steepGap = IsSteepGap(nx, y - 1);
            bool allowSlide = steepGap
                ? Random.value < tunables.lateralSpreadChance
                : Random.value >= tunables.pileStability && Random.value < tunables.lateralSpreadChance;

            if (allowSlide) {
                int destIdx = (y - 1) * width + nx;
                SetCell(nx, y - 1, c);
                SetCell(x, y, EMPTY);
                settledStreak[destIdx] = 0;
                fallProgress[destIdx] = 0f;
                return;
            }
        }

        if (settledStreak[idx] < 255) settledStreak[idx]++;

        if (settledStreak[idx] > 2 && settledStreak[idx] <= tunables.settleJitterWindowTicks && Random.value < tunables.settleJitterChance) {
            bool jitterLeft = x > 0 && GetCell(x - 1, y) == EMPTY;
            bool jitterRight = x < width - 1 && GetCell(x + 1, y) == EMPTY;
            if (jitterLeft || jitterRight) {
                bool goLeft = jitterLeft && (!jitterRight || Random.value < 0.5f);
                int nx = goLeft ? x - 1 : x + 1;
                int destIdx = y * width + nx;
                SetCell(nx, y, c);
                SetCell(x, y, EMPTY);
                fallProgress[destIdx] = 0f;
            }
        }
    }

    bool IsSteepGap(int x, int destY) {
        int emptyCount = 0;
        for (int i = 1; i <= tunables.minGapForFreeCascade; i++) {
            int cy = destY - i;
            if (cy >= 0 && GetCell(x, cy) == EMPTY) emptyCount++;
            else break;
        }
        return emptyCount >= tunables.minGapForFreeCascade;
    }
}
