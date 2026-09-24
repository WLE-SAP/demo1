using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「怎么刷出来」的规则，食物与可交互物品共用。用法（字段都有合理默认值，写你需要的那几个就行）：
/// <code>
/// spawn = SpawnRule.Pool(20f)                                  // 每区块的食物位里有 20 的权重被抽中
/// spawn = new SpawnRule { weight = 5f, hamletOnly = true }      // 只在村庄区块、权重 5
/// spawn = SpawnRule.Fixed(1, 2, 0.5f)                          // 每个区块固定 1~2 个（50% 的区块才有）
/// spawn = SpawnRule.Pool(20f).InNatures(NatureKind.Forest)       // 只长在森林里（蘑菇）
/// spawn = SpawnRule.Pool(10f).InSettlements(SettlementKind.City) // 只刷在城市里（垃圾桶）
/// </code>
/// 注意：这是 class 而不是 struct，所以「不写的字段」= 类里给的默认值，**不要**用 <c>new SpawnRule()</c> 之后再手动补全。
/// 「聚落 × 自然」两套体系见 <see cref="WorldBiome"/>。
/// </summary>
public class SpawnRule
{
    /// <summary>「位」抽签权重：每个区块的食物位 / 物品位按它抽种类；0 = 不进随机池（只按下面的固定数量生成）。</summary>
    public float weight;

    /// <summary>每个区块额外固定生成多少个（min~max，含两端）。</summary>
    public int minPerChunk;
    public int maxPerChunk;

    /// <summary>固定生成前先掷一次的几率：1 = 必出，0.45 = 45% 的区块有。</summary>
    public float chance = 1f;

    /// <summary>只在村庄区块生成（荒野区块不刷）。村庄 = 农村 + 城市。</summary>
    public bool hamletOnly;

    /// <summary>只在野外区块生成（村庄区块不刷）。</summary>
    public bool wildOnly;

    // ---------------- 按「聚落 / 自然体系」门控（2026-09-25 加）----------------
    //
    // 世界现在由两套体系组成（见 WorldBiome）：聚落决定「有没有人、有没有房子」，
    // 自然决定「长什么样」。这里就是内容与这两套体系的挂接点：
    // 蘑菇只在森林里长、垃圾桶只在城市里刷、麦穗只长在草原上。

    /// <summary>只在这些聚落里生成；null 或空 = 所有聚落都可以。</summary>
    public SettlementKind[] settlements;

    /// <summary>只在这些自然体系里生成；null 或空 = 所有自然体系都可以。</summary>
    public NatureKind[] natures;

    /// <summary>限定自然体系（可读写法：<c>SpawnRule.Pool(20f).InNatures(NatureKind.Forest)</c>）。</summary>
    public SpawnRule InNatures(params NatureKind[] kinds)
    {
        natures = kinds;
        return this;
    }

    /// <summary>限定聚落（可读写法：<c>SpawnRule.Pool(10f).InSettlements(SettlementKind.City)</c>）。</summary>
    public SpawnRule InSettlements(params SettlementKind[] kinds)
    {
        settlements = kinds;
        return this;
    }

    /// <summary>找空地时的占地半径（越大越难挤进密集的地方）。</summary>
    public float clearance = 0.3f;

    /// <summary>生成后把这块地标记成已占用（半边长），防止别的东西叠上来。</summary>
    public float footprint = 0.35f;

    /// <summary>与别的东西之间额外留的间隙。</summary>
    public float padding = 0.1f;

    // ---------------- 就近生成（2026-09-25 加）----------------
    //
    // 让「什么长在哪」有生活感：果子 / 嫩叶只长在树旁边，电池聚在发电站附近。
    // 找不到参照物时会**自动退回普通随机落点**（不会因为这一片没树就什么都不刷）。

    /// <summary>只在树旁边生成（<see cref="nearRadius"/> 以内要有树）。</summary>
    public bool nearTrees;

    /// <summary>只在某类设施旁边生成（填 <see cref="VillageMap.AddAnchor"/> 里的种类名，例如 "power"）。</summary>
    public string nearAnchor;

    /// <summary>贴着参照物多近（世界单位）。</summary>
    public float nearRadius = 2.5f;

    /// <summary>离参照物至少多远（0 = 贴着也行）。</summary>
    public float nearMinDistance = 0.5f;

