using UnityEngine;

[RequireComponent(typeof(SandGrid))]
public class SandGridRenderer : MonoBehaviour {

    [SerializeField] Color32 backgroundColor = new Color32(24, 28, 46, 255);
    [SerializeField] Color32[] palette = new Color32[] {
        new Color32(240, 235, 220, 255),
        new Color32(230, 70, 70, 255),
        new Color32(70, 130, 230, 255),
        new Color32(240, 200, 50, 255),
        new Color32(90, 200, 120, 255),
    };

    SandGrid grid;
    Texture2D texture;
    Color32[] pixels;
    Material material;
    int builtWidth, builtHeight;

    public Color32[] Palette => palette;

    void Awake() {
        grid = GetComponent<SandGrid>();
    }

    public void Init(Renderer targetRenderer, Material sourceMaterial) {
        if (sourceMaterial == null) {
            Debug.LogError("SandGridRenderer: sourceMaterial is null — assign SandFeelDemoBootstrap's Sand Material field in the Inspector (Assets/Game/CurrentGame/SandFeelDemo/Materials/SandUnlit.mat).");
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
        int w = grid.Width;
        int h = grid.Height;
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                byte c = grid.GetCell(x, y);
                int i = y * w + x;
                if (c == SandGrid.EMPTY) {
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
