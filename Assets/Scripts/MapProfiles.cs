using UnityEngine;

/// <summary>地图类型：区别是「村子有多密、npc 有多少」。</summary>
public enum MapKind
{
    Wilderness = 0,   // 荒野：几乎没人，空旷、树多
    Village = 1,      // 农村：正常的村庄，npc 数量中等
    City = 2          // 城市：全是房子，npc 数量很大
}

/// <summary>
/// 三张地图的参数表 + 记忆（PlayerPrefs）。
///
/// 世界的生成方式没变（区块坐标 + 种子），变的只是「密度参数」：
/// 主菜单「开始游戏」时选一张地图 → <see cref="VillageWorld"/> 建世界前调用
/// <see cref="Apply"/> 覆盖 <see cref="VillageGenerator"/> / <see cref="VillageWorld"/> 上的参数，
/// 之后每个区块都按这套参数生成。
/// 存档里也会记下地图类型，「继续游戏」时用同一张地图把世界还原回来。
/// </summary>
public static class MapProfiles
{
    const string KeyMap = "game.mapKind";

    /// <summary>三张地图（主菜单按这个顺序排按钮）。</summary>
    public static readonly MapKind[] All = { MapKind.Wilderness, MapKind.Village, MapKind.City };

    /// <summary>当前（或上次选中）的地图类型，存在 PlayerPrefs 里。</summary>
    public static MapKind Current
    {
        get { return (MapKind)Mathf.Clamp(PlayerPrefs.GetInt(KeyMap, (int)MapKind.Village), 0, All.Length - 1); }
        set
        {
            PlayerPrefs.SetInt(KeyMap, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static string Label(MapKind kind)
    {
        switch (kind)
        {
            case MapKind.Wilderness: return "荒野";
            case MapKind.City: return "城市";
            default: return "农村";
        }
    }

    /// <summary>主菜单地图按钮上的一句话说明（重点是 npc 数量）。</summary>
    public static string Description(MapKind kind)
    {
        switch (kind)
        {
            case MapKind.Wilderness: return "npc 很少 · 荒凉空旷，树多、人烟稀少";
            case MapKind.City: return "npc 很多 · 房屋密集，人挤人";
            default: return "npc 数量中等 · 有农田摊位的寻常村庄";
        }
    }

    /// <summary>
    /// 把某张地图的密度参数套到生成器与世界流式加载器上。
    /// 必须在建区块之前调用（<see cref="VillageWorld.Start"/> / <see cref="VillageWorld.LoadWorld"/>）。
    /// </summary>
    public static void Apply(MapKind kind, VillageGenerator generator, VillageWorld world)
    {
        if (generator != null) ApplyToGenerator(kind, generator);
        if (world != null) ApplyToWorld(kind, world);
    }

    static void ApplyToGenerator(MapKind kind, VillageGenerator g)
    {
        switch (kind)
        {
            case MapKind.Wilderness:
                g.hamletChance = 0.15f;          // 大部分区块是野外
                g.houseMin = 0; g.houseMax = 1;  // 偶尔一栋孤屋
                g.treeMin = 12; g.treeMax = 18;  // 树多
                g.foodMin = 7; g.foodMax = 11;   // 吃的多（没人打理，反而野生食物多）
                g.crateMax = 1;
                g.villagerMin = 0; g.villagerMax = 1;
                g.specialFoodChance = 0.5f;
                g.buildFacilities = false;       // 荒野没有农田摊位
                break;

            case MapKind.City:
                g.hamletChance = 1f;             // 每个区块都是「城区」
                g.houseMin = 6; g.houseMax = 9;  // 房子密
                g.treeMin = 3; g.treeMax = 7;    // 城里没多少树
                g.foodMin = 8; g.foodMax = 12;
                g.crateMax = 3;
                g.villagerMin = 6; g.villagerMax = 9;   // npc 数量很大
                g.specialFoodChance = 0.5f;
                g.buildFacilities = true;
                g.farmChance = 0.45f;
                g.penChance = 0.20f;
                g.bakeryChance = 0.35f;
                g.smithyChance = 0.35f;
                g.stallChance = 0.95f;
                g.gardenChance = 0.5f;
                g.boardChance = 0.5f;
                break;

            default:                             // 农村：维持原本的村庄参数
                g.hamletChance = 0.65f;
                g.houseMin = 3; g.houseMax = 5;
                g.treeMin = 10; g.treeMax = 16;
                g.foodMin = 5; g.foodMax = 8;
                g.crateMax = 2;
                g.villagerMin = 2; g.villagerMax = 4;
                g.specialFoodChance = 0.45f;
                g.buildFacilities = true;
                g.farmChance = 0.7f;
                g.penChance = 0.35f;
                g.bakeryChance = 0.22f;
                g.smithyChance = 0.22f;
                g.stallChance = 0.8f;
                g.gardenChance = 0.7f;
                g.boardChance = 0.4f;
                break;
        }
    }

    static void ApplyToWorld(MapKind kind, VillageWorld world)
    {
        switch (kind)
        {
            case MapKind.Wilderness:
                world.keepVillagersInView = false;   // 荒野就该看不见人，不补人
                world.minVillagersInView = 0;
                world.maxExtraPerChunk = 0;
                world.freezeRadius = 26f;
                break;

            case MapKind.City:
                world.keepVillagersInView = true;
                world.minVillagersInView = 5;        // 城里到处都该有人
                world.maxExtraPerChunk = 6;
                world.freezeRadius = 22f;            // 人太多，冻得近一点省性能
                break;

            default:
                world.keepVillagersInView = true;
                world.minVillagersInView = 2;
                world.maxExtraPerChunk = 4;
                world.freezeRadius = 26f;
                break;
        }
    }
}
