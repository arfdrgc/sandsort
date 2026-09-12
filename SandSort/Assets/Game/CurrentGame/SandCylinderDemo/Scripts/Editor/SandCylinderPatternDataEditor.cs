using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Custom Inspector for SandCylinderPatternData — lets you paint a
// SandCylinderDemo initial sand layout by hand, cell by cell, instead of
// only ever getting a random Tetris-piece layout at Play time. Lives under
// an "Editor" folder so it (and the UnityEditor dependency) is stripped
// from player builds; SandCylinderPatternData itself has no such
// dependency and stays fully readable at runtime.
[CustomEditor(typeof(SandCylinderPatternData))]
public class SandCylinderPatternDataEditor : Editor {

    // Preview-only swatch colors for the grid buttons below — a
    // ScriptableObject asset can't reference a particular scene's
    // SandCylinderTunables, so these just mirror its sandColors defaults
    // closely enough to paint by eye. What's actually stored per cell is a
    // palette INDEX (1-5), never a Color, so this array only affects how
    // this Inspector looks, never what renders in-game.
    static readonly Color[] SlotColors = {
        new Color(0.25f, 0.25f, 0.25f), // 0 = empty (no sand placed here)
        new Color(0.94f, 0.92f, 0.86f), // 1
        new Color(0.90f, 0.27f, 0.27f), // 2
        new Color(0.27f, 0.51f, 0.90f), // 3
        new Color(0.94f, 0.78f, 0.20f), // 4
        new Color(0.35f, 0.78f, 0.47f), // 5
    };

    const float CellSize = 26f;
    const float MinCellSize = 14f;
    const float BlockGap = 5f; // extra spacing between BLOCKS (not sub-cells) so block boundaries stay visible when subdivided
    const float PaletteSwatchSize = 32f;

    // Which SlotColors index (0 = erase, 1-5 = palette colors) a grid-cell
    // click paints with — an Editor-instance field rather than anything on
    // the asset itself, since it's purely a tool-session preference, not
    // part of the pattern's own data. Resets to the first real color
    // whenever this Inspector is freshly opened on a pattern (a new Editor
    // instance is created then), which is a reasonable default to start
    // painting with.
    int activeSlot = 1;

    public override void OnInspectorGUI() {
        var pattern = (SandCylinderPatternData)target;

        EditorGUILayout.HelpBox(
            "Pick an active color below, then click cells to paint them with it directly (no cycling). " +
            "Row 0 is drawn at the bottom, matching where sand actually sits lowest in the game. " +
            "Slot numbers match SandCylinderTunables.sandColors order (slot 1 = sandColors[0], etc). " +
            "Subdivisions > 1 paints each block as a grid of independently-colored sub-cells (extra spacing marks block edges) without changing the block grid's own size.",
            MessageType.Info);

        DrawActiveColorPalette();

        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUILayout.IntField("Width (blocks)", pattern.width);
        int newHeight = EditorGUILayout.IntField("Height (blocks)", pattern.height);
        int newSubdivisions = EditorGUILayout.IntSlider("Subdivisions Per Block", pattern.subdivisionsPerBlock, 1, 4);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(pattern, "Resize Sand Pattern");
            // Resize(new, new, new) — not setting pattern's fields directly
            // first — so it can still read the OLD paint dimensions to
            // correctly remap existing painted cells before overwriting them.
            pattern.Resize(newWidth, newHeight, newSubdivisions);
            EditorUtility.SetDirty(pattern);
        }

        EditorGUILayout.Space();

        int subdivisions = Mathf.Max(1, pattern.subdivisionsPerBlock);
        float cellSize = Mathf.Max(MinCellSize, CellSize / subdivisions);
        int paintWidth = pattern.PaintWidth;
        int paintHeight = pattern.PaintHeight;

