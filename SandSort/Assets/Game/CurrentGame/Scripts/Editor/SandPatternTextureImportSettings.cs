using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Forces the import settings a sand-pattern PNG must have, on every texture dropped into
// SandPatterns/, and then checks it against the palette. A level designer saves a PNG into the
// folder and is done — there is no checkbox for them to get wrong, and because the settings land in
// the .meta they are identical for everyone who pulls the repo rather than per-machine.
//
// Every value below was measured in Faz 0 against a 170x170 probe, not assumed. Two of them are the
// ones that actually bite:
//  - alphaIsTransparency MUST be false. With it on, Unity dilates neighbouring RGB into fully
//    transparent pixels (measured: 1700 transparent pixels came back carrying three different
//    neighbour colours), so a deliberately empty region would read as sand.
//  - the Android/iOS platform overrides MUST be set to uncompressed RGBA32. The default ASTC
//    compression shifts colours, which would break exact palette matching on device while leaving
//    the Editor looking perfect — the same class of Editor-only-works trap as the Shader.Find note
//    in SandCylinderRenderer.
public class SandPatternTextureImportSettings : AssetPostprocessor {

    public const string PATTERN_FOLDER = "Assets/Game/CurrentGame/Data/SandPatterns/";

    // Only used by the editor-only consistency check below, which reads this prefab and never
    // writes to it. Nothing at runtime resolves the Level prefab by path.
    const string LEVEL_PREFAB_PATH = "Assets/Game/CurrentGame/Prefabs/Level.prefab";

    public static bool isSandPatternPath(string path) =>
        !string.IsNullOrEmpty(path) &&
        path.StartsWith(PATTERN_FOLDER, System.StringComparison.Ordinal) &&
        path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

    static readonly string[] PLATFORMS = { "Android", "iPhone", "Standalone" };

