using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 区块生成器：按「区块坐标」生成一小片村庄内容（道路、设施、房屋、树、食物、木箱、村民）。
/// 每个区块的十字路口都在区块中心，相邻区块能自然接上，于是整个世界就是一片连续的村落。
/// 由 <see cref="VillageWorld"/> 按小虫的位置决定生成 / 回收哪些区块。
///
/// 生成是「纯函数式」的：同一个区块坐标 + 同一个世界种子 → 每次生成的内容完全一样，
/// 所以走远再走回来，村庄还是原来的样子。
/// </summary>
public class VillageGenerator : MonoBehaviour
{
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
    public float roadWidth = 3.4f;
    public float plazaSize = 10f;

    [Header("区块内容")]
    [Tooltip("村庄区块（有房屋设施）的比例，其余是野外")]
    [Range(0f, 1f)] public float hamletChance = 0.65f;
    public int houseMin = 3;
    public int houseMax = 5;
    public int treeMin = 10;
    public int treeMax = 16;
    public int foodMin = 5;
    public int foodMax = 8;
    public int crateMax = 2;
    public int villagerMin = 2;
    public int villagerMax = 4;

    [Header("地洞")]
    [Tooltip("每个区块的地洞数量范围")]
    public int burrowMin = 0;
    public int burrowMax = 2;
    [Tooltip("其中是「地道」（可两两传送到另一头）的比例")]
    [Range(0f, 1f)] public float tunnelChance = 0.45f;

    [Header("特殊食物")]
    [Tooltip("每个区块出现「神奇果实」的概率（吃下去小虫会长大）")]
    [Range(0f, 1f)] public float specialFoodChance = 0.45f;

    [Header("设施概率（村庄区块）")]
    public bool buildFacilities = true;
    [Range(0f, 1f)] public float farmChance = 0.7f;
    [Range(0f, 1f)] public float penChance = 0.35f;
    [Range(0f, 1f)] public float bakeryChance = 0.22f;
    [Range(0f, 1f)] public float smithyChance = 0.22f;
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

    static readonly Color[] TreeColors =
    {
        new Color(0.16f, 0.33f, 0.13f),
        new Color(0.20f, 0.39f, 0.15f),
        new Color(0.13f, 0.29f, 0.12f)
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
    System.Random rng;
    Vector2 center;
    float radius;
    bool hamlet;
    Transform roadsRoot, housesRoot, facilitiesRoot, treesRoot, foodRoot, propsRoot, villagersRoot, burrowRoot;

    public Vector2 CurrentCenter { get { return center; } }

    void Awake()
    {
        if (map == null) map = FindObjectOfType<VillageMap>();
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

        center = new Vector2(coord.x * chunkSize, coord.y * chunkSize);
        radius = chunkSize * 0.5f;
        rng = new System.Random(Hash(worldSeed, coord.x, coord.y));
        occupied.Clear();

        GameObject chunkGo = new GameObject("Chunk_" + coord.x + "_" + coord.y);
        chunkGo.transform.SetParent(transform, false);
        chunkGo.transform.position = new Vector3(center.x, center.y, 0f);

        roadsRoot = MakeRoot(chunkGo.transform, "Roads");
        housesRoot = MakeRoot(chunkGo.transform, "Houses");
        facilitiesRoot = MakeRoot(chunkGo.transform, "Facilities");
        treesRoot = MakeRoot(chunkGo.transform, "Trees");
        foodRoot = MakeRoot(chunkGo.transform, "Food");
        propsRoot = MakeRoot(chunkGo.transform, "Props");
        villagersRoot = MakeRoot(chunkGo.transform, "Villagers");
        burrowRoot = MakeRoot(chunkGo.transform, "Burrows");

        // 原点区块固定是村庄，保证出生点在广场上
        hamlet = coord == Vector2Int.zero || rng.NextDouble() < hamletChance;

        BuildRoads();
        if (hamlet)
        {
            BuildWell();
            if (buildFacilities) BuildFacilities();
            BuildCrates();
        }
        BuildHouses();
        BuildFood();
        BuildSpecialFood();
        BuildBurrows();
        BuildVillagers();
        BuildTrees();
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
        return 1000 - Mathf.RoundToInt(y * 10f);
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

    SpriteRenderer AddSlice(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 localPos, Color color, int order, bool groundLayer = false)
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
        return sr;
    }

    /// <summary>纯色长方形：直接缩放 1x1 的矩形贴图，不做九宫格，尺寸多小都不会变形。</summary>
    SpriteRenderer AddRect(Transform parent, string name, Vector2 size, Vector2 localPos, Color color, int order, bool groundLayer = false)
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
        return sr;
    }

