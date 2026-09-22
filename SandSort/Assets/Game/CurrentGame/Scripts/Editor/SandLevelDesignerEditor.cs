using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Inspector for SandLevelDesigner: the component's own fields, then the Sand Texture Colour
// Debugger — every colour the analysed texture contains (swatch, ColorSO, placed count, pixels),
// with missing ColorSOs, off-palette pixels and placed-but-absent colours called out.
//
// Two data sources, same table: while designing (Play + F2) it shows the designer's live analysis —
// the one C / Shift+C and 1 are restricted to — and repaints continuously so placed counts follow
// the edits; otherwise it asks the designer to analyse the assigned texture against the Level
// prefab (SandLevelDesigner.analyseForInspector), re-running only when the texture or the tolerance
// changes or Re-analyse is pressed. Placed counts exist only while designing.
[CustomEditor(typeof(SandLevelDesigner))]
public class SandLevelDesignerEditor : Editor {

    const float ROW_HEIGHT = 22f;
    const float SWATCH_SIZE = 16f;
    const float COLOR_COLUMN = 120f;
    const float PLACED_COLUMN = 60f;
    const float PIXELS_COLUMN = 80f;

    static readonly Color MISSING_ROW_TINT = new Color(1f, 0.25f, 0.25f, 0.12f);
    static readonly Color APPROX_ROW_TINT = new Color(1f, 0.7f, 0.1f, 0.12f);
    static readonly Color SWATCH_BORDER = new Color(0f, 0f, 0f, 0.6f);

    Texture2D _lastTexture;
    int _lastTolerance = -1;
    bool _lastActive;
    bool _analysedOnce;

    GUIStyle _missingStyle;
    GUIStyle _okStyle;
    GUIStyle _columnHeaderStyle;

    public override bool RequiresConstantRepaint() => ((SandLevelDesigner)target).isActive;

    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        SandLevelDesigner designer = (SandLevelDesigner)target;
        ensureStyles();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Sand Texture Colour Debugger", EditorStyles.boldLabel);

        // Edit-mode analysis runs only when its inputs change; the live one is the designer's own.
        Texture2D texture = serializedObject.FindProperty("_sandTexture").objectReferenceValue as Texture2D;
        int tolerance = serializedObject.FindProperty("_colorTolerance").intValue;
        bool inputsChanged = !_analysedOnce || texture != _lastTexture || tolerance != _lastTolerance || designer.isActive != _lastActive;

        using (new EditorGUILayout.HorizontalScope()) {
            EditorGUILayout.LabelField(
                designer.isActive
                    ? "Live — the colours C / Shift+C and 1 can assign right now."
                    : "Preview against the Level prefab. Placed counts appear while designing (Play + F2).",
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Re-analyse", GUILayout.Width(90f))) inputsChanged = true;
        }

        if (!designer.isActive && inputsChanged) designer.analyseForInspector();
        _lastTexture = texture;
        _lastTolerance = tolerance;
        _lastActive = designer.isActive;
        _analysedOnce = true;

        using (new EditorGUI.DisabledScope(true)) {
            EditorGUILayout.ObjectField("Analysed Texture", designer.analysedTexture, typeof(Texture2D), false);
        }
        EditorGUILayout.LabelField(designer.textureReport, EditorStyles.wordWrappedMiniLabel);

        if (!designer.textureAnalysed) {
            EditorGUILayout.HelpBox(designer.textureReport, MessageType.Warning);
            return;
        }

        IReadOnlyList<SandLevelDesigner.TextureColor> colors = designer.textureColors;
        int matched = 0;
        List<string> approx = new();
        foreach (SandLevelDesigner.TextureColor info in colors) {
            if (info.hasColorSO) matched++;
            if (!info.exact) approx.Add($"{info.color} ({string.Join(" ", info.hexes)})");
        }

        HashSet<ColorSO.ItemColor> placedColors = new();
        if (designer.isActive) {
            foreach (ContainerData data in designer.entries) placedColors.Add(data.color);
        }

        drawSummary(colors.Count, matched, colors.Count - matched, designer.isActive ? placedColors.Count.ToString() : "–");
        drawTable(designer, colors);

        if (designer.isActive) {
            EditorGUILayout.LabelField("Assignable (C / Shift+C / 1)", string.Join(", ", designer.colorChoices), EditorStyles.wordWrappedLabel);
        }

        if (colors.Count - matched > 0) {
            EditorGUILayout.HelpBox($"{colors.Count - matched} texture colour(s) have no ColorSO on the Level prefab (rows in red). Pieces cannot be given those colours, and that sand can never be collected.", MessageType.Error);
        }
        if (approx.Count > 0) {
            EditorGUILayout.HelpBox($"Off-palette pixels matched only within the tolerance (rows in orange): {string.Join("; ", approx)}. The level loader matches exactly and will refuse this texture until those pixels are fixed.", MessageType.Warning);
        }
        if (designer.unmatchedTextureColors.Count > 0) {
            EditorGUILayout.HelpBox($"Texture colours with no palette slot within tolerance: {string.Join(", ", designer.unmatchedTextureColors)}.", MessageType.Warning);
        }

        if (designer.isActive) {
            List<string> notInTexture = new();
            foreach (ColorSO.ItemColor color in placedColors) {
                if (designer.textureColorOf(color) == null) notInTexture.Add($"{color} ({designer.placedCountOf(color)})");
            }
            if (notInTexture.Count > 0) {
                EditorGUILayout.HelpBox($"Placed but not in the texture: {string.Join(", ", notInTexture)}. Those pieces would auto-complete at level start (no sand to collect).", MessageType.Warning);
            }
        }
    }

