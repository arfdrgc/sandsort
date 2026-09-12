using UnityEngine;

// Procedural-texture sand renderer for SandCanvas. Ported from the RENDERING ARCHITECTURE of
// Assets/Game/CurrentGame/SandCylinderDemo/Scripts/SandCylinderRenderer.cs: a byte/color grid is
// painted every time it changes into a Texture2D (Color32[] buffer + SetPixels32 + per-texel hash
// noise for an organic grain look, same Hash01 formula), applied as the mainTexture of a single
// Quad's material — NOT one GameObject per grain. This replaces an earlier version of SandCanvas
// that built a small cube per sand grain (500+ Cube primitives for a full picture), which is the
// "3D küp" visual this class exists to eliminate entirely: one Quad, one Texture2D, full stop.
public class SandCanvasRenderer : MonoBehaviour {

    const int TEXELS_PER_CELL = 8; // texture resolution multiplier for a grainy look — NOT extra logical cells
    const float NOISE_AMOUNT = 0.18f; // same role as SandCylinderTunables.colorNoiseAmount

    Vector2Int _size;
    Texture2D _texture;
    Color32[] _pixels;
    bool _dirty;

    public void initialize(Vector2Int size) {
        _size = size;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "SandCanvasQuad";
        quad.transform.SetParent(transform, false);
        Destroy(quad.GetComponent<Collider>());
        quad.transform.localScale = new Vector3(size.x * Board.CELL_SIZE, size.y * Board.CELL_SIZE, 1f);
        quad.transform.localPosition = new Vector3((size.x - 1) * Board.CELL_SIZE * 0.5f, (size.y - 1) * Board.CELL_SIZE * 0.5f, 0f);

        int texWidth = size.x * TEXELS_PER_CELL;
        int texHeight = size.y * TEXELS_PER_CELL;
        _texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        _texture.filterMode = FilterMode.Point;
        _texture.wrapMode = TextureWrapMode.Clamp;
        _pixels = new Color32[texWidth * texHeight];

        // Sprites/Default: alpha-blended, modulates by vertex/tint color, guaranteed present in
        // every Unity project (unlike referencing SandCylinderDemo's own .mat asset directly,
        // which would put a cross-feature asset dependency on core gameplay code for no real
        // visual benefit — the technique being ported is the texture-painting architecture, not
        // that specific material file).
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = _texture;
        quad.GetComponent<Renderer>().sharedMaterial = material;
    }

    // Repaints one logical cell's whole texel block. color == NONE paints fully transparent
    // (alpha 0), so an eroded cell reads as the picture actually disappearing there rather than
    // being replaced by an opaque backdrop.
    public void setCell(int x, int z, ColorSO.ItemColor color) {
        Color32 baseColor = color == ColorSO.ItemColor.NONE ? new Color32(0, 0, 0, 0) : (Color32)SandColorUtility.toUnityColor(color);
        int texWidth = _size.x * TEXELS_PER_CELL;

        for (int ty = 0; ty < TEXELS_PER_CELL; ty++) {
            for (int tx = 0; tx < TEXELS_PER_CELL; tx++) {
                int px = x * TEXELS_PER_CELL + tx;
                int py = z * TEXELS_PER_CELL + ty;
                int index = py * texWidth + px;

                if (baseColor.a == 0) {
                    _pixels[index] = baseColor;
                    continue;
                }

                float n = Hash01(px, py);
                float mul = 1f + (n - 0.5f) * NOISE_AMOUNT;
                _pixels[index] = new Color32(
                    (byte)Mathf.Clamp(baseColor.r * mul, 0, 255),
                    (byte)Mathf.Clamp(baseColor.g * mul, 0, 255),
                    (byte)Mathf.Clamp(baseColor.b * mul, 0, 255),
                    255);
            }
        }

        _dirty = true;
    }

    void LateUpdate() {
        if (!_dirty) return;

        _texture.SetPixels32(_pixels);
        _texture.Apply(false);
        _dirty = false;
    }

    static float Hash01(int x, int y) {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }
}