    SpriteRenderer AddDisc(Transform parent, string name, float size, Vector2 localPos, Color color, int order, bool groundLayer = false)
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
        return sr;
    }

    // ---------------- 道路（区块中心十字路，相邻区块连成路网）----------------

    void BuildRoads()
    {
        Rect horizontal = new Rect(center.x - radius, center.y - roadWidth * 0.5f, radius * 2f, roadWidth);
        Rect vertical = new Rect(center.x - roadWidth * 0.5f, center.y - radius, roadWidth, radius * 2f);

        AddRoadRect(horizontal);
        AddRoadRect(vertical);
        occupied.Add(horizontal);
        occupied.Add(vertical);

        if (hamlet)
        {
            Rect plaza = new Rect(center.x - plazaSize * 0.5f, center.y - plazaSize * 0.5f, plazaSize, plazaSize);
            AddRoadRect(plaza);
            occupied.Add(plaza);
        }

        if (map == null) return;
        // 守卫巡逻用的路上取样点
        for (float d = 6f; d <= radius - 2f; d += 6f)
        {
            map.roads.Add(center + new Vector2(d, 0f));
            map.roads.Add(center + new Vector2(-d, 0f));
            map.roads.Add(center + new Vector2(0f, d));
            map.roads.Add(center + new Vector2(0f, -d));
        }
    }

    void AddRoadRect(Rect area)
    {
        GameObject go = new GameObject("Road");
        go.transform.SetParent(roadsRoot, false);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = roadSprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(area.width, area.height);
        // 道路和草地同在 Ground 层（草地 -900），路比草地高一点才看得见
        ApplyGroundLayer(sr);
        sr.sortingOrder = -885;
    }

    // ---------------- 水井 ----------------

    void BuildWell()
    {
        GameObject go = new GameObject("Well");
        go.transform.SetParent(housesRoot, false);
        go.transform.position = new Vector3(center.x, center.y, 0f);

        int order = YOrder(center.y);
        AddDisc(go.transform, "Rim", 3.0f, Vector2.zero, new Color(0.52f, 0.51f, 0.48f), order);
        AddDisc(go.transform, "Water", 2.1f, Vector2.zero, new Color(0.14f, 0.24f, 0.34f), order + 1);
        AddDisc(go.transform, "Post", 0.9f, new Vector2(1.5f, 0.4f), new Color(0.42f, 0.29f, 0.16f), order + 2);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 1.5f;
        occupied.Add(new Rect(center.x - 1.6f, center.y - 1.6f, 3.2f, 3.2f));

        if (map != null) map.wells.Add(center);
    }

    // ---------------- 设施 ----------------

    void BuildFacilities()
    {
        if (Chance(farmChance)) BuildFarm();
        if (Chance(penChance)) BuildPen();
        if (Chance(bakeryChance)) CreateShop("Bakery", new Color(0.88f, 0.74f, 0.40f), new Color(0.45f, 0.32f, 0.24f), false);
        if (Chance(smithyChance)) CreateShop("Smithy", new Color(0.62f, 0.60f, 0.58f), new Color(0.32f, 0.30f, 0.32f), true);
        if (Chance(stallChance)) BuildStall(Rand(0f, 360f));
        if (Chance(gardenChance)) BuildGarden();
        if (Chance(boardChance)) BuildNoticeBoard();
        int benches = RandInt(1, 3);
        for (int i = 0; i < benches; i++) BuildBench();
        BuildLamps();
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
            AddSlice(go.transform, "Soil", rectSprite, new Vector2(w, h), Vector2.zero, new Color(0.37f, 0.27f, 0.18f), -870, true);

            int rows = Mathf.Max(3, Mathf.FloorToInt(h / 0.6f));
            for (int r = 0; r < rows; r++)
            {
                float rowY = -h * 0.5f + 0.45f + r * (h - 0.6f) / Mathf.Max(1, rows - 1);
                AddRect(go.transform, "Row", new Vector2(w - 0.55f, 0.15f), new Vector2(0f, rowY), new Color(0.30f, 0.21f, 0.13f), -860, true);
                for (int k = 0; k < 3; k++)
                    AddDisc(go.transform, "Sprout", 0.2f, new Vector2(-w * 0.3f + k * w * 0.3f, rowY + 0.07f), new Color(0.42f, 0.62f, 0.28f), -850, true);
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
            AddSlice(go.transform, "Grass", rectSprite, new Vector2(w, h), Vector2.zero, new Color(0.38f, 0.48f, 0.26f), -845, true);
            AddRect(go.transform, "FenceN", new Vector2(w, 0.2f), new Vector2(0f, h * 0.5f), fence, YOrder(c.y + h * 0.5f) + 1);
            AddRect(go.transform, "FenceW", new Vector2(0.2f, h), new Vector2(-w * 0.5f, 0f), fence, order + 1);
            AddRect(go.transform, "FenceE", new Vector2(0.2f, h), new Vector2(w * 0.5f, 0f), fence, order + 1);

            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2(Rand(-w * 0.32f, w * 0.32f), Rand(-h * 0.32f, h * 0.32f));
                // 羊各自按自己的 Y 排序，站在羊附近的人才不会被羊挡住
                int sheepOrder = YOrder(c.y + p.y) + 2;
                AddDisc(go.transform, "Sheep", 0.52f, p, new Color(0.93f, 0.92f, 0.87f), sheepOrder);
                AddDisc(go.transform, "SheepHead", 0.24f, p + new Vector2(0.24f, 0.14f), new Color(0.30f, 0.28f, 0.30f), sheepOrder + 1);
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

            GameObject go = CreateHouse(area, housesRoot);
            go.name = name;

            int order = YOrder(c.y);
            float wallHeight = Mathf.Clamp(area.height * 0.30f, 0.5f, 0.8f);
            float wallY = -area.height * 0.5f + wallHeight * 0.5f;
            AddRect(go.transform, "Sign", new Vector2(0.95f, 0.42f), new Vector2(0f, wallY + 0.42f), signColor, order + 3);
            AddRect(go.transform, "Chimney", new Vector2(0.38f, 0.7f), new Vector2(area.width * 0.28f, area.height * 0.5f + 0.2f), chimneyColor, order + 1);

            if (forge)
            {
                AddRect(go.transform, "Anvil", new Vector2(0.5f, 0.32f), new Vector2(-area.width * 0.42f, -area.height * 0.5f - 0.5f), new Color(0.22f, 0.22f, 0.24f), order + 2);
                SpriteRenderer fire = AddDisc(go.transform, "Forge", 0.55f, new Vector2(-area.width * 0.42f, -area.height * 0.5f - 0.3f), new Color(0.95f, 0.45f, 0.15f), order + 1);
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
            float distance = plazaSize * 0.5f + 3.2f + (attempt % 3) * 1.6f;
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
            AddRect(go.transform, "Counter", new Vector2(1.7f, 0.5f), new Vector2(0f, -0.3f), new Color(0.56f, 0.41f, 0.25f), order);
            AddRect(go.transform, "Awning", new Vector2(1.95f, 0.45f), new Vector2(0f, 0.35f), awning, order + 1);
            AddRect(go.transform, "PostL", new Vector2(0.12f, 0.9f), new Vector2(-0.92f, 0.05f), new Color(0.45f, 0.33f, 0.20f), order);
            AddRect(go.transform, "PostR", new Vector2(0.12f, 0.9f), new Vector2(0.92f, 0.05f), new Color(0.45f, 0.33f, 0.20f), order);
            AddDisc(go.transform, "Goods1", 0.3f, new Vector2(-0.5f, -0.1f), new Color(0.85f, 0.48f, 0.26f), order + 2);
            AddDisc(go.transform, "Goods2", 0.26f, new Vector2(0f, -0.08f), new Color(0.90f, 0.80f, 0.35f), order + 2);
            AddDisc(go.transform, "Goods3", 0.28f, new Vector2(0.5f, -0.1f), new Color(0.45f, 0.66f, 0.36f), order + 2);

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
            AddSlice(go.transform, "Bed", rectSprite, new Vector2(w, h), Vector2.zero, new Color(0.30f, 0.40f, 0.24f), -840, true);
            AddRect(go.transform, "EdgeN", new Vector2(w, 0.14f), new Vector2(0f, h * 0.5f), new Color(0.62f, 0.58f, 0.50f), -835, true);
            AddRect(go.transform, "EdgeS", new Vector2(w, 0.14f), new Vector2(0f, -h * 0.5f), new Color(0.62f, 0.58f, 0.50f), -835, true);
            for (int i = 0; i < 7; i++)
                AddDisc(go.transform, "Flower", Rand(0.18f, 0.26f), new Vector2(Rand(-w * 0.36f, w * 0.36f), Rand(-h * 0.34f, h * 0.34f)), Pick(FlowerColors), -830, true);

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
            float distance = plazaSize * 0.5f + Rand(1.8f, 3.4f);
            Vector2 c = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Rect area = new Rect(c.x - 0.8f, c.y - 0.5f, 1.6f, 1.0f);
            if (!IsFree(area, 0.7f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("NoticeBoard");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            AddRect(go.transform, "Post", new Vector2(0.16f, 1.0f), new Vector2(0f, -0.4f), new Color(0.44f, 0.31f, 0.19f), order);
            AddRect(go.transform, "Board", new Vector2(1.25f, 0.8f), new Vector2(0f, 0.28f), new Color(0.60f, 0.44f, 0.27f), order + 1);
            AddRect(go.transform, "Paper1", new Vector2(0.32f, 0.4f), new Vector2(-0.3f, 0.3f), new Color(0.93f, 0.92f, 0.86f), order + 2);
            AddRect(go.transform, "Paper2", new Vector2(0.28f, 0.34f), new Vector2(0.3f, 0.32f), new Color(0.90f, 0.88f, 0.80f), order + 2);

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
            float distance = plazaSize * 0.5f + Rand(1.4f, 4.2f);
            Vector2 c = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            Rect area = new Rect(c.x - 0.85f, c.y - 0.5f, 1.7f, 1.0f);
            if (!IsFree(area, 0.7f)) continue;
            occupied.Add(area);

            GameObject go = new GameObject("Bench");
            go.transform.SetParent(facilitiesRoot, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            int order = YOrder(c.y);
            AddRect(go.transform, "Seat", new Vector2(1.5f, 0.34f), new Vector2(0f, 0f), new Color(0.56f, 0.40f, 0.24f), order);
            AddRect(go.transform, "Back", new Vector2(1.5f, 0.16f), new Vector2(0f, 0.34f), new Color(0.48f, 0.34f, 0.20f), order + 1);

            if (map != null) map.benches.Add(new Vector2(c.x, c.y - 0.7f));
            return;
        }
    }

    /// <summary>路灯：沿区块内的十字路排布（相邻区块接上就是一整条街的灯）。</summary>
    void BuildLamps()
    {
        float offset = roadWidth * 0.5f + 0.55f;
        for (float d = 4.5f; d <= radius - 3f; d += 6.5f)
        {
            CreateLamp(center + new Vector2(d, offset));
            CreateLamp(center + new Vector2(-d, -offset));
            CreateLamp(center + new Vector2(offset, d));
            CreateLamp(center + new Vector2(-offset, -d));
        }
    }

    void CreateLamp(Vector2 position)
    {
        GameObject go = new GameObject("Lamp");
        go.transform.SetParent(facilitiesRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y);
        AddRect(go.transform, "Post", new Vector2(0.13f, 1.1f), new Vector2(0f, 0.55f), new Color(0.34f, 0.31f, 0.29f), order);
        AddDisc(go.transform, "Head", 0.34f, new Vector2(0f, 1.16f), new Color(0.92f, 0.88f, 0.62f), order + 1);

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
        int target = hamlet ? RandInt(houseMin, houseMax + 1) : RandInt(0, 2);
        int placed = 0;
        int attempts = 0;
        while (placed < target && attempts < target * 60)
        {
            attempts++;
            float w = Rand(2.9f, 4.4f);
            float h = Rand(2.5f, 3.5f);
            Vector2 c = center + new Vector2(Rand(-radius + w * 0.5f + 0.6f, radius - w * 0.5f - 0.6f), Rand(-radius + h * 0.5f + 0.6f, radius - h * 0.5f - 0.6f));
            Rect area = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            if (!IsFree(area, 1.0f)) continue;

            occupied.Add(area);
            CreateHouse(area, housesRoot);
            if (map != null) map.houses.Add(new Vector2(area.center.x, area.yMin - 0.9f));   // 门口
            placed++;
        }
    }

    GameObject CreateHouse(Rect area, Transform parent)
    {
        GameObject go = new GameObject("House");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);

        int order = YOrder(area.center.y);
        float wallHeight = Mathf.Clamp(area.height * 0.30f, 0.5f, 0.8f);
        float wallY = -area.height * 0.5f + wallHeight * 0.5f;

        AddSlice(go.transform, "Roof", rectSprite, new Vector2(area.width, area.height), Vector2.zero, Pick(RoofColors), order);
        AddSlice(go.transform, "Wall", rectSprite, new Vector2(area.width, wallHeight), new Vector2(0f, wallY), new Color(0.88f, 0.84f, 0.73f), order + 1);
        AddSlice(go.transform, "Door", rectSprite, new Vector2(0.66f, wallHeight * 0.88f), new Vector2(0f, wallY), new Color(0.44f, 0.28f, 0.16f), order + 2);

        // 窗户白天是反光的玻璃色，夜里透出暖黄的灯光
        Color windowDay = new Color(0.70f, 0.85f, 0.92f);
        Color windowNight = new Color(1f, 0.90f, 0.58f);
        SpriteRenderer windowL = AddSlice(go.transform, "WindowL", rectSprite, new Vector2(0.52f, wallHeight * 0.44f), new Vector2(-area.width * 0.28f, wallY + wallHeight * 0.12f), windowDay, order + 2);
        SpriteRenderer windowR = AddSlice(go.transform, "WindowR", rectSprite, new Vector2(0.52f, wallHeight * 0.44f), new Vector2(area.width * 0.28f, wallY + wallHeight * 0.12f), windowDay, order + 2);
        NightGlow glowL = windowL.gameObject.AddComponent<NightGlow>();
        glowL.dayColor = windowDay;
        glowL.nightColor = windowNight;
        NightGlow glowR = windowR.gameObject.AddComponent<NightGlow>();
        glowR.dayColor = windowDay;
        glowR.nightColor = windowNight;

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(area.width, area.height);

        return go;
    }

    // ---------------- 树木 ----------------

    void BuildTrees()
    {
        int target = hamlet ? RandInt(treeMin, treeMax + 1) : RandInt(treeMin * 2, treeMax * 3 + 1);
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
        float size = Rand(1.7f, 2.7f);
        Color canopy = Pick(TreeColors);

        AddDisc(go.transform, "Shadow", size * 0.95f, new Vector2(0.06f, -0.08f), new Color(0f, 0f, 0f, 0.18f), order - 1);
        AddDisc(go.transform, "Canopy", size, Vector2.zero, canopy, order);
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

    // ---------------- 食物 / 木箱 ----------------

    void BuildFood()
    {
        int target = RandInt(foodMin, foodMax + 1);
        for (int i = 0; i < target; i++)
        {
            Vector2 point;
            if (!TryFindFreePoint(0.3f, 0.1f, out point)) continue;
            CreateFood(point, Chance(0.66f));
        }
    }

    void CreateFood(Vector2 position, bool berry)
    {
        occupied.Add(new Rect(position.x - 0.35f, position.y - 0.35f, 0.7f, 0.7f));
        GameObject go = new GameObject(berry ? "Berry" : "Leaf");
        go.transform.SetParent(foodRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = Vector3.one * (berry ? 0.26f : 0.34f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = berry ? berrySprite : leafSprite;
        sr.sortingOrder = YOrder(position.y) + 1;

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        collider.isTrigger = true;

        // 隐藏数值（分量）：决定吃下去恢复多少体力，不显示给玩家
        int value = berry ? RandInt(6, 11) : RandInt(10, 15);
        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = value;
        hidden.note = berry ? "果子：小分量" : "叶子：中等分量";

        Edible edible = go.AddComponent<Edible>();
        edible.nutrition = 1;
        edible.satiety = value;

        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = berry ? "野果子" : "嫩叶";
        info.kind = "食物";
        info.description = berry
            ? "随处可见的小果子。恢复的体力不多，但满地都是。"
            : "一片嫩叶，比果子顶饱一些。";

        go.AddComponent<Highlighter>().Setup(discSprite);
    }

    /// <summary>特殊食物：吃掉能让小虫长大（更大 / 更快 / 吃得更远 / 体力上限更高）。</summary>
    void BuildSpecialFood()
    {
        if (!Chance(specialFoodChance)) return;

        Vector2 point;
        if (!TryFindFreePoint(0.7f, 0.5f, out point, 80)) return;
        occupied.Add(new Rect(point.x - 0.7f, point.y - 0.7f, 1.4f, 1.4f));
        CreateSpecialFood(point);
    }

    void CreateSpecialFood(Vector2 position)
    {
        GameObject go = new GameObject("SpecialFood");
        go.transform.SetParent(foodRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        int order = YOrder(position.y) + 2;
        AddDisc(go.transform, "Halo", 1.55f, Vector2.zero, new Color(1f, 0.84f, 0.35f, 0.30f), order);
        AddDisc(go.transform, "Body", 0.74f, Vector2.zero, new Color(0.97f, 0.76f, 0.25f), order + 1);
        AddDisc(go.transform, "Inner", 0.44f, new Vector2(-0.05f, 0.06f), new Color(1f, 0.95f, 0.72f), order + 2);
        AddDisc(go.transform, "SparkA", 0.18f, new Vector2(0.52f, 0.44f), new Color(1f, 0.97f, 0.85f), order + 2);
        AddDisc(go.transform, "SparkB", 0.13f, new Vector2(-0.5f, -0.42f), new Color(1f, 0.97f, 0.85f), order + 2);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 0.8f;
        collider.isTrigger = true;

        // 隐藏数值（分量）：特殊食物的饱食度各不相同，越大的越顶饱
        int value = RandInt(22, 31);
        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = value;
        hidden.note = "特殊食物：大分量";

        Edible edible = go.AddComponent<Edible>();
        edible.nutrition = 5;
        edible.satiety = value;
        edible.growth = 1;

        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = "神奇果实";
        info.kind = "特殊食物";
        info.description = "吃下去小虫会长大一截：更大、跑得更快、捕食范围更广、体力上限更高。";

        go.AddComponent<Highlighter>().Setup(discSprite);
    }

    void BuildCrates()
    {
        int target = RandInt(0, crateMax + 1);
        for (int i = 0; i < target; i++)
        {
            Vector2 point;
            if (!TryFindFreePoint(0.45f, 0.25f, out point, 100)) continue;
            CreateCrate(point);
        }
    }

    void CreateCrate(Vector2 position)
    {
        occupied.Add(new Rect(position.x - 0.55f, position.y - 0.55f, 1.1f, 1.1f));
        GameObject go = new GameObject("Crate");
        go.transform.SetParent(propsRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        float size = Rand(0.6f, 0.9f);
        SpriteRenderer sr = AddSlice(go.transform, "Visual", rectSprite, Vector2.one, Vector2.zero, new Color(0.66f, 0.48f, 0.27f), 10);
        sr.transform.localScale = Vector3.one * size;

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * size;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        go.AddComponent<Draggable>().weight = 1f;

        // 木箱也有隐藏数值（分量）和一段介绍
        HiddenValue hidden = go.AddComponent<HiddenValue>();
        hidden.value = RandInt(12, 19);
        hidden.note = "木箱：可搬动的道具";
        EntityInfo info = go.AddComponent<EntityInfo>();
        info.title = "木箱";
        info.kind = "道具";
        info.description = "搬到哪算哪的箱子。站在它前面按 F 就能搬起来，搬运时小虫会变慢；"
            + "长到 " + BugGrowth.CrateLevel + " 级以后可以直接啃掉。";

        // 木箱也能吃，要长到 2 级（HiddenValue 的分量决定吃下去回多少体力）
        Edible edible = go.AddComponent<Edible>();
        edible.nutrition = 3;
        edible.requiredLevel = BugGrowth.CrateLevel;

        // Highlighter 要在 YSort 之前加，保证它生成的发光底衬也被纳入深度排序
        go.AddComponent<Highlighter>().Setup(roundRectSprite);
        go.AddComponent<YSort>();
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
        AddDisc(go.transform, "Rim", 1.10f, Vector2.zero, rim, -820, true);
        AddDisc(go.transform, "Hole", 0.80f, Vector2.zero, new Color(0.07f, 0.06f, 0.06f), -810, true);
        AddDisc(go.transform, "HoleInner", 0.48f, new Vector2(0.04f, -0.05f), new Color(0.02f, 0.02f, 0.02f), -800, true);

        if (tunnel)
        {
            // 地道多堆两个小石头当标记，好认
            AddDisc(go.transform, "StoneA", 0.24f, new Vector2(0.66f, 0.44f), new Color(0.56f, 0.56f, 0.53f), -795, true);
            AddDisc(go.transform, "StoneB", 0.17f, new Vector2(0.84f, 0.26f), new Color(0.49f, 0.49f, 0.46f), -795, true);
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
        int target = hamlet ? RandInt(villagerMin, villagerMax + 1) : RandInt(0, 2);
        int spawned = 0;
        int attempts = 0;
        while (spawned < target && attempts < target * 40)
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
        AddRect(visual.transform, "Body", new Vector2(0.40f, 0.52f), new Vector2(0f, -0.12f), shirt, 0);
        AddDisc(visual.transform, "Head", 0.36f, new Vector2(0f, 0.30f), skin, 1);

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
