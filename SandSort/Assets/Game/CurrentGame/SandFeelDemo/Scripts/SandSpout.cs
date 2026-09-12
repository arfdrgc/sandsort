using UnityEngine;

[RequireComponent(typeof(SandGrid))]
public class SandSpout : MonoBehaviour {

    const int ColorCount = 5;

    Camera targetCamera;
    Transform gridQuad;
    SandGrid grid;

    byte currentColorIndex;

    void Awake() {
        grid = GetComponent<SandGrid>();
    }

    public void SetCamera(Camera cam) => targetCamera = cam;
    public void SetGridQuad(Transform quad) => gridQuad = quad;

    void Update() {
        if (targetCamera == null || gridQuad == null) return;

        // Advance the color once per new press, not while held, so the whole
        // pour uses a single color until the player releases and presses again.
        if (Input.GetMouseButtonDown(0)) {
            currentColorIndex = (byte)(currentColorIndex % ColorCount + 1);
        }

        if (!Input.GetMouseButton(0)) return;
        if (!TryScreenToCell(Input.mousePosition, out int cx)) return;

        var t = grid.Tunables;
        int half = Mathf.Max(1, t.streamWidth) / 2;
        for (int i = 0; i < t.pourRatePerStep; i++) {
            int ox = Random.Range(-half, half + 1);
            int oy = Random.Range(0, 3);
            grid.SpawnCell(cx + ox, grid.Height - 1 - oy, currentColorIndex);
        }
    }

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
