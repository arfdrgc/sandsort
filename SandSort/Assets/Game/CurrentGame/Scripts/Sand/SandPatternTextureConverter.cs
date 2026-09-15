using System.Collections.Generic;
using UnityEngine;

// Turns a hand-painted PNG into the sand system's EXISTING starting-layout type,
// SandCylinderPatternData, so a level's opening picture can be authored in a real pixel-art tool
// instead of cell-by-cell in an Inspector.
//
// NOTHING IN SandCylinderDemo CHANGES (2026-09-15). This class only produces the same runtime
// SandCylinderPatternData instance Level.scalePatternUniformly has always produced, and hands it to
// the same SandCylinderTunables.customPattern field. The sand simulation, the extraction and every
// tuning value are untouched, and the picture still erodes the moment extraction starts — that
// falling-sand behaviour is the point of the mechanic, not something to preserve against.
//
// It lives on the gameplay side, NOT inside SandCylinderDemo/, and references no gameplay type
// either: SandCylinderDemo must stay liftable into another project on its own (see GameplayTunables'
// class header for that one-way rule).
//
// RESOLUTION. The pattern is built at SIMULATION resolution — subdivisionsPerBlock = blockCellSize
// makes its paint grid exactly the sand grid, so PaintFromPattern's own
// subCellSize = blockCellSize / subdivisionsPerBlock comes out at 1 cell and every PNG pixel lands
// on its own sand cell. This is the same trick, and the same reasoning, as
// Level.scalePatternUniformly; see that method's comment for the measurements behind it.
// (subdivisionsPerBlock carries a [Range(1, 4)] for its Inspector, which is an editor clamp only —
// these instances are never assets and never opened in that Inspector.)
public static class SandPatternTextureConverter {

    // How many unmatched colours a failure message names before it gives up listing them. Enough to
    // spot a pattern (one stray brush, an anti-aliased edge, a wrong palette) without producing a
    // console entry nobody will read.
    const int MAX_REPORTED_UNMATCHED = 8;

