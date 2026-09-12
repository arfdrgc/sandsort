using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;
using NaughtyAttributes;

[RequireComponent(typeof(SplineComputer))]
public class SplineShapeGenerator : MonoBehaviour
{
    public enum SegmentType
    {
        Forward,
        CornerLeft45,
        CornerLeft90,
        CornerRight45,
        CornerRight90
    }

    [System.Serializable]
    public class TemplateSegment
    {
        public SegmentType type;
        [Min(0.01f)] public float gridLength = 1f; 
        [Min(1)] public int resolution = 4;       
    }

    [Header("Settings")]
    private float gridSize = 0.69f;
    [SerializeField] float _startPositionZ;

    [Header("Gramer Template")]
    public List<TemplateSegment> templateSegments = new();

    private SplineComputer splineComputer;

    [Button("Generate Spline")]
    public void Generate()
    {
        if (splineComputer == null) splineComputer = GetComponent<SplineComputer>();
        if (!splineComputer) return;

        var pts = BuildGramerPath();
        splineComputer.SetPoints(pts);
        splineComputer.Close();
        splineComputer.Rebuild();
    }

    private SplinePoint[] BuildGramerPath()
    {
        List<Vector3> rightSide = new();

        Vector3 currentPos = new(0f, 0f, _startPositionZ);
        float currentAngleDeg = 0f; // 0 Derece = +X Yönü (Sağ)

        rightSide.Add(currentPos);

        foreach (var seg in templateSegments)
        {
            switch (seg.type)
            {
                case SegmentType.Forward:
                    ExecuteForward(rightSide, ref currentPos, currentAngleDeg, seg);
                    break;

                case SegmentType.CornerRight90: ExecuteCorner(rightSide, ref currentPos, ref currentAngleDeg, -90f, seg); break;
                case SegmentType.CornerLeft90:  ExecuteCorner(rightSide, ref currentPos, ref currentAngleDeg, 90f, seg); break;
                case SegmentType.CornerRight45: ExecuteCorner(rightSide, ref currentPos, ref currentAngleDeg, -45f, seg); break;
                case SegmentType.CornerLeft45:  ExecuteCorner(rightSide, ref currentPos, ref currentAngleDeg, 45f, seg); break;
            }
        }

        List<Vector3> finalPoints = new List<Vector3>(rightSide);
        for (int i = rightSide.Count - 2; i >= 1; i--)
        {
            finalPoints.Add(new Vector3(-rightSide[i].x, rightSide[i].y, rightSide[i].z));
        }

        return ConvertToSplinePoints(finalPoints);
    }

    private void ExecuteForward(List<Vector3> points, ref Vector3 currentPos, float angleDeg, TemplateSegment seg)
    {
        float totalLength = seg.gridLength * gridSize;
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

        for (int i = 1; i <= seg.resolution; i++)
        {
            float t = (float)i / seg.resolution;
            points.Add(currentPos + dir * (totalLength * t));
        }

        currentPos += dir * totalLength;
    }

    private void ExecuteCorner(List<Vector3> points, ref Vector3 currentPos, ref float currentAngleDeg, float turnAngleDeg, TemplateSegment seg)
    {
        float radius = seg.gridLength * gridSize;
        float currentAngleRad = currentAngleDeg * Mathf.Deg2Rad;

        // Sağ için -1, Sol için +1
        float turnSign = Mathf.Sign(turnAngleDeg); 
        
        // Pivot (Merkez), dönüş yönüne göre kaleme her zaman diktir (90 derece / PI * 0.5)
        float pivotAngleRad = currentAngleRad + (turnSign * Mathf.PI * 0.5f);

        // HATA BURADAYDI: "-" yerine "+" olmalıydı. Artık pivot tam olması gereken yerde.
        Vector3 center = currentPos + new Vector3(
            Mathf.Cos(pivotAngleRad) * radius,
            0f,
            Mathf.Sin(pivotAngleRad) * radius
        );

        Vector3 toStart = currentPos - center;
        float startArcAngleRad = Mathf.Atan2(toStart.z, toStart.x);
        float totalArcChangeRad = turnAngleDeg * Mathf.Deg2Rad;

        for (int i = 1; i <= seg.resolution; i++)
        {
            float t = (float)i / seg.resolution;
            float currentArcAngleRad = startArcAngleRad + (totalArcChangeRad * t);

            Vector3 arcPos = center + new Vector3(
                Mathf.Cos(currentArcAngleRad) * radius,
                currentPos.y,
                Mathf.Sin(currentArcAngleRad) * radius
            );

            points.Add(arcPos);
        }

        float finalArcAngleRad = startArcAngleRad + totalArcChangeRad;
        currentPos = center + new Vector3(
            Mathf.Cos(finalArcAngleRad) * radius,
            currentPos.y,
            Mathf.Sin(finalArcAngleRad) * radius
        );

        currentAngleDeg += turnAngleDeg;
    }

    private SplinePoint[] ConvertToSplinePoints(List<Vector3> pts)
    {
        SplinePoint[] sp = new SplinePoint[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            sp[i] = new SplinePoint { position = pts[i], color = Color.white, size = 1f, normal = Vector3.up };
        }
        return sp;
    }
}