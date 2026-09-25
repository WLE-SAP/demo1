using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 区块生成器：按「区块坐标」生成一小片世界内容（地面、道路、广场、建筑、设施、树、食物、物品、村民、地洞）。
///
/// **世界由两套体系组成**（2026-09-25 起，见 <see cref="WorldBiome"/>）：
/// <list type="bullet">
/// <item>**自然体系**决定「长什么样」：草原 / 森林 / 沙漠 —— 地面、树与植被、野外的食物与物品都跟着换；</item>
/// <item>**聚落体系**决定「有多热闹」：荒野 / 农村 / 城市 —— 房子类型、设施、广场、路面宽度与人口都跟着换；</item>
/// <item>两者都只依赖 **区块坐标 + 世界种子**（低频噪声，所以是成片的），是纯函数 ——
///       同一个种子下走远再回头、读档后回来，完全一模一样。</item>
/// </list>
///
/// **不再是「草地 + 十字路 + 中央水井」了**：地面按地貌铺、路型随机（十字 / T / L / 直路 / 没有路）、
/// 广场与水井（城市是喷泉）都只是「按几率出现的一种可能」。
///
/// 生成参数的**唯一权威是 <see cref="WorldBiome"/>**：下面那些 houseMin / treeMin / foodSlots…
/// 字段会在每个区块生成前被整体覆盖（不是累乘），所以不要在 Inspector 里手改它们。
///
/// 由 <see cref="VillageWorld"/> 按小虫的位置决定生成 / 回收哪些区块。
/// </summary>
public class VillageGenerator : MonoBehaviour
{
    /// <summary>房型：房子随聚落换样子（见 <see cref="PickHouseType"/>）。</summary>
    public enum HouseType
    {
        Cottage,     // 农舍：一层，坡屋顶 + 门窗，最普通的那种
        TwoStory,    // 两层小楼：南面两层墙带，窗户上下各一排在
        Barn,        // 谷仓：又大又宽，双开大门，没有窗
        RowHouse,    // 排屋：又宽又浅，两个门三扇窗（城市）
        Cabin,       // 木屋：小、圆木墙（荒野）
        Apartment    // 公寓：高大，一整排小窗（城市）
    }

    [Header("精灵")]
    public Sprite roadSprite;
    public Sprite rectSprite;
    public Sprite roundRectSprite;
    public Sprite discSprite;
    public Sprite berrySprite;
    public Sprite leafSprite;

    [Header("区块")]
    [Tooltip("区块边长（世界单位）")]
    public float chunkSize = 32f;
    [Tooltip("勾上则只在原点生成一个区块（调试用）")]
    public bool autoGenerateOnStart = false;
    [Tooltip("世界种子（由 VillageWorld 传入优先）")]
    public int seed = 20260922;

    [Header("道路与广场")]
    [Tooltip("路宽（会被 WorldBiome 按聚落覆盖：荒野 1.5 / 农村 2.0 / 城市 2.6。**要改路宽改 WorldBiome.RoadWidthOf**）")]
    public float roadWidth = 2f;
    [Tooltip("广场边长（会被 WorldBiome 按聚落覆盖）")]
    public float plazaSize = 10f;
    [Tooltip("**路型按 2×2 个区块一片抽签**：同一片常常是同一种路型，路才连得起来（见 PickRoadShape）")]
    public int roadRunLength = 2;

    [Header("路口瓦片（有图才用得上）")]
    [Tooltip("有路口 / 直路 / 尽头瓦片时，路改成「按路口形状铺瓦片」（不再靠长方形互相重叠）。关掉就退回贴图直铺")]
    public bool useRoadTiles = true;
    [Tooltip("把瓦片路网也铺到森林 / 沙漠 / 城市。**交付的这套瓦片自带草地底色**，铺过去会露出一块块绿，"
        + "所以默认只在草原、且不是城市的地方铺")]
    public bool roadTilesEverywhere = false;
    [Tooltip("瓦片图里「路面」占画布宽度的比例（这套是 36 ÷ 128 = 0.28125）。"
        + "路口瓦片的世界尺寸 = 路面宽度 ÷ 它，这样瓦片里的路正好和直路段一样宽；换了比例改这里")]
    [Range(0.05f, 1f)] public float roadTileRoadRatio = 0.28125f;
    [Tooltip("「路尽头」瓦片里，路尖到**有路那一侧**边缘的距离占瓦片尺寸的比例（这套是 0.70："
        + "roadhead-right 左半边是路、路尖在 70% 处）。摆瓦片时按它对齐，路尖才落在区块边界上；换了美术要重新量")]
    [Range(0.3f, 1f)] public float roadHeadTipRatio = 0.7f;

    [Header("地面 / 铺装的柔边（2026-09-25 用户要的「平滑」）")]
    [Tooltip("村中心要不要铺那块方形铺装（水井 / 喷泉周围）。**默认关**：2026-09-25 用户要求「取消水井周围的方形地块」")]
    public bool drawPlazaPaving = false;
    [Tooltip("不铺那块铺装时，村中心（水井 / 喷泉周围）仍然留出的空地直径（世界单位）。\n"
        + "留一小块是为了房子不会压到井上；铺装打开时用 plazaSize")]
    public float plazaClearRadius = 5f;
    [Tooltip("地貌地面（森林 / 沙漠）的边缘往外柔化多远（世界单位）；0 = 关掉。3 米 ≈ 一块 32 米地面的 9%")]
    public float groundFeatherWidth = 3f;
    [Tooltip("地貌地面柔边分几圈（圈数越多越平滑，每圈一个半透明四边形）")]
    [Range(1, 8)] public int groundFeatherSteps = 4;
    [Tooltip("铺装（水井 / 喷泉周围那块方砖）的边缘往外柔化多远")]
    public float plazaFeatherWidth = 1.6f;
    [Tooltip("铺装柔边分几圈")]
    [Range(1, 8)] public int plazaFeatherSteps = 4;

    // ---------------- 每个区块生成前被 WorldBiome 整体覆盖的数字 ----------------
    // 这里是「农村 + 草原」的基数，只作为 Inspector 的默认值存在，改数值请改 WorldBiome。

    [Header("区块内容（由 WorldBiome 覆盖，别在这里改）")]
    public int houseMin = 3;
    public int houseMax = 5;
    public int treeMin = 10;
    public int treeMax = 16;
    [Tooltip("灌木数量（草原 / 森林）")]
    public int bushMin = 2;
    public int bushMax = 5;
    [Tooltip("仙人掌数量（沙漠，代替树）")]
    public int cactusMin = 0;
    public int cactusMax = 0;
    [Tooltip("枯树数量（沙漠多、森林少）")]
    public int deadTreeMin = 0;
    public int deadTreeMax = 1;

    [Tooltip("每个区块的「食物位」数量：每个位按权重从 FoodCatalog 里抽一种（见 Assets/Scripts/FoodCatalog.cs）")]
    public int foodSlotsMin = 5;
    public int foodSlotsMax = 8;
    [Tooltip("每个区块的「可交互物品位」数量（木箱之类，按权重从 ItemCatalog 里抽）")]
    public int itemSlotsMin = 0;
    public int itemSlotsMax = 2;

    public int villagerMin = 2;
    public int villagerMax = 4;

    [Header("地洞")]
    [Tooltip("每个区块的地洞数量范围")]
    public int burrowMin = 0;
    public int burrowMax = 2;
    [Tooltip("其中是「地道」（可两两传送到另一头）的比例")]
    [Range(0f, 1f)] public float tunnelChance = 0.45f;

    [Header("设施概率（有设施的是村庄区块）")]
    public bool buildFacilities = true;
    [Tooltip("这个区块**有没有路**的几率（荒野只有一半区块有路）")]
    [Range(0f, 1f)] public float roadChance = 0.88f;
    [Tooltip("有没有广场（只有农村 / 城市才可能有广场）")]
    [Range(0f, 1f)] public float plazaChance = 0.55f;
    [Tooltip("有没有水井（荒野很低、城市为 0 —— 城市换成喷泉）")]
    [Range(0f, 1f)] public float wellChance = 0.55f;
    [Tooltip("有没有喷泉（只有城市）")]
    [Range(0f, 1f)] public float fountainChance = 0.6f;
    [Range(0f, 1f)] public float farmChance = 0.7f;
    [Range(0f, 1f)] public float penChance = 0.35f;
    [Range(0f, 1f)] public float bakeryChance = 0.22f;
    [Range(0f, 1f)] public float smithyChance = 0.22f;
    [Tooltip("发电站（电池聚在它旁边，2026-09-25 新增）")]
    [Range(0f, 1f)] public float powerPlantChance = 0.3f;
    [Tooltip("风车（农村的地标，叶片会转）")]
    [Range(0f, 1f)] public float windmillChance = 0.3f;
    [Tooltip("钟楼（城市的地标，整点敲钟）")]
    [Range(0f, 1f)] public float bellTowerChance = 0.06f;
    [Tooltip("篝火（荒野的营地）")]
    [Range(0f, 1f)] public float campfireChance = 0f;
    [Tooltip("绿洲（沙漠里的稀有景点：水面 + 棕榈）")]
    [Range(0f, 1f)] public float oasisChance = 0f;
    [Range(0f, 1f)] public float stallChance = 0.8f;
    [Range(0f, 1f)] public float gardenChance = 0.7f;
    [Range(0f, 1f)] public float boardChance = 0.4f;

    [Header("美术整图建筑（有图才用得上）")]
    [Tooltip("交了多少张「杂项建筑」（house_extra）就在这里抽多少几率：这一栋普通房子改用那张整图盖。\n"
        + "这是**每栋房子各自**的几率，不是每区块一次 —— 0 = 永远盖普通房子")]
    [Range(0f, 1f)] public float extraHouseChance = 0.18f;
    [Tooltip("城市里出现城堡（castle）的几率：稀有地标，只有城市会掷这一次")]
    [Range(0f, 1f)] public float castleChance = 0.08f;

    [Header("锚点表")]
    [Tooltip("设施锚点表；留空会自动找场景里的 VillageMap")]
    public VillageMap map;

    [Header("玩家")]
    public Transform player;
    public Vector2 playerSpawn = new Vector2(0f, -3.4f);
    [Tooltip("新开一局时把小虫放到出生点（读档时由 AutoSave 关掉，位置以存档为准）")]
    public bool placePlayerOnFirstChunk = true;

    /// <summary>只放一次：区块会反复生成 / 回收，不能每建一次原点区块就把小虫搬回出生点。</summary>
    bool playerPlaced;

    static readonly Color[] StallColors =
    {
        new Color(0.80f, 0.36f, 0.30f),
        new Color(0.30f, 0.46f, 0.68f),
        new Color(0.38f, 0.60f, 0.38f),
        new Color(0.82f, 0.66f, 0.30f),
        new Color(0.58f, 0.38f, 0.62f)
    };

    static readonly Color[] FlowerColors =
    {
        new Color(0.92f, 0.55f, 0.62f),
        new Color(0.94f, 0.82f, 0.40f),
        new Color(0.72f, 0.62f, 0.92f),
        new Color(0.95f, 0.95f, 0.92f),
        new Color(0.90f, 0.62f, 0.34f)
    };

    static readonly Color[] RoofColors =
    {
        new Color(0.62f, 0.26f, 0.22f),
        new Color(0.48f, 0.30f, 0.22f),
        new Color(0.40f, 0.36f, 0.46f),
        new Color(0.34f, 0.42f, 0.34f),
        new Color(0.58f, 0.42f, 0.24f),
        new Color(0.44f, 0.26f, 0.32f)
    };

    /// <summary>沙漠的屋顶色：只有土黄 / 沙色一类，不会出现红瓦绿瓦。</summary>
    static readonly Color[] DesertRoofColors =
    {
        new Color(0.74f, 0.61f, 0.40f),
        new Color(0.68f, 0.55f, 0.36f),
        new Color(0.79f, 0.68f, 0.48f),
        new Color(0.62f, 0.50f, 0.34f)
    };

    static readonly Color[] TreeColors =
    {
        new Color(0.16f, 0.33f, 0.13f),
        new Color(0.20f, 0.39f, 0.15f),
        new Color(0.13f, 0.29f, 0.12f)
    };

    /// <summary>森林的树更暗更密（一眼看出「进林子了」）。</summary>
    static readonly Color[] ForestTreeColors =
    {
        new Color(0.10f, 0.24f, 0.11f),
        new Color(0.13f, 0.29f, 0.12f),
        new Color(0.08f, 0.20f, 0.10f)
    };

    static readonly Color[] SkinColors =
    {
        new Color(0.96f, 0.82f, 0.68f),
        new Color(0.87f, 0.70f, 0.53f),
        new Color(0.72f, 0.55f, 0.40f)
    };

    static readonly string[] SurnameList =
    {
        "王", "李", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴", "徐", "孙",
        "马", "朱", "胡", "林", "何", "郭", "高", "罗", "梁", "宋", "郑", "谢"
    };

    static readonly string[] GivenList =
    {
        "禾", "木", "山", "水", "云", "石", "田", "春", "夏", "秋", "冬", "安",
        "宁", "福", "贵", "顺", "平", "旺", "兴", "乐", "大牛", "小满", "阿宝", "铁柱"
    };

    readonly List<Rect> occupied = new List<Rect>();
    /// <summary>这个区块里真正铺过的路面 / 广场（路灯只放在它们旁边）。</summary>
    readonly List<Rect> roadRects = new List<Rect>();
    System.Random rng;
    Vector2 center;
    Vector2Int chunkCoord;
    /// <summary>当前区块用的世界种子（路型抽签也要靠它，不然不同世界会长出一样的路网）。</summary>
    int chunkWorldSeed = 20260922;
    float radius;
    bool hamlet;

    /// <summary>这个区块的自然体系（地面 / 植被 / 野外产出）。</summary>
    public NatureKind nature { get; private set; }
    /// <summary>这个区块的聚落体系（房子 / 设施 / 人口）。</summary>
    public SettlementKind settlement { get; private set; }

    Transform roadsRoot, groundRoot, housesRoot, facilitiesRoot, treesRoot, natureRoot,
              foodRoot, propsRoot, villagersRoot, burrowRoot;

    /// <summary>无限草地的贴图：森林地面用它染成深绿，所以从场景里的 <see cref="InfiniteGround"/> 上取。</summary>
    Sprite groundSprite;

    public Vector2 CurrentCenter { get { return center; } }

    void Awake()
    {
        if (map == null) map = FindObjectOfType<VillageMap>();

        // 新增内容（食物 / 可交互物品）用到的程序化形状与内容目录都要先准备好，
        // 之后每个区块生成时直接按目录刷（见 FoodCatalog / ItemCatalog / ContentPack）
        ArtShapes.Use(discSprite, rectSprite, roundRectSprite, berrySprite, leafSprite);
        FoodCatalog.EnsureDefaults();
        ItemCatalog.EnsureDefaults();
        ContentPack.RegisterAll();

        // 森林地面用无限草地那张图（染成深绿），所以拿一次它的贴图
        InfiniteGround ground = FindObjectOfType<InfiniteGround>();
        if (ground != null)
        {
            SpriteRenderer sr = ground.GetComponent<SpriteRenderer>();
            if (sr != null) groundSprite = sr.sprite;
        }
    }

    void Start()
    {
        if (autoGenerateOnStart) BuildChunk(Vector2Int.zero, seed);
    }

    // ---------------- 区块构建 ----------------

