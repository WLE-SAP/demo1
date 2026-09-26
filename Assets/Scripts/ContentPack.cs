using UnityEngine;

/// <summary>
/// **按「聚落 × 自然体系」分家的内容包**（2026-09-25 加）。
///
/// 世界现在由两套体系组成（见 <see cref="WorldBiome"/>）：
/// 聚落决定「有没有人、有没有房子」，自然体系决定「长什么样」。
/// 这个文件把「只有某一种地貌 / 聚落才该出现的东西」集中注册进来，
/// 让 <see cref="FoodCatalog"/> / <see cref="ItemCatalog"/> 保持「通用内容」的干净。
///
/// 加一样新东西的步骤和别的内容完全一样（写定义 → <c>Register</c> 一行），
/// 只是多了一步：用 <see cref="SpawnRule.InNatures"/> / <see cref="SpawnRule.InSettlements"/>
/// 说清它属于哪片地貌 —— 门控由 <see cref="SpawnRule.Includes"/> 在生成时判断。
///
/// 由 <see cref="VillageGenerator"/> 在 Awake 里调一次（在目录装好默认内容之后）。
/// **同 id 已存在时不覆盖**，所以外部脚本可以先注册一个同 id 的定义来替换这里的。
/// </summary>
public static class ContentPack
{
    public static void RegisterAll()
    {
        int foodBefore = FoodCatalog.All.Count;
        int itemBefore = ItemCatalog.All.Count;

        RegisterFoods();
        RegisterItems();

        Debug.Log("[Content] 按地貌 / 聚落分家：新增食物 " + (FoodCatalog.All.Count - foodBefore)
            + " 种（蘑菇 / 松果 = 森林，仙人掌果 = 沙漠，麦穗 = 草原），新增物品 " + (ItemCatalog.All.Count - itemBefore)
            + " 种（石头四档 = 沙漠 / 草原 / 森林（越大的越少见），草垛 = 草原，木料 = 森林，垃圾桶 = 城市，"
            + "木桶 / 灯笼 = 农村 / 城市）。"
            + "合计食物 " + FoodCatalog.All.Count + " 种、可交互物品 " + ItemCatalog.All.Count + " 种。");
    }

    // ---------------- 食物：换一种地貌就换一批吃的 ----------------
    //
    // 设计意图：走到哪儿都有东西吃，但吃到的种类完全不同 ——
    // 草原是果子 / 嫩叶 / 麦穗，森林是蘑菇 / 松果（树底下更多），沙漠是仙人掌果。
    // 神奇果实（唯一的长大途径）和五种能力食物是**通用**的，不在这里门控（见 FoodCatalog）。

    public static void RegisterFoods()
    {
        if (FoodCatalog.Find(BiomeFoodIds.Mushroom) == null) FoodCatalog.Register(MakeMushroom());
        if (FoodCatalog.Find(BiomeFoodIds.Pinecone) == null) FoodCatalog.Register(MakePinecone());
        if (FoodCatalog.Find(BiomeFoodIds.CactusFruit) == null) FoodCatalog.Register(MakeCactusFruit());
        if (FoodCatalog.Find(BiomeFoodIds.Wheat) == null) FoodCatalog.Register(MakeWheat());
    }

    /// <summary>蘑菇：森林限定，长在树底下。</summary>
    static FoodDefinition MakeMushroom()
    {
        return new FoodDefinition
        {
            id = BiomeFoodIds.Mushroom,
            artKey = ArtKeys.Mushroom,
            shape = ArtShape.Round,
            size = 0.30f,
            nutrition = 2,
            satietyMin = 9,
            satietyMax = 15,
            hiddenNote = "蘑菇：森林里的小伞",
            title = "蘑菇",
            kind = "食物",
            description = "林子里阴暗处的蘑菇。比果子顶饱，长在树根附近。",
            spawn = new SpawnRule
            {
                weight = 26f,
                clearance = 0.3f,
                padding = 0.1f,
                footprint = 0.35f,
                nearTrees = true,
                nearRadius = 2.4f,
                nearMinDistance = 0.6f
            }.InNatures(NatureKind.Forest)
        };
    }

