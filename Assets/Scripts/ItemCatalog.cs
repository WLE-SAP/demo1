using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一种**可交互物品**（木箱 / 木桶 / 石碑 / 你自己加的东西）。默认是「可搬动」的，
/// 也可以设成不可搬只可吃、或者两个都关掉当纯景物。
///
/// 加一种新物品的步骤和食物一样：写定义 → <c>ItemCatalog.Register(def)</c>
/// （详见 <see cref="FoodCatalog"/> 的说明）。可搬动的物品会自动被
/// <see cref="DragController"/>（F 键拾取）与 <see cref="ProximityHighlight"/>（高亮）接管，
/// 所以注册完就能玩，不需要改别的地方。
/// </summary>
public class ItemDefinition : IContentDefinition
{
    // ---------------- 身份 ----------------
    /// <summary>唯一 id（同时是物体名）。</summary>
    public string id = "item";

    /// <summary>美术 key（<see cref="ArtKeys"/> 里的常量）。留空 = 不做图片替换。</summary>
    public string artKey;

    // ---------------- 外观 ----------------
    /// <summary>没放图片时用哪个程序化形状画它。</summary>
    public ArtShape shape = ArtShape.Rect;

    /// <summary>直接指定精灵（比 <see cref="shape"/> 优先）。</summary>
    public Sprite sprite;

    /// <summary>按九宫格（Sliced）拉伸，配合下面的大小范围使用。</summary>
    public bool sliced = true;

    /// <summary>大小范围（世界单位，每次生成随机取一个）。</summary>
    public float sizeMin = 0.6f;
    public float sizeMax = 0.9f;

    /// <summary>颜色（程序化外观用；放了图片时会被刷成白色）。</summary>
    public Color color = Color.white;

    /// <summary>初始排序偏移（挂了 <see cref="YSort"/> 时它只是层级内的相对偏移）。</summary>
    public int orderOffset = 10;

    // ---------------- 交互 ----------------
    /// <summary>能不能按 F 搬起来。</summary>
    public bool draggable = true;

    /// <summary>搬运迟滞：数值越大越不跟手（<see cref="Draggable.weight"/>）。</summary>
    public float carryWeight = 1f;

    /// <summary>能不能吃（挂了 <see cref="Edible"/>）。</summary>
    public bool edible;

    /// <summary>吃掉得到的营养 / 分数。</summary>
    public int nutrition = 3;

    /// <summary>需要小虫长到几级才吃得下（0 = 一开始就能吃；门槛用 <see cref="BugGrowth"/> 里的常量）。</summary>
    public int requiredLevel = 2;

    /// <summary>吃下去长大几级（普通物品 0）。</summary>
    public int growth;

    /// <summary>固定恢复多少体力；-1 = 按隐藏数值「分量」算。</summary>
    public int satiety = -1;

    /// <summary>隐藏数值「分量」的范围（<see cref="satiety"/> 为 -1 时决定恢复多少体力）。</summary>
    public int satietyMin = 12;
    public int satietyMax = 19;

    /// <summary>隐藏数值的编辑器备注（不显示给玩家）。</summary>
    public string hiddenNote = "";

    // ---------------- 悬浮窗 ----------------
    public string title = "道具";
    public string kind = "道具";
    public string description = "";

    /// <summary>高亮底衬用圆角矩形（默认圆形）。</summary>
    public bool rectHighlight;

    /// <summary>参与俯视深度排序（可搬动的物品都该开着，不然会被别的东西挡错）。</summary>
    public bool useYSort = true;

    // ---------------- 生成 ----------------
    /// <summary>在区块里怎么刷（见 <see cref="SpawnRule"/>）。</summary>
    public SpawnRule spawn = SpawnRule.Pool(1f, 0.45f, 0.25f, 0.55f);

    /// <summary>高级用法：物体造好后再自己补东西（加子精灵 / 加组件都行）。见 <see cref="ArtShapes.AddSprite"/>。</summary>
    public System.Action<GameObject> decorate;

    public string Id { get { return id; } }
    public SpawnRule Spawn { get { return spawn; } }

    public GameObject Create(Transform parent, Vector2 position, int yOrder)
    {
        return ItemCatalog.Create(this, parent, position, yOrder);
    }
}

/// <summary>内置可交互物品的 id。</summary>
public static class ItemIds
{
    public const string Crate = "crate";
}

/// <summary>
/// 可交互物品目录：**新增物品只需要在这里（或你自己的脚本里）Register 一行**。
/// 与 <see cref="FoodCatalog"/> 完全对称，同样由 <see cref="VillageGenerator"/> 自动刷出来。
/// </summary>
public static class ItemCatalog
{
    /// <summary>当前所有物品定义。</summary>
    public static readonly List<ItemDefinition> All = new List<ItemDefinition>();

