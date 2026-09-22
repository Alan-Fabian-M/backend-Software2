using UnityEngine;

/// <summary>
/// Draws wireframe bounding boxes around objects during scene generation preview.
/// Provides visual feedback that objects are being detected and instantiated.
/// </summary>
public class WireframeDrawer : MonoBehaviour
{
    /// <summary>
    /// Draws a wireframe bounding box around an object's bounds.
    /// Uses Debug.DrawLine which only shows in Scene view unless showInGame is true.
    /// </summary>
    public static void DrawBounds(GameObject obj, Color color, float duration)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
            renderer = obj.GetComponentInChildren<Renderer>();

        if (renderer == null)
            return;

        Bounds bounds = renderer.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        // Define the 8 corners of the bounding box
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
        corners[1] = center + new Vector3(extents.x, -extents.y, -extents.z);
        corners[2] = center + new Vector3(extents.x, extents.y, -extents.z);
        corners[3] = center + new Vector3(-extents.x, extents.y, -extents.z);
        corners[4] = center + new Vector3(-extents.x, -extents.y, extents.z);
        corners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
        corners[6] = center + new Vector3(extents.x, extents.y, extents.z);
        corners[7] = center + new Vector3(-extents.x, extents.y, extents.z);

        // Draw the 12 edges of the box
        // Bottom face
        Debug.DrawLine(corners[0], corners[1], color, duration);
        Debug.DrawLine(corners[1], corners[2], color, duration);
        Debug.DrawLine(corners[2], corners[3], color, duration);
        Debug.DrawLine(corners[3], corners[0], color, duration);

        // Top face
        Debug.DrawLine(corners[4], corners[5], color, duration);
        Debug.DrawLine(corners[5], corners[6], color, duration);
        Debug.DrawLine(corners[6], corners[7], color, duration);
        Debug.DrawLine(corners[7], corners[4], color, duration);

        // Vertical edges
        Debug.DrawLine(corners[0], corners[4], color, duration);
        Debug.DrawLine(corners[1], corners[5], color, duration);
        Debug.DrawLine(corners[2], corners[6], color, duration);
        Debug.DrawLine(corners[3], corners[7], color, duration);
    }

    /// <summary>
    /// Draws a wireframe box from transform scale (cube-like visualization).
    /// Useful for primitive geometry visualization.
    /// </summary>
    public static void DrawTransformBox(Transform transform, Color color, float duration)
    {
        Vector3 center = transform.position;
        Vector3 extents = transform.localScale * 0.5f;

        // Local to world transformation
        Vector3[] localCorners = new Vector3[8];
        localCorners[0] = new Vector3(-extents.x, -extents.y, -extents.z);
        localCorners[1] = new Vector3(extents.x, -extents.y, -extents.z);
        localCorners[2] = new Vector3(extents.x, extents.y, -extents.z);
        localCorners[3] = new Vector3(-extents.x, extents.y, -extents.z);
        localCorners[4] = new Vector3(-extents.x, -extents.y, extents.z);
        localCorners[5] = new Vector3(extents.x, -extents.y, extents.z);
        localCorners[6] = new Vector3(extents.x, extents.y, extents.z);
        localCorners[7] = new Vector3(-extents.x, extents.y, extents.z);

        // Transform local to world
        Vector3[] worldCorners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            worldCorners[i] = transform.TransformPoint(localCorners[i]);
        }

        // Draw the 12 edges
        // Bottom face
        Debug.DrawLine(worldCorners[0], worldCorners[1], color, duration);
        Debug.DrawLine(worldCorners[1], worldCorners[2], color, duration);
        Debug.DrawLine(worldCorners[2], worldCorners[3], color, duration);
        Debug.DrawLine(worldCorners[3], worldCorners[0], color, duration);

        // Top face
        Debug.DrawLine(worldCorners[4], worldCorners[5], color, duration);
        Debug.DrawLine(worldCorners[5], worldCorners[6], color, duration);
        Debug.DrawLine(worldCorners[6], worldCorners[7], color, duration);
        Debug.DrawLine(worldCorners[7], worldCorners[4], color, duration);

        // Vertical edges
        Debug.DrawLine(worldCorners[0], worldCorners[4], color, duration);
        Debug.DrawLine(worldCorners[1], worldCorners[5], color, duration);
        Debug.DrawLine(worldCorners[2], worldCorners[6], color, duration);
        Debug.DrawLine(worldCorners[3], worldCorners[7], color, duration);
    }

    /// <summary>
    /// Draws a wireframe sphere around an object's position for debugging.
    /// </summary>
    public static void DrawSphere(Vector3 center, float radius, Color color, float duration)
    {
        int segments = 16;
        float segmentAngle = 360f / segments;

        // Draw horizontal circles
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * segmentAngle * Mathf.Deg2Rad;
            float angle2 = (i + 1) * segmentAngle * Mathf.Deg2Rad;

            Vector3 p1 = center + new Vector3(
                Mathf.Cos(angle1) * radius,
                0,
                Mathf.Sin(angle1) * radius
            );
            Vector3 p2 = center + new Vector3(
                Mathf.Cos(angle2) * radius,
                0,
                Mathf.Sin(angle2) * radius
            );

            Debug.DrawLine(p1, p2, color, duration);
        }
    }
}
