using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一种**能吃的东西**（果子 / 嫩叶 / 神奇果实 / 你自己加的东西）。
///
/// 加一种新食物只要三步：
/// <list type="number">
/// <item>（可选）画一张图丢进 <c>Assets/Resources/ArtOverride/</c>，并在 <see cref="ArtKeys"/> 与
///       <see cref="ArtOverride.Slots"/> 加一行（不加 key 也能用，走程序化外观）；</item>
/// <item>写一个定义：<c>new FoodDefinition { id = "mushroom", artKey = "mushroom", nutrition = 1, ... }</c>；</item>
/// <item>注册：<c>FoodCatalog.Register(def)</c>（写在 <see cref="FoodCatalog.Defaults"/> 里，
///       或自己的脚本里用 <c>[RuntimeInitializeOnLoadMethod]</c> 调用）。</item>
/// </list>
/// 吃的逻辑（<see cref="BugEat"/>）与悬浮窗（<see cref="EntityInfo"/>）、高亮（<see cref="Highlighter"/>）
/// 都是按组件找的，所以注册完就自动生效，不需要改别的地方。
/// </summary>
public class FoodDefinition : IContentDefinition
{
    // ---------------- 身份 ----------------
    /// <summary>唯一 id（同时是物体名）。</summary>
    public string id = "food";

    /// <summary>美术 key（<see cref="ArtKeys"/> 里的常量）。留空 = 不做图片替换。</summary>
    public string artKey;

    // ---------------- 外观 ----------------
    /// <summary>没放图片时用哪个程序化形状画它。</summary>
    public ArtShape shape = ArtShape.Round;

    /// <summary>直接指定精灵（比 <see cref="shape"/> 优先，可以配合 <c>Resources.Load</c> 用）。</summary>
    public Sprite sprite;

    /// <summary>基准大小（世界单位）。</summary>
    public float size = 0.3f;

    /// <summary>大小随机浮动比例（0.2 = 上下浮动 20%）；0 = 固定大小。</summary>
    public float sizeJitter;

    /// <summary>高亮底衬用圆角矩形（默认圆形）。</summary>
    public bool rectHighlight;

    // ---------------- 吃 ----------------
    /// <summary>吃掉得到的营养 / 分数。</summary>
    public int nutrition = 1;

    /// <summary>需要小虫长到几级才吃得下（0 = 一开始就能吃；门槛用 <see cref="BugGrowth"/> 里的常量）。</summary>
    public int requiredLevel;

    /// <summary>吃下去长大几级（普通食物 0）。</summary>
    public int growth;

    /// <summary>固定恢复多少体力；-1 = 按隐藏数值「分量」算。</summary>
    public int satiety = -1;

    /// <summary>隐藏数值「分量」的范围（<see cref="satiety"/> 为 -1 时决定恢复多少体力）。</summary>
    public int satietyMin = 6;
    public int satietyMax = 11;

    /// <summary>隐藏数值的编辑器备注（不显示给玩家）。</summary>
    public string hiddenNote = "";

    /// <summary>触发碰撞体半径，**按物体大小算**（0.5 = 物体本身的一半）。</summary>
    public float colliderRadius = 0.5f;

    // ---------------- 悬浮窗 ----------------
    public string title = "食物";
    public string kind = "食物";
    public string description = "";

    // ---------------- 生成 ----------------
    /// <summary>在区块里怎么刷（见 <see cref="SpawnRule"/>）。</summary>
    public SpawnRule spawn = SpawnRule.Pool(1f);

    /// <summary>
    /// 高级用法：物体造好后再自己补点东西（加子精灵 / 加组件都行）。
    /// 拼组合外观时用它，例如神奇果实的光晕与星星（见 <see cref="FoodCatalog"/> 里的例子）。
    /// 子精灵请用 <see cref="ArtShapes.AddSprite"/>，排序取本体的 <c>sortingOrder</c> 加减。
    /// </summary>
    public System.Action<GameObject> decorate;

    public string Id { get { return id; } }
    public SpawnRule Spawn { get { return spawn; } }

    public GameObject Create(Transform parent, Vector2 position, int yOrder)
    {
        return FoodCatalog.Create(this, parent, position, yOrder);
    }
}