    // Decoded PNG at its OWN resolution: one slot byte per pixel, 0 = EMPTY, 1..N = palette slot.
    // Row 0 is the BOTTOM row, because that is what both sides already use — Texture2D.GetPixels32
    // returns bottom-up, and SandCylinderPatternData's paintY 0 is the bottom (the sand's floor).
    // So there is no flip anywhere in this file; the two conventions already agree. Verified in
    // Faz 0 with corner probe pixels.
    public static bool tryReadSlots(
        Texture2D source, SandPaletteSO palette,
        out byte[] slots, out int width, out int height, out string error) {

        slots = null;
        width = height = 0;

        if (source == null) { error = "No sand texture assigned."; return false; }
        if (palette == null) { error = "No SandPaletteSO assigned — cannot tell which colour is which sand slot."; return false; }
        if (palette.slotCount <= 0) { error = $"SandPaletteSO '{palette.name}' has no slots."; return false; }

        Dictionary<int, byte> lookup = palette.buildLookup(out string duplicateReport);
        if (duplicateReport != null) { error = $"SandPaletteSO '{palette.name}': {duplicateReport}"; return false; }

        Color32[] pixels;
        try {
            pixels = source.GetPixels32();
        } catch (UnityException e) {
            // Almost always Read/Write being off. Worth naming explicitly: the failure is otherwise
            // an opaque native exception, and the fix is one import checkbox.
            error = $"Could not read '{source.name}' — is Read/Write enabled on its import settings? ({e.Message})";
            return false;
        }

        width = source.width;
        height = source.height;
        slots = new byte[width * height];

        List<string> unmatched = new();
        int unmatchedTotal = 0;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int i = y * width + x;
                Color32 p = pixels[i];

                // ALPHA FIRST, ALWAYS — never fall through to the RGB lookup for a transparent
                // pixel. See SandPaletteSO.ALPHA_SAND_THRESHOLD for the measurement behind this.
                if (p.a < SandPaletteSO.ALPHA_SAND_THRESHOLD) {
                    slots[i] = SandCylinderSandGrid.EMPTY;
                    continue;
                }

                if (lookup.TryGetValue(SandPaletteSO.packRgb(p), out byte slot)) {
                    slots[i] = slot;
                    continue;
                }

                // No nearest-colour fallback, on purpose: silently rounding an off-palette pixel to
                // its closest neighbour is how a level ships in the wrong colours. An unmatched
                // pixel is a content bug and is reported as one.
                unmatchedTotal++;
                if (unmatched.Count < MAX_REPORTED_UNMATCHED) {
                    // Reported in PNG space (row 0 at the TOP), which is what the designer sees in
                    // their paint tool — not the bottom-up order this loop walks in.
                    unmatched.Add($"{SandPaletteSO.hexOf(p)} at ({x},{height - 1 - y})");
                }
            }
        }

        if (unmatchedTotal > 0) {
            string more = unmatchedTotal > unmatched.Count ? $" (+{unmatchedTotal - unmatched.Count} more)" : "";
            error = $"'{source.name}': {unmatchedTotal} pixel(s) match no slot in palette '{palette.name}': {string.Join(", ", unmatched)}{more}. Anti-aliasing and off-palette brushes are the usual causes.";
            slots = null;
            return false;
        }

        error = null;
        return true;
    }

    // Per-slot pixel counts at the PNG's own resolution, index 0 = EMPTY. Used by
    // SandLevelSO.validateLevel to answer the same "which colours does this level contain"
    // questions the hand-painted path already answers, without building a whole pattern.
    public static bool tryCountSlots(Texture2D source, SandPaletteSO palette, out int[] counts, out string error) {
        counts = null;
        if (!tryReadSlots(source, palette, out byte[] slots, out _, out _, out error)) return false;

        counts = new int[palette.slotCount + 1];
        for (int i = 0; i < slots.Length; i++) counts[slots[i]]++;
        return true;
    }

    // The runtime-only SandCylinderPatternData a level actually plays with. Caller owns it and must
    // Destroy it (Level does, via _runtimePattern/OnDestroy — the same field the hand-painted scaled
    // clone already used, so no new lifetime handling was needed).
    //
    // targetWidthBlocks is the board's column count: the board fixes the width, and the PNG's own
    // aspect ratio then fixes the height, so the sand area is a uniform scale of the picture rather
    // than a stretched one. Identical in spirit to Level.fitSandAreaToBoard's existing maths — only
    // the source is measured in pixels here instead of whole blocks, which is strictly more precise
    // (no block quantisation on the way in).
    public static bool tryBuildPattern(
        Texture2D source, SandPaletteSO palette,
        int targetWidthBlocks, int blockCellSize, int maxHeightBlocks,
        out SandCylinderPatternData pattern, out string error) {

        pattern = null;
        if (!tryReadSlots(source, palette, out byte[] slots, out int srcWidth, out int srcHeight, out error)) return false;

        blockCellSize = Mathf.Max(1, blockCellSize);
        targetWidthBlocks = Mathf.Max(1, targetWidthBlocks);
        maxHeightBlocks = Mathf.Max(1, maxHeightBlocks);

        int destWidth = targetWidthBlocks * blockCellSize;
        float cellsPerSourcePixel = destWidth / (float)srcWidth;

        int targetHeightBlocks = Mathf.Max(1, Mathf.RoundToInt(srcHeight * cellsPerSourcePixel / blockCellSize));
        if (targetHeightBlocks > maxHeightBlocks) {
            Debug.LogWarning($"[SandPatternTextureConverter] '{source.name}' ({srcWidth}x{srcHeight}) at {targetWidthBlocks} blocks wide would need {targetHeightBlocks} blocks of sand height, past the {maxHeightBlocks}-block cap cylinderHeight's own Inspector range allows — clamped. The sand area is no longer a uniform scale of the picture.");
            targetHeightBlocks = maxHeightBlocks;
        }

        pattern = ScriptableObject.CreateInstance<SandCylinderPatternData>();
        pattern.name = $"{source.name} (PNG {targetWidthBlocks}x{targetHeightBlocks})";
        pattern.width = targetWidthBlocks;
        pattern.height = targetHeightBlocks;
        pattern.subdivisionsPerBlock = blockCellSize;
        pattern.EnsureSized();

        int paintWidth = pattern.PaintWidth;
        int paintHeight = pattern.PaintHeight;

        // Nearest-neighbour, and it has to be: these bytes are colour SLOT INDICES, so averaging
        // slot 2 and slot 4 into slot 3 would invent a third colour rather than blend. Sampling is
        // centred — floor((d + 0.5) / cellsPerSourcePixel) — so the picture is not shifted half a
        // source pixel toward the low edge.
        for (int y = 0; y < paintHeight; y++) {
            int sourceY = Mathf.FloorToInt((y + 0.5f) / cellsPerSourcePixel);
            // Past the top of the picture: the rounding remainder from targetHeightBlocks. Left
            // EMPTY, and it has to be at the TOP — that is the sand's open surface. Never at the
            // bottom, where the extraction band reads.
            if (sourceY >= srcHeight) continue;
            sourceY = Mathf.Max(0, sourceY);

            for (int x = 0; x < paintWidth; x++) {
                int sourceX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / cellsPerSourcePixel), 0, srcWidth - 1);
                byte slot = slots[sourceY * srcWidth + sourceX];
                if (slot != SandCylinderSandGrid.EMPTY) pattern.SetCell(x, y, slot);
            }
        }

        return true;
    }
}
