using System.Text;
using UnityEditor;
using UnityEngine;

// Writes the sand palette out as a GIMP .gpl file, which Aseprite, Piskel, Krita and GIMP all
// import directly. Reached from the gear menu on a SandPaletteSO's Inspector.
//
// Why this exists at all (measured in Faz 0): the sand a player sees is NOT the palette colour.
// SandCylinderRenderer multiplies every pixel by a per-pixel noise factor (colorNoiseAmount 0.14 —
// measured up to 18/255 of channel deviation, with only 3% of pixels landing exactly on the palette
// value), and the gameplay camera then applies Bloom, Vignette and ColorAdjustments on top. So a
// designer who eyedroppers a colour out of a screenshot gets a value that matches nothing, and
// every pixel they paint with it is rejected as off-palette. Handing them the palette as a file is
// the whole fix, and it is deliberately this small — not a window.
public static class SandPaletteGplExport {

    [MenuItem("CONTEXT/SandPaletteSO/Export Palette (.gpl for Aseprite/Piskel)")]
    static void export(MenuCommand command) {
        SandPaletteSO palette = (SandPaletteSO)command.context;

        string path = EditorUtility.SaveFilePanel(
            "Export sand palette", Application.dataPath, palette.name, "gpl");
        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new();
        sb.AppendLine("GIMP Palette");
        sb.AppendLine($"Name: {palette.name}");
        sb.AppendLine($"Columns: {Mathf.Max(1, palette.slotCount)}");
        sb.AppendLine("#");
        sb.AppendLine("# Sand colour slots. The slot NUMBER is what a pattern PNG encodes; the RGB");
        sb.AppendLine("# below is only how that slot is spelled in the PNG. Paint with these exact");
        sb.AppendLine("# values and nothing else — matching is exact, and anti-aliasing must be off.");
        sb.AppendLine("# Fully transparent (alpha 0) means EMPTY: no sand at all.");
        sb.AppendLine("#");

        for (int i = 0; i < palette.slotCount; i++) {
            Color32 c = palette.slots[i].authorColor;
            string label = string.IsNullOrEmpty(palette.slots[i].name) ? $"slot{i + 1}" : palette.slots[i].name;
            sb.AppendLine($"{c.r,3} {c.g,3} {c.b,3}\tslot {i + 1} {label} {SandPaletteSO.hexOf(c)}");
        }

        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log($"[SandPattern] Palette exported to {path}", palette);

        if (path.StartsWith(Application.dataPath, System.StringComparison.Ordinal)) AssetDatabase.Refresh();
    }
}