    /// <summary>松果：森林限定，掉在树底下。</summary>
    static FoodDefinition MakePinecone()
    {
        return new FoodDefinition
        {
            id = BiomeFoodIds.Pinecone,
            artKey = ArtKeys.Pinecone,
            shape = ArtShape.Round,
            size = 0.26f,
            nutrition = 1,
            satietyMin = 8,
            satietyMax = 13,
            hiddenNote = "松果：小分量",
            title = "松果",
            kind = "食物",
            description = "从树上掉下来的松果，硬，但饿的时候也能啃。",
            spawn = new SpawnRule
            {
                weight = 20f,
                clearance = 0.3f,
                padding = 0.1f,
                footprint = 0.35f,
                nearTrees = true,
                nearRadius = 2.2f,
                nearMinDistance = 0.5f
            }.InNatures(NatureKind.Forest)
        };
    }

    /// <summary>仙人掌果：沙漠限定，长在仙人掌旁边。</summary>
    static FoodDefinition MakeCactusFruit()
    {
        return new FoodDefinition
        {
            id = BiomeFoodIds.CactusFruit,
            artKey = ArtKeys.CactusFruit,
            shape = ArtShape.Round,
            size = 0.30f,
            nutrition = 2,
            satietyMin = 10,
            satietyMax = 16,
            hiddenNote = "仙人掌果：沙漠里的水分",
            title = "仙人掌果",
            kind = "食物",
            description = "沙漠里唯一多汁的东西，长在仙人掌上，摘下来就能吃。",
            // 挂在仙人掌旁边（仙人掌的位置由 VillageGenerator 登记成锚点 "cactus"）
            spawn = new SpawnRule
            {
                weight = 34f,
                clearance = 0.3f,
                padding = 0.12f,
                footprint = 0.35f,
                nearAnchor = "cactus",
                nearRadius = 2.2f,
                nearMinDistance = 0.7f
            }.InNatures(NatureKind.Desert)
        };
    }

    /// <summary>麦穗：草原限定。</summary>
    static FoodDefinition MakeWheat()
    {
        return new FoodDefinition
        {
            id = BiomeFoodIds.Wheat,
            artKey = ArtKeys.Wheat,
            shape = ArtShape.Leaf,
            size = 0.32f,
            nutrition = 2,
            satietyMin = 10,
            satietyMax = 15,
            hiddenNote = "麦穗：草原上的口粮",
            title = "麦穗",
            kind = "食物",
            description = "草原上随处可见的野麦穗，一把就能垫垫肚子。",
            spawn = new SpawnRule
            {
                weight = 26f,
                clearance = 0.3f,
                padding = 0.1f,
                footprint = 0.35f
            }.InNatures(NatureKind.Grassland)
        };
    }

    // ---------------- 可交互物品：换一片地方就换一批能玩的东西 ----------------

    public static void RegisterItems()
    {
        if (ItemCatalog.Find(BiomeItemIds.RockSmall) == null) ItemCatalog.Register(MakeRock(RockTierSmall));
        if (ItemCatalog.Find(BiomeItemIds.RockMedium) == null) ItemCatalog.Register(MakeRock(RockTierMedium));
        if (ItemCatalog.Find(BiomeItemIds.RockLarge) == null) ItemCatalog.Register(MakeRock(RockTierLarge));
        if (ItemCatalog.Find(BiomeItemIds.RockHuge) == null) ItemCatalog.Register(MakeRock(RockTierHuge));
        if (ItemCatalog.Find(BiomeItemIds.HayBale) == null) ItemCatalog.Register(MakeHayBale());
        if (ItemCatalog.Find(BiomeItemIds.Log) == null) ItemCatalog.Register(MakeLog());
        if (ItemCatalog.Find(BiomeItemIds.TrashCan) == null) ItemCatalog.Register(MakeTrashCan());
        if (ItemCatalog.Find(BiomeItemIds.Bucket) == null) ItemCatalog.Register(MakeBucket());
        if (ItemCatalog.Find(BiomeItemIds.Lantern) == null) ItemCatalog.Register(MakeLantern());
    }

