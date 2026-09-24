using UnityEngine;

/// <summary>
/// 程序化精灵的「形状」。新增食物 / 可交互物品时挑一个形状当默认外观即可，
/// 玩家在 <c>Resources/ArtOverride</c> 里放了对应 key 的图片就会把它顶掉（见 <see cref="ArtOverride"/>）。
/// </summary>
public enum ArtShape
{
    /// <summary>正圆（S_Disc）：果子、石头、光晕、圆形底衬……</summary>
    Round,
    /// <summary>方块（S_Rect）：木箱、牌子、柱子……</summary>
    Rect,
    /// <summary>圆角矩形（S_RoundRect）：面板、箱子底衬……</summary>
    RoundRect,
    /// <summary>项目自带的果子图元（S_FoodBerry）。</summary>
    Berry,
    /// <summary>项目自带的叶子图元（S_FoodLeaf）。</summary>
    Leaf
}

/// <summary>
/// 形状 → 程序化精灵的登记表。由 <see cref="VillageGenerator"/> 在 Awake 里把自己的精灵登记进来，
/// 之后 <see cref="FoodCatalog"/> / <see cref="ItemCatalog"/> 造物体时按形状取用。
/// </summary>
public static class ArtShapes
{
    static Sprite round;
    static Sprite rect;
    static Sprite roundRect;
    static Sprite berry;
    static Sprite leaf;

    /// <summary>登记各形状用的精灵（由 <see cref="VillageGenerator"/> 调用一次）。</summary>
    public static void Use(Sprite disc, Sprite rectangle, Sprite roundRectangle, Sprite berrySprite, Sprite leafSprite)
    {
        round = disc;
        rect = rectangle;
        roundRect = roundRectangle;
        berry = berrySprite;
        leaf = leafSprite;
    }

    /// <summary>取某个形状的精灵；没登记过的形状退回圆形 / 矩形，不会返回 null（除非一个都没登记）。</summary>
    public static Sprite Get(ArtShape shape)
    {
        switch (shape)
        {
            case ArtShape.Rect: return rect != null ? rect : round;
            case ArtShape.RoundRect: return roundRect != null ? roundRect : rect;
            case ArtShape.Berry: return berry != null ? berry : round;
            case ArtShape.Leaf: return leaf != null ? leaf : round;
            default: return round != null ? round : rect;
        }
    }

    /// <summary>
    /// 造一个精灵子物体 —— **拼组合外观时用**（给果实加一圈光晕、给箱子加一条封条之类）。
    /// 位置与大小都是父物体的本地单位；<paramref name="artKey"/> 非空时会走美术替换。
    /// </summary>
    public static SpriteRenderer AddSprite(Transform parent, string name, ArtShape shape, float scale, Vector2 localPos, Color color, int order, string artKey = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Get(shape);
        sr.color = color;
        sr.sortingOrder = order;
        if (!string.IsNullOrEmpty(artKey)) ArtOverride.Apply(sr, artKey);
        return sr;
    }
}
