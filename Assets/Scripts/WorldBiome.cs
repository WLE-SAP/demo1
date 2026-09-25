using UnityEngine;

/// <summary>
/// **自然体系**：这一片区块长什么样（地面、树、野外产出、能捡到的东西）。
/// </summary>
public enum NatureKind
{
    Grassland = 0,   // 草原：草地 + 阔叶树 + 果子 / 嫩叶 / 麦穗，灌木与草垛
    Forest = 1,      // 森林：深色林地 + 密集高树 + 蘑菇 / 松果 / 木料，灌木多
    Desert = 2       // 沙漠：沙地 + 仙人掌 / 枯树 + 仙人掌果，偶尔一片绿洲
}

/// <summary>
/// **聚落体系**：这一片区块有多热闹（房子多少、有没有设施、有多少人）。
/// </summary>
public enum SettlementKind
{
    Wilderness = 0,  // 荒野：几乎没有房子与人，木屋 + 篝火 + 地洞多
    Village = 1,     // 农村：农舍 / 两层小楼 / 谷仓 / 风车 + 农田摊位
    City = 2         // 城市：排屋 / 公寓 / 钟楼 / 喷泉，人挤人
}

/// <summary>
/// 世界的「地貌 + 聚落」两套体系（2026-09-25 取代原来的「开局选三张地图」）。
///
/// <list type="bullet">
/// <item>**没有全局地图类型了**：世界是一整片连续的无限地图，每个区块自己属于
///       一种<b>自然体系</b>（草原 / 森林 / 沙漠）与一种<b>聚落体系</b>（荒野 / 农村 / 城市），
///       9 种组合各自有地面、建筑、树木与可交互物；</item>
/// <item>两种体系都由**低频值噪声**决定（见 <see cref="Noise"/>）：噪声在同一格子内平滑变化，
///       所以地貌是**成片**的（一片森林、一片沙漠），不会一个区块一个样地花掉；</item>
/// <item>两者都只依赖 **区块坐标 + 世界种子**，是纯函数 —— 走远再回头、读档后回来，完全一致；</item>
/// <item>出生点周围 <see cref="StartRadius"/> 格固定是「农村 + 草原」，保证开局有村子、有吃的。</item>
/// </list>
///
/// 这是**生成参数的唯一权威**：<see cref="VillageGenerator"/> 上那些
/// houseMin / treeMin / foodSlots… 字段会在每个区块生成前被 <see cref="Apply"/> **整体覆盖**
/// （不是累乘），所以不要在 Inspector 里手改它们，改这里。
/// </summary>
public static class WorldBiome
{
    // ---------------- 尺度 ----------------

    /// <summary>自然区域的大小（单位：区块）。越大，同一片地貌越辽阔。</summary>
    public const int NatureRegion = 3;

    /// <summary>聚落区域的大小（单位：区块）。城市 / 农村 / 荒野各自成片。</summary>
    public const int SettlementRegion = 4;

    /// <summary>出生点周围这么大一圈（以区块计）固定是「农村 + 草原」。</summary>
    public const int StartRadius = 1;

    /// <summary>
    /// **全局密度系数**（原 <c>MapProfiles.Density</c>）：房子 / 树 / 灌木 / 仙人掌 / 食物位 / 物品位 /
    /// 各类设施几率都乘它，村民数量**不乘**（人口直接影响性能与手感，单独调）。
    /// </summary>
    public const float Density = 1.8f;

    // ---------------- 分类阈值 ----------------

    const float GrasslandMax = 0.40f;      // 噪声 < 0.40 → 草原
    const float ForestMax = 0.70f;         // 0.40 ~ 0.70 → 森林，其余 → 沙漠
    const float WildernessMax = 0.45f;     // 聚落噪声 < 0.45 → 荒野
    const float VillageMax = 0.80f;        // 0.45 ~ 0.80 → 农村，其余 → 城市

    // 噪声的盐：两套体系各用一组独立的格子，互不干扰
    const int NatureSalt = 101;
    const int SettlementSalt = 977;

    // ---------------- 查询 ----------------

    /// <summary>出生地周围这一圈（强制农村 + 草原）。</summary>
    public static bool IsStartArea(Vector2Int coord)
    {
        return Mathf.Abs(coord.x) <= StartRadius && Mathf.Abs(coord.y) <= StartRadius;
    }