    // ---------------- 石头：按大小分四档（2026-09-25）----------------
    //
    // 美术交的石头是 7 张图（stone1~4 灰、stone5~7 土黄，从小到大），代码这边按**大小**归成四档。
    // 四档的差别（就是「一块石头值不值得搬」的三个维度）：
    //   · 占地大小 —— 小碎石 0.34，巨石 1.25；
    //   · 搬运手感 —— carryWeight（拖动跟手程度）+ carrySpeedMultiplier（搬着它走路的速度倍率）；
    //   · 能不能吃 —— requiredLevel（几级才啃得动）与营养 / 分量（越大的越顶饱）。
    // 再往下还有「碎掉要撞几下」（hp）和「出现概率」。
    //
    // 出现概率按场景调：`SpawnRule.WeightOf` = 基础权重 × 地貌倍率 × 聚落倍率（见 SpawnKit），
    // 所以小碎石在哪都多、巨石基本只在沙漠里偶尔见，城里石头最少。
    // 用哪张配色（灰 / 土黄）由 variantWeights 按地貌决定（见 RockColorWeights）。

    /// <summary>一档石头的全部数值（<see cref="MakeRock"/> 只负责把它变成一个物品定义）。</summary>
    struct RockTier
    {
        public string id, key, title, description, hiddenNote;
        /// <summary>占地大小范围（世界单位）。</summary>
        public float sizeMin, sizeMax;
        /// <summary>搬运迟滞：越大越不跟手。</summary>
        public float carryWeight;
        /// <summary>搬着它走路的速度倍率：越小越慢。</summary>
        public float carrySpeed;
        /// <summary>长到几级才啃得动（1 = 一开始就能吃）。</summary>
        public int level;
        public int nutrition;
        public int satietyMin, satietyMax;
        /// <summary>撞几下才碎。</summary>
        public float hp;
        /// <summary>碎掉掉几块渣。</summary>
        public int debris;
        /// <summary>「位」抽签的基础权重。</summary>
        public float weight;
        /// <summary>按地貌的权重倍率（下标 = <see cref="NatureKind"/> 的值：草原 0 / 森林 1 / 沙漠 2）。</summary>
        public float[] natureScale;
        /// <summary>按聚落的权重倍率（下标 = <see cref="SettlementKind"/> 的值：荒野 0 / 农村 1 / 城市 2）。</summary>
        public float[] settlementScale;
    }

    static readonly RockTier RockTierSmall = new RockTier
    {
        id = BiomeItemIds.RockSmall,
        key = ArtKeys.RockSmall,
        title = "小石头",
        description = "随手就能搬起来的小石子。一开始就啃得动 —— 没什么肉，胜在到处都是。",
        hiddenNote = "小石头：分量很小",
        sizeMin = 0.34f, sizeMax = 0.44f,
        carryWeight = 1.1f, carrySpeed = 0.34f,
        level = 1, nutrition = 1, satietyMin = 4, satietyMax = 8,
        hp = 1f, debris = 5,
        weight = 9f,
        natureScale = new[] { 1.0f, 0.6f, 1.3f },
        settlementScale = new[] { 1.2f, 0.9f, 0.5f }
    };

    static readonly RockTier RockTierMedium = new RockTier
    {
        id = BiomeItemIds.RockMedium,
        key = ArtKeys.RockMedium,
        title = "石头",
        description = "一块压手的石头。搬起来明显慢了，长到 " + BugGrowth.CrateLevel + " 级才啃得动，撞两下能碎。",
        hiddenNote = "石头：分量还行",
        sizeMin = 0.55f, sizeMax = 0.68f,
        carryWeight = 1.9f, carrySpeed = 0.26f,
        level = BugGrowth.CrateLevel, nutrition = 2, satietyMin = 8, satietyMax = 14,
        hp = 2f, debris = 6,
        weight = 6f,
        natureScale = new[] { 1.0f, 0.6f, 1.3f },
        settlementScale = new[] { 1.2f, 0.9f, 0.4f }
    };

