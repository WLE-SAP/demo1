using UnityEngine;

/// <summary>
/// 俯视 2D 的深度排序：按世界 Y 计算 sortingOrder，越低（越靠前）画得越靠上。
///
/// **各渲染器的「初始 sortingOrder」会被当成层级内的相对偏移保留** —— 所以挂 YSort 的物体，
/// 子渲染器只该写一个**很小的**偏移：这里的 scale 是「每世界单位 10 档」，写 20 就等于
/// 「硬往前挤 2 米」，房子 / 树当然挡不住它（2026-09-26 用户报的
/// 「虫子头部的显示优先级太高，场地挡不住」就是头部写了 20、尾巴写了 10）；
///
/// **缓存的是 <see cref="Renderer"/> 而不是 <see cref="SpriteRenderer"/>**：小虫的尾巴是
/// <see cref="LineRenderer"/>，只认 SpriteRenderer 的话它根本进不了排序表，
/// 会永远停在写死的那个绝对序号上（= 一直画在整个世界后面）。
/// </summary>
public class YSort : MonoBehaviour
{
    public int baseOrder = 1000;
    public float scale = 10f;

    Renderer[] renderers;
    int[] offsets;

    void Start()
    {
        // 放在 Start 里缓存，确保 Highlighter 等在 Awake 中新增的子物体也被计入
        Cache();
    }

    public void Cache()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
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
