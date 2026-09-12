using UnityEngine;

public class SandFeelDemoBootstrap : MonoBehaviour {

    [SerializeField] Camera cam;
    [SerializeField] Material sandMaterial;

    SandGrid grid;
    SandGridRenderer sandRenderer;
    SandSpout spout;

    void Start() {
        if (cam == null) cam = Camera.main;

        GameObject quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadGO.name = "SandSurface";
        Transform quad = quadGO.transform;
        quad.SetParent(transform, false);
        quad.localPosition = Vector3.zero;
        quad.localRotation = Quaternion.identity;

        Collider quadCollider = quadGO.GetComponent<Collider>();
        if (quadCollider != null) Destroy(quadCollider);

        grid = gameObject.AddComponent<SandGrid>();
        sandRenderer = gameObject.AddComponent<SandGridRenderer>();
        spout = gameObject.AddComponent<SandSpout>();

        sandRenderer.Init(quadGO.GetComponent<MeshRenderer>(), sandMaterial);

        float aspect = grid.Width / (float)grid.Height;
        quad.localScale = new Vector3(aspect * 6f, 6f, 1f);

        spout.SetCamera(cam);
        spout.SetGridQuad(quad);
    }
}
