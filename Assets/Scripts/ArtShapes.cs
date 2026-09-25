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
    /// 整图素材装进 <paramref name="footprint"/> 正方形后的实际尺寸
    /// （**contain**：长的那个方向填满，另一个方向按比例留白，绝不拉伸）。
    /// </summary>
    public static Vector2 FitWholeImage(Sprite image, float footprint)
    {
        if (image == null) return new Vector2(footprint, footprint);

        float aspect = image.rect.width / Mathf.Max(1f, image.rect.height);
        float height = Mathf.Min(footprint, footprint / Mathf.Max(0.0001f, aspect));
        return new Vector2(height * aspect, height);
    }

    /// <summary>
    /// 造一个**整图素材**的精灵（树 / 石头 / 灌木这类「一张图 = 一个完整物件」）。
    ///
    /// 摆放规则（和整栋建筑同一套）：按图片**内容的宽高比**装进 <paramref name="footprint"/> 正方形，
    /// **底边压在占地的南边**（物件「立」在地上），水平居中。
    /// 尺寸走 <c>SpriteRenderer.size</c>（世界单位），所以与导入的 Pixels Per Unit 无关 ——
    /// 整图素材在导入时已经按内容裁掉了透明边（见 <see cref="ArtOverride.Slot.whole"/>），
    /// 这里拿到的宽高比就是物体本身的比例，交 64px 还是 256px 都不会变形。
    /// </summary>
    public static SpriteRenderer AddWholeImage(Transform parent, string name, Sprite image, float footprint, Vector2 localPos, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Vector2 size = FitWholeImage(image, footprint);
        go.transform.localPosition = new Vector2(localPos.x, localPos.y - footprint * 0.5f + size.y * 0.5f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = image;
        sr.color = Color.white;                     // 整图就是最终外观，不做代码染色
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        return sr;
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