    /// <summary>生成一个区块，返回区块根物体（由 VillageWorld 负责回收）。</summary>
    public GameObject BuildChunk(Vector2Int coord, int worldSeed)
    {
        if (map == null) map = FindObjectOfType<VillageMap>();

        chunkCoord = coord;
        chunkWorldSeed = worldSeed;
        center = new Vector2(coord.x * chunkSize, coord.y * chunkSize);
        radius = chunkSize * 0.5f;
        int chunkSeed = Hash(worldSeed, coord.x, coord.y);
        rng = new System.Random(chunkSeed);
        occupied.Clear();
        roadRects.Clear();

        // 这个区块属于哪种自然 / 聚落体系，然后把参数整体套上去（先覆盖再生成）
        nature = WorldBiome.NatureAt(coord, worldSeed);
        settlement = WorldBiome.SettlementAt(coord, worldSeed);
        hamlet = settlement != SettlementKind.Wilderness;
        WorldBiome.Apply(settlement, nature, this);

        // 内容定义（石头这类）在造物体时要按地貌挑外观、按位置挑变体图，所以把「当前区块」广播出去
        SpawnContext.Nature = nature;
        SpawnContext.Settlement = settlement;
        SpawnContext.Seed = chunkSeed;

        GameObject chunkGo = new GameObject("Chunk_" + coord.x + "_" + coord.y);
        chunkGo.transform.SetParent(transform, false);
        chunkGo.transform.position = new Vector3(center.x, center.y, 0f);

        roadsRoot = MakeRoot(chunkGo.transform, "Roads");
        groundRoot = MakeRoot(chunkGo.transform, "Ground");
        housesRoot = MakeRoot(chunkGo.transform, "Houses");
        facilitiesRoot = MakeRoot(chunkGo.transform, "Facilities");
        treesRoot = MakeRoot(chunkGo.transform, "Trees");
        natureRoot = MakeRoot(chunkGo.transform, "Nature");
        foodRoot = MakeRoot(chunkGo.transform, "Food");
        propsRoot = MakeRoot(chunkGo.transform, "Props");
        villagersRoot = MakeRoot(chunkGo.transform, "Villagers");
        burrowRoot = MakeRoot(chunkGo.transform, "Burrows");

        BuildGround();                 // 地貌地面（草原不铺，直接用无限草地）
        BuildRoads();                  // 随机的路型
        BuildPlazaAndCenterpiece();    // 广场 + 水井 / 喷泉（都只是「可能」）
        if (hamlet && buildFacilities) BuildFacilities();
        BuildLooseLandmarks();         // 荒野的篝火、沙漠的绿洲（不分有没有聚落）
        BuildHouses();
        BuildTrees();                  // 树先放：食物里的「长在树旁」才有树可依附
        BuildNatureProps();            // 灌木 / 仙人掌 / 枯树
        BuildFoods();                  // 食物位 + 固定生成的食物（内容来自 FoodCatalog）
        BuildItems();                  // 可交互物品位 + 固定生成的物品（内容来自 ItemCatalog）
        BuildBurrows();
        BuildVillagers();
        if (coord == Vector2Int.zero && placePlayerOnFirstChunk && !playerPlaced)
        {
            PlacePlayer();
            playerPlaced = true;
        }

        return chunkGo;
    }

    static int Hash(int worldSeed, int cx, int cy)
    {
        unchecked
        {
            int h = worldSeed;
            h = h * 73856093 ^ cx * 19349663 ^ cy * 83492791;
            return h & 0x7fffffff;
        }
    }

    Transform MakeRoot(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    float Rand(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    int RandInt(int minInclusive, int maxExclusive)
    {
        return minInclusive >= maxExclusive ? minInclusive : rng.Next(minInclusive, maxExclusive);
    }

    T Pick<T>(T[] list)
    {
        return list[RandInt(0, list.Length)];
    }

    bool Chance(float probability)
    {
        return rng.NextDouble() < probability;
    }

    /// <summary>俯视 2D 深度排序：越靠下（Y 越小）画得越靠前。世界无限大，所以用绝对 Y。</summary>
    static int YOrder(float y)
    {
        return SpawnKit.YOrder(y);   // 公式只写在 SpawnKit 一处，新增内容也用它
    }

    bool IsFree(Rect area, float padding)
    {
        Rect padded = new Rect(area.xMin - padding, area.yMin - padding, area.width + padding * 2f, area.height + padding * 2f);
        for (int i = 0; i < occupied.Count; i++)
            if (occupied[i].Overlaps(padded)) return false;
        return true;
    }

    /// <summary>在区块内随机找一块空地。</summary>
    bool TryFindFreePoint(float itemRadius, float padding, out Vector2 point, int attempts = 80)
    {
        for (int i = 0; i < attempts; i++)
        {
            Vector2 p = center + new Vector2(Rand(-radius + itemRadius, radius - itemRadius), Rand(-radius + itemRadius, radius - itemRadius));
            Rect area = new Rect(p.x - itemRadius, p.y - itemRadius, itemRadius * 2f, itemRadius * 2f);
            if (IsFree(area, padding))
            {
                point = p;
                return true;
            }
        }
        point = center;
        return false;
    }

    /// <summary>在区块中心一带（离中心 <paramref name="range"/> 以内）找一块空地。</summary>
    bool TryFindNearCenter(float clearance, float padding, float range, out Vector2 point, int attempts = 60)
    {
        for (int i = 0; i < attempts; i++)
        {
            float angle = Rand(0f, Mathf.PI * 2f);
            float distance = Rand(2f, Mathf.Max(2.4f, range));
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Rect area = new Rect(candidate.x - clearance, candidate.y - clearance, clearance * 2f, clearance * 2f);
            if (IsFree(area, padding))
            {
                point = candidate;
                return true;
            }
        }
        return TryFindFreePoint(clearance, padding, out point, 60);
    }

    /// <summary>在一个点周围找一块空地（村民围着工作点安家）。</summary>
    bool TryFindPointAround(Vector2 origin, float minDistance, float maxDistance, float clearance, out Vector2 point, int attempts = 50)
    {
        for (int i = 0; i < attempts; i++)
        {
            float angle = Rand(0f, Mathf.PI * 2f);
            float distance = Rand(minDistance, maxDistance);
            Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Rect area = new Rect(candidate.x - clearance, candidate.y - clearance, clearance * 2f, clearance * 2f);
            if (!IsFree(area, 0.2f)) continue;
            if (Physics2D.OverlapCircle(candidate, clearance) != null) continue;
            point = candidate;
            return true;
        }
        point = origin;
        return false;
    }

    /// <summary>地面上的东西（草地/道路/农田/花坛/地洞）统一放在 Ground 排序层，永远不遮挡角色。</summary>
    static int groundLayerId = -1;

    static int GroundLayerId
    {
        get
        {
            if (groundLayerId >= 0) return groundLayerId;
            groundLayerId = 0;                       // 找不到就用 Default，不会出错
            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != "Ground") continue;
                groundLayerId = layers[i].id;
                break;
            }
            return groundLayerId;
        }
    }

    static void ApplyGroundLayer(SpriteRenderer sr)
    {
        sr.sortingLayerID = GroundLayerId;
    }

    /// <summary>平铺素材必须 Repeat，否则边缘会被拉伸出一道糊边（美术替换的图导入时也按 key 设好了）。</summary>
    static void ForceTiling(Sprite sprite)
    {
        if (sprite != null && sprite.texture != null) sprite.texture.wrapMode = TextureWrapMode.Repeat;
    }

    /// <summary>
    /// 九宫格图形（按 size 拉伸，四角不变形）。<paramref name="key"/> 是这个物件的「一物一图」美术 key，
    /// 玩家在 Resources/ArtOverride 里放了同名图片就换成图片（见 <see cref="ArtOverride"/>）。
    /// </summary>
    SpriteRenderer AddSlice(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 localPos, Color color, int order, bool groundLayer = false, string key = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = order;
        if (groundLayer) ApplyGroundLayer(sr);
        if (key != null) ArtOverride.Apply(sr, key);
        return sr;
    }

    /// <summary>纯色长方形：直接缩放 1x1 的矩形贴图，不做九宫格，尺寸多小都不会变形。</summary>
    SpriteRenderer AddRect(Transform parent, string name, Vector2 size, Vector2 localPos, Color color, int order, bool groundLayer = false, string key = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = rectSprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (groundLayer) ApplyGroundLayer(sr);
        if (key != null) ArtOverride.Apply(sr, key);
        return sr;
    }

    SpriteRenderer AddDisc(Transform parent, string name, float size, Vector2 localPos, Color color, int order, bool groundLayer = false, string key = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * size;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = discSprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (groundLayer) ApplyGroundLayer(sr);
        if (key != null) ArtOverride.Apply(sr, key);
        return sr;
    }

    /// <summary>
    /// **整栋建筑**：玩家给这个 key 放了图，就整栋用那一张图（屋顶 / 墙 / 门 / 窗都不用再拼），返回 true。
    ///
    /// 摆放规则：**按原图比例缩放进占地矩形**（contain —— 水平居中、底边压在占地的南边、不超出地块），
    /// 所以图不会被拉变形，也不会盖到邻居头上；代价是图的长宽比和地块差得多时两侧（或上方）会留白。
    /// 尺寸靠 <c>SpriteRenderer.size</c>（世界单位）给定，与导入的 Pixels Per Unit 无关。
    /// </summary>
    bool AddWholeBuilding(Transform parent, string key, Rect area, int order, string name = "WholeBuilding")
    {
        return AddWholeBuilding(parent, key, ArtOverride.Get(key), area, order, name);
    }

    /// <summary>
    /// 同上，但**直接指定用哪一张图**：给「同名多图 = 变体」的整图 key（<c>house_extra</c> / <c>castle</c>）
    /// 按区块挑一张用。注意这种情况下 <c>key</c> 只用来记账（<see cref="ArtOverride.Get"/> 会取回第一张变体，
    /// 那样挑出来的变体就被覆盖了），所以这里不走 <c>AddSlice</c> 的 key 参数。
    /// </summary>
    bool AddWholeBuilding(Transform parent, string key, Sprite image, Rect area, int order, string name = "WholeBuilding")
    {
        if (image == null) return false;

        float aspect = image.rect.width / Mathf.Max(1f, image.rect.height);
        float h = Mathf.Min(area.height, area.width / aspect);
        float w = h * aspect;
        Vector2 local = new Vector2(0f, area.yMin - area.center.y + h * 0.5f);
        AddSlice(parent, name, image, new Vector2(w, h), local, Color.white, order);
        return true;
    }

    // ---------------- 地貌地面 ----------------

