using UnityEngine;

/// <summary>
/// 2D 正交俯视跟随相机：平滑跟随角色，并朝角色朝向轻微前移。
/// </summary>
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public BugController bug;

    [Tooltip("跟随偏移")]
    public Vector2 offset = Vector2.zero;
    public float smooth = 8f;
    [Tooltip("朝角色朝向前移的距离，方便看到前方")]
    public float aimLead = 0.6f;
    [Tooltip("相机所在的 Z（2D 下保持负值）")]
    public float cameraZ = -10f;

    void Awake()
    {
        if (target == null)
        {
            BugController b = FindObjectOfType<BugController>();
            if (b != null) target = b.transform;
        }
        if (bug == null) bug = FindObjectOfType<BugController>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector2 desired = (Vector2)target.position + offset;
        if (bug != null) desired += bug.Facing * aimLead;

        Vector3 p = transform.position;
        Vector2 current = new Vector2(p.x, p.y);
        Vector2 next = Vector2.Lerp(current, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        transform.position = new Vector3(next.x, next.y, cameraZ);
    }

    /// <summary>立刻贴到目标上（走地道瞬移之后用，免得镜头一路拉过去）。</summary>
    public void Snap()
    {
        if (target == null) return;
        Vector2 desired = (Vector2)target.position + offset;
        if (bug != null) desired += bug.Facing * aimLead;
        transform.position = new Vector3(desired.x, desired.y, cameraZ);
    }
}