/// <summary>内置食物的 id（<see cref="FoodCatalog.SetChance"/> 之类要按 id 找它们时用）。</summary>
public static class FoodIds
{
    public const string Berry = "berry";
    public const string Leaf = "leaf";
    public const string SpecialFood = "special_food";
}

/// <summary>
/// 食物目录：**新增食物只需要在这里（或你自己的脚本里）Register 一行**。
/// 世界生成（<see cref="VillageGenerator"/>）按 <see cref="SpawnRule"/> 从这里取内容，
/// 所以注册完就自动会刷出来，不需要改生成器。
/// </summary>
public static class FoodCatalog
{
    /// <summary>当前所有食物定义（顺序 = 抽签时的先后，不影响权重计算）。</summary>
    public static readonly List<FoodDefinition> All = new List<FoodDefinition>();

    static bool defaultsLoaded;

    /// <summary>注册一种食物；同 id 会**覆盖**（方便改内置食物，比如把果子换成别的东西）。</summary>
    public static void Register(FoodDefinition definition)
    {
        if (definition == null) return;
        if (string.IsNullOrEmpty(definition.id))
        {
            Debug.LogWarning("[Content] 食物定义没有 id，已忽略。", null);
            return;
        }

        FoodDefinition existing = Find(definition.id);
        if (existing != null) All[All.IndexOf(existing)] = definition;
        else All.Add(definition);
    }

