using UnityEngine;

// Entry point for the isolated "Sand Cylinder + Color Cubes" prototype.
// Builds everything from primitives at runtime (no prefabs/images), mirroring
// the code-first setup style of SandFeelDemo/Scripts/SandFeelDemoBootstrap.cs
// without touching that file. Attach this to a single GameObject in
// SandCylinderDemoScene.unity; a SandCylinderTunables component sits alongside
// it as the one place to tune the whole mechanic.
//
// Android note: every Material here is a [SerializeField] pointing at a
// pre-authored asset under SandCylinderDemo/Materials/, not a runtime
// Shader.Find(). A shader with no serialized Material in the build can be
// stripped entirely in an IL2CPP/release build, which makes Shader.Find
// return null on-device even though it works fine in the Editor — that's
// what previously broke SandFeelDemo, and its fix (SandUnlit.mat + a
// serialized Material field) is the same pattern used here.
[RequireComponent(typeof(SandCylinderTunables))]
public class SandCylinderDemoBootstrap : MonoBehaviour {

    [SerializeField] Material sandMaterial;
    [SerializeField] Material cubeMaterial;

    SandCylinderTunables tunables;
    SandCylinderSandGrid grid;
    SandCylinderRenderer sandRenderer;
    SandExtractionController controller;

    void Start() {
        tunables = GetComponent<SandCylinderTunables>();

        Transform sandQuad = BuildSandQuad();

        grid = sandQuad.gameObject.AddComponent<SandCylinderSandGrid>();
        grid.Init(tunables);
        grid.FillInitialLayers();

        sandRenderer = sandQuad.gameObject.AddComponent<SandCylinderRenderer>();
        sandRenderer.Init(sandQuad.GetComponent<MeshRenderer>(), sandMaterial);

        GameObject controllerGO = new GameObject("SandExtractionController");
        controllerGO.transform.SetParent(transform, false);
        controller = controllerGO.AddComponent<SandExtractionController>();

        Vector3 sandAreaBottomWorld = transform.position + Vector3.down * (tunables.cylinderHeight * 0.5f);
        controller.Init(grid, sandAreaBottomWorld, tunables.SandAreaWorldWidth, cubeMaterial);
    }

    // No enclosing shell any more — the sand was always rendered as a flat
    // quad (see SandCylinderRenderer); the glass cylinder primitive around
    // it was purely decorative, and its own radius tunable is gone (grid
    // width is now driven by SandCylinderTunables.blockCellSize instead —
    // see SandAreaWorldWidth), so there's nothing left to size a shell to.
    Transform BuildSandQuad() {
        GameObject quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadGO.name = "SandCrossSection";
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