    static readonly RockTier RockTierLarge = new RockTier
    {
        id = BiomeItemIds.RockLarge,
        key = ArtKeys.RockLarge,
        title = "大石头",
        description = "要两只手才推得动的大石头。搬着它走路慢得让人着急，"
            + BugGrowth.TreeLevel + " 级以后才啃得动，得撞上好几下才碎。",
        hiddenNote = "大石头：很顶饱",
        sizeMin = 0.78f, sizeMax = 0.94f,
        carryWeight = 2.7f, carrySpeed = 0.18f,
        level = BugGrowth.TreeLevel, nutrition = 3, satietyMin = 12, satietyMax = 18,
        hp = 3f, debris = 7,
        weight = 3.5f,
        natureScale = new[] { 0.8f, 0.3f, 1.5f },
        settlementScale = new[] { 1.3f, 0.8f, 0.3f }
    };

    static readonly RockTier RockTierHuge = new RockTier
    {
        id = BiomeItemIds.RockHuge,
        key = ArtKeys.RockHuge,
        title = "巨石",
        description = "沙漠里那种半人高的巨石。满级（" + BugGrowth.VillagerLevel
            + " 级）才啃得动，搬起来几乎是一步一挪 —— 但它撞碎时的动静，够全村人听见。",
        hiddenNote = "巨石：最大的分量",
        sizeMin = 1.05f, sizeMax = 1.25f,
        carryWeight = 3.6f, carrySpeed = 0.12f,
        level = BugGrowth.VillagerLevel, nutrition = 4, satietyMin = 16, satietyMax = 24,
        hp = 4f, debris = 9,
        weight = 1.6f,
        natureScale = new[] { 0.5f, 0.1f, 1.6f },
        settlementScale = new[] { 1.3f, 0.7f, 0.2f }
    };

    /// <summary>把一档石头变成物品定义（四档共用这一套：能搬、能啃、能撞碎）。</summary>
    static ItemDefinition MakeRock(RockTier tier)
    {
        return new ItemDefinition
        {
            id = tier.id,
            artKey = tier.key,
            shape = ArtShape.Round,        // 没交图时的程序化外观
            sliced = false,
            wholeArt = true,               // 石头是整图素材：有图就按内容比例装进占地、底边贴地
            variantWeights = RockColorWeights,
            sizeMin = tier.sizeMin,
            sizeMax = tier.sizeMax,
            color = new Color(0.54f, 0.53f, 0.50f),
            draggable = true,
            carryWeight = tier.carryWeight,
            carrySpeedMultiplier = tier.carrySpeed,
            edible = true,
            nutrition = tier.nutrition,
            requiredLevel = tier.level,
            satietyMin = tier.satietyMin,
            satietyMax = tier.satietyMax,
            hiddenNote = tier.hiddenNote,
            title = tier.title,
            kind = "景物",
            description = tier.description,
            rectHighlight = true,
            spawn = new SpawnRule
            {
                weight = tier.weight,
                clearance = 0.45f,
                padding = 0.3f,
                footprint = 0.55f,
                weightInNature = nature => ScaleAt(tier.natureScale, (int)nature),
                weightInSettlement = settlement => ScaleAt(tier.settlementScale, (int)settlement)
            }.InNatures(NatureKind.Desert, NatureKind.Grassland, NatureKind.Forest),
            decorate = go =>
            {
                Breakable breakable = go.AddComponent<Breakable>();
                breakable.hp = tier.hp;
                breakable.debrisCount = tier.debris;
                breakable.debrisColor = new Color(0.52f, 0.50f, 0.47f);
            }
        };
    }

    /// <summary>取倍率表里的第 <paramref name="index"/> 项；越界（新加了地貌 / 聚落没补表）时按 1 倍算。</summary>
    static float ScaleAt(float[] table, int index)
    {
        if (table == null || index < 0 || index >= table.Length) return 1f;
        return table[index];
    }

