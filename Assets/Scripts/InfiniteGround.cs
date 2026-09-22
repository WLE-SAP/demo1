using UnityEngine;

/// <summary>
/// 无限地面：让一块很大的平铺草地跟着小虫走，并对齐到贴图网格，
/// 这样地图看起来是无限延伸的（格子不会跟着人滑动）。
/// </summary>
public class InfiniteGround : MonoBehaviour
{
    [Tooltip("跟随的目标（留空自动找小虫）")]
    public Transform target;

    [Tooltip("对齐到多少世界单位（= 一张贴图的尺寸，T_Grass 256px @64ppu = 4）")]
    public float tileSize = 4f;

    [Tooltip("比这更近就不动它，避免每帧微调")]
    public float moveThreshold = 0.001f;

    SpriteRenderer sprite;
    Vector2 last;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        if (target == null)
        {
            BugController bug = FindObjectOfType<BugController>();
            if (bug != null) target = bug.transform;
        }
        last = transform.position;
        Snap();
    }

    void LateUpdate()
    {
        Snap();
    }

    void Snap()
    {
        if (target == null || tileSize <= 0.0001f) return;

        float x = Mathf.Round(target.position.x / tileSize) * tileSize;
        float y = Mathf.Round(target.position.y / tileSize) * tileSize;
        if (Mathf.Abs(x - last.x) < moveThreshold && Mathf.Abs(y - last.y) < moveThreshold) return;

        last = new Vector2(x, y);
        transform.position = new Vector3(x, y, transform.position.z);
    }
}