    void drawSummary(int textureColors, int matched, int missing, string placed) {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
            EditorGUILayout.LabelField($"Texture Colors: {textureColors}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Matched ColorSO: {matched}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Missing ColorSO: {missing}", missing > 0 ? _missingStyle : EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Placed Colors: {placed}", EditorStyles.boldLabel);
        }
    }

    // One row per colour: swatch | colour | ColorSO (a pingable object field, or MISSING) | placed | pixels.
    void drawTable(SandLevelDesigner designer, IReadOnlyList<SandLevelDesigner.TextureColor> colors) {
        Rect header = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
        layoutRow(header, out _, out Rect colorCell, out Rect colorSOCell, out Rect placedCell, out Rect pixelsCell);
        GUI.Label(colorCell, "Colour", _columnHeaderStyle);
        GUI.Label(colorSOCell, "ColorSO", _columnHeaderStyle);
        GUI.Label(placedCell, "Placed", _columnHeaderStyle);
        GUI.Label(pixelsCell, "Pixels", _columnHeaderStyle);

        foreach (SandLevelDesigner.TextureColor info in colors) {
            Rect row = EditorGUILayout.GetControlRect(false, ROW_HEIGHT);
            if (!info.hasColorSO) EditorGUI.DrawRect(row, MISSING_ROW_TINT);
            else if (!info.exact) EditorGUI.DrawRect(row, APPROX_ROW_TINT);

            layoutRow(row, out Rect swatch, out colorCell, out colorSOCell, out placedCell, out pixelsCell);

            EditorGUI.DrawRect(new Rect(swatch.x - 1f, swatch.y - 1f, swatch.width + 2f, swatch.height + 2f), SWATCH_BORDER);
            EditorGUI.DrawRect(swatch, info.swatch);

            GUI.Label(colorCell, new GUIContent(info.color.ToString(), string.Join("\n", info.hexes)));

            if (info.hasColorSO) {
                using (new EditorGUI.DisabledScope(true)) {
                    EditorGUI.ObjectField(colorSOCell, info.colorSO, typeof(ColorSO), false);
                }
            } else {
                GUI.Label(colorSOCell, "✗ MISSING ColorSO", _missingStyle);
            }

            GUI.Label(placedCell, designer.isActive ? designer.placedCountOf(info.color).ToString() : "–");
            GUI.Label(pixelsCell, info.exact ? info.pixels.ToString() : $"{info.pixels} ~");
        }
    }

    static void layoutRow(Rect row, out Rect swatch, out Rect color, out Rect colorSO, out Rect placed, out Rect pixels) {
        float y = row.y + (row.height - SWATCH_SIZE) * 0.5f;
        swatch = new Rect(row.x + 4f, y, SWATCH_SIZE, SWATCH_SIZE);
        float x = swatch.xMax + 8f;
        color = new Rect(x, row.y, COLOR_COLUMN, row.height);
        x += COLOR_COLUMN;
        pixels = new Rect(row.xMax - PIXELS_COLUMN, row.y, PIXELS_COLUMN, row.height);
        placed = new Rect(pixels.x - PLACED_COLUMN, row.y, PLACED_COLUMN, row.height);
        colorSO = new Rect(x, row.y + 2f, Mathf.Max(60f, placed.x - x - 8f), row.height - 4f);
    }

    void ensureStyles() {
        if (_missingStyle != null) return;
        _missingStyle = new GUIStyle(EditorStyles.boldLabel);
        _missingStyle.normal.textColor = new Color(0.95f, 0.3f, 0.3f);
        _okStyle = new GUIStyle(EditorStyles.label);
        _columnHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel);
    }
}
