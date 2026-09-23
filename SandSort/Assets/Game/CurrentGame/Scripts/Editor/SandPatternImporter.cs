using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

// Turns ANY picture (a photo, a drawing with anti-aliasing, a JPG) into a sand-pattern PNG the
// existing pipeline accepts: resized to a whole number of 34 px cells wide (3-10, default 5; aspect kept),
// then every pixel snapped to its nearest SandPaletteSO slot.
//
// This is the one place nearest-colour matching is allowed, and it is allowed only because it runs
// BEFORE the PNG exists, where a designer can look at the result. SandPatternTextureConverter keeps
// its exact-match-only rule untouched: the file written here contains nothing but palette colours,
// so it passes that rule by construction, and verify() proves it by running the real converter.
//
// The source is read as raw file bytes, never through its importer: it may live outside the
// project, and even inside it the file on disk and its import settings are never written to.
//
// Output is an INDEXED PNG (colour type 3) written by hand, because Texture2D.EncodeToPNG only writes
// truecolour — 3-4 bytes per pixel of entropy where a palette image needs at most one. Only the
// slots actually used go into PLTE, the bit depth drops to 1/2/4 when that few are used, and a tRNS
// chunk (the only alpha information in the file) is written only when the picture has EMPTY pixels.
//
// ALLOWED COLOURS: every mapping call takes an allowed[] mask (index = slot - 1, null = all). A
// disabled slot is removed from the candidate set entirely — including for source pixels that are
// an EXACT match of it — so it can never appear in the output, whatever the source looks like.
public static class SandPatternImporter {

    // How the source is brought down to the output width.
    //
    // PaletteMajority (default): every source pixel is snapped to its nearest ALLOWED slot first,
    //   then each output pixel takes the slot with the largest coverage-weighted share of the source
    //   pixels under it. No new colour is ever created: red next to blue stays red and blue, it can
    //   never average into a purple that then snaps to the PURPLE slot.
    // AreaAverage (legacy): average the real colours, then snap the averaged colour. Smoother on
    //   photos, but an edge between two regions averages into an in-between colour, and that
    //   in-between colour is snapped like any other — the source of "red + blue = purple" pixels.
    //
    // Both decide EMPTY vs sand the same way (coverage-weighted alpha against
    // ALPHA_SAND_THRESHOLD), so silhouettes are identical between the two modes.
    public enum ResizeMode { PaletteMajority, AreaAverage }

    public const string DEFAULT_SUFFIX = "_sand";

    // Project scale rule: one sand grid cell is always 34 px of pattern. The output is a whole number
    // of cells wide (the designer picks MIN..MAX, default 5 = 170 px); height follows the source's
    // aspect ratio, so nothing is stretched.
    public const int PIXELS_PER_CELL = 34;
    public const int DEFAULT_WIDTH_CELLS = 5;
    public const int MIN_WIDTH_CELLS = 3;
    public const int MAX_WIDTH_CELLS = 10;

    public static int outputWidthFor(int widthCells) => PIXELS_PER_CELL * widthCells;

    public static int outputHeightFor(int outputWidth, int sourceWidth, int sourceHeight) =>
        Mathf.Max(1, Mathf.RoundToInt(outputWidth * sourceHeight / (float)sourceWidth));

    // The importer caps sand patterns at this size; past it Unity downsamples on import, which
    // averages colours and breaks exact palette matching. See SandPatternTextureImportSettings.
    public const int IMPORTER_MAX_SIZE = 2048;

    public class Result {
        public string outputAssetPath;
        public long sourceBytes;
        public long outputBytes;
        public int sourceWidth, sourceHeight;
        public int width, height;        // output resolution
        public int widthCells;           // width / PIXELS_PER_CELL
        public int emptyPixels;
        public ResizeMode resizeMode;
        // Pixels that were already an exact ALLOWED palette colour before snapping, out of
        // matchSampled: the opaque resized pixels (AreaAverage) or the opaque source pixels
        // (PaletteMajority, which snaps before resizing).
        public int exactPixels;
        public int matchSampled;
        public int[] slotCounts;         // index 0 = EMPTY, 1..N = palette slot
        public bool[] allowed;           // index = slot - 1; the mask this result was mapped with
        public int bitDepth;
        public string verifyMessage;
        public bool verified;

