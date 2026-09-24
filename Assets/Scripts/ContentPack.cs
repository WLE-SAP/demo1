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
            + " 种（岩石 = 沙漠 / 草原，草垛 = 草原，木料 = 森林，垃圾桶 = 城市，木桶 / 灯笼 = 农村 / 城市）。"
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
        if (ItemCatalog.Find(BiomeItemIds.Rock) == null) ItemCatalog.Register(MakeRock());
        if (ItemCatalog.Find(BiomeItemIds.HayBale) == null) ItemCatalog.Register(MakeHayBale());
        if (ItemCatalog.Find(BiomeItemIds.Log) == null) ItemCatalog.Register(MakeLog());
        if (ItemCatalog.Find(BiomeItemIds.TrashCan) == null) ItemCatalog.Register(MakeTrashCan());
        if (ItemCatalog.Find(BiomeItemIds.Bucket) == null) ItemCatalog.Register(MakeBucket());
        if (ItemCatalog.Find(BiomeItemIds.Lantern) == null) ItemCatalog.Register(MakeLantern());
    }

    /// <summary>岩石：沙漠 / 草原。很沉（搬起来很慢），但砸得碎 —— 沙漠里唯一能推能砸的东西。</summary>
    static ItemDefinition MakeRock()
    {
        return new ItemDefinition
        {
            id = BiomeItemIds.Rock,
            artKey = ArtKeys.Rock,
            shape = ArtShape.Round,
            sliced = false,
            sizeMin = 0.55f,
            sizeMax = 0.85f,
            color = new Color(0.52f, 0.50f, 0.47f),
            draggable = true,
            carryWeight = 2.2f,          // 沉：搬运时小虫明显更慢
            edible = false,
            title = "岩石",
            kind = "景物",
            description = "一块压手的石头。搬起来很费劲，但撞碎了也能当碎片玩。",
            rectHighlight = true,
            spawn = new SpawnRule
            {
                weight = 8f,
                clearance = 0.45f,
                padding = 0.3f,
                footprint = 0.55f
            }.InNatures(NatureKind.Desert, NatureKind.Grassland),
            decorate = go =>
            {
                Breakable breakable = go.AddComponent<Breakable>();
                breakable.hp = 2f;
                breakable.debrisCount = 7;
                breakable.debrisColor = new Color(0.52f, 0.50f, 0.47f);
            }
        };
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
            orderOffset = 14,
            spawn = SpawnRule.Pool(6f, 0.4f, 0.25f, 0.45f)
                .InSettlements(SettlementKind.Village, SettlementKind.City),
            decorate = go =>
            {
                // 灯火：白天几乎看不见，夜里亮起来（和路灯 / 窗户共用 NightGlow）
                SpriteRenderer body = go.GetComponentInChildren<SpriteRenderer>();
                int order = body != null ? body.sortingOrder : SpawnKit.YOrder(go.transform.position.y) + 14;
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
    /// <summary>岩石（沙漠 / 草原）。</summary>
    public const string Rock = "rock";
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