    /// <summary>
    /// 石头用哪张配色（下标 = 变体顺序 = 文件名结尾数字的顺序，也就是 [0] = 灰那张、[1] = 土黄那张）：
    /// 沙漠里多是土黄的、森林里几乎都是灰的、草原两种都有。
    /// **只有一张图时这套权重不起作用**（没得挑，直接用那唯一一张）。
    /// </summary>
    static float[] RockColorWeights(NatureKind nature)
    {
        switch (nature)
        {
            case NatureKind.Desert: return new[] { 0.25f, 0.75f };
            case NatureKind.Forest: return new[] { 0.90f, 0.10f };
            default: return new[] { 0.60f, 0.40f };
        }
    }

    /// <summary>草垛：草原。能搬也能啃（2 级）。</summary>
    static ItemDefinition MakeHayBale()
    {
        return new ItemDefinition
        {
            id = BiomeItemIds.HayBale,
            artKey = ArtKeys.HayBale,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.8f,
            sizeMax = 1.0f,
            color = new Color(0.82f, 0.70f, 0.34f),
            draggable = true,
            carryWeight = 1.3f,
            edible = true,
            nutrition = 3,
            requiredLevel = BugGrowth.CrateLevel,
            satietyMin = 13,
            satietyMax = 20,
            hiddenNote = "草垛：干草，顶饱",
            title = "草垛",
            kind = "道具",
            description = "晒干的草垛，草原上到处都有。搬得动，长到 " + BugGrowth.CrateLevel + " 级以后也啃得下。",
            spawn = new SpawnRule
            {
                weight = 9f,
                clearance = 0.5f,
                padding = 0.3f,
                footprint = 0.6f
            }.InNatures(NatureKind.Grassland)
        };
    }

    /// <summary>木料堆：森林。能搬也能啃（2 级）。</summary>
    static ItemDefinition MakeLog()
    {
        return new ItemDefinition
        {
            id = BiomeItemIds.Log,
            artKey = ArtKeys.Log,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.8f,
            sizeMax = 1.05f,
            color = new Color(0.50f, 0.35f, 0.20f),
            draggable = true,
            carryWeight = 1.5f,
            edible = true,
            nutrition = 3,
            requiredLevel = BugGrowth.CrateLevel,
            satietyMin = 14,
            satietyMax = 21,
            hiddenNote = "木料：湿木头，很顶饱",
            title = "木料堆",
            kind = "道具",
            description = "樵夫砍好堆着的木料。搬起来慢，但啃下去很顶饱。",
            spawn = new SpawnRule
            {
                weight = 11f,
                clearance = 0.5f,
                padding = 0.3f,
                footprint = 0.6f
            }.InNatures(NatureKind.Forest)
        };
    }

    /// <summary>垃圾桶：城市限定。能搬也能啃（2 级）—— 城里最好找的「吃的」。</summary>
    static ItemDefinition MakeTrashCan()
    {
        return new ItemDefinition
        {
            id = BiomeItemIds.TrashCan,
            artKey = ArtKeys.TrashCan,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.7f,
            sizeMax = 0.9f,
            color = new Color(0.44f, 0.48f, 0.46f),
            draggable = true,
            carryWeight = 1.0f,
            edible = true,
            nutrition = 2,
            requiredLevel = BugGrowth.CrateLevel,
            satietyMin = 10,
            satietyMax = 16,
            hiddenNote = "垃圾桶：城里的剩饭",
            title = "垃圾桶",
            kind = "道具",
            description = "城里人家门口的垃圾桶。翻一翻总有点能吃的，整个也能拖走。",
            spawn = SpawnRule.Pool(10f, 0.5f, 0.3f, 0.6f).InSettlements(SettlementKind.City)
        };
    }

    /// <summary>木桶：村庄 / 城市的草原与森林。能搬也能啃（2 级）。</summary>
    static ItemDefinition MakeBucket()
    {
        return new ItemDefinition
        {
            id = BiomeItemIds.Bucket,
            artKey = ArtKeys.Bucket,
            shape = ArtShape.Rect,
            sliced = true,
            sizeMin = 0.55f,
            sizeMax = 0.75f,
            color = new Color(0.58f, 0.42f, 0.24f),
            draggable = true,
            carryWeight = 0.9f,
            edible = true,
            nutrition = 2,
            requiredLevel = BugGrowth.CrateLevel,
            satietyMin = 10,
            satietyMax = 15,
            hiddenNote = "木桶：空的，但木屑管饱",
            title = "木桶",
            kind = "道具",
            description = "闲置的木桶，比木箱轻，好搬。饿急了连桶一起啃。",
            spawn = SpawnRule.Pool(8f, 0.45f, 0.25f, 0.5f)
                .InSettlements(SettlementKind.Village, SettlementKind.City)
                .InNatures(NatureKind.Grassland, NatureKind.Forest)
        };
    }

