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
[RequireComponent(typeof(SandCylinderSandGrid))]
public class SandCylinderRenderer : MonoBehaviour {

    // Near-black, matching the mockup's sand panel (#0A0B12): the darker the backdrop, the more
    // the grain colours read. Purely visual — the grid, the fill and the extraction never see it.
    [SerializeField] Color32 backgroundColor = new Color32(10, 11, 18, 255);

    SandCylinderSandGrid grid;
    Texture2D texture;
    Color32[] pixels;
    Material material;
    int builtWidth, builtHeight;

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
    }

    void LateUpdate() {
        if (texture == null) return;
        if (builtWidth != grid.Width || builtHeight != grid.Height) BuildTexture();

        float noiseAmount = grid.Tunables.colorNoiseAmount;
        Color[] palette = grid.Tunables.sandColors;
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
                    float n = Hash01(x, y);
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
    }

    static float Hash01(int x, int y) {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }
}