        for (int y = paintHeight - 1; y >= 0; y--) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((y / subdivisions).ToString(), GUILayout.Width(20));
            for (int x = 0; x < paintWidth; x++) {
                byte slot = pattern.GetCell(x, y);
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = SlotColors[Mathf.Clamp(slot, 0, SlotColors.Length - 1)];
                if (GUILayout.Button(GUIContent.none, GUILayout.Width(cellSize), GUILayout.Height(cellSize))) {
                    Undo.RecordObject(pattern, "Paint Sand Pattern Cell");
                    pattern.SetCell(x, y, (byte)activeSlot);
                    EditorUtility.SetDirty(pattern);
                }
                GUI.backgroundColor = prevColor;

                // Extra gap right after the last sub-cell of a block column
                // (not after every sub-cell) so block boundaries stay
                // visually distinct at any subdivision level.
                if (subdivisions > 1 && (x + 1) % subdivisions == 0 && x + 1 < paintWidth) {
                    GUILayout.Space(BlockGap);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (subdivisions > 1 && y % subdivisions == 0 && y > 0) {
                GUILayout.Space(BlockGap);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All")) {
            Undo.RecordObject(pattern, "Clear Sand Pattern");
            for (int y = 0; y < paintHeight; y++)
                for (int x = 0; x < paintWidth; x++)
                    pattern.SetCell(x, y, 0);
            EditorUtility.SetDirty(pattern);
        }
        if (GUILayout.Button("Randomize (Tetris pieces)")) {
            Undo.RecordObject(pattern, "Randomize Sand Pattern");
            RandomizeWithTetrisPieces(pattern);
            EditorUtility.SetDirty(pattern);
        }
        EditorGUILayout.EndHorizontal();
    }

    // Draws the "Erase" + 5 palette-color swatches as buttons; clicking one
    // sets activeSlot, and a white frame is drawn around whichever swatch
    // is currently active so it's clear at a glance what a grid-cell click
    // will paint with.
    void DrawActiveColorPalette() {
        EditorGUILayout.LabelField("Active Color", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int slot = 0; slot < SlotColors.Length; slot++) {
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = SlotColors[slot];
            string label = slot == 0 ? "Erase" : slot.ToString();
            bool clicked = GUILayout.Button(label, GUILayout.Width(PaletteSwatchSize), GUILayout.Height(PaletteSwatchSize));
            GUI.backgroundColor = prevColor;
            if (clicked) activeSlot = slot;

            // Frame the currently-active swatch so it's clear at a glance
            // what a grid-cell click will paint with. Drawn after every
            // swatch (not just on click) so it stays visible across repaints.
            if (slot == activeSlot) {
                Rect swatchRect = GUILayoutUtility.GetLastRect();
                const float t = 3f;
                EditorGUI.DrawRect(new Rect(swatchRect.x - t, swatchRect.y - t, swatchRect.width + t * 2, t), Color.white);
                EditorGUI.DrawRect(new Rect(swatchRect.x - t, swatchRect.yMax, swatchRect.width + t * 2, t), Color.white);
                EditorGUI.DrawRect(new Rect(swatchRect.x - t, swatchRect.y - t, t, swatchRect.height + t * 2), Color.white);
                EditorGUI.DrawRect(new Rect(swatchRect.xMax, swatchRect.y - t, t, swatchRect.height + t * 2), Color.white);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // Reuses SandCylinderSandGrid's own public piece-tiling/coloring
    // methods (BuildPieceIdGrid/ColorPieces/PieceShapes) — which operate at
    // BLOCK resolution, same as the runtime generator itself — so a
    // "Randomize" click here previews exactly the kind of layout the game
    // would generate on its own, then replicates each block's chosen color
    // across all of that block's sub-cells (subdivisionsPerBlock x
    // subdivisionsPerBlock of them) so the result is still valid at
    // whatever paint resolution is currently set. It's a convenient
    // starting point to hand-edit finer detail into afterward, not itself a
    // source of sub-block-level randomness.
    static void RandomizeWithTetrisPieces(SandCylinderPatternData pattern) {
        int colorCount = SlotColors.Length - 1; // 5 real colors; slot 0 is "empty"
        int[,] pieceIdGrid = SandCylinderSandGrid.BuildPieceIdGrid(pattern.width, pattern.height, out List<List<Vector2Int>> pieces);
        int[] pieceColors = SandCylinderSandGrid.ColorPieces(pieceIdGrid, pieces, pattern.width, pattern.height, colorCount);

        int subdivisions = Mathf.Max(1, pattern.subdivisionsPerBlock);
        for (int by = 0; by < pattern.height; by++) {
            for (int bx = 0; bx < pattern.width; bx++) {
                byte colorIndex = (byte)(pieceColors[pieceIdGrid[bx, by]] + 1);
                for (int sy = 0; sy < subdivisions; sy++) {
                    for (int sx = 0; sx < subdivisions; sx++) {
                        pattern.SetCell(bx * subdivisions + sx, by * subdivisions + sy, colorIndex);
                    }
                }
            }
        }
    }
}