    /// <summary>灯笼：村庄 / 城市。能搬走，夜里发亮。</summary>
    static ItemDefinition MakeLantern()
    {
        return new ItemDefinition
        {
            id = BiomeItemIds.Lantern,
            artKey = ArtKeys.Lantern,
            shape = ArtShape.Round,
            sliced = false,
            sizeMin = 0.4f,
            sizeMax = 0.5f,
            color = new Color(0.95f, 0.82f, 0.45f),
            draggable = true,
            carryWeight = 0.6f,
            edible = false,
            title = "灯笼",
            kind = "道具",
            description = "挂在门口的小灯笼。可以搬走 —— 搬着它在夜里也看得清。",
            orderOffset = 2,          // 挂了 YSort，所以这里只是「相对偏移」（见 ItemDefinition.orderOffset）
            spawn = SpawnRule.Pool(6f, 0.4f, 0.25f, 0.45f)
                .InSettlements(SettlementKind.Village, SettlementKind.City),
            decorate = go =>
            {
                // 灯火：白天几乎看不见，夜里亮起来（和路灯 / 窗户共用 NightGlow）
                SpriteRenderer body = go.GetComponentInChildren<SpriteRenderer>();
                int order = body != null ? body.sortingOrder : 2;      // 同样是相对偏移（挂了 YSort）
                SpriteRenderer glow = ArtShapes.AddSprite(go.transform, "Glow", ArtShape.Round, 1.5f,
                    Vector2.zero, new Color(1f, 0.90f, 0.60f, 0f), order - 1);
                NightGlow night = glow.gameObject.AddComponent<NightGlow>();
                night.dayColor = new Color(1f, 0.90f, 0.60f, 0.06f);
                night.nightColor = new Color(1f, 0.86f, 0.52f, 0.38f);
                night.nightScale = 1.2f;
            }
        };
    }
}

/// <summary>按自然体系分家的食物 id。</summary>
public static class BiomeFoodIds
{
    /// <summary>蘑菇（森林）。</summary>
    public const string Mushroom = "mushroom";
    /// <summary>松果（森林）。</summary>
    public const string Pinecone = "pinecone";
    /// <summary>仙人掌果（沙漠）。</summary>
    public const string CactusFruit = "cactus_fruit";
    /// <summary>麦穗（草原）。</summary>
    public const string Wheat = "wheat";
}

/// <summary>按聚落 / 自然体系分家的可交互物品 id。</summary>
public static class BiomeItemIds
{
    /// <summary>石头·小：到处都有（森林里少一点），一开始就搬得动、啃得动。</summary>
    public const string RockSmall = "rock_small";
    /// <summary>石头·中：要 <see cref="BugGrowth.CrateLevel"/> 级。</summary>
    public const string RockMedium = "rock_medium";
    /// <summary>石头·大：很沉，要 <see cref="BugGrowth.TreeLevel"/> 级。</summary>
    public const string RockLarge = "rock_large";
    /// <summary>石头·巨石：基本只在沙漠，最沉，要满级。</summary>
    public const string RockHuge = "rock_huge";
    /// <summary>草垛（草原）。</summary>
    public const string HayBale = "hay_bale";
    /// <summary>木料堆（森林）。</summary>
    public const string Log = "log";
    /// <summary>垃圾桶（城市）。</summary>
    public const string TrashCan = "trash_can";
    /// <summary>木桶（村庄 / 城市）。</summary>
    public const string Bucket = "bucket";
    /// <summary>灯笼（村庄 / 城市）。</summary>
    public const string Lantern = "lantern";
}
