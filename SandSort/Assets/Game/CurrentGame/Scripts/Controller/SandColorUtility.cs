using UnityEngine;

// Prototype-only fallback color mapping for ColorSO.ItemColor, used until a real
// ColorPaletteSO asset is authored for this game (LevelSO.GetColor already supports one —
// see LevelSO.cs — this utility exists purely so Container/SandCanvas visuals aren't all
// default gray while that asset doesn't exist yet).
public static class SandColorUtility {

    public static Color toUnityColor(ColorSO.ItemColor color) {
        switch (color) {
            case ColorSO.ItemColor.BLUE: return new Color(0.20f, 0.45f, 0.90f);
            case ColorSO.ItemColor.BLUE_LIGHT: return new Color(0.55f, 0.75f, 1.00f);
            case ColorSO.ItemColor.BLUE_DARK: return new Color(0.10f, 0.20f, 0.50f);
            case ColorSO.ItemColor.BROWN: return new Color(0.50f, 0.32f, 0.12f);
            case ColorSO.ItemColor.BROWN_LIGHT: return new Color(0.70f, 0.50f, 0.30f);
            case ColorSO.ItemColor.BROWN_DARK: return new Color(0.30f, 0.18f, 0.05f);
            case ColorSO.ItemColor.GREEN: return new Color(0.20f, 0.70f, 0.30f);
            case ColorSO.ItemColor.GREEN_LIGHT: return new Color(0.55f, 0.90f, 0.55f);
            case ColorSO.ItemColor.GREEN_DARK: return new Color(0.10f, 0.40f, 0.15f);
            case ColorSO.ItemColor.PURPLE: return new Color(0.60f, 0.20f, 0.80f);
            case ColorSO.ItemColor.PURPLE_LIGHT: return new Color(0.80f, 0.55f, 0.90f);
            case ColorSO.ItemColor.PURPLE_DARK: return new Color(0.35f, 0.10f, 0.45f);
            case ColorSO.ItemColor.ORANGE: return new Color(1.00f, 0.60f, 0.10f);
            case ColorSO.ItemColor.PINK: return new Color(1.00f, 0.55f, 0.70f);
            case ColorSO.ItemColor.RED: return new Color(0.85f, 0.20f, 0.20f);
            case ColorSO.ItemColor.WHITE: return Color.white;
            case ColorSO.ItemColor.YELLOW: return new Color(0.95f, 0.85f, 0.20f);
            default: return Color.magenta; // ALL / RANDOM / NONE, or anything unmapped
        }
    }
}
