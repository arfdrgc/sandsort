using UnityEngine;

// Isolated counterpart to SandFeelDemo/Scripts/SandGridRenderer.cs — draws the
// SandCylinderSandGrid into a procedural texture on a quad.
//
// Android note: this used to build its material via Shader.Find at runtime.
// In an IL2CPP/release build, a shader that no serialized Material in the
// build references can be stripped entirely, so Shader.Find silently returns
// null on-device even though it works in the Editor (this is exactly what
// happened to SandFeelDemo). The fix, mirrored from SandFeelDemo's own
// SandUnlit.mat + [SerializeField] Material field: Init() now takes a
// pre-authored Material asset (SandCylinderDemo/Materials/SandCylinderSandUnlit.mat)
// so the shader dependency is visible to Unity's build-time asset scan.
// Later execution order so this LateUpdate always draws after
// SandCylinderSandGrid.LateUpdate has run its active-region sub-steps. It has
// no Update, so Update ordering (grid Step vs extraction) is unaffected.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(SandCylinderSandGrid))]
public class SandCylinderRenderer : MonoBehaviour {

    // Neutral grey (#A6A6A6) behind the empty cells. Purely visual — the grid, the fill and the
    // extraction never see it.
    [SerializeField] Color32 backgroundColor = new Color32(166, 166, 166, 255);

    SandCylinderSandGrid grid;
    Texture2D texture;
    Color32[] pixels;
    Material material;
    int builtWidth, builtHeight;

    // CHANGE GATE (2026-09-16). The texture is a pure function of the grid's
    // cells and their per-grain tints plus colorNoiseAmount and the sandColors
    // palette — and a tint only ever changes together with its cell, through
    // the grid's Place/Move/Remove primitives, each of which bumps
    // CellsVersion — so on a frame where none of those changed the rebuild
    // below would recompute the exact same pixels and re-upload them. On device (Mi 9T, IL2CPP) that cost
    // 11.3 ms/frame on the 306x306 9-colour grid *while the sand was static* —
    // 42% of a 27 ms frame. These fields record the inputs of the last
    // completed draw; LateUpdate returns early when they all still match.
    //
    // Why no frame that needs a redraw can be skipped: this component carries
    // DefaultExecutionOrder(100) and has only a LateUpdate, so it is the last
    // thing in the frame to touch the sand (Step and extraction run in Update,
    // the grid's active sub-steps in its own order-0 LateUpdate). Any write
    // from any of those has therefore already bumped grid.CellsVersion by the
    // time this gate reads it. hasDrawn is false until a draw completes — and
    // it resets with every other non-serialized field on a domain reload — so
    // the first frame, a fresh Init and a mid-Play recompile all redraw.
    bool hasDrawn;
    ulong drawnCellsVersion;
    float drawnNoiseAmount;
    Color[] drawnPalette;

    void Awake() {
        grid = GetComponent<SandCylinderSandGrid>();
    }

    public void Init(Renderer targetRenderer, Material sourceMaterial) {
        if (sourceMaterial == null) {
            Debug.LogError("SandCylinderRenderer: sourceMaterial is null — assign SandCylinderDemoBootstrap's Sand Material field in the Inspector (Assets/Game/CurrentGame/SandCylinderDemo/Materials/SandCylinderSandUnlit.mat).");
            return;
        }
        material = new Material(sourceMaterial);
        targetRenderer.sharedMaterial = material;
        BuildTexture();
    }

    void BuildTexture() {
        builtWidth = grid.Width;
        builtHeight = grid.Height;
        texture = new Texture2D(builtWidth, builtHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[builtWidth * builtHeight];
        if (material != null) material.mainTexture = texture;
        hasDrawn = false; // a brand-new texture and an all-zero pixel buffer must be filled
    }

    void LateUpdate() {
        if (texture == null) return;
        if (builtWidth != grid.Width || builtHeight != grid.Height) BuildTexture();

        float noiseAmount = grid.Tunables.colorNoiseAmount;
        Color[] palette = grid.Tunables.sandColors;

        // Read the version BEFORE the loop reads the cells: recording an older
        // version than the grid actually has can only cause an extra redraw
        // next frame, never a missed one.
        ulong version = grid.CellsVersion;
        if (!NeedsRedraw(version, noiseAmount, palette)) return;

        int w = grid.Width;
        int h = grid.Height;
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                byte c = grid.GetCell(x, y);
                int i = y * w + x;
                if (c == SandCylinderSandGrid.EMPTY || palette.Length == 0) {
                    pixels[i] = backgroundColor;
                } else {
                    Color32 baseColor = palette[(c - 1) % palette.Length];
                    // The grain's own persistent tint, not a function of (x, y): it was
                    // assigned when the grain was created and travels with it through every
                    // move (see SandCylinderSandGrid.tint), so the grain pattern falls and
                    // collapses together with the sand instead of staying pinned to the quad.
                    float n = grid.GetTint(x, y) * (1f / 255f);
                    float mul = 1f + (n - 0.5f) * noiseAmount;
                    pixels[i] = new Color32(
                        (byte)Mathf.Clamp(baseColor.r * mul, 0, 255),
                        (byte)Mathf.Clamp(baseColor.g * mul, 0, 255),
                        (byte)Mathf.Clamp(baseColor.b * mul, 0, 255),
                        255);
                }
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);

        hasDrawn = true;
        drawnCellsVersion = version;
        drawnNoiseAmount = noiseAmount;
        if (drawnPalette == null || drawnPalette.Length != palette.Length) drawnPalette = new Color[palette.Length];
        System.Array.Copy(palette, drawnPalette, palette.Length);
    }

    // True when anything the texture depends on has changed since the last
    // completed draw. The palette is compared element-wise (17 entries at most)
    // rather than by reference, so editing a colour in the Inspector mid-Play
    // still repaints.
    bool NeedsRedraw(ulong version, float noiseAmount, Color[] palette) {
        if (!hasDrawn) return true;
        if (version != drawnCellsVersion) return true;
        if (noiseAmount != drawnNoiseAmount) return true;
        if (drawnPalette == null || drawnPalette.Length != palette.Length) return true;
        for (int i = 0; i < palette.Length; i++) {
            if (drawnPalette[i] != palette[i]) return true;
        }
        return false;
    }
}
