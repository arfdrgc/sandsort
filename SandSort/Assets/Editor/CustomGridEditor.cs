#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CustomGrid))]
public class CustomGridEditor : Editor
{
    private void OnSceneGUI()
    {
        CustomGrid grid = (CustomGrid)target;

        Handles.color = new Color(1f, 1f, 1f, 0.2f);

        Vector3 origin = grid.transform.position;

        for (int x = 0; x <= grid.width; x++)
        {
            Vector3 start = origin + Vector3.right * (x * grid.cellSize);
            Vector3 end = start + Vector3.forward * (grid.height * grid.cellSize);
            Handles.DrawLine(start, end);
        }

        for (int y = 0; y <= grid.height; y++)
        {
            Vector3 start = origin + Vector3.forward * (y * grid.cellSize);
            Vector3 end = start + Vector3.right * (grid.width * grid.cellSize);
            Handles.DrawLine(start, end);
        }
    }
}
#endif