    void OnPreprocessTexture() {
        if (!isSandPatternPath(assetPath)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;                  // GetPixels32 throws without it
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;              // mip generation averages colours
        importer.sRGBTexture = true;                 // matches the sand's own runtime texture exactly
        importer.alphaIsTransparency = false;        // see class header — this one is not cosmetic
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.npotScale = TextureImporterNPOTScale.None;   // no silent power-of-two rescale
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;              // below the picture's size, Unity downsamples it
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        for (int i = 0; i < PLATFORMS.Length; i++) {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = PLATFORMS[i],
                overridden = true,
                maxTextureSize = 2048,
                format = TextureImporterFormat.RGBA32,
                textureCompression = TextureImporterCompression.Uncompressed,
                compressionQuality = 100,
                crunchedCompression = false,
            });
        }
    }

    // Validation runs here rather than in OnPostprocessTexture so the AssetDatabase is settled
    // before the palette asset is looked up — the import batch is already finished by this point.
    static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths) {

        SandPaletteSO palette = null;
        bool paletteResolved = false;

        for (int i = 0; i < importedAssets.Length; i++) {
            string path = importedAssets[i];
            if (!isSandPatternPath(path)) continue;

            if (!paletteResolved) {
                palette = findPalette();
                paletteResolved = true;
                if (palette != null) validatePaletteConsistency(palette);
            }
            // No palette in the project yet: stay quiet rather than erroring on every import. The
            // level itself still reports a missing palette when it tries to build (SandLevelSO
            // .validateLevel), which is the point where it actually matters.
            if (palette == null) return;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;

            if (SandPatternTextureConverter.tryCountSlots(texture, palette, out int[] counts, out string error)) {
                Debug.Log($"[SandPattern] {System.IO.Path.GetFileName(path)} OK — {texture.width}x{texture.height}, {describeCounts(counts, palette)}", texture);
            } else {
                Debug.LogError($"[SandPattern] {System.IO.Path.GetFileName(path)}: {error}", texture);
            }
        }
    }

    static string describeCounts(int[] counts, SandPaletteSO palette) {
        System.Text.StringBuilder sb = new();
        sb.Append($"empty {counts[0]}");
        for (int slot = 1; slot < counts.Length; slot++) {
            string label = slot - 1 < palette.slotCount ? palette.slots[slot - 1].name : "?";
            sb.Append($", {label} {counts[slot]}");
        }
        return sb.ToString();
    }

    // Checks that the three parallel slot tables still agree, and REPORTS ONLY — it never writes to
    // any of them. Which one is authoritative does not change: SandCylinderTunables.sandColors is
    // still what the sand renders, _sandPaletteColors is still the slot -> ItemColor mapping, and
    // SandPaletteSO is still their read-only mirror for PNG matching. Auto-repairing any of these
    // would silently change what the game looks like, which is exactly what this must not do.
    //
    // Why it is needed at all: the three are hand-maintained and indexed positionally, and a drift
    // between them fails in an unhelpful way. The worst case is a length mismatch —
    // Level.sandColorIndexOf takes Mathf.Min of the first two, so a palette that is longer in one
    // table than the other silently truncates the usable colour range instead of erroring.
    [MenuItem("Tools/SandSort/Check Sand Palette Consistency")]
    static void checkPaletteConsistencyMenu() {
        SandPaletteSO palette = findPalette();
        if (palette == null) {
            Debug.LogWarning("[SandPattern] No single SandPaletteSO found — nothing to check.");
            return;
        }
        if (validatePaletteConsistency(palette))
            Debug.Log($"[SandPattern] Palette consistency OK — {palette.slotCount} slot(s) agree across sandColors, _sandPaletteColors and {palette.name}.", palette);
    }

    static bool validatePaletteConsistency(SandPaletteSO palette) {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LEVEL_PREFAB_PATH);
        if (prefab == null) return true;   // no prefab to compare against: not this check's problem

        SandCylinderTunables tunables = prefab.GetComponentInChildren<SandCylinderTunables>(true);
        Level level = prefab.GetComponentInChildren<Level>(true);
        if (tunables == null || level == null) return true;

        // _sandPaletteColors is private; this is an Editor-only script, so read it the Editor way
        // rather than widening the field's access for a diagnostic.
        SerializedProperty mapping = new SerializedObject(level).FindProperty("_sandPaletteColors");
        if (mapping == null) return true;

        int sandCount = tunables.sandColors.Length;
        int mapCount = mapping.arraySize;
        int slotCount = palette.slotCount;
        List<string> issues = new();

        if (sandCount != mapCount || sandCount != slotCount) {
            issues.Add($"slot counts disagree — sandColors {sandCount}, _sandPaletteColors {mapCount}, {palette.name} {slotCount}. Level.sandColorIndexOf uses the SMALLER of the first two, so only the first {Mathf.Min(sandCount, mapCount)} colour(s) are reachable.");
        }

        // Per-slot RGB. A mismatch here means every PNG painted with that slot's colour stops
        // matching, which surfaces as an off-palette import error naming a colour that "looks right".
        int shared = Mathf.Min(sandCount, slotCount);
        for (int i = 0; i < shared; i++) {
            Color32 sand = tunables.sandColors[i];
            Color32 authored = palette.slots[i].authorColor;
            if (sand.r != authored.r || sand.g != authored.g || sand.b != authored.b) {
                issues.Add($"slot {i + 1}: sandColors is {SandPaletteSO.hexOf(sand)} but {palette.name} expects {SandPaletteSO.hexOf(authored)} — PNGs painted with either value will not match the other.");
            }
        }

        // A repeated ItemColor makes sandColorIndexOf resolve every container of that colour to the
        // FIRST slot, so the later one's sand can never be collected.
        HashSet<int> seenColors = new();
        for (int i = 0; i < mapCount; i++) {
            int value = mapping.GetArrayElementAtIndex(i).intValue;
            if (!seenColors.Add(value)) {
                issues.Add($"_sandPaletteColors lists {(ColorSO.ItemColor)value} more than once (slot {i + 1}) — containers of that colour all resolve to its first slot.");
            }
        }

        palette.buildLookup(out string duplicateReport);
        if (duplicateReport != null) issues.Add($"{palette.name}: {duplicateReport}");

        // Labels are cosmetic, so drift there is a warning rather than an error — but it is usually
        // the first visible sign that one of the three lists was reordered on its own.
        List<string> labelDrift = new();
        int labelShared = Mathf.Min(mapCount, slotCount);
        for (int i = 0; i < labelShared; i++) {
            string expected = ((ColorSO.ItemColor)mapping.GetArrayElementAtIndex(i).intValue).ToString();
            string actual = palette.slots[i].name;
            if (!string.IsNullOrEmpty(actual) && actual != expected) labelDrift.Add($"slot {i + 1} is labelled '{actual}' but maps to {expected}");
        }

        if (labelDrift.Count > 0)
            Debug.LogWarning($"[SandPattern] Sand palette labels drifted (cosmetic, but check the ordering): {string.Join("; ", labelDrift)}", palette);

        if (issues.Count == 0) return true;

        Debug.LogError($"[SandPattern] Sand palette tables disagree — PNG colour matching is unreliable until this is fixed. Nothing was changed automatically. {string.Join(" ", issues)}", palette);
        return false;
    }

    // The single SandPaletteSO in the project. More than one is ambiguous rather than wrong, so it
    // says so and picks none — a level that silently validated against the wrong palette would be
    // worse than one that refuses to guess.
    public static SandPaletteSO findPalette() {
        string[] guids = AssetDatabase.FindAssets("t:SandPaletteSO");
        if (guids.Length == 0) return null;
        if (guids.Length > 1) {
            Debug.LogWarning($"[SandPattern] {guids.Length} SandPaletteSO assets found — sand pattern validation needs exactly one. Assign the right one on the Level prefab; import-time validation is skipped.");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<SandPaletteSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
