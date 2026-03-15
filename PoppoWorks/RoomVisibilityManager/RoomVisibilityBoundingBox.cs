using UnityEngine;

[ExecuteAlways]
public class RoomVisibilityBoundingBox : MonoBehaviour
{
    [Tooltip("このバウンディングボックスが担当する部屋ルートです。")]
    public GameObject roomRoot;

    [Tooltip("Scene ビューで Gizmo を表示するかです。")]
    public bool showGizmo = true;

    [Tooltip("面を塗って表示するかです。OFF の場合は枠線のみ表示します。")]
    public bool fillGizmo = false;

    [Tooltip("Scene ビューでの表示色です。")]
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.18f);

    private void OnEnable()
    {
    }

    private void OnDrawGizmos()
    {
        DrawBoundingBox(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawBoundingBox(true);
    }

    private void DrawBoundingBox(bool selected)
    {
        if (!enabled || !showGizmo)
        {
            return;
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;

        Color baseColor = gizmoColor;
        if (selected)
        {
            baseColor.a = 0.32f;
        }

        if (fillGizmo)
        {
            Gizmos.color = baseColor;
            Gizmos.DrawCube(box.center, box.size);
        }

        Color wireColor = baseColor;
        wireColor.a = 0.95f;
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