    /// <summary>
    /// 按自然体系在这块区块底下铺一层地面（**草原不铺**：直接用场景里那张跟随小虫的无限草地，
    /// 所以 `ground` 这个 key 依然有效；森林铺深色林地、沙漠铺沙地）。
    /// 放在 Ground 层里比无限草地（−900）高一点、比道路（−885）低，所以永远压在路和角色下面。
    /// </summary>
    void BuildGround()
    {
        string key = WorldBiome.GroundKey(nature);
        if (string.IsNullOrEmpty(key)) return;

        // 地面底图：优先用玩家的 `ground` 图片（草地贴图），否则用场景无限草地的贴图；
        // 沙漠另外优先用路面那张土黄色贴图，看起来更像沙地
        Sprite baseSprite = ArtOverride.Get(ArtKeys.Ground);
        if (baseSprite == null) baseSprite = groundSprite != null ? groundSprite : rectSprite;
        Sprite sprite = nature == NatureKind.Desert && roadSprite != null ? roadSprite : baseSprite;

        // 这个地貌自己的地面图（森林 ground_forest / 沙漠 ground_desert）：同名多图 = 变体
        //（草地 2 张、沙 4 张），**按区块**挑一张 —— 一片沙漠不会整片只有一种沙。
        // 挑不到就退回上面那张底图（草原本来就走无限草地，这里直接 return 了）。
        Sprite biomeGround = ArtOverride.PickVariant(key, null, (float)rng.NextDouble());
        if (biomeGround != null) sprite = biomeGround;

        GameObject go = new GameObject("BiomeGround");
        go.transform.SetParent(groundRoot, false);
        go.transform.position = new Vector3(center.x, center.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(chunkSize, chunkSize);   // 正好一块区块，和邻块的网格对齐
        // 用了图片就是「图说了算」（不染色）；还是程序化贴图时才按地貌染色
        Color groundColor = biomeGround != null ? Color.white : WorldBiome.GroundTint(nature);
        sr.color = groundColor;
        ApplyGroundLayer(sr);
        sr.sortingOrder = -895;

        ForceTiling(sr.sprite);

        // **柔边**：往外再铺几圈同样的地面贴图、一层比一层淡。这样这块地貌地面和邻块
        //（尤其是草原的无限草地）之间不再是「一刀切的直角」，而是几米宽的渐变 —— 一块块方形
        // 地貌的边界就化开了（2026-09-25 用户要的「缩小 + 平滑」，见 Spec §4.15）。
        // 邻块是同一片地貌时，几圈半透明同色贴图叠在同一色上，看不出任何痕迹。
        Rect chunkArea = new Rect(center.x - chunkSize * 0.5f, center.y - chunkSize * 0.5f, chunkSize, chunkSize);
        AddFeatherRing(groundRoot, "GroundFeather", chunkArea, sprite, groundColor,
            groundFeatherWidth, groundFeatherSteps, -894);
    }

    /// <summary>
    /// 给一块「铺在地上的矩形」加柔边：往外铺 <paramref name="steps"/> 圈同样的贴图，一层比一层淡。
    /// 好处是**贴图与颜色和本体完全一致**（不需要一整套软边素材），只是透明度往外递减；
    /// 圈数是叠加的，所以从边缘往外是一条阶梯状的透明渐变（4 圈 ≈ 5 级台阶，肉眼已经连续）。
    /// 顺序随便：同色叠加和先后无关。**Order 要压在道路（−885 ~ −882）之下**，免得给路蒙一层。
    /// </summary>
    void AddFeatherRing(Transform parent, string name, Rect area, Sprite sprite, Color color,
        float width, int steps, int order)
    {
        if (sprite == null || width <= 0.01f || steps <= 0) return;

        float step = width / steps;
        for (int i = 0; i < steps; i++)
        {
            float grow = step * (i + 1);
            Rect ring = new Rect(area.xMin - grow, area.yMin - grow, area.width + grow * 2f, area.height + grow * 2f);
            Color ringColor = color;
            // 越外圈越淡（0.55 / 0.41 / 0.28 / 0.14 …）；叠起来正好接上本体的不透明边缘
            ringColor.a = color.a * 0.55f * (1f - i / (float)steps);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(ring.center.x, ring.center.y, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(ring.width, ring.height);
            sr.color = ringColor;
            ApplyGroundLayer(sr);
            sr.sortingOrder = order;
            ForceTiling(sr.sprite);
        }
    }

    // ---------------- 道路（路型随机，相邻区块靠「成片抽签」尽量接得上）----------------

    /// <summary>路型：不再是固定的十字路。</summary>
    enum RoadShape { None, StraightH, StraightV, Cross, TJunction, LJunction }

    // 路在「哪几边」用位表示（路口瓦片就是按这个拼出来的，见 RoadTileKey）
    const int ArmRight = 1;
    const int ArmUp = 2;
    const int ArmLeft = 4;
    const int ArmDown = 8;

    void BuildRoads()
    {
        string key = WorldBiome.RoadKey(settlement);
        Color tint = WorldBiome.RoadTint(settlement);

        RoadShape shape = PickRoadShape();
        if (shape == RoadShape.None) return;

        float half = roadWidth * 0.5f;
        Rect horizontal = new Rect(center.x - radius, center.y - half, radius * 2f, roadWidth);
        Rect vertical = new Rect(center.x - half, center.y - radius, roadWidth, radius * 2f);
        Rect horizontalHalf = new Rect(center.x, center.y - half, radius, roadWidth);
        Rect verticalHalf = new Rect(center.x - half, center.y, roadWidth, radius);

        // 有路口瓦片就按「路口形状」铺瓦片，没有就照旧用长方形互相重叠
        if (UseRoadTilesFor(nature, settlement) && BuildTiledRoad(shape, key, tint))
        {
            // 占位 / 巡逻取样点：按**实际铺出去的那几条边**登记（路口瓦片的朝向是抽出来的，
            // 所以不能再用那几张写死方向的老长方形 —— 否则路铺在下面、房子却盖上去）
            int mask = ArmMask(shape, TurnIndex(chunkCoord, chunkWorldSeed));
            if ((mask & ArmLeft) != 0) RegisterRoad(new Rect(center.x - radius, center.y - half, radius, roadWidth));
            if ((mask & ArmRight) != 0) RegisterRoad(new Rect(center.x, center.y - half, radius, roadWidth));
            if ((mask & ArmUp) != 0) RegisterRoad(new Rect(center.x - half, center.y, roadWidth, radius));
            if ((mask & ArmDown) != 0) RegisterRoad(new Rect(center.x - half, center.y - radius, roadWidth, radius));
            return;
        }

        switch (shape)
        {
            case RoadShape.StraightH:
                AddRoad(horizontal, key, tint);
                break;
            case RoadShape.StraightV:
                AddRoad(vertical, key, tint);
                break;
            case RoadShape.Cross:
                AddRoad(horizontal, key, tint);
                AddRoad(vertical, key, tint);
                break;
            case RoadShape.TJunction:
                AddRoad(horizontal, key, tint);
                AddRoad(verticalHalf, key, tint);
                break;
            case RoadShape.LJunction:
                AddRoad(horizontalHalf, key, tint);
                AddRoad(verticalHalf, key, tint);
                break;
        }
    }

    /// <summary>
    /// 抽这个区块的路型。**按「2×2 个区块一片」的粒度抽**（<see cref="roadRunLength"/>）：
    /// 同一片里的区块大概率是同一种路型，路才能连成一段像样的路网，
    /// 而不是一个区块一个样、满地断头路。有没有路另按 <see cref="roadChance"/> 掷一次（荒野多无路）。
    /// </summary>
    RoadShape PickRoadShape()
    {
        // 「有没有路」走**纯函数**版（哈希版的第一次抽签），这样邻居区块也算得出一模一样的结果
        // （路口瓦片要知道「对面的路接不接得上」）。这里照样消耗一次 rng —— 让后面的随机序列
        // 和以前完全一致，同种子的村庄不会因为这次改动换个样子。
        Chance(roadChance);
        return PickRoadShape(chunkCoord, chunkWorldSeed, settlement, roadChance, roadRunLength);
    }

    /// <summary>
    /// **纯函数**版的路型抽签：只看「区块坐标 + 世界种子 + 聚落 + 路几率 + 成片长度」，
    /// 所以能给**还没生成的邻居区块**算出它的路型。改抽签规则只改这里（实例版也调它）。
    /// </summary>
    static RoadShape PickRoadShape(Vector2Int coord, int worldSeed, SettlementKind settlement, float roadChance, int runLength)
    {
        if (!RoadPresent(coord, worldSeed, roadChance)) return RoadShape.None;

        int run = Mathf.Max(1, runLength);
        int bx = Mathf.FloorToInt(coord.x / (float)run);
        int by = Mathf.FloorToInt(coord.y / (float)run);
        System.Random shapeRng = new System.Random(Hash(worldSeed * 31 + 17, bx * 7919 + 13, by * 104729 + 7));
        double roll = shapeRng.NextDouble();

        if (settlement == SettlementKind.City)
        {
            if (roll < 0.44) return RoadShape.Cross;
            if (roll < 0.60) return RoadShape.TJunction;
            if (roll < 0.76) return RoadShape.LJunction;
            if (roll < 0.88) return RoadShape.StraightH;
            return RoadShape.StraightV;
        }
        if (settlement == SettlementKind.Village)
        {
            if (roll < 0.35) return RoadShape.Cross;
            if (roll < 0.475) return RoadShape.TJunction;
            if (roll < 0.60) return RoadShape.LJunction;
            if (roll < 0.80) return RoadShape.StraightH;
            return RoadShape.StraightV;
        }
        // 荒野：以直路为主，偶尔一个路口
        if (roll < 0.40) return RoadShape.StraightH;
        if (roll < 0.80) return RoadShape.StraightV;
        if (roll < 0.90) return RoadShape.LJunction;
        return RoadShape.Cross;
    }

    /// <summary>
    /// 「这个区块有没有路」= 以区块种子为种子、拿路几率做门槛抽的第一个随机数。
    /// 是**纯函数**（所以邻居也算得出来），而且结果和以前那个实例 rng 抽签**完全一样**。
    /// 前提是「<see cref="BuildChunk"/> 里第一次用 rng 就是抽路」—— 改生成顺序时记得回来看这里。
    /// </summary>
    static bool RoadPresent(Vector2Int coord, int worldSeed, float roadChance)
    {
        return new System.Random(Hash(worldSeed, coord.x, coord.y)).NextDouble() < roadChance;
    }

    /// <summary>路口的朝向（0~3）：也是纯函数，邻居和自己算出来必须一样。</summary>
    static int TurnIndex(Vector2Int coord, int worldSeed)
    {
        return new System.Random(Hash(worldSeed * 31 + 101, coord.x * 5227 + 3, coord.y * 8123 + 5)).Next(4);
    }

    /// <summary>路在哪几边。丁字路口的 <paramref name="turn"/> 决定**缺哪一边**、L 型决定**连哪两边**。</summary>
    static int ArmMask(RoadShape shape, int turn)
    {
        int all = ArmLeft | ArmRight | ArmUp | ArmDown;
        switch (shape)
        {
            case RoadShape.StraightH: return ArmLeft | ArmRight;
            case RoadShape.StraightV: return ArmUp | ArmDown;
            case RoadShape.Cross: return all;
            case RoadShape.TJunction: return all & ~TurnMissing(turn);
            case RoadShape.LJunction:
                switch (((turn % 4) + 4) % 4)
                {
                    case 0: return ArmRight | ArmUp;
                    case 1: return ArmLeft | ArmUp;
                    case 2: return ArmLeft | ArmDown;
                    default: return ArmRight | ArmDown;
                }
            default: return 0;
        }
    }

    /// <summary>丁字路口缺的那一边：0 缺下、1 缺右、2 缺上、3 缺左（和瓦片文件名一一对应）。</summary>
    static int TurnMissing(int turn)
    {
        switch (((turn % 4) + 4) % 4)
        {
            case 0: return ArmDown;
            case 1: return ArmRight;
            case 2: return ArmUp;
            default: return ArmLeft;
        }
    }

    /// <summary>这个「路在哪几边」的形状对应哪张路口瓦片；没有正好对应的返回 null（那就铺路面贴图）。</summary>
    static string RoadTileKey(int mask)
    {
        switch (mask)
        {
            case ArmLeft | ArmRight | ArmUp | ArmDown: return ArtKeys.RoadCross;
            case ArmLeft | ArmRight | ArmUp: return ArtKeys.RoadTUp;
            case ArmLeft | ArmRight | ArmDown: return ArtKeys.RoadTDown;
            case ArmLeft | ArmUp | ArmDown: return ArtKeys.RoadTLeft;
            case ArmRight | ArmUp | ArmDown: return ArtKeys.RoadTRight;
            case ArmLeft | ArmUp: return ArtKeys.RoadCornerUpLeft;
            case ArmRight | ArmUp: return ArtKeys.RoadCornerUpRight;
            case ArmLeft | ArmDown: return ArtKeys.RoadCornerDownLeft;
            case ArmRight | ArmDown: return ArtKeys.RoadCornerDownRight;
            default: return null;
        }
    }

    static string HeadKeyOf(int dir)
    {
        switch (dir)
        {
            case ArmUp: return ArtKeys.RoadHeadUp;
            case ArmDown: return ArtKeys.RoadHeadDown;
            case ArmLeft: return ArtKeys.RoadHeadLeft;
            default: return ArtKeys.RoadHeadRight;
        }
    }

    static int OppositeArm(int dir)
    {
        switch (dir)
        {
            case ArmUp: return ArmDown;
            case ArmDown: return ArmUp;
            case ArmLeft: return ArmRight;
            default: return ArmLeft;
        }
    }

    /// <summary>
    /// **纯函数**：某个区块的路在那几边（连「这个区块用不用瓦片」也算进去了 ——
    /// 不用瓦片的区块保留老朝向，所以两边的判断必须一致）。
    /// </summary>
    int ArmsAt(Vector2Int coord)
    {
        SettlementKind settlementThere = WorldBiome.SettlementAt(coord, chunkWorldSeed);
        NatureKind natureThere = WorldBiome.NatureAt(coord, chunkWorldSeed);
        RoadShape shape = PickRoadShape(coord, chunkWorldSeed, settlementThere,
            WorldBiome.RoadChanceOf(settlementThere), roadRunLength);
        int turn = UseRoadTilesFor(natureThere, settlementThere) ? TurnIndex(coord, chunkWorldSeed) : 0;
        return ArmMask(shape, turn);
    }

    /// <summary>
    /// 这块地要不要走「瓦片路网」。**默认只在草原铺**：交付的这套瓦片自带草地底色，
    /// 铺到森林 / 沙漠 / 城市会露出一块块绿（想铺就去 Inspector 打开 <see cref="roadTilesEverywhere"/>）。
    /// </summary>
    bool UseRoadTilesFor(NatureKind natureThere, SettlementKind settlementThere)
    {
        if (!useRoadTiles) return false;
        if (roadTilesEverywhere) return true;
        return natureThere == NatureKind.Grassland && settlementThere != SettlementKind.City;
    }

    /// <summary>
    /// 路口 / 直路瓦片自带的地面底色。地面已经是图片时不染色（图片说了算）；
    /// 地面还是程序化贴图时，把瓦片染成地貌地面的颜色，接缝处才不露馅。
    /// </summary>
    Color RoadTileGroundTint()
    {
        string groundKey = WorldBiome.GroundKey(nature);        // 草原返回 null（用无限草地）
        if (!string.IsNullOrEmpty(groundKey) && ArtOverride.Has(groundKey)) return Color.white;
        if (ArtOverride.Has(ArtKeys.Ground)) return Color.white;
        return WorldBiome.GroundTint(nature);
    }

    /// <summary>
    /// 按「路口形状」铺路：路口正中一张路口瓦片、每条直路段平铺直路瓦片、路走到边界而对面接不上时用尽头瓦片收口。
    /// 返回 false = 这套瓦片一张都没有，交回「长方形互相重叠」那条老路（行为完全不变）。
    /// </summary>
    bool BuildTiledRoad(RoadShape shape, string key, Color tint)
    {
        int mask = ArmMask(shape, TurnIndex(chunkCoord, chunkWorldSeed));
        string junctionKey = RoadTileKey(mask);
        Sprite junction = string.IsNullOrEmpty(junctionKey) ? null : ArtOverride.Get(junctionKey);
        Sprite straightH = ArtOverride.Get(ArtKeys.RoadStraightH);
        Sprite straightV = ArtOverride.Get(ArtKeys.RoadStraightV);
        if (junction == null && straightH == null && straightV == null) return false;

        float half = roadWidth * 0.5f;
        // 瓦片的世界尺寸 = 路面宽度 ÷ 「瓦片图里路面占画布的比例」：这样瓦片里的路正好和直路段一样宽
        float tile = roadWidth / Mathf.Clamp(roadTileRoadRatio, 0.05f, 1f);
        Color groundTint = RoadTileGroundTint();

        // 正中那一块：**必须也是道路砖块**，不能拿路面贴图去接 —— 那张贴图和瓦片里的路既不同色也不同纹理，
        // 接缝处会露出一块异色方块（用户 2026-09-25：「道路中间不要用除了道路砖块之外的砖块接驳」）。
        bool centreIsTile = false;
        if (junction != null)
        {
            // 注意**不传 key**：AddSlice 带 key 时会调 ArtOverride.Apply，那会把颜色刷成白的，
            // 刚算好的 groundTint（程序化地面时按地貌染色）就白算了。图在这里已经取好了。
            AddSlice(roadsRoot, "RoadJunction", junction, new Vector2(tile, tile), Vector2.zero,
                groundTint, -883, true);
            centreIsTile = true;
        }
        else
        {
            // 直路区块（路只在左右、或只在上下）：正中用**同方向的直路瓦片**收口 ——
            // 它的路是「通长」的，正好把两条臂接起来，纹理与颜色完全一致。
            Sprite centreTile = (mask & (ArmLeft | ArmRight)) != 0 ? straightH : straightV;
            if (centreTile != null)
            {
                AddSlice(roadsRoot, "RoadStraight", centreTile, new Vector2(tile, tile), Vector2.zero,
                    groundTint, -883, true);
                centreIsTile = true;
            }
        }
        if (!centreIsTile)
        {
            // 连直路瓦片都没交时才退回路面贴图（免得中间空一块）—— 有素材的工程走不到这里
            AddRoadRect(new Rect(center.x - half, center.y - half, roadWidth, roadWidth), key, tint, -883);
        }

        // 直路段从瓦片边缘开始铺（用瓦片收口时），否则从路面方块边缘开始
        float start = centreIsTile ? tile * 0.5f : half;

        // 左右臂的路是**横向**的 → 用横向直路瓦片（road_straight_h，图里的路沿 X 走）；
        // 上下臂反过来。**别搞反**：搞反了每段路会画成一条横在路中央的挡板（2026-09-25 踩到）。
        BuildRoadArm(ArmLeft, mask, straightH, start, tile, key, tint, groundTint);
        BuildRoadArm(ArmRight, mask, straightH, start, tile, key, tint, groundTint);
        BuildRoadArm(ArmUp, mask, straightV, start, tile, key, tint, groundTint);
        BuildRoadArm(ArmDown, mask, straightV, start, tile, key, tint, groundTint);
        return true;
    }

    /// <summary>
    /// 铺一条直路段，并在「这条路走到区块边界、对面却没有对接的路」时放一张尽头瓦片收口。
    /// <paramref name="straightTile"/> 为空就用路面贴图（两条路都对得上，因为宽度和中心线是一样的）。
    /// </summary>
    void BuildRoadArm(int dir, int mask, Sprite straightTile, float start, float tile, string key, Color tint, Color groundTint)
    {
        if ((mask & dir) == 0) return;                       // 这一边本来就没有路

        float length = radius - start;
        if (length <= 0.05f) return;

        bool horizontal = dir == ArmLeft || dir == ArmRight;
        float sign = (dir == ArmRight || dir == ArmUp) ? 1f : -1f;
        Vector2 armCenter = horizontal
            ? new Vector2(sign * (start + length * 0.5f), 0f)
            : new Vector2(0f, sign * (start + length * 0.5f));

        if (straightTile != null)
        {
            // **用 Sliced（拉伸）而不是 Tiled**：Tiled 是按「精灵自己的自然尺寸」重复的，
            // 而这张瓦片的自然尺寸是 128px ÷ 64PPU = **2 世界单位**，可路面要好几米 ——
            // 于是一条路面上会并排重复好几次，画出来就是若干条细条纹（2026-09-25 踩到的「条纹状」）。
            //
            // 沿路方向**分几张铺**（不是一张拉到底）：瓦片里的路是「通长一条」、且左右边值对得上，
            // 所以相邻两张天然接得上；分几张是为了别把图的横向细节拉长（路越窄、瓦片越小，
            // 一张拉到底就会拉成 2 倍长）。张数 = 长度 ÷ 瓦片尺寸 四舍五入，长度再**均分**给每张。
            int count = Mathf.Max(1, Mathf.RoundToInt(length / tile));
            float piece = length / count;
            for (int i = 0; i < count; i++)
            {
                float along = start + piece * (i + 0.5f);
                Vector2 at = horizontal ? new Vector2(sign * along, 0f) : new Vector2(0f, sign * along);
                Vector2 size = horizontal ? new Vector2(piece, tile) : new Vector2(tile, piece);
                AddSlice(roadsRoot, "RoadStraight", straightTile, size, at, groundTint, -884, true);
            }
        }
        else
        {
            Vector2 size = horizontal ? new Vector2(length, roadWidth) : new Vector2(roadWidth, length);
            AddRoadRect(new Rect(center.x + armCenter.x - size.x * 0.5f, center.y + armCenter.y - size.y * 0.5f,
                size.x, size.y), key, tint, -884);
        }

        // 尽头收口：隔壁那块地没有能接上的路 → 在边界上盖一张「路尽头」瓦片
        Vector2Int step = horizontal
            ? new Vector2Int(dir == ArmRight ? 1 : -1, 0)
            : new Vector2Int(0, dir == ArmUp ? 1 : -1);
        if ((ArmsAt(chunkCoord + step) & OppositeArm(dir)) != 0) return;

        string headKey = HeadKeyOf(dir);
        Sprite head = ArtOverride.Get(headKey);
        if (head == null) return;

        // 路尽头瓦片里的「路尖」**不在瓦片正中**（交付的这套在 0.70 处，见 roadHeadTipRatio），
        // 所以按「让路尖正好落在区块边界上」来摆：把瓦片中心摆在边界上会让路多伸出去 0.2×tile ≈ 2.4 米。
        float offset = sign * (radius - (roadHeadTipRatio - 0.5f) * tile);
        Vector2 endPoint = horizontal ? new Vector2(offset, 0f) : new Vector2(0f, offset);
        AddSlice(roadsRoot, "RoadHead", head, new Vector2(tile, tile), endPoint, groundTint, -882, true);
    }

    /// <summary>铺一段路（占位 + 给守卫巡逻用的取样点）。</summary>
    void AddRoad(Rect area, string key, Color tint)
    {
        RegisterRoad(area);
        AddRoadRect(area, key, tint, -885);
    }

    /// <summary>只登记「这儿是路」（占位 + 守卫巡逻的取样点），不画东西。</summary>
    void RegisterRoad(Rect area)
    {
        occupied.Add(area);
        roadRects.Add(area);

        if (map == null) return;
        // 守卫巡逻用的路上取样点（沿着这段路走）
        if (area.width >= area.height)
        {
            for (float x = area.xMin + 3f; x <= area.xMax - 3f; x += 6f) map.roads.Add(new Vector2(x, area.center.y));
        }
        else
        {
            for (float y = area.yMin + 3f; y <= area.yMax - 3f; y += 6f) map.roads.Add(new Vector2(area.center.x, y));
        }
    }

    void AddRoadRect(Rect area, string key, Color tint, int order)
    {
        GameObject go = new GameObject("Road");
        go.transform.SetParent(roadsRoot, false);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = roadSprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(area.width, area.height);
        sr.color = tint;
        // 道路和草地同在 Ground 层（草地 -900），路比草地高一点才看得见
        ApplyGroundLayer(sr);
        sr.sortingOrder = order;
        ArtOverride.Apply(sr, key);
        ForceTiling(sr.sprite);
    }

    // ---------------- 广场 + 中央设施（水井 / 喷泉）----------------

    /// <summary>
    /// 广场与「村子中心那口井」都**不再是必然**了：农村约一半区块有广场、井另掷一次；
    /// 城市中心换成喷泉；荒野根本没有广场，只会偶尔有一口孤零零的井。
    /// </summary>
    void BuildPlazaAndCenterpiece()
    {
        bool plaza = settlement != SettlementKind.Wilderness && plazaSize > 0.5f && Chance(plazaChance);

        string key = WorldBiome.RoadKey(settlement);
        Color tint = WorldBiome.RoadTint(settlement);

        if (plaza)
        {
            // **不铺方砖时只留一小圈**：以前那块 10×10 是「铺装广场」所以要留空，
            // 现在村子中心只是「路口 + 水井」，再空着 10×10 就白占了 10% 的地（用户 2026-09-25
            // 紧接着要「房屋密度不够」）。所以不铺铺装时把保留区收到井边一小块就走。
            float reserve = drawPlazaPaving ? plazaSize : Mathf.Min(plazaSize, plazaClearRadius * 2f);
            Rect area = new Rect(center.x - reserve * 0.5f, center.y - reserve * 0.5f, reserve, reserve);
            // **位置照旧登记**（occupied / roadRects）：水井周围留一小块空地，房子不会压到井上；
            // 守卫巡逻的取样点也照旧。
            occupied.Add(area);
            roadRects.Add(area);

            // 但**默认不铺那块方形铺装了**（2026-09-25 用户：「取消水井周围的方形地块」）：
            // 村中心就是「路口 + 水井」，脚下是草地，不再有那块 10×10 的方砖。
            // 顺带解决了「广场盖住路口瓦片、四角露出一圈草地」那个遗留（见 §4.13）。
            // **注意不要动上面那次 Chance(plazaChance) 抽签** —— 少了它整条随机序列会错位，
            // 所有区块的布局都会变（Spec §4.2 的确定性）。
            if (drawPlazaPaving)
            {
                AddRoadRect(area, key, tint, -880);          // 广场比路面再高一层
                // 铺装柔边：往外几圈半透明路面贴图（order 压在道路之下，免得给路蒙一层）
                AddFeatherRing(roadsRoot, "PlazaFeather", area, roadSprite, tint,
                    plazaFeatherWidth, plazaFeatherSteps, -888);
            }
        }

        Vector2 spot = center;
        bool haveSpot = plaza;
        if (!haveSpot) haveSpot = TryFindNearCenter(2.4f, 1.4f, Mathf.Min(radius - 2f, plazaSize * 0.5f + 4f), out spot);
        if (!haveSpot) return;

        if (settlement == SettlementKind.City)
        {
            if (Chance(fountainChance)) BuildFountain(spot);
        }
        else if (Chance(wellChance))
        {
            BuildWell(spot);
        }
    }

    // ---------------- 水井 / 喷泉 ----------------

    void BuildWell(Vector2 at)
    {
        GameObject go = new GameObject("Well");
        go.transform.SetParent(facilitiesRoot, false);
        go.transform.position = new Vector3(at.x, at.y, 0f);

        int order = YOrder(at.y);
        AddDisc(go.transform, "Rim", 3.0f, Vector2.zero, new Color(0.52f, 0.51f, 0.48f), order, key: ArtKeys.WellRim);
        AddDisc(go.transform, "Water", 2.1f, Vector2.zero, new Color(0.14f, 0.24f, 0.34f), order + 1, key: ArtKeys.WellWater);
        AddDisc(go.transform, "Post", 0.9f, new Vector2(1.5f, 0.4f), new Color(0.42f, 0.29f, 0.16f), order + 2, key: ArtKeys.WellPost);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 1.5f;
        occupied.Add(new Rect(at.x - 1.6f, at.y - 1.6f, 3.2f, 3.2f));

        if (map != null) map.wells.Add(at);
    }

    /// <summary>喷泉：城市广场的中心（水井的城市版本）。</summary>
    void BuildFountain(Vector2 at)
    {
        GameObject go = new GameObject("Fountain");
        go.transform.SetParent(facilitiesRoot, false);
        go.transform.position = new Vector3(at.x, at.y, 0f);

        int order = YOrder(at.y);
        AddDisc(go.transform, "Rim", 3.6f, Vector2.zero, new Color(0.62f, 0.60f, 0.56f), order, key: ArtKeys.FountainRim);
        AddDisc(go.transform, "Water", 2.8f, Vector2.zero, new Color(0.24f, 0.46f, 0.62f), order + 1, key: ArtKeys.FountainWater);
        AddDisc(go.transform, "Jet", 0.55f, Vector2.zero, new Color(0.72f, 0.86f, 0.94f, 0.9f), order + 2, key: ArtKeys.FountainWater);
        AddDisc(go.transform, "JetTop", 0.3f, new Vector2(0f, 0.62f), new Color(0.82f, 0.92f, 0.97f, 0.8f), order + 3, key: ArtKeys.FountainWater);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 1.8f;
        occupied.Add(new Rect(at.x - 1.9f, at.y - 1.9f, 3.8f, 3.8f));

        if (map != null) map.AddAnchor("fountain", at);
    }

    // ---------------- 设施 ----------------

    void BuildFacilities()
    {
        if (Chance(farmChance)) BuildFarm();
        if (Chance(penChance)) BuildPen();
        if (Chance(bakeryChance)) CreateShop("Bakery", new Color(0.88f, 0.74f, 0.40f), new Color(0.45f, 0.32f, 0.24f), false);
        if (Chance(smithyChance)) CreateShop("Smithy", new Color(0.62f, 0.60f, 0.58f), new Color(0.32f, 0.30f, 0.32f), true);
        if (Chance(powerPlantChance)) BuildPowerPlant();
        if (Chance(windmillChance)) BuildWindmill();
        if (Chance(bellTowerChance)) BuildBellTower();
        if (settlement == SettlementKind.City && Chance(castleChance)) BuildCastle();
        if (Chance(stallChance)) BuildStall(Rand(0f, 360f));
        if (Chance(gardenChance)) BuildGarden();
        if (Chance(boardChance)) BuildNoticeBoard();
        int benches = RandInt(1, 3);
        for (int i = 0; i < benches; i++) BuildBench();
        BuildLamps();
    }

    /// <summary>
    /// 不分聚落的地标：荒野的篝火、沙漠的绿洲（绿洲在沙漠的村庄 / 城市里也可能有，
    /// 所以它不走 <see cref="BuildFacilities"/>）。
    /// </summary>
    void BuildLooseLandmarks()
    {
        if (campfireChance > 0f && Chance(campfireChance)) BuildCampfire();
        if (oasisChance > 0f && Chance(oasisChance)) BuildOasis();
    }

    /// <summary>
    /// 城堡：**城市里的稀有地标**（只有在城市、且 <see cref="castleChance"/> 掷中时才盖）。
    /// 是一整张图（<c>castle</c>，放两张就是两个变体），按原比例装进一块空地 —— 和整栋房子同一套摆放规则。
    /// 没有图片时什么都不做（城市照旧）。
    /// </summary>
    void BuildCastle()
    {
        if (ArtOverride.VariantCount(ArtKeys.Castle) == 0) return;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            float w = Rand(5.6f, 7.4f);
            float h = Rand(5.2f, 6.8f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1.2f, radius - w * 0.5f - 1.2f),
                                             Rand(-radius + h * 0.5f + 1.2f, radius - h * 0.5f - 1.2f));
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.4f)) continue;

