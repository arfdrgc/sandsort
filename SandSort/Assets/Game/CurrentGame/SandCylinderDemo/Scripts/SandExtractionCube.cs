using TMPro;
using UnityEngine;

// A single colored collector cube. Pure data + visuals — SandExtractionController
// owns movement/state-machine logic and drives Increment().
public class SandExtractionCube : MonoBehaviour {

    public byte ColorIndex { get; private set; }

    // A cube's lifetime ceiling on TotalCollected — SandCylinderTunables.
    // CubeMaximumCollectible, which is exactly one grid block's cell count
    // (blockCellSize * blockCellSize). "1-unit cube fully drains a matching
    // 1-unit block, then it's full" is now literal: there's no separate
    // lower "required" threshold to pause at first, unlike the old design.
    // This is what SandExtractionController gates active/rearm eligibility
    // on (IsCollectionFull).
    public int MaximumCollectible { get; private set; }
    public bool IsCollectionFull => TotalCollected >= MaximumCollectible;

    // How many more cells this cube can still accept before hitting
    // MaximumCollectible — lets a caller clamp a removal budget so
    // TotalCollected lands exactly on the ceiling instead of overshooting it.
    public int RemainingCapacity => Mathf.Max(0, MaximumCollectible - TotalCollected);

    // World X captured the first frame IsCollectionFull became true (see
    // SandExtractionController.RearmCompletedCubesThatHaveLeftTheZone) —
    // lets the controller detect "has physically travelled clear of the
    // zone it filled up in" purely from movement, instead of from whether
    // matching sand is still reachable nearby (which can stay true
    // indefinitely for a common/abundant color, permanently stalling the
    // reset). Cleared by ResetAmount.
    public float? FullSinceWorldX { get; private set; }

    public void MarkFullSincePosition(float worldX) {
        if (FullSinceWorldX == null) FullSinceWorldX = worldX;
    }

    // Cumulative actual sand cells this cube has collected during its
    // current pass (cubes are never destroyed/recreated — see
    // SandExtractionController). Unlike CurrentAmount this is never capped
    // below MaximumCollectible, but it IS cleared by ResetAmount once a full
    // cube has cleared the extraction zone, so a cube empties out and starts
    // a fresh collection pass rather than retiring for good. Driven only by
    // Increment's `amount`, which callers pass as the real ExtractColor
    // return value — never derived from particle counts.
    public int TotalCollected { get; private set; }

    MeshRenderer meshRenderer;
    MaterialPropertyBlock propertyBlock;

    TextMeshPro collectionLabel;

    void Awake() {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Setup(byte colorIndex, int maximumCollectible, Color color, float labelLocalHeight) {
        ColorIndex = colorIndex;
        MaximumCollectible = Mathf.Max(1, maximumCollectible);
        TotalCollected = 0;

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.GetPropertyBlock(propertyBlock);
        // Both keys are set so this works whichever the active shader exposes;
        // Unity silently ignores a SetColor call for a property the shader lacks.
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(propertyBlock);

        BuildCollectionLabel(labelLocalHeight);
        RefreshCollectionLabel();
    }

    void BuildCollectionLabel(float labelLocalHeight) {
        GameObject labelGO = new GameObject("CollectionLabel");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, labelLocalHeight, 0f);
        labelGO.transform.localScale = Vector3.one * 0.12f;

        collectionLabel = labelGO.AddComponent<TextMeshPro>();
        collectionLabel.alignment = TextAlignmentOptions.Center;
        collectionLabel.fontSize = 40f;
        collectionLabel.color = Color.black;
        collectionLabel.outlineWidth = 0.2f;
        collectionLabel.outlineColor = Color.black;
        collectionLabel.enableWordWrapping = false;
        collectionLabel.raycastTarget = false;
    }

    // Keeps the label facing the (fixed) camera so it stays readable
    // regardless of which side of the conveyor the cube is currently on.
    void LateUpdate() {
        if (collectionLabel == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        collectionLabel.transform.rotation = cam.transform.rotation;
    }

    public void Increment(int amount) {
        TotalCollected += amount;
        RefreshCollectionLabel();
    }

    // Percent is against this cube's OWN MaximumCollectible, not the
    // cylinder's grid-wide total — so it reads 0% right after a reset and
    // hits exactly 100% the moment IsCollectionFull triggers, matching what
    // the count actually means for this cube instead of a small, easily
    // misread fraction of every color combined.
    // Two stacked lines (count on top, percent below) rather than one
    // inline string.
    void RefreshCollectionLabel() {
        if (collectionLabel == null) return;
        int percent = MaximumCollectible > 0
            ? Mathf.RoundToInt((float)TotalCollected / MaximumCollectible * 100f)
            : 0;
        collectionLabel.text = TotalCollected + "\n" + percent + "%";
    }

    // Called once this cube has cleared the extraction range after
    // IsCollectionFull became true (see
    // SandExtractionController.RearmCompletedCubesThatHaveLeftTheZone).
    // Clears TotalCollected, so IsCollectionFull goes false again and the
    // cube becomes eligible again on its next pass — a full cube empties
    // out and is ready to draw sand again instead of retiring permanently.
    public void ResetAmount() {
        TotalCollected = 0;
        FullSinceWorldX = null;
        RefreshCollectionLabel();
    }
}
