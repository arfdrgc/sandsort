using UnityEngine;

[ExecuteAlways]
public class CustomGrid : MonoBehaviour
{
    public float cellSize = 0.8f;
    public int width = 10;
    public int height = 10;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 1f);

        // GRID CENTER OFFSET (X ortalanır, Z aşağı gider)
        Vector3 origin = transform.position;

        float totalWidth = width * cellSize;
        float totalHeight = height * cellSize;

        Vector3 start = origin;

        // X eksenini ortala
        start.x -= totalWidth * 0.5f;

        // Z eksenini yukarıdan başlat (ortadan değil, üstten aşağı -Z)
        start.z -= 0.9f;

        // Vertical lines (X direction)
        for (int x = 0; x <= width; x++)
        {
            Vector3 lineStart = start + Vector3.right * (x * cellSize);
            Vector3 lineEnd = lineStart + Vector3.forward * (-totalHeight);

            Gizmos.DrawLine(lineStart, lineEnd);
        }

        // Horizontal lines (Z direction)
        for (int y = 0; y <= height; y++)
        {
            Vector3 lineStart = start + Vector3.forward * (-y * cellSize);
            Vector3 lineEnd = lineStart + Vector3.right * totalWidth;

            Gizmos.DrawLine(lineStart, lineEnd);
        }
    }
}