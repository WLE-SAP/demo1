using UnityEngine;

/// <summary>
/// 物品高亮：0 = 普通，1 = 头部靠近，2 = 当前目标 / 搬运中。
/// 做法是在物品背后加一圈发光底衬，不改动物品本身的颜色，任何底色都看得清。
/// 运行时用 <see cref="Setup"/> 传入美术 key 与程序化底衬（AddComponent 会立刻执行 Awake，
/// 所以不能先 AddComponent 再赋字段）。
/// </summary>
public class Highlighter : MonoBehaviour
{
    [Tooltip("发光底衬用的精灵（圆形或圆角矩形）；运行时由 Setup 按美术 key 决定")]
    public Sprite glowSprite;

    [Header("高亮颜色")]
    public Color nearbyColor = new Color(1f, 0.96f, 0.50f, 0.34f);
    public Color activeColor = new Color(1f, 0.82f, 0.22f, 0.75f);

    [Header("底衬尺寸")]
    [Tooltip("相对物品尺寸的倍数")]
    public float sizeMultiplier = 1.42f;
    [Tooltip("额外增加的世界单位")]
    public Vector2 extraSize = new Vector2(0.07f, 0.07f);
    [Tooltip("被选中时底衬再放大一点点")]
    public float activeScaleBoost = 1.16f;

    SpriteRenderer glow;
    Vector3 glowBaseScale;
    int level = -1;

    public int Level { get { return level; } }
    public bool HasGlow { get { return glow != null; } }

    void Awake()
    {
        // 编辑器里预先挂好并赋了精灵的情况
        if (glowSprite != null) BuildGlow();
        Apply();
    }

    /// <summary>运行时创建：按美术 key 选底衬（放了图片用图片，否则用程序化精灵）并立即生成底衬。</summary>
    public void Setup(string artKey, Sprite fallbackSprite)
    {
        glowSprite = ArtOverride.Or(artKey, fallbackSprite);
        BuildGlow();
        Apply();
    }

    void BuildGlow()
    {
        if (glow != null || glowSprite == null) return;

        Renderer source = GetComponentInChildren<Renderer>();
        if (source == null) return;

        Vector3 worldSize = source.bounds.size;
        if (worldSize.x <= 0.0001f || worldSize.y <= 0.0001f) worldSize = Vector3.one * 0.5f;
        Vector3 rootScale = transform.lossyScale;

        float sx = (worldSize.x * sizeMultiplier + extraSize.x) / Mathf.Max(0.0001f, Mathf.Abs(rootScale.x));
        float sy = (worldSize.y * sizeMultiplier + extraSize.y) / Mathf.Max(0.0001f, Mathf.Abs(rootScale.y));
        glowBaseScale = new Vector3(sx, sy, 1f);

        GameObject go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = glowBaseScale;

        glow = go.AddComponent<SpriteRenderer>();
        glow.sprite = glowSprite;
        glow.color = nearbyColor;
        glow.sortingOrder = source.sortingOrder - 1;
        glow.enabled = false;

        // 若已经挂了 YSort，让它把新底衬纳入深度排序
        YSort sort = GetComponent<YSort>();
        if (sort != null) sort.Cache();
    }

    public void SetLevel(int value)
    {
        if (value == level) return;
        level = value;
        Apply();
    }

    void Apply()
    {
        if (glow == null) return;
        glow.enabled = level > 0;
        if (level <= 0) return;

        glow.color = level == 2 ? activeColor : nearbyColor;
        float boost = level == 2 ? activeScaleBoost : 1f;
        glow.transform.localScale = glowBaseScale * boost;
    }
}
