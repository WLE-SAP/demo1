using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 设施锚点表：由 <see cref="VillageGenerator"/> 在生成区块时填好，
/// 村民按职业来这里活动（农夫去农田、摊贩守摊位、守卫沿街巡逻……）。
/// 世界是无限流式的，所以区块被回收时用 <see cref="PruneFarFrom"/> 把远处的锚点剪掉。
/// </summary>
public class VillageMap : MonoBehaviour
{
    public readonly List<Vector2> farms = new List<Vector2>();
    public readonly List<Vector2> gardens = new List<Vector2>();
    public readonly List<Vector2> stalls = new List<Vector2>();
    public readonly List<Vector2> pens = new List<Vector2>();
    public readonly List<Vector2> benches = new List<Vector2>();
    public readonly List<Vector2> lamps = new List<Vector2>();
    public readonly List<Vector2> wells = new List<Vector2>();
    public readonly List<Vector2> trees = new List<Vector2>();
    public readonly List<Vector2> houses = new List<Vector2>();
    public readonly List<Vector2> roads = new List<Vector2>();

    /// <summary>
    /// 按「设施种类」记的锚点（2026-09-25 加）：物品的「就近生成」靠它
    /// —— 电池聚在发电站（`"power"`）旁边就是这么实现的。种类名用 <see cref="AddAnchor"/> 写入。
    /// </summary>
    public readonly Dictionary<string, List<Vector2>> anchors = new Dictionary<string, List<Vector2>>();

    /// <summary>面包房门口（可能为 null，表示附近没有）。</summary>
    public bool hasBakery;
    public Vector2 bakery;
    /// <summary>铁匠铺门口。</summary>
    public bool hasSmithy;
    public Vector2 smithy;
    /// <summary>公告板前。</summary>
    public bool hasBoard;
    public Vector2 board;

    public void Clear()
    {
        farms.Clear(); gardens.Clear(); stalls.Clear(); pens.Clear();
        benches.Clear(); lamps.Clear(); wells.Clear(); trees.Clear();
        houses.Clear(); roads.Clear();
        anchors.Clear();
        hasBakery = hasSmithy = hasBoard = false;
    }

    /// <summary>登记一个「某类设施在哪」的锚点（物品的就近生成会来查）。</summary>
    public void AddAnchor(string kind, Vector2 point)
    {
        if (string.IsNullOrEmpty(kind)) return;

        List<Vector2> list;
        if (!anchors.TryGetValue(kind, out list))
        {
            list = new List<Vector2>();
            anchors[kind] = list;
        }
        list.Add(point);
    }

    /// <summary>取某类设施的锚点表；没有就返回 null。</summary>
    public List<Vector2> AnchorList(string kind)
    {
        if (string.IsNullOrEmpty(kind)) return null;
        List<Vector2> list;
        return anchors.TryGetValue(kind, out list) ? list : null;
    }

    /// <summary>剪掉离 center 超过 radius 的锚点（区块回收时调用）。</summary>
    public void PruneFarFrom(Vector2 center, float radius)
    {
        float sqr = radius * radius;
        Prune(farms, center, sqr);
        Prune(gardens, center, sqr);
        Prune(stalls, center, sqr);
        Prune(pens, center, sqr);
        Prune(benches, center, sqr);
        Prune(lamps, center, sqr);
        Prune(wells, center, sqr);
        Prune(trees, center, sqr);
        Prune(houses, center, sqr);
        Prune(roads, center, sqr);

        foreach (KeyValuePair<string, List<Vector2>> pair in anchors)
            Prune(pair.Value, center, sqr);
    }

    static void Prune(List<Vector2> list, Vector2 center, float sqrRadius)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if ((list[i] - center).sqrMagnitude > sqrRadius) list.RemoveAt(i);
    }

    /// <summary>列表里随机取一个；列表为空返回 fallback。</summary>
    public static Vector2 Pick(List<Vector2> list, Vector2 fallback)
    {
        if (list == null || list.Count == 0) return fallback;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>列表里离 point 最近的一个；列表为空返回 fallback。</summary>
    public static Vector2 Nearest(List<Vector2> list, Vector2 point, Vector2 fallback)
    {
        if (list == null || list.Count == 0) return fallback;
        Vector2 best = fallback;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            float distance = (list[i] - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = list[i];
            }
        }
        return best;
    }

    /// <summary>在 origin 附近 maxDistance 内的锚点里随机取一个；没有就返回 fallback。</summary>
    public static Vector2 PickNear(List<Vector2> list, Vector2 origin, Vector2 fallback, float maxDistance)
    {
        if (list == null || list.Count == 0) return fallback;

        float sqr = maxDistance * maxDistance;
        int count = 0;
        Vector2 candidate = fallback;
        for (int i = 0; i < list.Count; i++)
        {
            if ((list[i] - origin).sqrMagnitude > sqr) continue;
            count++;
            // 蓄水池抽样：一次遍历里均匀随机挑一个，不用额外分配
            if (Random.Range(0, count) == 0) candidate = list[i];
        }
        return count > 0 ? candidate : fallback;
    }
}
