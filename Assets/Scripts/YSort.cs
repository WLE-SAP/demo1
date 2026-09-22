using UnityEngine;

/// <summary>
/// 俯视 2D 的深度排序：按世界 Y 计算 sortingOrder，越低（越靠前）画得越靠上。
/// 各 SpriteRenderer 的初始 sortingOrder 会作为层级内的相对偏移保留。
/// </summary>
public class YSort : MonoBehaviour
{
    public int baseOrder = 1000;
    public float scale = 10f;

    SpriteRenderer[] renderers;
    int[] offsets;

    void Start()
    {
        // 放在 Start 里缓存，确保 Highlighter 等在 Awake 中新增的子物体也被计入
        Cache();
    }

    public void Cache()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        offsets = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) offsets[i] = renderers[i].sortingOrder;
    }

    void LateUpdate()
    {
        if (renderers == null) return;
        int order = baseOrder - Mathf.RoundToInt(transform.position.y * scale);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sortingOrder = order + offsets[i];
        }
    }
}