            occupied.Add(area);
            GameObject go = new GameObject("Castle");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(area.center.x, area.center.y, 0f);

            Sprite image = ArtOverride.PickVariant(ArtKeys.Castle, null, (float)rng.NextDouble());
            AddWholeBuilding(go.transform, ArtKeys.Castle, image, area, YOrder(area.center.y), "CastleImage");

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(area.width * 0.9f, area.height * 0.82f);
            if (map != null) map.AddAnchor("castle", area.center);
            return;
        }
    }

    /// <summary>
    /// 发电站（2026-09-25 新增）：一栋厂房 + 烟囱 + 两个发光的线圈 + 两根电线杆。
    /// 它同时也是**电池的聚集点**（`ArtKeys` / `SpawnRule.nearAnchor = "power"`）——
    /// 「电池在发电站附近比较多」就是这么来的。它自己是个 <see cref="PoweredProp"/>，
    /// 所以剪断附近的电线也会让它停（和步骤③ 的断电链串起来）。
    /// </summary>
    void BuildPowerPlant()
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float w = Rand(3.4f, 4.2f);
            float h = Rand(2.4f, 3.0f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1f, radius - w * 0.5f - 1f),
                                             Rand(-radius + h * 0.5f + 1f, radius - h * 0.5f - 1f));
            if ((c - center).magnitude < plazaSize * 0.5f + 5f) continue;    // 别贴着广场
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.2f)) continue;
            occupied.Add(new Rect(area.x - 0.6f, area.y - 0.6f, area.width + 1.2f, area.height + 1.2f));

            GameObject go = new GameObject("PowerPlant");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            AddSlice(go.transform, "Body", rectSprite, new Vector2(w, h), Vector2.zero,
                new Color(0.52f, 0.55f, 0.60f), order, key: ArtKeys.PowerPlant);
            AddRect(go.transform, "Chimney", new Vector2(0.52f, 1.1f), new Vector2(-w * 0.3f, h * 0.5f + 0.3f),
                new Color(0.42f, 0.42f, 0.45f), order + 1, key: ArtKeys.HouseChimney);

            // 两个线圈：夜里发亮，一眼看出这是「有电的地方」
            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer coil = AddDisc(go.transform, "Coil", 0.46f,
                    new Vector2(w * (i == 0 ? -0.22f : 0.24f), -h * 0.28f),
                    new Color(0.75f, 0.82f, 0.95f), order + 2, key: ArtKeys.PowerCoil);
                NightGlow glow = coil.gameObject.AddComponent<NightGlow>();
                glow.dayColor = new Color(0.75f, 0.82f, 0.95f);
                glow.nightColor = new Color(0.62f, 0.95f, 1f);
            }

            // 两根电线杆（告诉玩家「电是从这儿送出去的」）
            AddRect(go.transform, "Pole", new Vector2(0.16f, 0.9f), new Vector2(-w * 0.5f - 0.5f, h * 0.2f),
                new Color(0.35f, 0.30f, 0.26f), order, key: ArtKeys.PowerPole);
            AddRect(go.transform, "Pole", new Vector2(0.16f, 0.9f), new Vector2(w * 0.5f + 0.5f, h * 0.2f),
                new Color(0.35f, 0.30f, 0.26f), order, key: ArtKeys.PowerPole);

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(w, h);

            // 它是一台「通电才工作」的机器：附近电线被剪断就会停
            PoweredProp prop = go.AddComponent<PoweredProp>();
            prop.offColor = new Color(0.42f, 0.44f, 0.47f);
            Breakable breakable = go.AddComponent<Breakable>();
            breakable.hp = 4f;                     // 结实：得撞好几下
            breakable.debrisColor = new Color(0.55f, 0.58f, 0.62f);

            HiddenValue hidden = go.AddComponent<HiddenValue>();
            hidden.value = RandInt(30, 45);
            hidden.note = "发电站：电池都堆在这附近";
            EntityInfo info = go.AddComponent<EntityInfo>();
            info.title = "发电站";
            info.kind = "设施";
            info.description = "嗡嗡作响的小发电站。附近总有人丢下的旧电池 —— 想拿能力就从这儿找。";

            // 登记锚点：物品的「就近生成」会来查这里（电池就靠它聚在附近）
            if (map != null) map.AddAnchor("power", c);
            return;
        }
    }

    /// <summary>
    /// 风车（农村的地标）：塔身 + 四片会转的叶片。
    /// 塔身与叶片各占一个美术 key（<c>windmill_body</c> / <c>windmill_blade</c>），塔顶画在塔身图里。
    /// </summary>
    void BuildWindmill()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float w = Rand(1.8f, 2.4f);
            float h = Rand(2.6f, 3.4f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1f, radius - w * 0.5f - 1f),
                                             Rand(-radius + h * 0.5f + 1f, radius - h * 0.5f - 1f));
            if ((c - center).magnitude < plazaSize * 0.5f + 4f) continue;
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.6f)) continue;
            occupied.Add(new Rect(area.x - 0.5f, area.y - 0.5f, area.width + 1f, area.height + 1f));

            GameObject go = new GameObject("Windmill");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);

            // 整栋风车图（叶片也画在图上）：放了这张图就整栋用图，不再生成会转的叶片（会重复画两套叶片）
            if (!AddWholeBuilding(go.transform, ArtKeys.Windmill, area, order))
            {
                AddSlice(go.transform, "Body", rectSprite, new Vector2(w, h), Vector2.zero,
                    new Color(0.82f, 0.76f, 0.62f), order, key: ArtKeys.WindmillBody);

                // 四片叶片挂在同一个轮毂上，整体慢慢转（Spinner）
                GameObject hub = new GameObject("Blades");
                hub.transform.SetParent(go.transform, false);
                hub.transform.localPosition = new Vector3(0f, h * 0.5f + 0.15f, 0f);
                for (int i = 0; i < 4; i++)
                {
                    GameObject blade = new GameObject("Blade" + i);
                    blade.transform.SetParent(hub.transform, false);
                    blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                    AddRect(blade.transform, "Blade", new Vector2(0.26f, 2.1f), new Vector2(0f, 1.05f),
                        new Color(0.88f, 0.84f, 0.72f), order + 2, key: ArtKeys.WindmillBlade);
                }
                hub.AddComponent<Spinner>().degreesPerSecond = Rand(22f, 42f);
            }

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(w, h);

            HiddenValue hidden = go.AddComponent<HiddenValue>();
            hidden.value = RandInt(20, 34);
            hidden.note = "风车：磨面的地方";
            EntityInfo info = go.AddComponent<EntityInfo>();
            info.title = "风车";
            info.kind = "设施";
            info.description = "叶片慢慢转着的风车。塔身结实，撞不动 —— 但它旁边老是堆着没人要的东西。";

            if (map != null) map.AddAnchor("windmill", c);
            return;
        }
    }

    /// <summary>钟楼（城市的地标）：塔身 + 一口钟。整点会敲（见 <see cref="BellTower"/>）。</summary>
    void BuildBellTower()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float w = Rand(1.6f, 2.0f);
            float h = Rand(3.6f, 4.6f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1f, radius - w * 0.5f - 1f),
                                             Rand(-radius + h * 0.5f + 1f, radius - h * 0.5f - 1f));
            if ((c - center).magnitude < plazaSize * 0.5f + 3f) continue;
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.6f)) continue;
            occupied.Add(new Rect(area.x - 0.5f, area.y - 0.5f, area.width + 1f, area.height + 1f));

            GameObject go = new GameObject("BellTower");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            AddSlice(go.transform, "Tower", rectSprite, new Vector2(w, h), Vector2.zero,
                new Color(0.80f, 0.76f, 0.68f), order, key: ArtKeys.BellTower);

            Vector2 bellPos = new Vector2(0f, h * 0.5f - 0.6f);
            AddDisc(go.transform, "Bell", 0.62f, bellPos, new Color(0.76f, 0.64f, 0.30f), order + 2, key: ArtKeys.Bell);
            // 光晕是运行时表现，不占美术 key（同路灯的 Glow / 头顶的「!」）
            SpriteRenderer halo = AddDisc(go.transform, "BellHalo", 2.6f, bellPos, new Color(1f, 0.92f, 0.60f, 0f), order + 1);

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(w, h);

            BellTower tower = go.AddComponent<BellTower>();
            tower.halo = halo;

            EntityInfo info = go.AddComponent<EntityInfo>();
            info.title = "钟楼";
            info.kind = "设施";
            info.description = "城里最高的那栋。每到整点就敲一次钟 —— 一声下去，半个村子的人都抬头。";

            if (map != null) map.AddAnchor("bell", c);
            return;
        }
    }

    /// <summary>篝火（荒野）：一圈石头 + 一簇火苗（夜里更亮）。</summary>
    void BuildCampfire()
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 c;
            if (!TryFindFreePoint(1.5f, 1.4f, out c, 40)) continue;
            occupied.Add(new Rect(c.x - 1.5f, c.y - 1.5f, 3f, 3f));

            GameObject go = new GameObject("Campfire");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                AddDisc(go.transform, "Stone", 0.36f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.66f,
                    new Color(0.46f, 0.45f, 0.43f), order, key: ArtKeys.CampfireStone);
            }
            SpriteRenderer fire = AddDisc(go.transform, "Fire", 0.95f, Vector2.zero,
                new Color(0.95f, 0.55f, 0.18f, 0.9f), order + 1, key: ArtKeys.CampfireFire);
            NightGlow glow = fire.gameObject.AddComponent<NightGlow>();
            glow.dayColor = new Color(0.95f, 0.55f, 0.18f, 0.85f);
            glow.nightColor = new Color(1f, 0.70f, 0.25f, 1f);
            glow.nightScale = 1.3f;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.9f, 1.9f);

            HiddenValue hidden = go.AddComponent<HiddenValue>();
            hidden.value = RandInt(10, 20);
            hidden.note = "篝火：野地里唯一还活着的东西";
            EntityInfo info = go.AddComponent<EntityInfo>();
            info.title = "篝火";
            info.kind = "设施";
            info.description = "不知道谁生的火，还在烧。野地里唯一一点暖色。";

            if (map != null) map.AddAnchor("campfire", c);
            return;
        }
    }

    /// <summary>绿洲（沙漠里的稀有景点）：一汪水 + 几棵棕榈。</summary>
    void BuildOasis()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 c;
            if (!TryFindFreePoint(3.2f, 2.2f, out c, 50)) continue;
            occupied.Add(new Rect(c.x - 3.2f, c.y - 3.2f, 6.4f, 6.4f));

            GameObject go = new GameObject("Oasis");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            float pond = Rand(2.8f, 3.6f);
            AddDisc(go.transform, "Pond", pond, Vector2.zero, new Color(0.20f, 0.42f, 0.52f), order - 2,
                groundLayer: true, key: ArtKeys.OasisWater);

            int palms = RandInt(3, 6);
            for (int i = 0; i < palms; i++)
            {
                float a = i / (float)palms * Mathf.PI * 2f + Rand(-0.3f, 0.3f);
                float d = pond * 0.62f + Rand(0.1f, 0.7f);
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                CreatePalm(go.transform, p, YOrder(c.y + p.y) + 1);
            }

            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = pond * 0.72f;

            HiddenValue hidden = go.AddComponent<HiddenValue>();
            hidden.value = RandInt(24, 40);
            hidden.note = "绿洲：沙漠里的一口水";
            EntityInfo info = go.AddComponent<EntityInfo>();
            info.title = "绿洲";
            info.kind = "景物";
            info.description = "沙漠中间的一汪水，边上长着几棵棕榈。走到这儿说明你走了很远了。";

            if (map != null) map.AddAnchor("oasis", c);
            return;
        }
    }

    /// <summary>农田：一块翻好的地 + 垄 + 幼苗（可以走进去，农夫就在田里干活）。</summary>
    void BuildFarm()
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float w = Rand(5.0f, 6.6f);
            float h = Rand(3.4f, 4.4f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1f, radius - w * 0.5f - 1f), Rand(-radius + h * 0.5f + 1f, radius - h * 0.5f - 1f));
            if ((c - center).magnitude < plazaSize * 0.5f + 4f) continue;      // 别贴着广场
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.2f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("Farm");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            // 农田是「平铺在地上的」，放 Ground 层按固定次序排，
            // 这样站在田里的小虫/村民一定画在田上面，不会被整块地遮住
            AddSlice(go.transform, "Soil", rectSprite, new Vector2(w, h), Vector2.zero, new Color(0.37f, 0.27f, 0.18f), -870, true, ArtKeys.FarmSoil);

            int rows = Mathf.Max(3, Mathf.FloorToInt(h / 0.6f));
            for (int r = 0; r < rows; r++)
            {
                float rowY = -h * 0.5f + 0.45f + r * (h - 0.6f) / Mathf.Max(1, rows - 1);
                AddRect(go.transform, "Row", new Vector2(w - 0.55f, 0.15f), new Vector2(0f, rowY), new Color(0.30f, 0.21f, 0.13f), -860, true, ArtKeys.FarmRow);
                for (int k = 0; k < 3; k++)
                    AddDisc(go.transform, "Sprout", 0.2f, new Vector2(-w * 0.3f + k * w * 0.3f, rowY + 0.07f), new Color(0.42f, 0.62f, 0.28f), -850, true, ArtKeys.FarmSprout);
            }

            if (map != null) map.farms.Add(c);
            return;
        }
    }

    /// <summary>畜栏：三面围栏 + 一群羊（南面留口子，牧羊人走进去）。</summary>
    void BuildPen()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float w = Rand(5.5f, 7.0f);
            float h = Rand(4.4f, 5.4f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1f, radius - w * 0.5f - 1f), Rand(-radius + h * 0.5f + 1f, radius - h * 0.5f - 1f));
            if ((c - center).magnitude < plazaSize * 0.5f + 5f) continue;
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.4f)) continue;
            occupied.Add(new Rect(c.x - w * 0.5f - 0.4f, c.y - h * 0.5f - 0.4f, w + 0.8f, h + 0.8f));

            GameObject go = new GameObject("Pen");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y) - 4;
            Color fence = new Color(0.52f, 0.40f, 0.26f);
            // 牧场草地也是平铺在地上的 → Ground 层，不会挡住里面的羊和牧羊人
            AddSlice(go.transform, "Grass", rectSprite, new Vector2(w, h), Vector2.zero, new Color(0.38f, 0.48f, 0.26f), -845, true, ArtKeys.PenGrass);
            AddRect(go.transform, "FenceN", new Vector2(w, 0.2f), new Vector2(0f, h * 0.5f), fence, YOrder(c.y + h * 0.5f) + 1, key: ArtKeys.Fence);
            AddRect(go.transform, "FenceW", new Vector2(0.2f, h), new Vector2(-w * 0.5f, 0f), fence, order + 1, key: ArtKeys.Fence);
            AddRect(go.transform, "FenceE", new Vector2(0.2f, h), new Vector2(w * 0.5f, 0f), fence, order + 1, key: ArtKeys.Fence);

            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2(Rand(-w * 0.32f, w * 0.32f), Rand(-h * 0.32f, h * 0.32f));
                // 羊各自按自己的 Y 排序，站在羊附近的人才不会被羊挡住
                int sheepOrder = YOrder(c.y + p.y) + 2;
                AddDisc(go.transform, "Sheep", 0.52f, p, new Color(0.93f, 0.92f, 0.87f), sheepOrder, key: ArtKeys.Sheep);
                AddDisc(go.transform, "SheepHead", 0.24f, p + new Vector2(0.24f, 0.14f), new Color(0.30f, 0.28f, 0.30f), sheepOrder + 1, key: ArtKeys.SheepHead);
            }

            if (map != null) map.pens.Add(c);
            return;
        }
    }

    void CreateShop(string name, Color signColor, Color chimneyColor, bool forge)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float w = Rand(4.0f, 4.8f);
            float h = Rand(3.3f, 3.8f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 1f, radius - w * 0.5f - 1f), Rand(-radius + h * 0.5f + 1f, radius - h * 0.5f - 1f));
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.3f)) continue;
            occupied.Add(area);

            // 作坊的房子本身用农舍造型，但烟囱自己加（所以 CreateHouse 里不要重复加）
            GameObject go = CreateHouse(area, housesRoot, HouseType.Cottage, false);
            go.name = name;

            int order = YOrder(c.y);
            float wallHeight = Mathf.Clamp(area.height * 0.30f, 0.5f, 0.8f);
            float wallY = -area.height * 0.5f + wallHeight * 0.5f;
            AddRect(go.transform, "Sign", new Vector2(0.95f, 0.42f), new Vector2(0f, wallY + 0.42f), signColor, order + 3, key: ArtKeys.ShopSign);
            AddRect(go.transform, "Chimney", new Vector2(0.38f, 0.7f), new Vector2(area.width * 0.28f, area.height * 0.5f + 0.2f), chimneyColor, order + 1, key: ArtKeys.HouseChimney);

            if (forge)
            {
                AddRect(go.transform, "Anvil", new Vector2(0.5f, 0.32f), new Vector2(-area.width * 0.42f, -area.height * 0.5f - 0.5f), new Color(0.22f, 0.22f, 0.24f), order + 2, key: ArtKeys.Anvil);
                SpriteRenderer fire = AddDisc(go.transform, "Forge", 0.55f, new Vector2(-area.width * 0.42f, -area.height * 0.5f - 0.3f), new Color(0.95f, 0.45f, 0.15f), order + 1, key: ArtKeys.Forge);
                NightGlow glow = fire.gameObject.AddComponent<NightGlow>();
                glow.dayColor = new Color(0.95f, 0.45f, 0.15f, 0.45f);
                glow.nightColor = new Color(1f, 0.62f, 0.20f, 0.95f);
                glow.nightScale = 1.35f;
            }

            if (map != null)
            {
                Vector2 front = new Vector2(area.center.x, area.yMin - 0.9f);
                if (forge) { map.smithy = front; map.hasSmithy = true; }
                else { map.bakery = front; map.hasBakery = true; }
            }
            return;
        }
    }

    /// <summary>集市摊位：柜台 + 遮阳篷 + 货物，摆在广场边上。</summary>
    void BuildStall(float startAngle)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float angle = (startAngle + attempt * 29f) * Mathf.Deg2Rad;
            float distance = Mathf.Max(3.2f, plazaSize * 0.5f + 3.2f) + (attempt % 3) * 1.6f;
            Vector2 c = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if ((c - center).magnitude > radius - 1.5f) continue;
            Rect area = new Rect(c.x - 1.1f, c.y - 0.8f, 2.2f, 1.6f);
            if (!IsFree(area, 1.1f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("Stall");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);

            // 整栋棚子图（柜台 + 篷 + 货物一张图）：放了就整栋用图，不再拼那几件
            if (!AddWholeBuilding(go.transform, ArtKeys.Stall, area, order))
            {
                Color awning = Pick(StallColors);
                AddRect(go.transform, "Counter", new Vector2(1.7f, 0.5f), new Vector2(0f, -0.3f), new Color(0.56f, 0.41f, 0.25f), order, key: ArtKeys.StallCounter);
                AddRect(go.transform, "Awning", new Vector2(1.95f, 0.45f), new Vector2(0f, 0.35f), awning, order + 1, key: ArtKeys.StallAwning);
                AddRect(go.transform, "PostL", new Vector2(0.12f, 0.9f), new Vector2(-0.92f, 0.05f), new Color(0.45f, 0.33f, 0.20f), order, key: ArtKeys.StallPost);
                AddRect(go.transform, "PostR", new Vector2(0.12f, 0.9f), new Vector2(0.92f, 0.05f), new Color(0.45f, 0.33f, 0.20f), order, key: ArtKeys.StallPost);
                AddDisc(go.transform, "Goods1", 0.3f, new Vector2(-0.5f, -0.1f), new Color(0.85f, 0.48f, 0.26f), order + 2, key: ArtKeys.StallGoods);
                AddDisc(go.transform, "Goods2", 0.26f, new Vector2(0f, -0.08f), new Color(0.90f, 0.80f, 0.35f), order + 2, key: ArtKeys.StallGoods);
                AddDisc(go.transform, "Goods3", 0.28f, new Vector2(0.5f, -0.1f), new Color(0.45f, 0.66f, 0.36f), order + 2, key: ArtKeys.StallGoods);
            }

            if (map != null) map.stalls.Add(new Vector2(c.x, c.y - 1.15f));   // 摊主站在柜台前
            return;
        }
    }

    /// <summary>花园：花坛 + 一圈小花。</summary>
    void BuildGarden()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float w = Rand(2.6f, 3.6f);
            float h = Rand(2.2f, 3.0f);
            Vector2 c = center + new Vector2(Rand(-radius + 2f, radius - 2f), Rand(-radius + 2f, radius - 2f));
            if ((c - center).magnitude < plazaSize * 0.5f + 1.5f) continue;
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 0.9f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("Garden");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            // 花坛同样是地面上的平铺物件 → Ground 层，村民站在花坛里也看得见
            AddSlice(go.transform, "Bed", rectSprite, new Vector2(w, h), Vector2.zero, new Color(0.30f, 0.40f, 0.24f), -840, true, ArtKeys.GardenBed);
            AddRect(go.transform, "EdgeN", new Vector2(w, 0.14f), new Vector2(0f, h * 0.5f), new Color(0.62f, 0.58f, 0.50f), -835, true, ArtKeys.GardenEdge);
            AddRect(go.transform, "EdgeS", new Vector2(w, 0.14f), new Vector2(0f, -h * 0.5f), new Color(0.62f, 0.58f, 0.50f), -835, true, ArtKeys.GardenEdge);
            for (int i = 0; i < 7; i++)
                AddDisc(go.transform, "Flower", Rand(0.18f, 0.26f), new Vector2(Rand(-w * 0.36f, w * 0.36f), Rand(-h * 0.34f, h * 0.34f)), Pick(FlowerColors), -830, true, ArtKeys.GardenFlower);

            if (map != null) map.gardens.Add(c);
            return;
        }
    }

    /// <summary>公告板：广场边上的留言板。</summary>
    void BuildNoticeBoard()
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = Rand(0f, Mathf.PI * 2f);
            float distance = Mathf.Max(1.8f, plazaSize * 0.5f + Rand(1.8f, 3.4f));
            Vector2 c = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Rect area = new Rect(c.x - 0.8f, c.y - 0.5f, 1.6f, 1.0f);
            if (!IsFree(area, 0.7f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("NoticeBoard");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            AddRect(go.transform, "Post", new Vector2(0.16f, 1.0f), new Vector2(0f, -0.4f), new Color(0.44f, 0.31f, 0.19f), order, key: ArtKeys.BoardPost);
            AddRect(go.transform, "Board", new Vector2(1.25f, 0.8f), new Vector2(0f, 0.28f), new Color(0.60f, 0.44f, 0.27f), order + 1, key: ArtKeys.Board);
            AddRect(go.transform, "Paper1", new Vector2(0.32f, 0.4f), new Vector2(-0.3f, 0.3f), new Color(0.93f, 0.92f, 0.86f), order + 2, key: ArtKeys.BoardPaper);
            AddRect(go.transform, "Paper2", new Vector2(0.28f, 0.34f), new Vector2(0.3f, 0.32f), new Color(0.90f, 0.88f, 0.80f), order + 2, key: ArtKeys.BoardPaper);

            if (map != null) { map.board = new Vector2(c.x, c.y - 1.0f); map.hasBoard = true; }
            return;
        }
    }

    /// <summary>长椅：广场周围给人坐着聊天。</summary>
    void BuildBench()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float angle = Rand(0f, Mathf.PI * 2f);
            float distance = Mathf.Max(1.4f, plazaSize * 0.5f + Rand(1.4f, 4.2f));
            Vector2 c = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Rect area = new Rect(c.x - 0.85f, c.y - 0.5f, 1.7f, 1.0f);
            if (!IsFree(area, 0.7f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("Bench");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            AddRect(go.transform, "Seat", new Vector2(1.5f, 0.34f), new Vector2(0f, 0f), new Color(0.56f, 0.40f, 0.24f), order, key: ArtKeys.BenchSeat);
            AddRect(go.transform, "Back", new Vector2(1.5f, 0.16f), new Vector2(0f, 0.34f), new Color(0.48f, 0.34f, 0.20f), order + 1, key: ArtKeys.BenchBack);

            if (map != null) map.benches.Add(new Vector2(c.x, c.y - 0.7f));
            return;
        }
    }

    /// <summary>路灯：沿区块内的路排布（相邻区块接上就是一整条街的灯）。没路的区块也就没有灯。</summary>
    void BuildLamps()
    {
        float offset = roadWidth * 0.5f + 0.55f;
        for (float d = 4.5f; d <= radius - 3f; d += 6.5f)
        {
            // 只沿着「真的有路」的方向放灯（路型是随机的，所以灯也跟着随机）
            bool alongHorizontal = HasRoadAt(center + new Vector2(d, 0f));
            bool alongVertical = HasRoadAt(center + new Vector2(0f, d));
            if (alongHorizontal)
            {
                CreateLamp(center + new Vector2(d, offset));
                CreateLamp(center + new Vector2(-d, -offset));
            }
            if (alongVertical)
            {
                CreateLamp(center + new Vector2(offset, d));
                CreateLamp(center + new Vector2(-offset, -d));
            }
        }
    }

    /// <summary>这个点是不是压在铺好的路面 / 广场上（路灯只放在路边）。</summary>
    bool HasRoadAt(Vector2 point)
    {
        for (int i = 0; i < roadRects.Count; i++)
            if (roadRects[i].Contains(point)) return true;
        return false;
    }

    void CreateLamp(Vector2 position)
    {
        GameObject go = new GameObject("Lamp");
        go.transform.SetParent(facilitiesRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        AddRect(go.transform, "Post", new Vector2(0.13f, 1.1f), new Vector2(0f, 0.55f), new Color(0.34f, 0.31f, 0.29f), order, key: ArtKeys.LampPost);
        AddDisc(go.transform, "Head", 0.34f, new Vector2(0f, 1.16f), new Color(0.92f, 0.88f, 0.62f), order + 1, key: ArtKeys.LampHead);

        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.16f, 0f);
        glow.transform.localScale = Vector3.one * 2.2f;
        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = discSprite;
        sr.sortingOrder = order + 2;
        NightGlow night = glow.AddComponent<NightGlow>();
        night.dayColor = new Color(1f, 0.92f, 0.62f, 0f);
        night.nightColor = new Color(1f, 0.88f, 0.55f, 0.30f);

        if (map != null) map.lamps.Add(position);
    }

    // ---------------- 房屋 ----------------

    void BuildHouses()
    {
        int target = RandInt(houseMin, houseMax + 1);
        int placed = 0;
        int attempts = 0;
        int sinceLastPlaced = 0;
        while (placed < target && attempts < Mathf.Max(60, target * 60))
        {
            attempts++;
            sinceLastPlaced++;
            HouseType type = PickHouseType();
            // 这块地已经挤不下了：连着失败 60 次就改盖「小一号」的房子。
            // 不这么做的话，城市会因为在路口与广场挤出的碎地块里塞不进大公寓，只盖出稀稀拉拉几栋。
            if (sinceLastPlaced > 60) type = SmallerHouse(type);
            Vector2 size = HouseSize(type);
            float w = size.x;
            float h = size.y;
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 0.6f, radius - w * 0.5f - 0.6f), Rand(-radius + h * 0.5f + 0.6f, radius - h * 0.5f - 0.6f));
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.0f)) continue;

            occupied.Add(area);
            // 美术交了「杂项建筑」（house_extra：中世纪建筑包那类拆不出墙 / 门 / 窗的整图）时，
            // 这一栋就改用整图盖 —— 同一片村庄里于是会混进几栋画风不一样的房子。
            if (!BuildExtraHouse(area)) CreateHouse(area, housesRoot, type);
            if (map != null) map.houses.Add(new Vector2(area.center.x, area.yMin - 0.9f));   // 门口
            placed++;
            sinceLastPlaced = 0;
        }
    }

    /// <summary>
    /// 用「杂项建筑」整图（<c>house_extra</c>）盖一栋房子，返回 false = 没有这种图、或者这次没抽中（那就盖普通房子）。
    /// 同一个 key 放几张图就是几个变体，每栋自己挑一张（用生成器的确定性随机数，走远回头还是那几栋）。
    /// </summary>
    bool BuildExtraHouse(Rect area)
    {
        if (extraHouseChance <= 0f) return false;
        if (ArtOverride.VariantCount(ArtKeys.HouseExtra) == 0) return false;
        if (rng.NextDouble() >= extraHouseChance) return false;

        GameObject go = new GameObject("House");
        go.transform.SetParent(housesRoot, false);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);

        Sprite image = ArtOverride.PickVariant(ArtKeys.HouseExtra, null, (float)rng.NextDouble());
        AddWholeBuilding(go.transform, ArtKeys.HouseExtra, image, area, YOrder(area.center.y), "ExtraBuilding");

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(area.width, area.height);
        return true;
    }

    /// <summary>挤不下时换成小一号的房型（公寓 → 排屋 → 两层小楼 → 农舍）。</summary>
    static HouseType SmallerHouse(HouseType type)
    {
        switch (type)
        {
            case HouseType.Apartment: return HouseType.RowHouse;
            case HouseType.RowHouse: return HouseType.TwoStory;
            case HouseType.Barn: return HouseType.Cottage;
            case HouseType.TwoStory: return HouseType.Cottage;
            default: return HouseType.Cottage;
        }
    }

    /// <summary>这个区块该盖什么样的房子（聚落决定，沙漠里没有谷仓与木屋）。</summary>
    HouseType PickHouseType()
    {
        double roll = rng.NextDouble();
        HouseType type;

        if (settlement == SettlementKind.City)
        {
            if (roll < 0.40) type = HouseType.RowHouse;
            else if (roll < 0.72) type = HouseType.TwoStory;
            else type = HouseType.Apartment;
        }
        else if (settlement == SettlementKind.Village)
        {
            if (roll < 0.45) type = HouseType.Cottage;
            else if (roll < 0.70) type = HouseType.TwoStory;
            else if (roll < 0.88) type = HouseType.Barn;
            else type = HouseType.Cabin;
        }
        else
        {
            type = HouseType.Cabin;      // 荒野：只有小木屋
        }

        // 沙漠里没有木屋和谷仓（木结构站不住），一律换成土黄的农舍
        if (nature == NatureKind.Desert && (type == HouseType.Cabin || type == HouseType.Barn)) type = HouseType.Cottage;
        return type;
    }

    Vector2 HouseSize(HouseType type)
    {
        switch (type)
        {
            case HouseType.TwoStory: return new Vector2(Rand(3.2f, 4.2f), Rand(3.4f, 4.4f));
            case HouseType.Barn: return new Vector2(Rand(4.6f, 6.2f), Rand(3.6f, 4.6f));
            case HouseType.RowHouse: return new Vector2(Rand(5.4f, 7.6f), Rand(2.6f, 3.2f));
            case HouseType.Cabin: return new Vector2(Rand(2.2f, 2.8f), Rand(2.0f, 2.6f));
            case HouseType.Apartment: return new Vector2(Rand(3.2f, 4.4f), Rand(4.2f, 5.4f));
            default: return new Vector2(Rand(2.9f, 4.4f), Rand(2.5f, 3.5f));
        }
    }

    /// <summary>这个房型的「整栋」key（玩家放了这个 key 的图，整栋房子就用那一张图，见 <see cref="AddWholeBuilding"/>）。</summary>
    static string HouseWholeKey(HouseType type)
    {
        switch (type)
        {
            case HouseType.TwoStory: return ArtKeys.HouseTwoStory;
            case HouseType.Barn: return ArtKeys.HouseBarn;
            case HouseType.RowHouse: return ArtKeys.HouseRowHouse;
            case HouseType.Cabin: return ArtKeys.HouseCabin;
            case HouseType.Apartment: return ArtKeys.HouseApartment;
            default: return ArtKeys.HouseCottage;
        }
    }

    /// <summary>屋顶 / 墙体的配色：沙漠只有土黄沙色，森林与草原用原来的调色板。</summary>
    Color RoofColor()
    {
        return nature == NatureKind.Desert ? Pick(DesertRoofColors) : Pick(RoofColors);
    }

    Color WallColor()
    {
        switch (nature)
        {
            case NatureKind.Desert: return new Color(0.87f, 0.79f, 0.62f);
            case NatureKind.Forest: return new Color(0.84f, 0.79f, 0.68f);
            default: return new Color(0.88f, 0.84f, 0.73f);
        }
    }

    GameObject CreateHouse(Rect area, Transform parent, HouseType type = HouseType.Cottage, bool chimney = true)
    {
        GameObject go = new GameObject("House");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);

        int order = YOrder(area.center.y);

        // 整栋建筑一图替换：这个房型放了整栋图 → 直接画一整栋就收工。
        // 墙 / 门 / 窗 / 烟囱都不再生成（图里都画好了），代价是窗子不会在夜里发亮（一张图亮不起来）。
        if (AddWholeBuilding(go.transform, HouseWholeKey(type), area, order))
        {
            BoxCollider2D wholeCollider = go.AddComponent<BoxCollider2D>();
            wholeCollider.size = new Vector2(area.width, area.height);
            return go;
        }

        Color roof = RoofColor();
        Color wall = WallColor();
        Color door = new Color(0.44f, 0.28f, 0.16f);
        Color windowDay = new Color(0.70f, 0.85f, 0.92f);
        Color windowNight = new Color(1f, 0.90f, 0.58f);

        // 墙带高度：就是「南面那一条看得见的立面」
        float band = Mathf.Clamp(area.height * 0.30f, 0.5f, 0.8f);

        switch (type)
        {
            case HouseType.Apartment:
                {
                    // 公寓：上面一小条屋顶 + 下面一整片墙，墙上三排小窗
                    float roofHeight = area.height * 0.42f;
                    AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, roofHeight),
                        new Vector2(0f, area.height * 0.5f - roofHeight * 0.5f), roof, order, key: ArtKeys.ApartmentRoof);

                    float wallHeight = area.height - roofHeight;
                    float wallCenter = -area.height * 0.5f + wallHeight * 0.5f;
                    AddSlice(go.transform, "Wall", rectSprite, new Vector2(area.width, wallHeight),
                        new Vector2(0f, wallCenter), wall, order + 1, key: ArtKeys.HouseUpperWall);

                    for (int row = 0; row < 3; row++)
                    {
                        float ry = wallCenter + wallHeight * (0.30f - row * 0.28f);
                        for (int col = 0; col < 2; col++)
                        {
                            float rx = (col == 0 ? -1f : 1f) * area.width * 0.24f;
                            SpriteRenderer win = AddSlice(go.transform, "Window", rectSprite,
                                new Vector2(area.width * 0.28f, wallHeight * 0.16f), new Vector2(rx, ry), windowDay,
                                order + 2, key: ArtKeys.ApartmentWindow);
                            NightGlow glow = win.gameObject.AddComponent<NightGlow>();
                            glow.dayColor = windowDay;
                            glow.nightColor = windowNight;
                        }
                    }

                    AddSlice(go.transform, "Door", rectSprite, new Vector2(0.7f, wallHeight * 0.4f),
                        new Vector2(0f, -area.height * 0.5f + wallHeight * 0.2f), door, order + 3, key: ArtKeys.HouseDoor);
                    break;
                }

            case HouseType.TwoStory:
                {
                    // 两层小楼：整片屋顶 + 南面上下两条墙带（各有窗）
                    AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, area.height), Vector2.zero, roof, order, key: ArtKeys.HouseRoof);

                    float upperY = -area.height * 0.5f + band * 1.5f;
                    float lowerY = -area.height * 0.5f + band * 0.5f;
                    AddSlice(go.transform, "WallUpper", rectSprite, new Vector2(area.width, band), new Vector2(0f, upperY), wall, order + 1, key: ArtKeys.HouseUpperWall);
                    AddSlice(go.transform, "WallLower", rectSprite, new Vector2(area.width, band), new Vector2(0f, lowerY), wall, order + 1, key: ArtKeys.HouseWall);
                    AddSlice(go.transform, "Door", rectSprite, new Vector2(0.66f, band * 0.88f), new Vector2(0f, lowerY), door, order + 2, key: ArtKeys.HouseDoor);

                    for (int col = 0; col < 2; col++)
                    {
                        float x = (col == 0 ? -1f : 1f) * area.width * 0.28f;
                        SpriteRenderer up = AddSlice(go.transform, "WindowUpper", rectSprite, new Vector2(0.52f, band * 0.44f), new Vector2(x, upperY + band * 0.12f), windowDay, order + 2, key: ArtKeys.HouseWindow);
                        AddWindowGlow(up, windowDay, windowNight);
                        SpriteRenderer low = AddSlice(go.transform, "WindowLower", rectSprite, new Vector2(0.52f, band * 0.44f), new Vector2(x, lowerY + band * 0.12f), windowDay, order + 2, key: ArtKeys.HouseWindow);
                        AddWindowGlow(low, windowDay, windowNight);
                    }
                    break;
                }

            case HouseType.Barn:
                {
                    // 谷仓：又宽又高，深色屋顶 + 南面双开大门，没有窗
                    AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, area.height), Vector2.zero,
                        new Color(0.42f, 0.26f, 0.18f), order, key: ArtKeys.BarnRoof);
                    float barnBand = Mathf.Clamp(area.height * 0.34f, 0.6f, 1.0f);
                    float by = -area.height * 0.5f + barnBand * 0.5f;
                    AddSlice(go.transform, "Wall", rectSprite, new Vector2(area.width, barnBand), new Vector2(0f, by),
                        new Color(0.70f, 0.34f, 0.26f), order + 1, key: ArtKeys.BarnRoof);
                    float halfDoor = area.width * 0.22f;
                    AddSlice(go.transform, "DoorL", rectSprite, new Vector2(halfDoor, barnBand * 0.86f), new Vector2(-halfDoor * 0.55f, by), door, order + 2, key: ArtKeys.BarnDoor);
                    AddSlice(go.transform, "DoorR", rectSprite, new Vector2(halfDoor, barnBand * 0.86f), new Vector2(halfDoor * 0.55f, by), door, order + 2, key: ArtKeys.BarnDoor);
                    break;
                }

            case HouseType.RowHouse:
                {
                    // 排屋：又宽又浅，一条墙带上两个门三扇窗
                    AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, area.height), Vector2.zero, roof, order, key: ArtKeys.HouseRoof);
                    float rowBand = Mathf.Clamp(area.height * 0.40f, 0.6f, 0.95f);
                    float ry = -area.height * 0.5f + rowBand * 0.5f;
                    AddSlice(go.transform, "Wall", rectSprite, new Vector2(area.width, rowBand), new Vector2(0f, ry), wall, order + 1, key: ArtKeys.HouseWall);

                    for (int i = 0; i < 2; i++)
                    {
                        float x = (i == 0 ? -1f : 1f) * area.width * 0.26f;
                        AddSlice(go.transform, "Door", rectSprite, new Vector2(0.62f, rowBand * 0.86f), new Vector2(x, ry), door, order + 2, key: ArtKeys.HouseDoor);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float x = (i - 1) * area.width * 0.26f;
                        SpriteRenderer win = AddSlice(go.transform, "Window", rectSprite, new Vector2(0.5f, rowBand * 0.42f), new Vector2(x, ry + rowBand * 0.30f), windowDay, order + 2, key: ArtKeys.HouseWindow);
                        AddWindowGlow(win, windowDay, windowNight);
                    }
                    break;
                }

            case HouseType.Cabin:
                {
                    // 木屋：小、圆木墙，一扇门一扇窗
                    AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, area.height), Vector2.zero,
                        new Color(0.36f, 0.24f, 0.16f), order, key: ArtKeys.CabinRoof);
                    float cabinBand = Mathf.Clamp(area.height * 0.34f, 0.5f, 0.75f);
                    float cy = -area.height * 0.5f + cabinBand * 0.5f;
                    AddSlice(go.transform, "Wall", rectSprite, new Vector2(area.width, cabinBand), new Vector2(0f, cy),
                        new Color(0.52f, 0.38f, 0.24f), order + 1, key: ArtKeys.CabinWall);
                    AddSlice(go.transform, "Door", rectSprite, new Vector2(0.52f, cabinBand * 0.86f), new Vector2(-area.width * 0.2f, cy), door, order + 2, key: ArtKeys.HouseDoor);
                    SpriteRenderer win = AddSlice(go.transform, "Window", rectSprite, new Vector2(0.42f, cabinBand * 0.42f), new Vector2(area.width * 0.24f, cy + cabinBand * 0.16f), windowDay, order + 2, key: ArtKeys.HouseWindow);
                    AddWindowGlow(win, windowDay, windowNight);
                    break;
                }

            default:   // Cottage
                {
                    AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, area.height), Vector2.zero, roof, order, key: ArtKeys.HouseRoof);
                    float wallY = -area.height * 0.5f + band * 0.5f;
                    AddSlice(go.transform, "Wall", rectSprite, new Vector2(area.width, band), new Vector2(0f, wallY), wall, order + 1, key: ArtKeys.HouseWall);
                    AddSlice(go.transform, "Door", rectSprite, new Vector2(0.66f, band * 0.88f), new Vector2(0f, wallY), door, order + 2, key: ArtKeys.HouseDoor);

                    for (int col = 0; col < 2; col++)
                    {
                        float x = (col == 0 ? -1f : 1f) * area.width * 0.28f;
                        SpriteRenderer win = AddSlice(go.transform, "Window", rectSprite, new Vector2(0.52f, band * 0.44f), new Vector2(x, wallY + band * 0.12f), windowDay, order + 2, key: ArtKeys.HouseWindow);
                        AddWindowGlow(win, windowDay, windowNight);
                    }
                    break;
                }
        }

        // 烟囱：沙漠的房子不砌烟囱（也不加装饰）
        if (chimney && nature != NatureKind.Desert)
            AddRect(go.transform, "Chimney", new Vector2(0.34f, 0.62f), new Vector2(area.width * 0.28f, area.height * 0.5f + 0.12f),
                new Color(0.45f, 0.36f, 0.30f), order + 1, key: ArtKeys.HouseChimney);

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(area.width, area.height);

        return go;
    }

    /// <summary>窗户白天反光、夜里透出暖黄（所有房型的窗都共用这一套）。</summary>
    static void AddWindowGlow(SpriteRenderer window, Color day, Color night)
    {
        NightGlow glow = window.gameObject.AddComponent<NightGlow>();
        glow.dayColor = day;
        glow.nightColor = night;
    }

    // ---------------- 树木与植被 ----------------

    void BuildTrees()
    {
        int target = RandInt(treeMin, treeMax + 1);
        for (int i = 0; i < target; i++)
        {
            Vector2 point;
            if (!TryFindFreePoint(0.85f, 0.15f, out point, 120)) continue;
            occupied.Add(new Rect(point.x - 0.85f, point.y - 0.85f, 1.7f, 1.7f));
            CreateTree(point);
        }
    }

    /// <summary>
    /// 有「整棵树」素材（<see cref="ArtKeys.Tree"/>）时挑哪一张：下标 = 变体顺序 = 文件名结尾的数字
    /// （<c>tree1.png</c> → 第 0 项、<c>tree2.png</c> → 第 1 项…）。**只影响整棵树素材**，
    /// <c>tree_canopy</c> 那条老路不受影响。数值随便调：写几项就按几项算，没写的按 1 算。
    /// </summary>
    static float[] TreeVariantWeights(NatureKind nature)
    {
        switch (nature)
        {
            case NatureKind.Forest: return new[] { 0.15f, 0.25f, 0.45f, 0.15f };  // 森林：大树为主
            case NatureKind.Desert: return new[] { 0.10f, 0.20f, 0.10f, 0.60f };  // 沙漠：只剩最瘦小的那棵
            default: return new[] { 0.30f, 0.30f, 0.10f, 0.30f };                 // 草原：中等树为主，大树少
        }
    }

    /// <summary>
    /// 灌木按地貌换图：<c>bush1</c>（编号 1）= 草原、<c>bush2</c>（编号 2）= 森林。
    /// 想对调就改这一行；只交了一张时另一片地貌也用它（<see cref="ArtOverride.Get(string, int)"/> 会退回第一个变体）。
    /// </summary>
    static int BushVariantNumber(NatureKind nature)
    {
        return nature == NatureKind.Forest ? 2 : 1;
    }

    void CreateTree(Vector2 position)
    {
        GameObject go = new GameObject("Tree");
        go.transform.SetParent(treesRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        float size = nature == NatureKind.Forest ? Rand(2.0f, 3.2f) : Rand(1.7f, 2.7f);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();

        // 美术交了「整棵树」（tree1~4.png 这种整图）：整棵替换掉圆形树冠，按地貌权重挑一张
        // （森林多大树、沙漠只剩最瘦的那棵），所以一片林子里不会每棵都长一样。
        Sprite wholeTree = ArtOverride.PickVariant(ArtKeys.Tree, TreeVariantWeights(nature), (float)rng.NextDouble());
        if (wholeTree != null)
        {
            // 影子和「内部高光」都是给圆形树冠凑数的：整图自带明暗，影子还会比树本身大一圈（红线 35），跳过
            ArtShapes.AddWholeImage(go.transform, "Tree", wholeTree, size, Vector2.zero, order);

            // 整图是「底边贴地」摆的（树干在下半截），碰撞体跟着挪到树干那一带，别挡住树冠上空
            collider.radius = size * 0.22f;
            collider.offset = new Vector2(0f, -(size * 0.5f - collider.radius));
        }
        else
        {
            Color canopy = nature == NatureKind.Forest ? Pick(ForestTreeColors) : Pick(TreeColors);
            AddDisc(go.transform, "Shadow", size * 0.95f, new Vector2(0.06f, -0.08f), new Color(0f, 0f, 0f, 0.18f), order - 1);
            AddDisc(go.transform, "Canopy", size, Vector2.zero, canopy, order, key: ArtKeys.TreeCanopy);
            // 「内部高光」只是程序化树冠的受光面；玩家给了树冠图就不再叠它
            if (!ArtOverride.Has(ArtKeys.TreeCanopy))
                AddDisc(go.transform, "CanopyInner", size * 0.58f, new Vector2(-size * 0.08f, size * 0.10f), Color.Lerp(canopy, Color.white, 0.22f), order + 1);

            collider.radius = size * 0.26f;
        }

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = RandInt(18, 31);
        hidden.note = "树：挡路的景物";
        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = "树";
        info.kind = "景物";
        info.description = "挡住去路的树。小虫可以绕开它，樵夫会来这儿砍柴；"
            + "长到 " + BugGrowth.TreeLevel + " 级以后，连树也啃得动。";

        // 树也能吃，只是要长到 3 级（挡住路的大树 = 一顿大餐）
        Edible edible = go.AddComponent<Edible>();
        edible.nutrition = 4;
        edible.requiredLevel = BugGrowth.TreeLevel;

        if (map != null) map.trees.Add(position);
    }

    /// <summary>按自然体系撒景物：草原 / 森林是灌木，沙漠换成仙人掌与枯树。</summary>
    void BuildNatureProps()
    {
        int bushes = RandInt(bushMin, bushMax + 1);
        if (bushes > 0)
        {
            for (int i = 0; i < bushes; i++)
            {
                Vector2 point;
                if (!TryFindFreePoint(0.6f, 0.25f, out point, 60)) continue;
                occupied.Add(new Rect(point.x - 0.6f, point.y - 0.6f, 1.2f, 1.2f));
                CreateBush(point);
            }
        }

        int cacti = RandInt(cactusMin, cactusMax + 1);
        if (cacti > 0)
        {
            for (int i = 0; i < cacti; i++)
            {
                Vector2 point;
                if (!TryFindFreePoint(0.7f, 0.3f, out point, 70)) continue;
                occupied.Add(new Rect(point.x - 0.7f, point.y - 0.7f, 1.4f, 1.4f));
                CreateCactus(point);
            }
        }

        int dead = RandInt(deadTreeMin, deadTreeMax + 1);
        for (int i = 0; i < dead; i++)
        {
            Vector2 point;
            if (!TryFindFreePoint(0.55f, 0.3f, out point, 60)) continue;
            occupied.Add(new Rect(point.x - 0.55f, point.y - 0.55f, 1.1f, 1.1f));
            CreateDeadTree(point);
        }
    }

    /// <summary>灌木：草原 / 森林里的小障碍（挡路、但不挡视线）。</summary>
    void CreateBush(Vector2 position)
    {
        GameObject go = new GameObject("Bush");
        go.transform.SetParent(natureRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        float size = Rand(0.8f, 1.35f);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = size * 0.3f;

        // 灌木的整图：草原用 bush1、森林用 bush2（见 BushVariantNumber）
        Sprite whole = ArtOverride.Get(ArtKeys.Bush, BushVariantNumber(nature));
        if (whole != null)
        {
            ArtShapes.AddWholeImage(go.transform, "Body", whole, size, Vector2.zero, order);
        }
        else
        {
            Color color = nature == NatureKind.Forest ? new Color(0.18f, 0.32f, 0.16f) : new Color(0.28f, 0.42f, 0.20f);
            AddDisc(go.transform, "Shadow", size * 0.85f, new Vector2(0.05f, -0.07f), new Color(0f, 0f, 0f, 0.15f), order - 1);
            AddDisc(go.transform, "Body", size, Vector2.zero, color, order, key: ArtKeys.Bush);
        }

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = RandInt(6, 14);
        hidden.note = "灌木：挡路的小丛";
        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = "灌木";
        info.kind = "景物";
        info.description = "一丛矮灌木，绕开就好。";
    }

    /// <summary>仙人掌（沙漠里代替树）：主干 + 一两根侧枝，也是「仙人掌果」的依附点。</summary>
    void CreateCactus(Vector2 position)
    {
        GameObject go = new GameObject("Cactus");
        go.transform.SetParent(natureRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        float height = Rand(1.1f, 1.9f);
        float width = Rand(0.36f, 0.5f);
        Color color = new Color(0.28f, 0.48f, 0.26f);

        AddRect(go.transform, "Trunk", new Vector2(width, height), Vector2.zero, color, order, key: ArtKeys.CactusBody);

        int arms = RandInt(1, 3);
        for (int i = 0; i < arms; i++)
        {
            bool left = i % 2 == 0;
            float side = left ? -1f : 1f;
            float armY = Rand(-height * 0.15f, height * 0.3f);
            AddRect(go.transform, "Arm", new Vector2(width * 0.62f, height * 0.42f),
                new Vector2(side * (width * 0.5f + width * 0.28f), armY), color, order + 1, key: ArtKeys.CactusArm);
        }

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = width * 0.62f;

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = RandInt(14, 26);
        hidden.note = "仙人掌：沙漠里的水";
        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = "仙人掌";
        info.kind = "景物";
        info.description = "沙漠里最高的东西。听说上面结的果子能吃。";

        // 登记锚点：仙人掌果（ContentPack 里的 cactus_fruit）就长在它旁边
        if (map != null) map.AddAnchor("cactus", position);
    }

    /// <summary>枯树（沙漠多、森林少）：只剩光秃秃的树干与几根枝。</summary>
    void CreateDeadTree(Vector2 position)
    {
        GameObject go = new GameObject("DeadTree");
        go.transform.SetParent(natureRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        float height = Rand(1.2f, 2.0f);
        Color bark = new Color(0.44f, 0.38f, 0.30f);
        AddRect(go.transform, "Trunk", new Vector2(0.2f, height), Vector2.zero, bark, order, key: ArtKeys.DeadTree);

        int branches = RandInt(2, 4);
        for (int i = 0; i < branches; i++)
        {
            bool left = i % 2 == 0;
            AddRect(go.transform, "Branch", new Vector2(0.14f, height * 0.34f),
                new Vector2((left ? -1f : 1f) * 0.16f, height * Rand(0.05f, 0.42f)), bark, order + 1, key: ArtKeys.DeadTree);
        }

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 0.2f;

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = RandInt(8, 16);
        hidden.note = "枯树：一点就着的样子";
        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = "枯树";
        info.kind = "景物";
        info.description = "早就枯死的树，只剩一副骨架。";
    }

    /// <summary>棕榈（绿洲旁）：一根树干 + 几片垂下来的叶子。</summary>
    void CreatePalm(Transform parent, Vector2 localPosition, int order)
    {
        GameObject go = new GameObject("Palm");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        float height = Rand(1.6f, 2.4f);
        AddRect(go.transform, "Trunk", new Vector2(0.2f, height), Vector2.zero, new Color(0.48f, 0.36f, 0.22f), order, key: ArtKeys.PalmTrunk);

        int leaves = RandInt(4, 6);
        for (int i = 0; i < leaves; i++)
        {
            float angle = i / (float)leaves * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
            AddDisc(go.transform, "Leaf", 0.85f, new Vector2(offset.x, height * 0.5f + offset.y * 0.6f),
                new Color(0.26f, 0.48f, 0.24f), order + 1, key: ArtKeys.PalmLeaf);
        }
    }

    // ---------------- 食物 / 可交互物品（内容目录驱动）----------------

    /// <summary>
    /// 生成这个区块的食物：先抽「食物位」（<see cref="foodSlotsMin"/>~<see cref="foodSlotsMax"/> 个），
    /// 每个位按权重从 <see cref="FoodCatalog"/> 里抽一种；再处理定义里写了固定数量的（比如神奇果实）。
    /// **想加新食物只要往 FoodCatalog 里注册一行**，这里不用改。
    /// </summary>
    void BuildFoods()
    {
        BuildSlots(FoodCatalog.All, foodSlotsMin, foodSlotsMax, foodRoot);
        BuildFixed(FoodCatalog.All, foodRoot);
    }

    /// <summary>生成这个区块的可交互物品（木箱之类）：规则同 <see cref="BuildFoods"/>，内容来自 <see cref="ItemCatalog"/>。</summary>
    void BuildItems()
    {
        BuildSlots(ItemCatalog.All, itemSlotsMin, itemSlotsMax, propsRoot);
        BuildFixed(ItemCatalog.All, propsRoot);
    }

    /// <summary>每区块抽 N 个「位」，每个位按权重抽一种（抽不到就提前收工，不白占位）。</summary>
    void BuildSlots<T>(List<T> definitions, int min, int max, Transform root) where T : class, IContentDefinition
    {
        if (definitions == null || max <= 0) return;

        int slots = RandInt(min, max + 1);
        for (int i = 0; i < slots; i++)
        {
            T definition = SpawnKit.PickWeighted(definitions, rng, settlement, nature);
            if (definition == null) return;
            Place(definition, root);
        }
    }

    /// <summary>处理定义里的「固定生成」（<c>minPerChunk</c>~<c>maxPerChunk</c>，先掷一次 chance）。</summary>
    void BuildFixed<T>(List<T> definitions, Transform root) where T : class, IContentDefinition
    {
        if (definitions == null) return;

        for (int i = 0; i < definitions.Count; i++)
        {
            T definition = definitions[i];
            if (definition == null || definition.Spawn == null) continue;

            SpawnRule rule = definition.Spawn;
            if (rule.maxPerChunk <= 0 || !rule.Includes(settlement, nature)) continue;
            if (rule.chance < 1f && !Chance(rule.chance)) continue;

            int count = RandInt(rule.minPerChunk, rule.maxPerChunk + 1);
            for (int k = 0; k < count; k++) Place(definition, root);
        }
    }

    /// <summary>找一块空地把它放下来（找不到就跳过这一次）。</summary>
    void Place<T>(T definition, Transform root) where T : class, IContentDefinition
    {
        SpawnRule rule = definition.Spawn;
        Vector2 point;

        // 先按「就近偏好」找点（果子长在树旁、电池聚在发电站旁、仙人掌果长在仙人掌旁）；
        // 找不到参照物 / 参照物旁边没空地 → 退回普通随机落点（不会因为这片没树就什么都不刷）
        if (!TryFindNearPoint(rule, out point) && !TryFindFreePoint(rule.clearance, rule.padding, out point))
            return;

        if (rule.footprint > 0.001f)
            occupied.Add(new Rect(point.x - rule.footprint, point.y - rule.footprint, rule.footprint * 2f, rule.footprint * 2f));

        definition.Create(root, point, YOrder(point.y));
    }

    /// <summary>
    /// 按 <see cref="SpawnRule.nearTrees"/> / <see cref="SpawnRule.nearAnchor"/> 在参照物旁边找落点。
    /// 没有参照物（或试了几次都不行）就返回 false，让调用方退回普通随机点。
    /// </summary>
    bool TryFindNearPoint(SpawnRule rule, out Vector2 point)
    {
        point = Vector2.zero;
        if (rule == null || map == null) return false;
        if (!rule.nearTrees && string.IsNullOrEmpty(rule.nearAnchor)) return false;

        List<Vector2> anchors = rule.nearTrees ? map.trees : map.AnchorList(rule.nearAnchor);
        if (anchors == null || anchors.Count == 0) return false;

        // 随机挑几个参照物试，别每次只盯一个（那一个挤满了就全失败）
        int tries = Mathf.Min(anchors.Count, 6);
        for (int i = 0; i < tries; i++)
        {
            Vector2 anchor = anchors[RandInt(0, anchors.Count)];
            float min = Mathf.Max(0.2f, rule.nearMinDistance);
            float max = Mathf.Max(min + 0.3f, rule.nearRadius);
            if (TryFindPointAround(anchor, min, max, rule.clearance, out point, 24)) return true;
        }
        return false;
    }

    // ---------------- 地洞 / 地道 ----------------

    void BuildBurrows()
    {
        int target = RandInt(burrowMin, burrowMax + 1);
        for (int i = 0; i < target; i++)
        {
            Vector2 point;
            if (!TryFindFreePoint(0.75f, 0.7f, out point, 80)) continue;
            occupied.Add(new Rect(point.x - 0.75f, point.y - 0.75f, 1.5f, 1.5f));
            CreateBurrow(point, Chance(tunnelChance));
        }
    }

    void CreateBurrow(Vector2 position, bool tunnel)
    {
        GameObject go = new GameObject(tunnel ? "Tunnel" : "Burrow");
        go.transform.SetParent(burrowRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        // 地洞是「地面上的洞」，放 Ground 层：小虫和村民钻进去时不会被洞口盖住
        Color rim = tunnel ? new Color(0.34f, 0.42f, 0.40f) : new Color(0.44f, 0.34f, 0.24f);
        AddDisc(go.transform, "Rim", 1.10f, Vector2.zero, rim, -820, true, ArtKeys.BurrowRim);
        AddDisc(go.transform, "Hole", 0.80f, Vector2.zero, new Color(0.07f, 0.06f, 0.06f), -810, true, ArtKeys.BurrowHole);
        AddDisc(go.transform, "HoleInner", 0.48f, new Vector2(0.04f, -0.05f), new Color(0.02f, 0.02f, 0.02f), -800, true, ArtKeys.BurrowInner);

        if (tunnel)
        {
            // 地道多堆两个小石头当标记，好认
            AddDisc(go.transform, "StoneA", 0.24f, new Vector2(0.66f, 0.44f), new Color(0.56f, 0.56f, 0.53f), -795, true, ArtKeys.BurrowStone);
            AddDisc(go.transform, "StoneB", 0.17f, new Vector2(0.84f, 0.26f), new Color(0.49f, 0.49f, 0.46f), -795, true, ArtKeys.BurrowStone);
        }

        Burrow burrow = go.AddComponent<Burrow>();
        burrow.tunnel = tunnel;

        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = tunnel ? 8 : 5;
        hidden.note = tunnel ? "地道：可传送" : "地洞：可躲藏";
    }

    // ---------------- 村民 ----------------

    void BuildVillagers()
    {
        int target = RandInt(villagerMin, villagerMax + 1);
        int spawned = 0;
        int attempts = 0;
        while (spawned < target && attempts < Mathf.Max(40, target * 40))
        {
            attempts++;
            VillagerJob job = VillagerJobs.All[RandInt(0, VillagerJobs.All.Length)];

            // 先定工作点，再在工作点附近安家，村庄看起来才有分区
            Vector2 workplace = WorkplaceFor(job);
            Vector2 point;
            if (!TryFindPointAround(workplace, 1.2f, 4.5f, 0.5f, out point))
                if (!TryFindFreePoint(0.45f, 0.6f, out point, 30)) continue;

            CreateVillager(point, spawned, job, workplace);
            spawned++;
        }
    }

    /// <summary>村里的房子，离某个点最近的那栋（当“家”）；只在自己这块地里找。</summary>
    Vector2 NearestHouse(Vector2 point)
    {
        if (map == null || map.houses.Count == 0) return center;
        return VillageMap.Nearest(map.houses, point, center);
    }

    /// <summary>这个职业平时待的地方（只挑自己这块区块里的设施，越界就当没有）。</summary>
    Vector2 WorkplaceFor(VillagerJob job)
    {
        if (map == null) return center;
        float range = chunkSize;
        switch (job)
        {
            case VillagerJob.Farmer: return VillageMap.PickNear(map.farms, center, center, range);
            case VillagerJob.Merchant: return VillageMap.PickNear(map.stalls, center, center, range);
            case VillagerJob.Baker: return map.hasBakery && (map.bakery - center).magnitude < range ? map.bakery : center;
            case VillagerJob.Smith: return map.hasSmithy && (map.smithy - center).magnitude < range ? map.smithy : center;
            case VillagerJob.Shepherd: return VillageMap.PickNear(map.pens, center, center, range);
            case VillagerJob.Woodcutter: return center + new Vector2(Rand(-radius * 0.6f, radius * 0.6f), Rand(-radius * 0.6f, radius * 0.6f));
            case VillagerJob.Guard: return center;
            case VillagerJob.Child: return center;
            default: return VillageMap.PickNear(map.wells, center, center, range);
        }
    }

    Villager CreateVillager(Vector2 position, int index, VillagerJob job, Vector2 workplace, Transform parent = null)
    {
        occupied.Add(new Rect(position.x - 0.5f, position.y - 0.5f, 1f, 1f));
        GameObject go = new GameObject("Villager_" + index + "_" + VillagerJobs.Label(job));
        go.transform.SetParent(parent != null ? parent : villagersRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 0.28f;

        // 隐藏数值（体魄）：影响追逐的耐心和一点点速度，不显示给玩家
        int grit = RandInt(2, 11);
        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = grit;
        hidden.note = "体魄：影响追逐耐心与速度";
        float gritScale = Mathf.Lerp(0.75f, 1.35f, (grit - 2) / 8f);

        // npc 也能吃：小虫长到满级（4 级）之后，村民就是「会走路的食物」。
        // 吃的时候现场目击的村民会从此躲着小虫（见 Villager.ReportEaten）。
        Edible edible = go.AddComponent<Edible>();
        edible.nutrition = 10;
        edible.satiety = 45;
        edible.requiredLevel = BugGrowth.VillagerLevel;

        // 极简美术：一个长方形当身子 + 一个圆当头。
        // 整体保持「头朝上」的姿势，移动时由 Villager 只做左右翻转，不做旋转。
        // 衣服颜色 = 职业标识（同职业的人也有深浅差异）。
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);

        // 美术交了「整身村民」（man1~7 / woman1~4 这种一张图画完整个人的）就整身替换掉「方块身子 + 圆头」：
        // 每个村民按区块确定性随机挑一张变体（同一片村子里不会所有人都长一样），**图原样显示、不染职业色**。
        Sprite wholeVillager = ArtOverride.PickVariant(ArtKeys.Villager, null, (float)rng.NextDouble());
        if (wholeVillager != null)
        {
            // 底边贴地：和原来「身子 + 头」的脚底对齐，换图之后脚还站在地上
            Vector2 wholePos = new Vector2(0f, villagerFootY + villagerVisualHeight * 0.5f);
            ArtShapes.AddWholeImage(visual.transform, "Villager", wholeVillager, villagerVisualHeight, wholePos, 0);
        }
        else
        {
            Color shirt = VillagerJobs.Shirt(job);
            shirt = Color.Lerp(shirt, rng.NextDouble() < 0.5 ? Color.white : Color.black, Rand(0.02f, 0.15f));
            Color skin = Pick(SkinColors);
            AddRect(visual.transform, "Body", new Vector2(0.40f, 0.52f), new Vector2(0f, -0.12f), shirt, 0, key: ArtKeys.VillagerBody);
            AddDisc(visual.transform, "Head", 0.36f, new Vector2(0f, 0.30f), skin, 1, key: ArtKeys.VillagerHead);
        }

        Villager villager = go.AddComponent<Villager>();
        villager.job = job;
        villager.displayName = Pick(SurnameList) + Pick(GivenList);
        villager.map = map;
        villager.home = NearestHouse(workplace);
        villager.workplace = workplace;
        villager.hangout = center;
        // 村民走得比小虫慢：小虫 walkSpeed = 2.6，村民 0.95~1.7（孩子快一点），
        // 再加上「发现小虫时的反应速度倍率」，追也追不上、躲得开；体魄高的稍微快一点
        villager.moveSpeed = (job == VillagerJob.Child ? Rand(1.5f, 2.2f) : Rand(0.95f, 1.7f)) * Mathf.Lerp(0.94f, 1.1f, (grit - 2) / 8f);
        villager.idleMin = Rand(0.5f, 1.2f);
        villager.idleMax = Rand(1.6f, 3.6f);
        villager.chatMin = Rand(4f, 7f);
        villager.chatMax = Rand(8f, 14f);
        // 体魄越高，追/躲得越久
        villager.reactMin = Mathf.Clamp(reactMinBase * gritScale, 1f, 12f);
        villager.reactMax = Mathf.Clamp(reactMaxBase * gritScale, villager.reactMin + 0.5f, 16f);

        go.AddComponent<YSort>();
        return villager;
    }

    /// <summary>
    /// 在某个点附近补一个村民（「保证视野里不少于 N 个村民」用）。
    /// 会挂到指定父物体（当前区块的 Villagers）下面，区块回收时跟着销毁。
    /// </summary>
    public Villager SpawnWalkerNear(Vector2 near, Transform parent, int index)
    {
        VillagerJob job = VillagerJobs.All[RandInt(0, VillagerJobs.All.Length)];

        Vector2 point;
        if (!TryFindPointAround(near, 3f, 8f, 0.5f, out point, 40)) point = near;

        Villager villager = CreateVillager(point, index, job, point, parent);
        if (villager != null)
        {
            villager.hangout = near;
            villager.home = point;
        }
        return villager;
    }

    // 村民反应时长的基础值（再乘体魄倍率）
    public float reactMinBase = 3f;
    public float reactMaxBase = 6f;

    // 整身村民图（`villager` key）的占地高度与脚底位置：默认对齐原来「方块身子 + 圆头」那套（总高 0.86、脚底 −0.38）
    [Tooltip("整身村民图的高度（世界单位）")]
    public float villagerVisualHeight = 0.86f;
    [Tooltip("整身村民图的脚底 Y（默认和原来「身子 + 头」的脚底一致，换图之后脚还站在地上）")]
    public float villagerFootY = -0.38f;

    // ---------------- 玩家 ----------------

    void PlacePlayer()
    {
        if (player == null) return;
        Vector2 point = playerSpawn;
        if (Physics2D.OverlapCircle(point, 0.5f) != null)
        {
            // 出生点被挡住时在附近由内向外找一个空地，尽量留在广场一带
            bool found = false;
            for (int ring = 1; ring <= 6 && !found; ring++)
            {
                for (int a = 0; a < 16; a++)
                {
                    float angle = a / 16f * Mathf.PI * 2f;
                    Vector2 candidate = playerSpawn + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * 0.8f);
                    if (Physics2D.OverlapCircle(candidate, 0.45f) == null)
                    {
                        point = candidate;
                        found = true;
                        break;
                    }
                }
            }
            if (!found) point = playerSpawn;
        }

        player.position = new Vector3(point.x, point.y, 0f);
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = point;
            rb.velocity = Vector2.zero;
        }
    }
}