    /// <summary>这个区块的自然体系。</summary>
    public static NatureKind NatureAt(Vector2Int coord, int seed)
    {
        if (IsStartArea(coord)) return NatureKind.Grassland;

        float n = Noise(coord, seed, NatureRegion, NatureSalt);
        if (n < GrasslandMax) return NatureKind.Grassland;
        return n < ForestMax ? NatureKind.Forest : NatureKind.Desert;
    }

    /// <summary>这个区块的聚落体系。</summary>
    public static SettlementKind SettlementAt(Vector2Int coord, int seed)
    {
        if (IsStartArea(coord)) return SettlementKind.Village;

        float n = Noise(coord, seed, SettlementRegion, SettlementSalt);
        if (n < WildernessMax) return SettlementKind.Wilderness;
        return n < VillageMax ? SettlementKind.Village : SettlementKind.City;
    }

    /// <summary>世界坐标属于哪个区块（和 <see cref="VillageWorld.chunkSize"/> 一致）。</summary>
    public static Vector2Int ChunkOf(Vector2 world, float chunkSize = DefaultChunkSize)
    {
        float size = chunkSize > 0.1f ? chunkSize : DefaultChunkSize;
        return new Vector2Int(Mathf.RoundToInt(world.x / size), Mathf.RoundToInt(world.y / size));
    }

    /// <summary>VillageWorld / VillageGenerator 的默认区块边长（两边必须一致）。</summary>
    public const float DefaultChunkSize = 32f;

    // ---------------- 文字 ----------------

    public static string NatureLabel(NatureKind nature)
    {
        switch (nature)
        {
            case NatureKind.Forest: return "森林";
            case NatureKind.Desert: return "沙漠";
            default: return "草原";
        }
    }

