using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「怎么刷出来」的规则，食物与可交互物品共用。用法（字段都有合理默认值，写你需要的那几个就行）：
/// <code>
/// spawn = SpawnRule.Pool(20f)                                  // 每区块的食物位里有 20 的权重被抽中
/// spawn = new SpawnRule { weight = 5f, hamletOnly = true }      // 只在村庄区块、权重 5
/// spawn = SpawnRule.Fixed(1, 2, 0.5f)                          // 每个区块固定 1~2 个（50% 的区块才有）
/// </code>
/// 注意：这是 class 而不是 struct，所以「不写的字段」= 类里给的默认值，**不要**用 <c>new SpawnRule()</c> 之后再手动补全。
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

    /// <summary>只在村庄区块生成（野外区块不刷）。</summary>
    public bool hamletOnly;

    /// <summary>只在野外区块生成（村庄区块不刷）。</summary>
    public bool wildOnly;

    /// <summary>找空地时的占地半径（越大越难挤进密集的地方）。</summary>
    public float clearance = 0.3f;

    /// <summary>生成后把这块地标记成已占用（半边长），防止别的东西叠上来。</summary>
    public float footprint = 0.35f;

    /// <summary>与别的东西之间额外留的间隙。</summary>
    public float padding = 0.1f;

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

    /// <summary>这个区块（村庄 / 野外）该不该生成它。</summary>
    public bool Includes(bool hamlet)
    {
        return (hamlet || !hamletOnly) && (!hamlet || !wildOnly);
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
    /// 从目录里按权重抽一个（跳过 weight = 0 的、以及当前区块不允许的）；
    /// 一个都抽不到（没注册 / 权重全 0 / 都不允许）时返回 null。
    /// </summary>
    public static T PickWeighted<T>(List<T> definitions, System.Random rng, bool hamlet) where T : class, IContentDefinition
    {
        float total = 0f;
        for (int i = 0; i < definitions.Count; i++)
        {
            T def = definitions[i];
            if (!Eligible(def, hamlet)) continue;
            total += def.Spawn.weight;
        }
        if (total <= 0f) return null;

        float roll = (float)rng.NextDouble() * total;
        T last = null;
        for (int i = 0; i < definitions.Count; i++)
        {
            T def = definitions[i];
            if (!Eligible(def, hamlet)) continue;
            last = def;
            roll -= def.Spawn.weight;
            if (roll <= 0f) return def;
        }
        return last;
    }

    static bool Eligible<T>(T def, bool hamlet) where T : class, IContentDefinition
    {
        return def != null && def.Spawn != null && def.Spawn.weight > 0f && def.Spawn.Includes(hamlet);
    }
}
