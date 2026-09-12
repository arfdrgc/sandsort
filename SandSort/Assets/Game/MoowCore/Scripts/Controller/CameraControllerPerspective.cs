using Moow;
using System;
using UnityEngine;

public class PerspectiveCameraController : MonoBehaviour {
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private float _expandTop = 0.0f;
    [SerializeField] private float _expandLeftRight = 0.0f;
    [SerializeField] private float _expandBottom = 0.0f;
    [SerializeField] private float _goBack = 1.0f;
    [SerializeField] private float _lerpSpeed = 0.1f;
    [SerializeField] private float _centerFactor = 0.5f;
    [SerializeField] private CameraFitMethod _fitMethod = CameraFitMethod.FIT_BOTH;

    Bounds bounds;

    private void OnEnable() {
        this.addListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.addListener<object>(Events.LEVEL_BOUNDS_CHANGED, onLevelBoundsChanged);
        this.addListener<object>(Events.LEVEL_READY_TO_PLAY, onLevelLoaded);
    }

    private void OnDisable() {
        this.removeListener<object>(Events.LEVEL_LOADED, onLevelLoaded);
        this.removeListener<object>(Events.LEVEL_BOUNDS_CHANGED, onLevelBoundsChanged);
        this.removeListener<object>(Events.LEVEL_READY_TO_PLAY, onLevelLoaded);
    }

    private void onLevelLoaded(UnityEngine.Object sender, Event<object> eventData) {
        calculateCameraPosition(true);
    }   
    
    private void onLevelBoundsChanged(UnityEngine.Object sender, Event<object> eventData) {
        calculateCameraPosition(true);
    }

    private void calculateCameraPosition(bool instaMove = false)
    {
        // 1. RAW bounds
        bounds = LevelGenerator.instance.currentLevel.levelBounds;
        _expandBottom = LevelGenerator.instance.currentLevel.levelSO.expandBottom;
        _centerFactor = -45 + LevelGenerator.instance.currentLevel.levelSO.centerFactor;

                // 2. aspect + FOV
        float aspectRatio = (float)Screen.width / Screen.height;
        float verticalFOV = _mainCamera.fieldOfView * Mathf.Deg2Rad;
        float horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(verticalFOV / 2f) * aspectRatio);

        // 3. expand padding (değişmedi)
        bounds.Expand(new Vector3(_expandLeftRight * 2, 0, _expandTop + _expandBottom));
        bounds.center += new Vector3(0, 0, (_expandTop - _expandBottom) * 0.5f);

        // 5. required distance hesaplama
        float requiredDistanceWidth =
            (bounds.extents.x / Mathf.Tan(horizontalFOV / 2f)) + _goBack;

        float requiredDistanceHeight =
            (bounds.extents.z / Mathf.Tan(verticalFOV / 2f)) + _goBack;

        float requiredDistance = 0f;

        switch (_fitMethod)
        {
            case CameraFitMethod.FIT_WIDTH:
                requiredDistance = requiredDistanceWidth;
                break;

            case CameraFitMethod.FIT_HEIGHT:
                requiredDistance = requiredDistanceHeight;
                break;

            case CameraFitMethod.FIT_BOTH:
                requiredDistance = Mathf.Max(requiredDistanceWidth, requiredDistanceHeight);
                break;
        }

        // 6. target position
        Vector3 targetPosition = bounds.center - transform.forward * requiredDistance;
        targetPosition += transform.up * bounds.extents.y * _centerFactor;

        // 7. smooth move
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            instaMove ? 1f : _lerpSpeed
        );

        // 8. clipping planes
        UpdateCameraClippingPlanes();
    }

    private void UpdateCameraClippingPlanes() {
        Vector3 cameraPosition = _mainCamera.transform.position;
        Vector3 boundsMin = bounds.min;
        Vector3 boundsMax = bounds.max;
        
        float minDistance = float.MaxValue;
        float maxDistance = float.MinValue;

        Vector3[] corners = new Vector3[8] {
        new Vector3(boundsMin.x, boundsMin.y, boundsMin.z),
        new Vector3(boundsMin.x, boundsMin.y, boundsMax.z),
        new Vector3(boundsMin.x, boundsMax.y, boundsMin.z),
        new Vector3(boundsMin.x, boundsMax.y, boundsMax.z),
        new Vector3(boundsMax.x, boundsMin.y, boundsMin.z),
        new Vector3(boundsMax.x, boundsMin.y, boundsMax.z),
        new Vector3(boundsMax.x, boundsMax.y, boundsMin.z),
        new Vector3(boundsMax.x, boundsMax.y, boundsMax.z)
    };

        foreach(var corner in corners) {
            float distance = Vector3.Dot(corner - cameraPosition, _mainCamera.transform.forward);
            minDistance = Mathf.Min(minDistance, distance);
            maxDistance = Mathf.Max(maxDistance, distance);
        }

        _mainCamera.nearClipPlane = Mathf.Max(0.1f, minDistance - 10);
        _mainCamera.farClipPlane = (maxDistance + _goBack) * 1.1f;
    }

    private void Update() {
#if UNITY_EDITOR
        //calculateCameraPosition();
#endif
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.black;

        GizmosExtensions.DrawWireCube(bounds.center, bounds.size);
    }
}