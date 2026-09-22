using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地洞 / 地道：小虫钻进去就能躲起来，村民看不见它。
/// <see cref="tunnel"/> 为 true 的是「地道」，由 <see cref="VillageWorld"/> 按距离和另一头配对，
/// 配好之后钻进去会直接从另一头钻出来（相互传送）。
/// </summary>
public class Burrow : MonoBehaviour
{
    /// <summary>场上所有地洞，供小虫就近查找。</summary>
    public static readonly List<Burrow> All = new List<Burrow>();

    [Tooltip("是不是「地道」（可以被配对成传送通道）")]
    public bool tunnel;

    [Tooltip("配对的另一头，只有地道配对后才有")]
    public Burrow partner;

    /// <summary>已经配对好的地道。</summary>
    public bool IsTunnel { get { return tunnel && partner != null; } }

    public Vector2 Position { get { return transform.position; } }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void OnDestroy()
    {
        // 另一头被回收（区块销毁）时，把配对解开，剩下一头等下次再配对
        if (partner != null && partner.partner == this) partner.partner = null;
    }

    /// <summary>离某个点最近、且在 maxDistance 之内的地洞。</summary>
    public static Burrow Nearest(Vector2 point, float maxDistance)
    {
        Burrow best = null;
        float bestSqr = maxDistance * maxDistance;
        for (int i = 0; i < All.Count; i++)
        {
            Burrow burrow = All[i];
            if (burrow == null) continue;
            float distance = ((Vector2)burrow.transform.position - point).sqrMagnitude;
            if (distance < bestSqr)
            {
                bestSqr = distance;
                best = burrow;
            }
        }
        return best;
    }

    /// <summary>已配对的地道数量。</summary>
    public static int CountTunnels()
    {
        int count = 0;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].IsTunnel) count++;
        return count;
    }
}
