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
    [Tooltip("路宽（会被 WorldBiome 按聚落覆盖：城市更宽、荒野是窄土路）")]
    public float roadWidth = 3.4f;
    [Tooltip("广场边长（会被 WorldBiome 按聚落覆盖）")]
    public float plazaSize = 10f;
    [Tooltip("**路型按 2×2 个区块一片抽签**：同一片常常是同一种路型，路才连得起来（见 PickRoadShape）")]
    public int roadRunLength = 2;

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
        rng = new System.Random(Hash(worldSeed, coord.x, coord.y));
        occupied.Clear();
        roadRects.Clear();

        // 这个区块属于哪种自然 / 聚落体系，然后把参数整体套上去（先覆盖再生成）
        nature = WorldBiome.NatureAt(coord, worldSeed);
        settlement = WorldBiome.SettlementAt(coord, worldSeed);
        hamlet = settlement != SettlementKind.Wilderness;
        WorldBiome.Apply(settlement, nature, this);

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

        GameObject go = new GameObject("BiomeGround");
        go.transform.SetParent(groundRoot, false);
        go.transform.position = new Vector3(center.x, center.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(chunkSize, chunkSize);   // 正好一块区块，和邻块的网格对齐
        sr.color = WorldBiome.GroundTint(nature);
        ApplyGroundLayer(sr);
        sr.sortingOrder = -895;

        ArtOverride.Apply(sr, key);
        ForceTiling(sr.sprite);
    }

    // ---------------- 道路（路型随机，相邻区块靠「成片抽签」尽量接得上）----------------

    /// <summary>路型：不再是固定的十字路。</summary>
    enum RoadShape { None, StraightH, StraightV, Cross, TJunction, LJunction }

    void BuildRoads()
    {
        string key = WorldBiome.RoadKey(settlement);
        Color tint = WorldBiome.RoadTint(settlement);

        RoadShape shape = PickRoadShape();
        float half = roadWidth * 0.5f;

        Rect horizontal = new Rect(center.x - radius, center.y - half, radius * 2f, roadWidth);
        Rect vertical = new Rect(center.x - half, center.y - radius, roadWidth, radius * 2f);
        Rect horizontalHalf = new Rect(center.x, center.y - half, radius, roadWidth);
        Rect verticalHalf = new Rect(center.x - half, center.y, roadWidth, radius);

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
        if (!Chance(roadChance)) return RoadShape.None;

        int run = Mathf.Max(1, roadRunLength);
        int bx = Mathf.FloorToInt(chunkCoord.x / (float)run);
        int by = Mathf.FloorToInt(chunkCoord.y / (float)run);
        System.Random shapeRng = new System.Random(Hash(chunkWorldSeed * 31 + 17, bx * 7919 + 13, by * 104729 + 7));
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

    /// <summary>铺一段路（占位 + 给守卫巡逻用的取样点）。</summary>
    void AddRoad(Rect area, string key, Color tint)
    {
        occupied.Add(area);
        roadRects.Add(area);
        AddRoadRect(area, key, tint, -885);

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
            Rect area = new Rect(center.x - plazaSize * 0.5f, center.y - plazaSize * 0.5f, plazaSize, plazaSize);
            occupied.Add(area);
            roadRects.Add(area);
            AddRoadRect(area, key, tint, -880);          // 广场比路面再高一层
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
            Color awning = Pick(StallColors);
            AddRect(go.transform, "Counter", new Vector2(1.7f, 0.5f), new Vector2(0f, -0.3f), new Color(0.56f, 0.41f, 0.25f), order, key: ArtKeys.StallCounter);
            AddRect(go.transform, "Awning", new Vector2(1.95f, 0.45f), new Vector2(0f, 0.35f), awning, order + 1, key: ArtKeys.StallAwning);
            AddRect(go.transform, "PostL", new Vector2(0.12f, 0.9f), new Vector2(-0.92f, 0.05f), new Color(0.45f, 0.33f, 0.20f), order, key: ArtKeys.StallPost);
            AddRect(go.transform, "PostR", new Vector2(0.12f, 0.9f), new Vector2(0.92f, 0.05f), new Color(0.45f, 0.33f, 0.20f), order, key: ArtKeys.StallPost);
            AddDisc(go.transform, "Goods1", 0.3f, new Vector2(-0.5f, -0.1f), new Color(0.85f, 0.48f, 0.26f), order + 2, key: ArtKeys.StallGoods);
            AddDisc(go.transform, "Goods2", 0.26f, new Vector2(0f, -0.08f), new Color(0.90f, 0.80f, 0.35f), order + 2, key: ArtKeys.StallGoods);
            AddDisc(go.transform, "Goods3", 0.28f, new Vector2(0.5f, -0.1f), new Color(0.45f, 0.66f, 0.36f), order + 2, key: ArtKeys.StallGoods);

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
            CreateHouse(area, housesRoot, type);
            if (map != null) map.houses.Add(new Vector2(area.center.x, area.yMin - 0.9f));   // 门口
            placed++;
            sinceLastPlaced = 0;
        }
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

    void CreateTree(Vector2 position)
    {
        GameObject go = new GameObject("Tree");
        go.transform.SetParent(treesRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        float size = nature == NatureKind.Forest ? Rand(2.0f, 3.2f) : Rand(1.7f, 2.7f);
        Color canopy = nature == NatureKind.Forest ? Pick(ForestTreeColors) : Pick(TreeColors);

        AddDisc(go.transform, "Shadow", size * 0.95f, new Vector2(0.06f, -0.08f), new Color(0f, 0f, 0f, 0.18f), order - 1);
        AddDisc(go.transform, "Canopy", size, Vector2.zero, canopy, order, key: ArtKeys.TreeCanopy);
        // 「内部高光」只是程序化树冠的受光面；玩家给了整棵树的树冠图就不再叠它
        if (!ArtOverride.Has(ArtKeys.TreeCanopy))
            AddDisc(go.transform, "CanopyInner", size * 0.58f, new Vector2(-size * 0.08f, size * 0.10f), Color.Lerp(canopy, Color.white, 0.22f), order + 1);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = size * 0.26f;

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
        Color color = nature == NatureKind.Forest ? new Color(0.18f, 0.32f, 0.16f) : new Color(0.28f, 0.42f, 0.20f);
        AddDisc(go.transform, "Shadow", size * 0.85f, new Vector2(0.05f, -0.07f), new Color(0f, 0f, 0f, 0.15f), order - 1);
        AddDisc(go.transform, "Body", size, Vector2.zero, color, order, key: ArtKeys.Bush);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = size * 0.3f;

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

        Color shirt = VillagerJobs.Shirt(job);
        shirt = Color.Lerp(shirt, rng.NextDouble() < 0.5 ? Color.white : Color.black, Rand(0.02f, 0.15f));
        Color skin = Pick(SkinColors);
        AddRect(visual.transform, "Body", new Vector2(0.40f, 0.52f), new Vector2(0f, -0.12f), shirt, 0, key: ArtKeys.VillagerBody);
        AddDisc(visual.transform, "Head", 0.36f, new Vector2(0f, 0.30f), skin, 1, key: ArtKeys.VillagerHead);

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