    public static string SettlementLabel(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.Wilderness: return "荒野";
            case SettlementKind.City: return "城市";
            default: return "农村";
        }
    }

    /// <summary>合起来的一句话，例如「森林 · 农村」（HUD 与继续游戏按钮用）。</summary>
    public static string Describe(NatureKind nature, SettlementKind settlement)
    {
        return NatureLabel(nature) + " · " + SettlementLabel(settlement);
    }

    // ---------------- 表现：地面与道路 ----------------

    /// <summary>
    /// 这个自然体系要额外铺一层区块地面用的美术 key；**草原返回 null**
    /// （草原直接用场景里那张跟随小虫的无限草地，不重复铺一层，`ground` key 仍然有效）。
    /// </summary>
    public static string GroundKey(NatureKind nature)
    {
        switch (nature)
        {
            case NatureKind.Forest: return ArtKeys.GroundForest;
            case NatureKind.Desert: return ArtKeys.GroundDesert;
            default: return null;
        }
    }

    /// <summary>没有地面图片时，用程序化地面乘这个颜色（森林 = 深林地，沙漠 = 干土黄）。</summary>
    public static Color GroundTint(NatureKind nature)
    {
        switch (nature)
        {
            case NatureKind.Forest: return new Color(0.70f, 0.78f, 0.52f);
            case NatureKind.Desert: return new Color(1f, 0.97f, 0.74f);
            default: return Color.white;
        }
    }

    /// <summary>这个聚落的路面用哪个 key（城市是压实的石板路，荒野是土路）。</summary>
    public static string RoadKey(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.City: return ArtKeys.RoadPaved;
            case SettlementKind.Wilderness: return ArtKeys.RoadDirt;
            default: return ArtKeys.Road;
        }
    }

    /// <summary>这个聚落的路面颜色（乘在路面贴图上；城市压深一点，荒野偏干）。</summary>
    public static Color RoadTint(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.City: return new Color(0.78f, 0.78f, 0.80f);
            case SettlementKind.Wilderness: return new Color(0.82f, 0.76f, 0.62f);
            default: return Color.white;
        }
    }

    /// <summary>
    /// 这个聚落的**路面宽度**。是**纯函数**（只看聚落），因为「路口瓦片要多大」要从它反推
    /// （瓦片图里路面只占画布的一部分，见 <c>VillageGenerator.RoadTileRoadRatio</c>），
    /// 而瓦片模式要能对**邻居区块**（还没生成的那个）算路型 —— 所以别在别处再写一份数值。
    ///
    /// **2026-09-25 用户反馈「路太粗」→ 整体收窄约 40%（原来是 2.4 / 3.4 / 4.2）**：
    /// 小虫只有 0.4× 原始尺寸、相机视野 16.8×29.9 世界单位，路宽相对屏幕本来是 20% 高，太抢眼。
    /// 想让路再细 / 再粗**只改这三个数**：路口瓦片尺寸、占位走廊、作坊离路的距离、巡逻取样点全都跟着走。
    /// </summary>
    public static float RoadWidthOf(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.Wilderness: return 1.5f;   // 荒野：窄土路
            case SettlementKind.City: return 2.6f;         // 城市：宽一点，但没到以前那么宽
            default: return 2.0f;                          // 农村
        }
    }

    /// <summary>这个聚落「一个区块有没有路」的几率（**纯函数**，同上：邻居区块也按它算）。</summary>
    public static float RoadChanceOf(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.City: return 1f;
            case SettlementKind.Wilderness: return 0.45f;
            default: return 0.88f;
        }
    }

    // ---------------- 世界流式加载（按当前区块的聚落取值） ----------------

    /// <summary>要不要「保证视野里有人」（荒野不补人，人少才像荒野）。</summary>
    public static bool KeepVillagersInView(SettlementKind settlement)
    {
        return settlement != SettlementKind.Wilderness;
    }

    /// <summary>可见范围里至少要有几个村民。</summary>
    public static int MinVillagersInView(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.City: return 5;
            case SettlementKind.Village: return 2;
            default: return 0;
        }
    }

    /// <summary>每个区块最多为此补几个村民（防止人越补越多）。</summary>
    public static int MaxExtraPerChunk(SettlementKind settlement)
    {
        switch (settlement)
        {
            case SettlementKind.City: return 6;
            case SettlementKind.Village: return 4;
            default: return 0;
        }
    }

    /// <summary>离小虫超过这个距离的村民被冻结（城里人太多，冻得近一点省性能）。</summary>
    public static float FreezeRadius(SettlementKind settlement)
    {
        return settlement == SettlementKind.City ? 22f : 26f;
    }

    // ---------------- 生成参数 ----------------

    /// <summary>
    /// 把「这个区块的聚落 + 自然体系」套到生成器上：聚落决定基数，自然体系再乘一遍，
    /// 最后统一乘 <see cref="Density"/>。**每个区块生成前都要调一次**（写在 <c>BuildChunk</c> 开头）。
    /// </summary>
    public static void Apply(SettlementKind settlement, NatureKind nature, VillageGenerator g)
    {
        if (g == null) return;

        ApplySettlement(settlement, g);
        ApplyNature(nature, g);
        ApplyDensity(g, settlement);

        // 神奇果实（长大唯一途径）在哪都刷，避免玩家走到沙漠 / 荒野断档
        FoodCatalog.SetChance(FoodIds.SpecialFood, 0.45f);
    }

    /// <summary>聚落决定的是「基数」：房子 / 人口 / 设施 / 广场 / 路宽。</summary>
    static void ApplySettlement(SettlementKind settlement, VillageGenerator g)
    {
        switch (settlement)
        {
            case SettlementKind.Wilderness:
                g.houseMin = 0; g.houseMax = 1;          // 偶尔一栋孤零零的木屋
                g.treeMin = 10; g.treeMax = 16;
                g.bushMin = 2; g.bushMax = 5;
                g.foodSlotsMin = 6; g.foodSlotsMax = 10; // 没人打理，野生食物反而多
                g.itemSlotsMin = 0; g.itemSlotsMax = 1;
                g.villagerMin = 0; g.villagerMax = 1;
                g.burrowMin = 1; g.burrowMax = 3;        // 野外的洞更多（躲与传送）
                g.buildFacilities = false;
                g.plazaChance = 0f;
                g.wellChance = 0.12f;
                g.fountainChance = 0f;
                g.windmillChance = 0f;
                g.bellTowerChance = 0f;
                g.campfireChance = 0.4f;
                g.roadWidth = RoadWidthOf(settlement);
                g.roadChance = RoadChanceOf(settlement);   // 大半区块根本没有路
                break;

            case SettlementKind.City:
                // 名义 8~12 栋（乘 `Density` 1.8 → 14~22）。城市地块被路面 + 设施切碎、大公寓塞不进，
                // `VillageGenerator.BuildHouses` 会「挤不下就把房型降小一号」，实测落在 10~14 栋左右。
                // **2026-09-25 用户「房屋密度不够」→ 农村 3~5 → 5~8、城市 6~9 → 8~12**（都再乘 1.8）。
                g.houseMin = 8; g.houseMax = 12;
                g.treeMin = 3; g.treeMax = 7;            // 城里没多少树
                g.bushMin = 0; g.bushMax = 2;
                g.foodSlotsMin = 8; g.foodSlotsMax = 12;
                g.itemSlotsMin = 1; g.itemSlotsMax = 3;
                g.villagerMin = 6; g.villagerMax = 9;
                g.burrowMin = 0; g.burrowMax = 2;
                g.buildFacilities = true;
                g.plazaChance = 0.7f;
                g.wellChance = 0f;                       // 城里是喷泉不是井
                g.fountainChance = 0.6f;
                g.windmillChance = 0f;
                g.bellTowerChance = 0.35f;
                g.campfireChance = 0f;
                g.roadWidth = RoadWidthOf(settlement);
                g.roadChance = RoadChanceOf(settlement);
                g.farmChance = 0.1f;                     // 城里几乎没有农田
                g.penChance = 0.05f;
                g.bakeryChance = 0.35f;
                g.smithyChance = 0.35f;
                g.powerPlantChance = 0.35f;
                g.stallChance = 0.95f;
                g.gardenChance = 0.5f;
                g.boardChance = 0.5f;
                break;

            default:                                     // 农村
                g.houseMin = 5; g.houseMax = 8;          // 名义 5~8（乘 Density 1.8 → 9~14 栋）
                g.treeMin = 10; g.treeMax = 16;
                g.bushMin = 2; g.bushMax = 5;
                g.foodSlotsMin = 5; g.foodSlotsMax = 8;
                g.itemSlotsMin = 0; g.itemSlotsMax = 2;
                g.villagerMin = 2; g.villagerMax = 4;
                g.burrowMin = 0; g.burrowMax = 2;
                g.buildFacilities = true;
                g.plazaChance = 0.55f;
                g.wellChance = 0.55f;
                g.fountainChance = 0f;
                g.windmillChance = 0.3f;
                g.bellTowerChance = 0.06f;
                g.campfireChance = 0f;
                g.roadWidth = RoadWidthOf(settlement);
                g.roadChance = RoadChanceOf(settlement);
                g.farmChance = 0.7f;
                g.penChance = 0.35f;
                g.bakeryChance = 0.22f;
                g.smithyChance = 0.22f;
                g.powerPlantChance = 0.3f;
                g.stallChance = 0.8f;
                g.gardenChance = 0.7f;
                g.boardChance = 0.4f;
                break;
        }
    }

    /// <summary>
    /// 自然体系是在聚落基数上的**调整**：地面 / 树多树少 / 野外产出 / 农事难不难做。
    /// 沙漠里也会有城市（沙漠城市），所以这里是乘系数而不是直接换一套。
    /// </summary>
    static void ApplyNature(NatureKind nature, VillageGenerator g)
    {
        switch (nature)
        {
            case NatureKind.Forest:
                g.treeMin = Mathf.RoundToInt(g.treeMin * 1.7f);      // 森林：树又多又密
                g.treeMax = Mathf.RoundToInt(g.treeMax * 1.7f);
                g.bushMin += 1;
                g.bushMax += 2;
                g.cactusMin = g.cactusMax = 0;
                g.deadTreeMin = 1; g.deadTreeMax = 2;
                g.foodSlotsMin += 2;                                  // 森林里能吃的东西最多
                g.foodSlotsMax += 2;
                g.oasisChance = 0f;
                g.farmChance *= 0.7f;                                 // 树多，地不好开
                g.gardenChance *= 0.8f;
                break;

            case NatureKind.Desert:
                g.treeMin = Mathf.Max(0, Mathf.RoundToInt(g.treeMin * 0.12f));
                g.treeMax = Mathf.Max(g.treeMin, Mathf.RoundToInt(g.treeMax * 0.12f));
                g.bushMin = g.bushMax = 0;
                g.cactusMin = 3; g.cactusMax = 7;                      // 仙人掌代替树
                g.deadTreeMin = 2; g.deadTreeMax = 4;
                g.foodSlotsMin += 1;                                   // 仙人掌果顶上
                g.foodSlotsMax += 1;
                g.oasisChance = 0.18f;
                g.farmChance *= 0.2f;                                  // 沙漠里种不出东西
                g.penChance *= 0.1f;
                g.gardenChance *= 0.35f;
                g.windmillChance *= 0.3f;
                break;

            default:                                                   // 草原
                g.cactusMin = g.cactusMax = 0;
                g.deadTreeMin = 0; g.deadTreeMax = 1;
                g.oasisChance = 0f;
                break;
        }
    }

    /// <summary>统一乘全局密度（村民数量故意不动）。</summary>
    static void ApplyDensity(VillageGenerator g, SettlementKind settlement)
    {
        g.houseMin = Mathf.RoundToInt(g.houseMin * Density);
        g.houseMax = Mathf.RoundToInt(g.houseMax * Density);
        g.treeMin = Mathf.RoundToInt(g.treeMin * Density);
        g.treeMax = Mathf.RoundToInt(g.treeMax * Density);
        g.bushMin = Mathf.RoundToInt(g.bushMin * Density);
        g.bushMax = Mathf.RoundToInt(g.bushMax * Density);
        g.cactusMin = Mathf.RoundToInt(g.cactusMin * Density);
        g.cactusMax = Mathf.RoundToInt(g.cactusMax * Density);
        g.deadTreeMin = Mathf.RoundToInt(g.deadTreeMin * Density);
        g.deadTreeMax = Mathf.RoundToInt(g.deadTreeMax * Density);
        g.foodSlotsMin = Mathf.RoundToInt(g.foodSlotsMin * Density);
        g.foodSlotsMax = Mathf.RoundToInt(g.foodSlotsMax * Density);
        g.itemSlotsMin = Mathf.RoundToInt(g.itemSlotsMin * Density);
        g.itemSlotsMax = Mathf.RoundToInt(g.itemSlotsMax * Density);

        // 村庄 / 城市保证「每个区块至少有一个能搬能玩的东西」，不然密度再高也可能空手
        if (settlement != SettlementKind.Wilderness) g.itemSlotsMin = Mathf.Max(g.itemSlotsMin, 1);

        g.farmChance = Mathf.Clamp01(g.farmChance * Density);
        g.penChance = Mathf.Clamp01(g.penChance * Density);
        g.bakeryChance = Mathf.Clamp01(g.bakeryChance * Density);
        g.smithyChance = Mathf.Clamp01(g.smithyChance * Density);
        g.powerPlantChance = Mathf.Clamp01(g.powerPlantChance * Density);
        g.stallChance = Mathf.Clamp01(g.stallChance * Density);
        g.gardenChance = Mathf.Clamp01(g.gardenChance * Density);
        g.boardChance = Mathf.Clamp01(g.boardChance * Density);
    }

    // ---------------- 低频值噪声 ----------------

    /// <summary>
    /// 低频值噪声（0~1）：以 <paramref name="region"/> 个区块为一个格子，
    /// 格子四角取随机值、平滑插值，所以**相邻区块的值是连续的** —— 地貌因此成片而不是噪点。
    /// 只依赖坐标与种子，是纯函数。
    /// </summary>
    static float Noise(Vector2Int coord, int seed, int region, int salt)
    {
        int span = Mathf.Max(1, region);
        float fx = coord.x / (float)span;
        float fy = coord.y / (float)span;
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = Mathf.SmoothStep(0f, 1f, fx - x0);
        float ty = Mathf.SmoothStep(0f, 1f, fy - y0);

        float a = Hash01(x0, y0, seed, salt);
        float b = Hash01(x0 + 1, y0, seed, salt);
        float c = Hash01(x0, y0 + 1, seed, salt);
        float d = Hash01(x0 + 1, y0 + 1, seed, salt);

        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    /// <summary>整数坐标 + 种子 → [0,1) 的确定性随机数。</summary>
    static float Hash01(int x, int y, int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041 + salt * 2246822519);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }
}
