using System.Collections.Generic;
using UnityEngine;

// Maps a hand-authored PNG's pixel colours onto the sand grid's colour SLOT indices, so a level
// designer can paint a level's starting picture in Aseprite/Piskel instead of cell-by-cell in an
// Inspector. See SandPatternTextureConverter, which is the only thing that reads it.
//
// DELIBERATELY NOT THE AUTHORITY FOR GAMEPLAY COLOUR (2026-09-15). What the sand actually RENDERS
// still comes from SandCylinderTunables.sandColors on the Level prefab, and the slot -> ItemColor
// mapping still comes from Level._sandPaletteColors. Nothing here is ever written back into either.
// This asset's only job is answering one question: "which slot is this PNG pixel?". Its authorColor
// values are therefore a MIRROR of the prefab's sandColors, seeded from them verbatim — if the two
// ever disagree, the PNG simply stops matching and the converter says so loudly, rather than
// quietly changing how the game looks.
//
// Slot numbering matches SandCylinderSandGrid exactly: 0 is EMPTY and is not in this list, so
// slots[i] is sand colour slot i + 1, the same 1-based index SandCylinderPatternData stores.
[CreateAssetMenu(fileName = "SandPalette", menuName = "_ScriptableObjects/SandPalette", order = 4)]
public class SandPaletteSO : ScriptableObject {

    // Alpha at or above this is sand; below it is EMPTY. Checked BEFORE any RGB lookup — measured in
    // Faz 0: Unity's alphaIsTransparency import option dilates neighbouring RGB into fully
    // transparent pixels, so a converter that matched RGB first would turn a deliberately empty
    // region into sand of whatever colour happened to border it. The importer now forces
    // alphaIsTransparency off (see SandPatternTextureImportSettings), and this ordering is the
    // second line of defence behind that.
    public const byte ALPHA_SAND_THRESHOLD = 128;

    [System.Serializable]
    public class Slot {
        [Tooltip("Label only — never read by the converter. Use it to keep the asset readable.")]
        public string name;
        [Tooltip("The exact RGB a PNG pixel must have to be read as this slot. Alpha is ignored here: transparency is decided separately, by ALPHA_SAND_THRESHOLD. Matching is EXACT — see SandPatternTextureConverter for why there is no nearest-colour fallback.")]
        public Color32 authorColor;
    }

    [SerializeField] List<Slot> _slots = new();
    public IReadOnlyList<Slot> slots => _slots;

    // 1-based, matching SandCylinderPatternData's own cell values. 0 is EMPTY and has no entry.
    public int slotCount => _slots.Count;

    // Packed RGB (alpha excluded) -> slot index, built fresh on demand. Not cached: the converter
    // runs once per level build, and a cache would have to be invalidated whenever the asset is
    // edited in the Inspector — a stale one would silently mis-map colours, which is exactly the
    // failure this whole design is built to make impossible.
    public Dictionary<int, byte> buildLookup(out string duplicateReport) {
        Dictionary<int, byte> lookup = new();
        List<string> duplicates = new();

        for (int i = 0; i < _slots.Count; i++) {
            int key = packRgb(_slots[i].authorColor);
            if (lookup.TryGetValue(key, out byte existing)) {
                duplicates.Add($"slot {i + 1} ({_slots[i].name}) has the same colour {hexOf(_slots[i].authorColor)} as slot {existing} — the later one can never be matched.");
                continue;
            }
            lookup[key] = (byte)(i + 1);
        }

        duplicateReport = duplicates.Count == 0 ? null : string.Join(" ", duplicates);
        return lookup;
    }

    public static int packRgb(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

    public static string hexOf(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";
}