    static bool defaultsLoaded;

    /// <summary>注册一种物品；同 id 会覆盖。</summary>
    public static void Register(ItemDefinition definition)
    {
        if (definition == null) return;
        if (string.IsNullOrEmpty(definition.id))
        {
            Debug.LogWarning("[Content] 物品定义没有 id，已忽略。", null);
            return;
        }

        ItemDefinition existing = Find(definition.id);
        if (existing != null) All[All.IndexOf(existing)] = definition;
        else All.Add(definition);
    }

    /// <summary>按 id 找一种物品；没有返回 null。</summary>
    public static ItemDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].id == id) return All[i];
        return null;
    }

    /// <summary>移除一种物品；返回是否真的移除了。</summary>
    public static bool Unregister(string id)
    {
        ItemDefinition found = Find(id);
        if (found == null) return false;
        All.Remove(found);
        return true;
    }

    /// <summary>改某种物品的「固定生成几率」。</summary>
    public static bool SetChance(string id, float chance)
    {
        ItemDefinition found = Find(id);
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
    /// 装上内置物品（由 <see cref="VillageGenerator"/> 在 Awake 调用一次）。
    /// **同 id 已存在时不覆盖**，所以玩家可以自己 Register 一个同 id 的定义来替换内置的。
    /// </summary>
    public static void EnsureDefaults()
    {
        if (defaultsLoaded) return;
        defaultsLoaded = true;

        if (Find(ItemIds.Crate) == null) Register(MakeCrate());

        Debug.Log("[Content] 可交互物品 " + All.Count + " 种：" + IdList());
    }

    /// <summary>按定义造一个物品（挂在 <paramref name="parent"/> 下，位置用世界坐标）。</summary>
    public static GameObject Create(ItemDefinition definition, Transform parent, Vector2 position, int yOrder)
    {
        GameObject go = new GameObject(string.IsNullOrEmpty(definition.id) ? "Item" : definition.id);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        float size = Mathf.Max(0.05f, Random.Range(definition.sizeMin, Mathf.Max(definition.sizeMin, definition.sizeMax)));

        // 「逻辑在根上、外观在 Visual 子物体上」
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * size;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = definition.sprite != null ? definition.sprite : ArtShapes.Get(definition.shape);
        sr.drawMode = definition.sliced ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
        if (definition.sliced) sr.size = Vector2.one;
        sr.color = definition.color;
        sr.sortingOrder = yOrder + definition.orderOffset;
        if (!string.IsNullOrEmpty(definition.artKey)) ArtOverride.Apply(sr, definition.artKey);

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * size;

        if (definition.draggable)
        {
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            go.AddComponent<Draggable>().weight = definition.carryWeight;
        }

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = definition.satiety >= 0 ? definition.satiety
                                              : Random.Range(definition.satietyMin, definition.satietyMax + 1);
        hidden.note = definition.hiddenNote;

        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = definition.title;
        info.kind = definition.kind;
        info.description = definition.description;

        if (definition.edible)
        {
            Edible edible = go.AddComponent<Edible>();
            edible.nutrition = definition.nutrition;
            edible.satiety = definition.satiety;
            edible.growth = definition.growth;
            edible.requiredLevel = definition.requiredLevel;
        }

        // Highlighter 要在 YSort 之前加，保证它生成的发光底衬也被纳入深度排序
        go.AddComponent<Highlighter>().Setup(
            definition.rectHighlight ? ArtKeys.HighlightRect : ArtKeys.Highlight,
            ArtShapes.Get(definition.rectHighlight ? ArtShape.RoundRect : ArtShape.Round));

        if (definition.useYSort) go.AddComponent<YSort>();
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

    // ---------------- 内置物品 ----------------

    /// <summary>木箱：能搬、长到 2 级也能啃（只在村庄区块刷）。</summary>
    static ItemDefinition MakeCrate()
    {
        return new ItemDefinition
        {
            id = ItemIds.Crate,
            artKey = ArtKeys.Crate,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.6f,
            sizeMax = 0.9f,
            color = new Color(0.66f, 0.48f, 0.27f),
            draggable = true,
            carryWeight = 1f,
            edible = true,
            nutrition = 3,
            requiredLevel = BugGrowth.CrateLevel,
            satietyMin = 12,
            satietyMax = 19,
            hiddenNote = "木箱：可搬动的道具",
            title = "木箱",
            kind = "道具",
            description = "搬到哪算哪的箱子。站在它前面按 F 就能搬起来，搬运时小虫会变慢；"
                + "长到 " + BugGrowth.CrateLevel + " 级以后可以直接啃掉。",
            spawn = new SpawnRule { weight = 1f, clearance = 0.45f, padding = 0.25f, footprint = 0.55f, hamletOnly = true }
        };
    }
}
