using Moow;
using UnityEngine;

public class CameraController : MonoBehaviour {
    [SerializeField] Camera _mainCamera;
    [SerializeField] Camera _dupeCamera;
    [SerializeField] CameraFitMethod _fitMethod;

    [SerializeField] Vector3 _expandMin;
    [SerializeField] Vector3 _expandMax;

    [SerializeField] float _heightFactor;
    [SerializeField] float _widthFactor;
    [SerializeField] float _goBack;
    [SerializeField] float _lerpSpeed;
    [SerializeField] Vector2 _levelAnchor = new Vector2(0.5f, 0.5f); // default center

    Bounds bounds;
    Vector3[] worldPoints;

    private void OnEnable() {
        this.addListener<Bounds>(Events.LEVEL_BOUNDS_CHANGED, onLevelBoundsChanged);
    }

    private void OnDisable() {
        this.removeListener<Bounds>(Events.LEVEL_BOUNDS_CHANGED, onLevelBoundsChanged);
    }

    private void onLevelBoundsChanged(UnityEngine.Object sender, Event<Bounds> eventData) {
        bounds = eventData.data;
        calculateFOV();
    }

    void calculateFOV() {
        float aspectRatio = (float)Screen.width / Screen.height;

        Vector3 boundsMin = bounds.min - _expandMin;
        Vector3 boundsMax = bounds.max + _expandMax;

        worldPoints = new Vector3[]{
            new Vector3(boundsMin.x, boundsMin.y, boundsMin.z),
            new Vector3(boundsMax.x, boundsMin.y, boundsMin.z),
            new Vector3(boundsMin.x, boundsMax.y, boundsMin.z),
            new Vector3(boundsMax.x, boundsMax.y, boundsMin.z),
            new Vector3(boundsMin.x, boundsMin.y, boundsMax.z),
            new Vector3(boundsMax.x, boundsMin.y, boundsMax.z),
            new Vector3(boundsMin.x, boundsMax.y, boundsMax.z),
            new Vector3(boundsMax.x, boundsMax.y, boundsMax.z),
        };

        Vector3[] cameraPoints = new Vector3[worldPoints.Length];
        for (int i = 0; i < worldPoints.Length; i++) {
            cameraPoints[i] = _mainCamera.worldToCameraMatrix.MultiplyPoint3x4(worldPoints[i]);
        }

        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;

        foreach (var p in cameraPoints) {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        float widthSize = (maxX - minX) / (2 * aspectRatio) * _widthFactor;
        float heightSize = (maxY - minY) / 2 * _heightFactor;
        float ortho = Mathf.Max(widthSize, heightSize);

        if (_fitMethod == CameraFitMethod.FIT_WIDTH) {
            ortho = widthSize;
        } else if (_fitMethod == CameraFitMethod.FIT_HEIGHT) {
            ortho = heightSize;
        }

        float targetOrtho = ortho;

        Vector3 anchorWorldPos = new Vector3(
            Mathf.Lerp(boundsMin.x, boundsMax.x, _levelAnchor.x),
            Mathf.Lerp(boundsMin.y, boundsMax.y, _levelAnchor.y),
            Mathf.Lerp(boundsMin.z, boundsMax.z, _levelAnchor.y) // Z uses same anchor.y for top-down setup
        );

        float cameraLocalX = (_levelAnchor.x - 0.5f) * 2 * targetOrtho * aspectRatio;
        float cameraLocalY = (_levelAnchor.y - 0.5f) * 2 * targetOrtho;

        Vector3 cameraLocalAnchor = new Vector3(cameraLocalX, cameraLocalY, 0);
        Vector3 cameraAnchorPos = transform.InverseTransformPoint(anchorWorldPos);
        Vector3 cameraLocalOffset = cameraAnchorPos - cameraLocalAnchor;

        Vector3 targetCameraPos = transform.position + transform.TransformVector(cameraLocalOffset);
        targetCameraPos += transform.forward * _goBack;

        _mainCamera.orthographicSize = Mathf.Lerp(_mainCamera.orthographicSize, targetOrtho, _lerpSpeed);
        transform.position = Vector3.Lerp(transform.position, targetCameraPos, _lerpSpeed);

        if (_dupeCamera != null) {
            _dupeCamera.orthographicSize = _mainCamera.orthographicSize;
        }
    }

    private void Update() {
#if UNITY_EDITOR
        calculateFOV();
#endif
    }

    private void OnDrawGizmos() {
        if (_mainCamera == null) return;
        Bounds drawBounds = new Bounds(bounds.center, bounds.size);
        Vector3 offset = Vector3.up * 0.0f;

        Gizmos.color = Color.black;
        GizmosExtensions.DrawWireCube(drawBounds.center + offset, drawBounds.size);

        Bounds expandedBounds = new Bounds(bounds.center, bounds.size);
        expandedBounds.min -= _expandMin;
        expandedBounds.max += _expandMax;

        GizmosExtensions.DrawWireCube(expandedBounds.center + offset, expandedBounds.size);

        Vector3 boundsMin = expandedBounds.min;
        Vector3 boundsMax = expandedBounds.max;

        Vector3 anchorWorldPos = new Vector3(
            Mathf.Lerp(boundsMin.x, boundsMax.x, _levelAnchor.x),
            Mathf.Lerp(boundsMin.y, boundsMax.y, _levelAnchor.y),
            Mathf.Lerp(boundsMin.z, boundsMax.z, _levelAnchor.y)
        );

        Gizmos.color = Color.red;
        GizmosExtensions.DrawWireSphere(anchorWorldPos, 1);

        if (worldPoints != null) {
            foreach (Vector3 worldPoint in worldPoints) {
                Gizmos.DrawSphere(worldPoint, 0.5f);
            }
        }
    }
}

public enum CameraFitMethod {
    FIT_WIDTH,
    FIT_HEIGHT,
    FIT_BOTH,
}