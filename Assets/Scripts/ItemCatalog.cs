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

    // —— 会引发连锁的东西（见 Spec §4.8）——
    /// <summary>电线：吃掉 / 腐蚀 / 打碎 → 附近的机器一起断电。</summary>
    public const string Wire = "wire";
    /// <summary>抽水泵：断电就停，然后开始漏水（链 A 的第二环）。</summary>
    public const string Pump = "pump";
    /// <summary>油桶：被电击 / 被打碎就炸（链 B 的第一环）。</summary>
    public const string OilBarrel = "oilbarrel";
    /// <summary>警报器：被电击 / 被炸到就响，全村撤离（链 B 的最后一环）。</summary>
    public const string Alarm = "alarm";
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

        // 会引发连锁的东西（电线 → 断电 → 漏水 → 滑倒；油桶 → 爆炸 → 警报 → 撤离）
        if (Find(ItemIds.Wire) == null) Register(MakeWire());
        if (Find(ItemIds.Pump) == null) Register(MakePump());
        if (Find(ItemIds.OilBarrel) == null) Register(MakeOilBarrel());
        if (Find(ItemIds.Alarm) == null) Register(MakeAlarm());

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

    /// <summary>木箱：能搬、长到 2 级也能啃、**还能被打碎**（只在村庄区块刷）。</summary>
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
                + "长到 " + BugGrowth.CrateLevel + " 级以后可以直接啃掉，也可以撞碎 / 腐蚀掉。",
            spawn = new SpawnRule { weight = 1f, clearance = 0.45f, padding = 0.25f, footprint = 0.55f, hamletOnly = true },
            decorate = go =>
            {
                // 木箱也能被打碎（撞 / 电击 / 爆炸 / 腐蚀都算），碎了一地木屑
                Breakable breakable = go.AddComponent<Breakable>();
                breakable.hp = 2f;
                breakable.debrisCount = 8;
                breakable.debrisColor = new Color(0.62f, 0.45f, 0.26f);
            }
        };
    }

    // ---------------- 会引发连锁的东西 ----------------
    //
    // 共同的约定（Spec §4.8）：这些物件都**不能搬**（免得玩家把整条链子搬走）、**都能被打碎**，
    // 「谁连到谁」按半径就近绑定，不做 id / 连线编辑器。

    /// <summary>电线：长到 2 级可以啃掉它，一啃附近的机器就断电（链 A 的第一环）。</summary>
    static ItemDefinition MakeWire()
    {
        return new ItemDefinition
        {
            id = ItemIds.Wire,
            artKey = ArtKeys.Wire,
            shape = ArtShape.Rect,
            sliced = false,
            sizeMin = 0.55f,
            sizeMax = 0.75f,
            color = new Color(0.24f, 0.24f, 0.28f),
            draggable = false,
            edible = true,
            nutrition = 2,
            requiredLevel = BugGrowth.CrateLevel,
            satietyMin = 8,
            satietyMax = 13,
            hiddenNote = "电线：一股铜腥味",
            title = "电线",
            kind = "设施",
            description = "从谁家墙上牵出来的一截电线。啃断它，附近靠它供电的东西就会停下来。",
            useYSort = false,
            spawn = new SpawnRule { weight = 0.9f, clearance = 0.4f, padding = 0.2f, footprint = 0.4f, hamletOnly = true },
            decorate = go =>
            {
                // 吃掉它 = 剪断线路（用 onConsumed，而不是 OnDestroy：区块回收不该断电，红线 22）
                ElectricWire wire = go.AddComponent<ElectricWire>();
                Edible edible = go.GetComponent<Edible>();
                if (edible != null) edible.onConsumed += _ => wire.CutPower();
            }
        };
    }

    /// <summary>抽水泵：通电时正常，**断电就停、管子开始漏水**（链 A 的第二环）。</summary>
    static ItemDefinition MakePump()
    {
        return new ItemDefinition
        {
            id = ItemIds.Pump,
            artKey = ArtKeys.Pump,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.9f,
            sizeMax = 1.1f,
            color = new Color(0.42f, 0.46f, 0.52f),
            draggable = false,
            title = "抽水泵",
            kind = "设施",
            description = "嗡嗡作响的抽水泵。要是它停了，接的管子大概就要开始漏水了。",
            // 故障链 A 是「村里的事故」：泵只在有电线、有人的村庄刷（荒野没电线，泵永远漏不了）
            spawn = SpawnRule.Fixed(1, 1, 0.55f, 0.9f, 0.6f, 0.9f)
                .InSettlements(SettlementKind.Village, SettlementKind.City),
            decorate = go =>
            {
                go.AddComponent<Breakable>().hp = 3f;      // 结实一点，得撞好几下才碎
                PoweredProp prop = go.AddComponent<PoweredProp>();
                prop.offColor = new Color(0.5f, 0.52f, 0.55f);
                WaterSource source = go.AddComponent<WaterSource>();
                source.leakWhenUnpowered = true;
                source.maxPuddles = 3;
            }
        };
    }

    /// <summary>油桶：被电击 / 被打碎就炸（链 B 的第一环）。</summary>
    static ItemDefinition MakeOilBarrel()
    {
        return new ItemDefinition
        {
            id = ItemIds.OilBarrel,
            artKey = ArtKeys.OilBarrel,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.75f,
            sizeMax = 0.9f,
            color = new Color(0.62f, 0.32f, 0.20f),
            draggable = false,
            title = "油桶",
            kind = "危险品",
            description = "一股油味。要是被电到，或者被撞破…你就知道为什么没人敢碰它了。",
            spawn = new SpawnRule { weight = 0.5f, clearance = 0.6f, padding = 0.4f, footprint = 0.7f, hamletOnly = true },
            decorate = go =>
            {
                Breakable breakable = go.AddComponent<Breakable>();
                breakable.hp = 1f;
                breakable.debrisColor = new Color(0.55f, 0.3f, 0.2f);
                Explosive explosive = go.AddComponent<Explosive>();
                // 被打碎 / 被腐蚀 → 炸（Breakable.Break 会把控制权交给 Explosive）
                Edible edible = go.GetComponent<Edible>();   // 油桶不能吃，这里只是留个口子
                if (edible != null) edible.onConsumed += _ => explosive.Detonate();
            }
        };
    }

    /// <summary>警报器：被电击 / 被炸到就响，全村撤离（链 B 的最后一环）。</summary>
    static ItemDefinition MakeAlarm()
    {
        return new ItemDefinition
        {
            id = ItemIds.Alarm,
            artKey = ArtKeys.Alarm,
            shape = ArtShape.Round,
            sliced = false,
            sizeMin = 0.5f,
            sizeMax = 0.6f,
            color = new Color(0.86f, 0.82f, 0.7f),
            draggable = false,
            title = "警报器",
            kind = "设施",
            description = "挂在杆子上的喇叭。一旦响起来，整个村子的人都往外跑。",
            // 和泵一样：警报是「村里的事故」，荒野里不该出现
            spawn = SpawnRule.Fixed(1, 1, 0.4f, 0.8f, 0.6f, 0.8f)
                .InSettlements(SettlementKind.Village, SettlementKind.City),
            decorate = go =>
            {
                go.AddComponent<Breakable>().hp = 2f;
                go.AddComponent<Alarm>();
            }
        };
    }
}