    /// <summary>按 id 找一种食物；没有返回 null。</summary>
    public static FoodDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].id == id) return All[i];
        return null;
    }

    /// <summary>移除一种食物；返回是否真的移除了。</summary>
    public static bool Unregister(string id)
    {
        FoodDefinition found = Find(id);
        if (found == null) return false;
        All.Remove(found);
        return true;
    }

    /// <summary>改某种食物的「固定生成几率」（地图类型用它调节稀有食物，比如神奇果实）。</summary>
    public static bool SetChance(string id, float chance)
    {
        FoodDefinition found = Find(id);
        if (found == null || found.spawn == null) return false;
        found.spawn.chance = Mathf.Clamp01(chance);
        return true;
    }

    /// <summary>清空目录（一般不用；主要是测试用）。</summary>
    public static void Reset()
    {
        All.Clear();
        defaultsLoaded = false;
    }

    /// <summary>
    /// 装上内置食物（由 <see cref="VillageGenerator"/> 在 Awake 调用一次）。
    /// **同 id 已存在时不覆盖**，所以玩家可以自己 Register 一个同 id 的定义来替换内置的。
    /// </summary>
    public static void EnsureDefaults()
    {
        if (defaultsLoaded) return;
        defaultsLoaded = true;

        if (Find(FoodIds.Berry) == null) Register(MakeBerry());
        if (Find(FoodIds.Leaf) == null) Register(MakeLeaf());
        if (Find(FoodIds.SpecialFood) == null) Register(MakeSpecialFood());

        Debug.Log("[Content] 食物 " + All.Count + " 种：" + IdList());
    }

    /// <summary>按定义造一个食物物体（挂在 <paramref name="parent"/> 下，位置用世界坐标）。</summary>
    public static GameObject Create(FoodDefinition definition, Transform parent, Vector2 position, int yOrder)
    {
        GameObject go = new GameObject(string.IsNullOrEmpty(definition.id) ? "Food" : definition.id);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        float scale = definition.size;
        if (definition.sizeJitter > 0.0001f)
            scale *= 1f + Random.Range(-definition.sizeJitter, definition.sizeJitter);

        // 「逻辑在根上、外观在 Visual 子物体上」：这样组合外观（光晕/星星）用世界单位写就行
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = definition.sprite != null ? definition.sprite : ArtShapes.Get(definition.shape);
        sr.sortingOrder = yOrder + 1;
        if (!string.IsNullOrEmpty(definition.artKey)) ArtOverride.Apply(sr, definition.artKey);

        // 碰撞体跟外观同一个缩放，所以半径按「物体大小」算
        CircleCollider2D collider = visual.AddComponent<CircleCollider2D>();
        collider.radius = definition.colliderRadius;
        collider.isTrigger = true;

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = definition.satiety >= 0 ? definition.satiety
                                               : Random.Range(definition.satietyMin, definition.satietyMax + 1);
        hidden.note = definition.hiddenNote;

        Edible edible = go.AddComponent<Edible>();
        edible.nutrition = definition.nutrition;
        edible.satiety = definition.satiety;
        edible.growth = definition.growth;
        edible.requiredLevel = definition.requiredLevel;

        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = definition.title;
        info.kind = definition.kind;
        info.description = definition.description;

        go.AddComponent<Highlighter>().Setup(
            definition.rectHighlight ? ArtKeys.HighlightRect : ArtKeys.Highlight,
            ArtShapes.Get(definition.rectHighlight ? ArtShape.RoundRect : ArtShape.Round));

        if (definition.decorate != null) definition.decorate(go);
        return go;
    }

    static string IdList()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] == null) continue;
            if (sb.Length > 0) sb.Append("、");
            sb.Append(All[i].id);
        }
        return sb.ToString();
    }

    // ---------------- 内置食物 ----------------

    static FoodDefinition MakeBerry()
    {
        return new FoodDefinition
        {
            id = FoodIds.Berry,
            artKey = ArtKeys.Berry,
            shape = ArtShape.Berry,
            size = 0.26f,
            nutrition = 1,
            satietyMin = 6,
            satietyMax = 11,
            hiddenNote = "果子：小分量",
            title = "野果子",
            kind = "食物",
            description = "随处可见的小果子。恢复的体力不多，但满地都是。",
            spawn = SpawnRule.Pool(66f, 0.3f, 0.1f, 0.35f)
        };
    }

    static FoodDefinition MakeLeaf()
    {
        return new FoodDefinition
        {
            id = FoodIds.Leaf,
            artKey = ArtKeys.Leaf,
            shape = ArtShape.Leaf,
            size = 0.34f,
            nutrition = 1,
            satietyMin = 10,
            satietyMax = 15,
            hiddenNote = "叶子：中等分量",
            title = "嫩叶",
            kind = "食物",
            description = "一片嫩叶，比果子顶饱一些。",
            spawn = SpawnRule.Pool(34f, 0.3f, 0.1f, 0.35f)
        };
    }

    /// <summary>特殊食物：吃掉能让小虫长大（更大 / 更快 / 吃得更远 / 体力上限更高）。</summary>
    static FoodDefinition MakeSpecialFood()
    {
        return new FoodDefinition
        {
            id = FoodIds.SpecialFood,
            artKey = ArtKeys.SpecialFood,
            shape = ArtShape.Round,
            size = 0.74f,
            nutrition = 5,
            growth = 1,
            satietyMin = 22,
            satietyMax = 31,
            hiddenNote = "特殊食物：大分量",
            title = "神奇果实",
            kind = "特殊食物",
            description = "吃下去小虫会长大一截：更大、跑得更快、捕食范围更广、体力上限更高。",
            // 1.08 × 本体 0.74 ≈ 世界半径 0.8（与改造前的数值一致）
            colliderRadius = 1.08f,
            spawn = SpawnRule.Fixed(1, 1, 0.45f, 0.7f, 0.5f, 0.7f),
            decorate = AddSpecialFoodDetail
        };
    }

    /// <summary>神奇果实的额外装饰：一圈光晕 + 内部亮斑 + 两颗小星星。</summary>
    static void AddSpecialFoodDetail(GameObject go)
    {
        SpriteRenderer body = go.GetComponentInChildren<SpriteRenderer>();
        int order = body != null ? body.sortingOrder : SpawnKit.YOrder(go.transform.position.y) + 1;

        ArtShapes.AddSprite(go.transform, "Halo", ArtShape.Round, 1.55f, Vector2.zero, new Color(1f, 0.84f, 0.35f, 0.30f), order - 1);
        ArtShapes.AddSprite(go.transform, "Inner", ArtShape.Round, 0.44f, new Vector2(-0.05f, 0.06f), new Color(1f, 0.95f, 0.72f), order + 1);
        ArtShapes.AddSprite(go.transform, "SparkA", ArtShape.Round, 0.18f, new Vector2(0.52f, 0.44f), new Color(1f, 0.97f, 0.85f), order + 1);
        ArtShapes.AddSprite(go.transform, "SparkB", ArtShape.Round, 0.13f, new Vector2(-0.5f, -0.42f), new Color(1f, 0.97f, 0.85f), order + 1);
    }
}
