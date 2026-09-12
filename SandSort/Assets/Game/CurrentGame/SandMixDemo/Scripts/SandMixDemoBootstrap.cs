using UnityEngine;

// Entry point for the "Sand Mix" demo — SandFeelDemo's pour-from-the-top
// interaction (SandMixSpout, adapted from SandFeelDemo/Scripts/SandSpout.cs)
// combined with SandCylinderDemo's color-matching collector cubes on a
// conveyor below (SandExtractionController/SandExtractionCube, reused
// completely unmodified). The sand area starts EMPTY — no
// SandCylinderSandGrid.FillInitialLayers() call, unlike
// SandCylinderDemoBootstrap — and is populated purely by the player pouring
// colored sand in from the top; everything below (gravity, extraction, the
// conveyor) works identically regardless of how the sand got there.
//
// Built from primitives at runtime, same code-first style as
// SandCylinderDemoBootstrap/SandFeelDemoBootstrap. Attach to a single
// GameObject alongside a SandMixTunables component in SandMixDemoScene.unity.
[RequireComponent(typeof(SandMixTunables))]
public class SandMixDemoBootstrap : MonoBehaviour {

    [SerializeField] Camera cam;
    [SerializeField] Material sandMaterial;
    [SerializeField] Material cubeMaterial;

    SandMixTunables tunables;
    SandCylinderSandGrid grid;
    SandCylinderRenderer sandRenderer;
    SandExtractionController controller;
    SandMixSpout spout;

    void Start() {
        if (cam == null) cam = Camera.main;
        tunables = GetComponent<SandMixTunables>();

        Transform sandQuad = BuildSandQuad();

        grid = sandQuad.gameObject.AddComponent<SandCylinderSandGrid>();
        grid.Init(tunables);
        // No FillInitialLayers() call — this demo starts with an empty sand
        // area; the player pours sand in via SandMixSpout instead.

        sandRenderer = sandQuad.gameObject.AddComponent<SandCylinderRenderer>();
        sandRenderer.Init(sandQuad.GetComponent<MeshRenderer>(), sandMaterial);

        spout = sandQuad.gameObject.AddComponent<SandMixSpout>();
        spout.SetCamera(cam);
        spout.SetGridQuad(sandQuad);

        GameObject controllerGO = new GameObject("SandExtractionController");
        controllerGO.transform.SetParent(transform, false);
        controller = controllerGO.AddComponent<SandExtractionController>();

        Vector3 sandAreaBottomWorld = transform.position + Vector3.down * (tunables.cylinderHeight * 0.5f);
        controller.Init(grid, sandAreaBottomWorld, tunables.SandAreaWorldWidth, cubeMaterial);
    }

    // Identical to SandCylinderDemoBootstrap.BuildSandQuad — see that
    // method's doc comment for why there's no enclosing shell.
    Transform BuildSandQuad() {
        GameObject quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadGO.name = "SandMixArea";
        Collider col = quadGO.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Transform t = quadGO.transform;
        t.SetParent(transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = new Vector3(tunables.SandAreaWorldWidth, tunables.cylinderHeight, 1f);
        return t;
    }
}
