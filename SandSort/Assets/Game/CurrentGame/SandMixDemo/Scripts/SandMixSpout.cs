using UnityEngine;

// SandMixDemo's pour interaction — adapted from
// SandFeelDemo/Scripts/SandSpout.cs for SandCylinderSandGrid instead of
// SandGrid (that file is untouched). Each new press advances to the next
// palette color (wrapping), matching SandSpout's own "different color every
// tap" behavior; while held, pours a stream of that color at the pointer's
// grid column into the top rows of the grid. That sand then falls/settles via
// the grid's own gravity exactly like SandCylinderDemo's Tetris-fill sand,
// and can be extracted by a matching-colored cube below the same way.
[RequireComponent(typeof(SandCylinderSandGrid))]
public class SandMixSpout : MonoBehaviour {

    Camera targetCamera;
    Transform gridQuad;
    SandCylinderSandGrid grid;
    SandMixTunables mixTunables;

    byte currentColorIndex;

    void Awake() {
        grid = GetComponent<SandCylinderSandGrid>();
        mixTunables = grid.Tunables as SandMixTunables;
    }

    public void SetCamera(Camera cam) => targetCamera = cam;
    public void SetGridQuad(Transform quad) => gridQuad = quad;

    void Update() {
        if (targetCamera == null || gridQuad == null) return;

        int colorCount = grid.Tunables.sandColors.Length;
        if (colorCount == 0) return;

        // Advance the color once per new press, not while held, so the whole
        // pour uses a single color until the player releases and presses again.
        if (Input.GetMouseButtonDown(0)) {
            currentColorIndex = (byte)(currentColorIndex % colorCount + 1);
        }

        if (!Input.GetMouseButton(0)) return;
        if (!TryScreenToCell(Input.mousePosition, out int cx)) return;

        int streamWidth = mixTunables != null ? mixTunables.streamWidth : 10;
        int pourRate = mixTunables != null ? mixTunables.pourRatePerStep : 40;
        int rowJitter = mixTunables != null ? mixTunables.pourSpawnRowJitter : 3;

        int half = Mathf.Max(1, streamWidth) / 2;
        for (int i = 0; i < pourRate; i++) {
            int ox = Random.Range(-half, half + 1);
            int oy = Random.Range(0, Mathf.Max(1, rowJitter));
            grid.SpawnCell(cx + ox, grid.Height - 1 - oy, currentColorIndex);
        }
    }

    // Identical math to SandSpout.TryScreenToCell — Transform.InverseTransformPoint
    // already un-scales into the quad's own -0.5..0.5 mesh space, so no
    // further division by localScale is needed.
    bool TryScreenToCell(Vector2 screenPos, out int cx) {
        cx = 0;
        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(gridQuad.forward, gridQuad.position);
        if (!plane.Raycast(ray, out float dist)) return false;

        Vector3 world = ray.GetPoint(dist);
        Vector3 local = gridQuad.InverseTransformPoint(world);
        float u = local.x + 0.5f;
        if (u < 0f || u > 1f) return false;

        cx = Mathf.Clamp(Mathf.FloorToInt(u * grid.Width), 0, grid.Width - 1);
        return true;
    }
}