    /// <summary>进随机池：在每个「位」里按 <paramref name="weight"/> 抽中它。</summary>
    public static SpawnRule Pool(float weight, float clearance = 0.3f, float padding = 0.1f, float footprint = 0.35f)
    {
        return new SpawnRule
        {
            weight = weight,
            clearance = clearance,
            padding = padding,
            footprint = footprint
        };
    }

    /// <summary>每区块固定生成 <paramref name="min"/>~<paramref name="max"/> 个（<paramref name="chance"/> 是先掷一次的几率）。</summary>
    public static SpawnRule Fixed(int min, int max, float chance = 1f, float clearance = 0.7f, float padding = 0.5f, float footprint = 0.7f)
    {
        return new SpawnRule
        {
            minPerChunk = min,
            maxPerChunk = max,
            chance = chance,
            clearance = clearance,
            padding = padding,
            footprint = footprint
        };
    }

    /// <summary>这个区块（哪种聚落 + 哪种自然体系）该不该生成它。</summary>
    public bool Includes(SettlementKind settlement, NatureKind nature)
    {
        // hamletOnly / wildOnly 是「村庄 / 野外」的老写法，映射到新的聚落语义上（村庄 = 农村 + 城市）
        if (hamletOnly && settlement == SettlementKind.Wilderness) return false;
        if (wildOnly && settlement != SettlementKind.Wilderness) return false;
        if (settlements != null && settlements.Length > 0 && !Contains(settlements, settlement)) return false;
        if (natures != null && natures.Length > 0 && !Contains(natures, nature)) return false;
        return true;
    }

    static bool Contains(SettlementKind[] list, SettlementKind value)
    {
        for (int i = 0; i < list.Length; i++) if (list[i] == value) return true;
        return false;
    }

    static bool Contains(NatureKind[] list, NatureKind value)
    {
        for (int i = 0; i < list.Length; i++) if (list[i] == value) return true;
        return false;
    }
}

/// <summary>
/// 「能在区块里刷出来的东西」—— 食物与可交互物品都实现它，
/// <see cref="VillageGenerator"/> 因此只按这一个接口生成，新增内容不用动生成器。
/// </summary>
public interface IContentDefinition
{
    /// <summary>唯一 id（同时是刷出来的物体名，方便在 Hierarchy / 日志里认）。</summary>
    string Id { get; }

    /// <summary>生成规则。</summary>
    SpawnRule Spawn { get; }

    /// <summary>
    /// 造一个物体：<paramref name="parent"/> 决定它属于哪个区块（区块回收时跟着销毁），
    /// <paramref name="position"/> 是世界坐标，<paramref name="yOrder"/> 是俯视排序基准（<see cref="SpawnKit.YOrder"/>）。
    /// </summary>
    GameObject Create(Transform parent, Vector2 position, int yOrder);
}

/// <summary>新增内容（食物 / 可交互物品）用的公共小工具。</summary>
public static class SpawnKit
{
    /// <summary>俯视 2D 深度排序：越靠下（Y 越小）画得越靠前。世界无限大，所以用绝对 Y。</summary>
    public static int YOrder(float y)
    {
        return 1000 - Mathf.RoundToInt(y * 10f);
    }

    /// <summary>
    /// 从目录里按权重抽一个（跳过 weight = 0 的、以及当前区块的聚落 / 自然体系不允许的）；
    /// 一个都抽不到（没注册 / 权重全 0 / 都不允许）时返回 null。
    /// </summary>
    public static T PickWeighted<T>(List<T> definitions, System.Random rng, SettlementKind settlement, NatureKind nature)
        where T : class, IContentDefinition
    {
        float total = 0f;
        for (int i = 0; i < definitions.Count; i++)
        {
            T def = definitions[i];
            if (!Eligible(def, settlement, nature)) continue;
            total += def.Spawn.weight;
        }
        if (total <= 0f) return null;

        float roll = (float)rng.NextDouble() * total;
        T last = null;
        for (int i = 0; i < definitions.Count; i++)
        {
            T def = definitions[i];
            if (!Eligible(def, settlement, nature)) continue;
            last = def;
            roll -= def.Spawn.weight;
            if (roll <= 0f) return def;
        }
        return last;
    }

    static bool Eligible<T>(T def, SettlementKind settlement, NatureKind nature) where T : class, IContentDefinition
    {
        return def != null && def.Spawn != null && def.Spawn.weight > 0f
            && def.Spawn.Includes(settlement, nature);
    }
}