        public int totalPixels => width * height;
        public int opaquePixels => totalPixels - emptyPixels;
        public float sourceMatchPercent => matchSampled == 0 ? 100f : 100f * exactPixels / matchSampled;
        public int colorsUsed {
            get { int n = 0; for (int i = 1; i < slotCounts.Length; i++) if (slotCounts[i] > 0) n++; return n; }
        }
    }

    public static string defaultOutputName(string sourcePath) => Path.GetFileNameWithoutExtension(sourcePath) + DEFAULT_SUFFIX + ".png";

    // A decoded source picture. Kept separate from mapping so the window's live preview can re-map
    // on every settings change without decoding the file again.
    public class Source {
        public string path;
        public long bytes;
        public System.DateTime lastWrite;
        public int width, height;
        public Color32[] pixels;         // bottom-up, GetPixels32 order
    }

    public static bool tryLoadSource(string sourcePath, out Source source, out string error) {
        source = null;
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) { error = $"Source image not found: '{sourcePath}'."; return false; }

        byte[] bytes = File.ReadAllBytes(sourcePath);
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        try {
            if (!texture.LoadImage(bytes)) { error = $"'{Path.GetFileName(sourcePath)}' is not a readable PNG/JPG."; return false; }
            source = new Source {
                path = sourcePath,
                bytes = bytes.Length,
                lastWrite = File.GetLastWriteTimeUtc(sourcePath),
                width = texture.width,
                height = texture.height,
                pixels = texture.GetPixels32(),
            };
        } finally {
            Object.DestroyImmediate(texture);
        }
        error = null;
        return true;
    }

    // Palette + mask checks shared by preview and import. Null allowed = every slot allowed.
    static bool validatePalette(SandPaletteSO palette, bool[] allowed, out string error) {
        if (palette == null || palette.slotCount == 0) { error = "No SandPaletteSO (or it has no slots)."; return false; }
        if (palette.slotCount > 255) { error = "Palette has more than 255 slots — cannot index it."; return false; }
        palette.buildLookup(out string duplicateReport);
        if (duplicateReport != null) { error = $"SandPaletteSO '{palette.name}': {duplicateReport}"; return false; }
        if (allowed != null) {
            if (allowed.Length != palette.slotCount) { error = $"Allowed-colour mask has {allowed.Length} entries but the palette has {palette.slotCount} slots."; return false; }
            if (System.Array.IndexOf(allowed, true) < 0) { error = "No colours are enabled — enable at least one in Allowed Colors."; return false; }
        }
        error = null;
        return true;
    }

    // Maps without writing anything: the exact slots tryImport would write for the same inputs.
    public static bool tryMap(Source source, SandPaletteSO palette, bool[] allowed, ResizeMode mode, int widthCells, out Result result, out byte[] slots, out string error) {
        result = null;
        slots = null;
        if (source == null) { error = "No source image."; return false; }
        if (!validateWidthCells(widthCells, out error)) return false;
        if (!validatePalette(palette, allowed, out error)) return false;

        result = new Result {
            sourceBytes = source.bytes,
            sourceWidth = source.width,
            sourceHeight = source.height,
            widthCells = widthCells,
            resizeMode = mode,
            slotCounts = new int[palette.slotCount + 1],
            allowed = allowed != null ? (bool[])allowed.Clone() : allEnabled(palette.slotCount),
        };
        slots = mapToSlots(source, palette, result);
        return checkAllowed(result, palette, out error);
    }

    static bool validateWidthCells(int widthCells, out string error) {
        if (widthCells < MIN_WIDTH_CELLS || widthCells > MAX_WIDTH_CELLS) {
            error = $"Target width {widthCells} cells is outside {MIN_WIDTH_CELLS}..{MAX_WIDTH_CELLS}."; return false;
        }
        error = null;
        return true;
    }

    public static bool[] allEnabled(int slotCount) {
        bool[] mask = new bool[slotCount];
        for (int i = 0; i < mask.Length; i++) mask[i] = true;
        return mask;
    }

    // Defensive: the mapper only ever picks allowed slots, and this proves it on every run.
    static bool checkAllowed(Result result, SandPaletteSO palette, out string error) {
        for (int s = 1; s < result.slotCounts.Length; s++) {
            if (result.slotCounts[s] > 0 && !result.allowed[s - 1]) {
                error = $"Internal error: {result.slotCounts[s]} pixel(s) mapped to disabled colour {s}. {palette.slots[s - 1].name}.";
                return false;
            }
        }
        error = null;
        return true;
    }

    // outputAssetPath must be under Assets/. Never overwrites sourcePath. Null allowed = all slots.
    public static bool tryImport(string sourcePath, SandPaletteSO palette, bool[] allowed, ResizeMode mode, int widthCells, string outputAssetPath, out Result result, out string error) {
        result = null;
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) { error = $"Source image not found: '{sourcePath}'."; return false; }
        if (!validatePalette(palette, allowed, out error)) return false;
        if (string.IsNullOrEmpty(outputAssetPath) || !outputAssetPath.StartsWith("Assets/", System.StringComparison.Ordinal)) {
            error = "Output must be inside the project's Assets folder."; return false;
        }
        if (Path.GetFullPath(sourcePath) == Path.GetFullPath(outputAssetPath)) {
            error = "Output path is the source image — refusing to overwrite the original."; return false;
        }

        byte[] slots;
        try {
            EditorUtility.DisplayProgressBar("Sand Pattern Importer", "Reading source…", 0f);
            if (!tryLoadSource(sourcePath, out Source source, out error)) return false;
            EditorUtility.DisplayProgressBar("Sand Pattern Importer", "Resizing and mapping to the palette…", 0.3f);
            if (!tryMap(source, palette, allowed, mode, widthCells, out result, out slots, out error)) return false;
            result.outputAssetPath = outputAssetPath;

            EditorUtility.DisplayProgressBar("Sand Pattern Importer", "Encoding indexed PNG…", 0.6f);
            byte[] png = encodeIndexedPng(slots, result.width, result.height, palette, result.slotCounts, out result.bitDepth);

            string directory = Path.GetDirectoryName(outputAssetPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(outputAssetPath, png);
            result.outputBytes = png.Length;

            EditorUtility.DisplayProgressBar("Sand Pattern Importer", "Importing and verifying…", 0.85f);
            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
            result.verified = verify(png, slots, result, palette, out result.verifyMessage);
        } finally {
            EditorUtility.ClearProgressBar();
        }

        error = null;
        return true;
    }

    // One slot byte per output pixel, bottom-up (GetPixels32 order — the same convention the converter uses).
    static byte[] mapToSlots(Source source, SandPaletteSO palette, Result result) {
        result.width = outputWidthFor(result.widthCells);
        result.height = outputHeightFor(result.width, source.width, source.height);

        Snapper snapper = new(palette, result.allowed);
        byte[] slots = result.resizeMode == ResizeMode.PaletteMajority
            ? majorityResample(source.pixels, source.width, source.height, result.width, result.height, snapper, result)
            : averageThenSnap(source.pixels, source.width, source.height, result.width, result.height, snapper, result);

        for (int i = 0; i < slots.Length; i++) {
            if (slots[i] == SandCylinderSandGrid.EMPTY) result.emptyPixels++;
            result.slotCounts[slots[i]]++;
        }
        return slots;
    }

    // Colour -> nearest ALLOWED slot. A disabled slot is not a candidate at all, not even for a
    // pixel that is exactly its colour, so it cannot come out of either resize mode.
    class Snapper {
        readonly Dictionary<int, byte> _exact = new();
        readonly Dictionary<int, byte> _nearestCache = new();   // photos repeat colours; Lab is the expensive part
        readonly Vector3[] _lab;
        readonly byte[] _labSlot;

        public Snapper(SandPaletteSO palette, bool[] allowed) {
            foreach (KeyValuePair<int, byte> pair in palette.buildLookup(out _))
                if (allowed[pair.Value - 1]) _exact[pair.Key] = pair.Value;

            List<Vector3> lab = new();
            List<byte> labSlot = new();
            for (int i = 0; i < palette.slotCount; i++) {
                if (!allowed[i]) continue;
                lab.Add(toLab(palette.slots[i].authorColor));
                labSlot.Add((byte)(i + 1));
            }
            _lab = lab.ToArray();
            _labSlot = labSlot.ToArray();
        }

        public byte snap(Color32 p, out bool exact) {
            int key = SandPaletteSO.packRgb(p);
            if (_exact.TryGetValue(key, out byte slot)) { exact = true; return slot; }
            exact = false;
            if (_nearestCache.TryGetValue(key, out slot)) return slot;

            Vector3 lab = toLab(p);
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _lab.Length; i++) {
                float d = (_lab[i] - lab).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            slot = _labSlot[best];
            _nearestCache[key] = slot;
            return slot;
        }
    }

    // Legacy mode: area-average the real colours, then snap each averaged pixel.
    static byte[] averageThenSnap(Color32[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight, Snapper snapper, Result result) {
        Color32[] pixels = resample(src, srcWidth, srcHeight, dstWidth, dstHeight);
        byte[] slots = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) {
            Color32 p = pixels[i];
            // Alpha first, with the converter's own threshold — see SandPaletteSO.ALPHA_SAND_THRESHOLD.
            if (p.a < SandPaletteSO.ALPHA_SAND_THRESHOLD) continue;   // EMPTY
            slots[i] = snapper.snap(p, out bool exact);
            result.matchSampled++;
            if (exact) result.exactPixels++;
        }
        return slots;
    }

    // ---- resize ------------------------------------------------------------------------------

    // Exact area-average (box) resampling: every output pixel is the coverage-weighted mean of the
    // source pixels under it, which is the correct low-pass for a large downscale (1446 -> 170) and
    // never overshoots the way bicubic/Lanczos ringing does — ringing would invent halo colours that
    // then snap to a wrong palette slot. On an upscale it degrades gracefully to near-nearest.
    //
    // Averaged in LINEAR light with PREMULTIPLIED alpha: sRGB averaging darkens thin bright lines,
    // and straight-alpha averaging bleeds the (meaningless) RGB of transparent pixels into edges.
    // Separable, two passes; the only intermediate is outWidth x srcHeight floats.
    static Color32[] resample(Color32[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight) {
        if (srcWidth == dstWidth && srcHeight == dstHeight) return src;

        float[] toLinear = new float[256];
        for (int i = 0; i < 256; i++) toLinear[i] = srgbToLinear(i / 255f);

        Tap[][] xTaps = buildTaps(srcWidth, dstWidth);
        Tap[][] yTaps = buildTaps(srcHeight, dstHeight);

        // Pass 1 — horizontal: srcHeight rows of dstWidth premultiplied linear RGBA.
        float[] mid = new float[dstWidth * srcHeight * 4];
        for (int y = 0; y < srcHeight; y++) {
            int srcRow = y * srcWidth;
            for (int x = 0; x < dstWidth; x++) {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                Tap[] taps = xTaps[x];
                for (int t = 0; t < taps.Length; t++) {
                    Color32 p = src[srcRow + taps[t].index];
                    float w = taps[t].weight * (p.a / 255f);
                    r += toLinear[p.r] * w; g += toLinear[p.g] * w; b += toLinear[p.b] * w;
                    a += w;
                }
                int m = (y * dstWidth + x) * 4;
                mid[m] = r; mid[m + 1] = g; mid[m + 2] = b; mid[m + 3] = a;
            }
        }

        // Pass 2 — vertical, then un-premultiply and back to sRGB bytes.
        Color32[] dst = new Color32[dstWidth * dstHeight];
        for (int y = 0; y < dstHeight; y++) {
            Tap[] taps = yTaps[y];
            for (int x = 0; x < dstWidth; x++) {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int t = 0; t < taps.Length; t++) {
                    int m = (taps[t].index * dstWidth + x) * 4;
                    float w = taps[t].weight;
                    r += mid[m] * w; g += mid[m + 1] * w; b += mid[m + 2] * w; a += mid[m + 3] * w;
                }
                Color32 c = new(0, 0, 0, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                if (a > 0f) {
                    c.r = linearToSrgbByte(r / a); c.g = linearToSrgbByte(g / a); c.b = linearToSrgbByte(b / a);
                }
                dst[y * dstWidth + x] = c;
            }
        }
        return dst;
    }

    // Palette-majority (mode) downscale. Uses the same coverage taps as resample(), so each output
    // pixel considers exactly the source area the area-average would have blended — but instead of
    // blending, the source pixels (already snapped to allowed slots) vote, weighted by coverage x
    // alpha. The winner is always a colour that is really there, so an edge between two regions
    // resolves to one of them, never to a third colour in between. Deterministic: exact ties go to
    // the slot of the source pixel under the output pixel's centre, else the lowest slot index.
    static byte[] majorityResample(Color32[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight, Snapper snapper, Result result) {
        // Snap every source pixel once. Fully transparent pixels never vote (their RGB is meaningless).
        byte[] srcSlots = new byte[src.Length];
        for (int i = 0; i < src.Length; i++) {
            Color32 p = src[i];
            if (p.a == 0) continue;
            srcSlots[i] = snapper.snap(p, out bool exact);
            if (p.a >= SandPaletteSO.ALPHA_SAND_THRESHOLD) {
                result.matchSampled++;
                if (exact) result.exactPixels++;
            }
        }

        Tap[][] xTaps = buildTaps(srcWidth, dstWidth);
        Tap[][] yTaps = buildTaps(srcHeight, dstHeight);
        float[] votes = new float[result.slotCounts.Length];
        List<byte> touched = new();
        byte[] slots = new byte[dstWidth * dstHeight];
        const float TIE_EPSILON = 1e-5f;

        for (int y = 0; y < dstHeight; y++) {
            Tap[] rows = yTaps[y];
            int centreY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * srcHeight / dstHeight), 0, srcHeight - 1);
            for (int x = 0; x < dstWidth; x++) {
                Tap[] cols = xTaps[x];
                float alpha = 0f;
                for (int ty = 0; ty < rows.Length; ty++) {
                    int srcRow = rows[ty].index * srcWidth;
                    for (int tx = 0; tx < cols.Length; tx++) {
                        int i = srcRow + cols[tx].index;
                        float w = rows[ty].weight * cols[tx].weight * (src[i].a / 255f);
                        if (w <= 0f) continue;
                        alpha += w;
                        byte s = srcSlots[i];
                        if (votes[s] == 0f) touched.Add(s);
                        votes[s] += w;
                    }
                }

                // Same EMPTY rule as the area-average path: coverage-weighted alpha, rounded to a byte.
                byte slot = SandCylinderSandGrid.EMPTY;
                if (Mathf.RoundToInt(alpha * 255f) >= SandPaletteSO.ALPHA_SAND_THRESHOLD) {
                    float best = -1f;
                    for (int s = 1; s < votes.Length; s++) {
                        if (votes[s] > best + TIE_EPSILON) { best = votes[s]; slot = (byte)s; }
                    }
                    int centreX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * srcWidth / dstWidth), 0, srcWidth - 1);
                    byte centre = srcSlots[centreY * srcWidth + centreX];
                    if (centre != SandCylinderSandGrid.EMPTY && centre != slot && votes[centre] >= best - TIE_EPSILON) slot = centre;
                }
                slots[y * dstWidth + x] = slot;

                for (int t = 0; t < touched.Count; t++) votes[touched[t]] = 0f;
                touched.Clear();
            }
        }
        return slots;
    }

    struct Tap { public int index; public float weight; }

    // For each output index, the source indices it covers and their normalised coverage.
    static Tap[][] buildTaps(int srcSize, int dstSize) {
        float scale = srcSize / (float)dstSize;
        Tap[][] taps = new Tap[dstSize][];
        List<Tap> list = new();
        for (int o = 0; o < dstSize; o++) {
            float start = o * scale, end = start + scale;
            int first = Mathf.Clamp(Mathf.FloorToInt(start), 0, srcSize - 1);
            int last = Mathf.Clamp(Mathf.CeilToInt(end) - 1, first, srcSize - 1);
            list.Clear();
            float sum = 0f;
            for (int i = first; i <= last; i++) {
                float w = Mathf.Min(end, i + 1) - Mathf.Max(start, i);
                if (w <= 0f) continue;
                list.Add(new Tap { index = i, weight = w });
                sum += w;
            }
            if (list.Count == 0) { list.Add(new Tap { index = first, weight = 1f }); sum = 1f; }
            Tap[] row = list.ToArray();
            for (int i = 0; i < row.Length; i++) row[i].weight /= sum;
            taps[o] = row;
        }
        return taps;
    }

    static byte linearToSrgbByte(float v) {
        v = Mathf.Clamp01(v);
        float s = v <= 0.0031308f ? v * 12.92f : 1.055f * Mathf.Pow(v, 1f / 2.4f) - 0.055f;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(s * 255f), 0, 255);
    }

    // CIELAB (D65). Plain RGB distance picks visibly wrong neighbours in darks and greens; Lab is
    // close enough to perceptual for a 17-colour snap and needs no tuning knob.
    static Vector3 toLab(Color32 c) {
        float r = srgbToLinear(c.r / 255f), g = srgbToLinear(c.g / 255f), b = srgbToLinear(c.b / 255f);
        float x = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f;
        float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        float z = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
        float fx = labF(x), fy = labF(y), fz = labF(z);
        return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
    }

    static float srgbToLinear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    static float labF(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;

    // ---- indexed PNG writer -------------------------------------------------------------------

    static byte[] encodeIndexedPng(byte[] slots, int width, int height, SandPaletteSO palette, int[] slotCounts, out int bitDepth) {
        // PNG palette: EMPTY (transparent) first when present, so tRNS is a single byte; then only
        // the slots actually used, in slot order.
        bool hasEmpty = slotCounts[SandCylinderSandGrid.EMPTY] > 0;
        byte[] slotToIndex = new byte[slotCounts.Length];
        List<Color32> entries = new();
        if (hasEmpty) { slotToIndex[0] = 0; entries.Add(new Color32(0, 0, 0, 0)); }
        for (int s = 1; s < slotCounts.Length; s++) {
            if (slotCounts[s] == 0) continue;
            slotToIndex[s] = (byte)entries.Count;
            entries.Add(palette.slots[s - 1].authorColor);
        }
        if (entries.Count == 0) entries.Add(new Color32(0, 0, 0, 0));   // unreachable for a non-empty image; keeps PLTE valid

        bitDepth = entries.Count <= 2 ? 1 : entries.Count <= 4 ? 2 : entries.Count <= 16 ? 4 : 8;

        using MemoryStream file = new();
        file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

        byte[] ihdr = new byte[13];
        writeBigEndian(ihdr, 0, (uint)width);
        writeBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = (byte)bitDepth;
        ihdr[9] = 3;   // colour type: indexed. compression / filter / interlace stay 0.
        writeChunk(file, "IHDR", ihdr);

        byte[] plte = new byte[entries.Count * 3];
        for (int i = 0; i < entries.Count; i++) { plte[i * 3] = entries[i].r; plte[i * 3 + 1] = entries[i].g; plte[i * 3 + 2] = entries[i].b; }
        writeChunk(file, "PLTE", plte);

        if (hasEmpty) writeChunk(file, "tRNS", new byte[] { 0 });   // entries past the first default to opaque

        writeChunk(file, "IDAT", compressRows(slots, width, height, slotToIndex, bitDepth));
        writeChunk(file, "IEND", new byte[0]);
        return file.ToArray();
    }

    // zlib stream of the filtered scanlines. Rows go straight into the deflater one at a time, so the
    // uncompressed image is never held in memory. Filter 0 (None) on every row — the PNG spec's own
    // recommendation for indexed images, where prediction across palette indices is meaningless.
    static byte[] compressRows(byte[] slots, int width, int height, byte[] slotToIndex, int bitDepth) {
        int rowBytes = (width * bitDepth + 7) / 8;
        int pixelsPerByte = 8 / bitDepth;
        byte[] row = new byte[rowBytes + 1];
        uint adlerA = 1, adlerB = 0;

        using MemoryStream compressed = new();
        compressed.WriteByte(0x78);
        compressed.WriteByte(0xDA);
        using (DeflateStream deflate = new(compressed, System.IO.Compression.CompressionLevel.Optimal, true)) {
            for (int pngY = 0; pngY < height; pngY++) {
                int srcRow = (height - 1 - pngY) * width;   // PNG is top-down, slots are bottom-up
                System.Array.Clear(row, 0, row.Length);
                for (int x = 0; x < width; x++) {
                    int index = slotToIndex[slots[srcRow + x]];
                    int shift = 8 - bitDepth * (x % pixelsPerByte + 1);
                    row[1 + x / pixelsPerByte] |= (byte)(index << shift);
                }
                for (int i = 0; i < row.Length; i++) {
                    adlerA = (adlerA + row[i]) % 65521;
                    adlerB = (adlerB + adlerA) % 65521;
                }
                deflate.Write(row, 0, row.Length);
            }
        }
        byte[] adler = new byte[4];
        writeBigEndian(adler, 0, (adlerB << 16) | adlerA);
        compressed.Write(adler, 0, 4);
        return compressed.ToArray();
    }

    static void writeChunk(Stream stream, string type, byte[] data) {
        byte[] header = new byte[8];
        writeBigEndian(header, 0, (uint)data.Length);
        for (int i = 0; i < 4; i++) header[4 + i] = (byte)type[i];
        stream.Write(header, 0, 8);
        stream.Write(data, 0, data.Length);

        uint crc = 0xFFFFFFFFu;
        for (int i = 4; i < 8; i++) crc = crcStep(crc, header[i]);
        for (int i = 0; i < data.Length; i++) crc = crcStep(crc, data[i]);
        byte[] crcBytes = new byte[4];
        writeBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFFu);
        stream.Write(crcBytes, 0, 4);
    }

    static uint[] s_crcTable;
    static uint crcStep(uint crc, byte b) {
        if (s_crcTable == null) {
            s_crcTable = new uint[256];
            for (uint n = 0; n < 256; n++) {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                s_crcTable[n] = c;
            }
        }
        return s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
    }

    static void writeBigEndian(byte[] buffer, int offset, uint value) {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    // ---- verification -------------------------------------------------------------------------

    // Two independent checks. (1) Decode the written bytes and compare slot-for-slot with what was
    // mapped — proves the encoder is lossless. (2) When the file is in SandPatterns/, run the
    // runtime's own SandPatternTextureConverter on the imported asset — proves the pipeline accepts it.
    static bool verify(byte[] png, byte[] expected, Result result, SandPaletteSO palette, out string message) {
        Texture2D decoded = new(2, 2, TextureFormat.RGBA32, false);
        try {
            if (!decoded.LoadImage(png)) { message = "Written PNG could not be decoded."; return false; }
            if (!slotsMatch(decoded, palette, expected, out message)) { message = $"Decode check failed: {message}"; return false; }
        } finally {
            Object.DestroyImmediate(decoded);
        }

        if (!SandPatternTextureImportSettings.isSandPatternPath(result.outputAssetPath)) {
            message = "Decode check OK (lossless). Output is outside SandPatterns/, so the Sand Pattern importer did not process it.";
            return true;
        }

        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(result.outputAssetPath);
        if (imported == null) { message = "Imported asset could not be loaded."; return false; }
        if (!slotsMatch(imported, palette, expected, out message)) { message = $"Sand Pattern importer check failed: {message}"; return false; }

        message = $"Accepted by SandPatternTextureConverter — 100% of {result.totalPixels:N0} pixels match palette '{palette.name}', identical to the mapped result (enabled colours only).";
        return true;
    }

    // ---- preview ------------------------------------------------------------------------------

    // The mapped slots as a point-filtered texture at the real output resolution, drawn with the
    // palette's authorColor — i.e. exactly the pixels the PNG will contain. Caller destroys it.
    public static Texture2D buildPreviewTexture(byte[] slots, int width, int height, SandPaletteSO palette) {
        Color32[] colors = new Color32[slots.Length];
        for (int i = 0; i < slots.Length; i++) {
            if (slots[i] == SandCylinderSandGrid.EMPTY) continue;   // transparent
            Color32 c = palette.slots[slots[i] - 1].authorColor;
            c.a = 255;
            colors[i] = c;
        }
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(colors);
        texture.Apply(false, false);
        return texture;
    }

    static bool slotsMatch(Texture2D texture, SandPaletteSO palette, byte[] expected, out string message) {
        if (!SandPatternTextureConverter.tryReadSlots(texture, palette, out byte[] actual, out _, out _, out message)) return false;
        if (actual.Length != expected.Length) { message = $"size changed ({actual.Length} vs {expected.Length} pixels)."; return false; }
        int differing = 0;
        for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[i]) differing++;
        if (differing > 0) { message = $"{differing} pixel(s) read back as a different slot."; return false; }
        message = null;
        return true;
    }
}
