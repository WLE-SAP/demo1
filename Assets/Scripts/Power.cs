using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通电才会工作的东西（抽水泵、机器、灯…）。
///
/// 「谁给谁供电」**不做 id / 连线**：<see cref="ElectricWire"/> 被弄断时，
/// 把它 <see cref="linkRadius"/> 内的所有 <see cref="PoweredProp"/> 一起断电
/// （<see cref="All"/> 是静态表，所以不用区块内查找，也不会因为区块回收而断链）。
/// 这样做的好处是随机生成的村庄不需要预先布线，玩家自己就能发现「电线在哪、管着哪台机器」。
/// </summary>
public class PoweredProp : MonoBehaviour
{
    /// <summary>场上所有会通电的东西（电线断电时遍历它）。</summary>
    public static readonly List<PoweredProp> All = new List<PoweredProp>();

    [Tooltip("有没有电")]
    public bool powered = true;

    [Tooltip("断电时的外观颜色（留空 alpha=0 就不改颜色）")]
    public Color offColor = new Color(0.55f, 0.55f, 0.58f, 1f);

    [Tooltip("停机时轻微缩一下，看起来像「不动了」")]
    public bool shrinkWhenOff = true;

    public bool HasPower { get { return powered; } }

    SpriteRenderer[] renderers;
    Color[] baseColors;
    Vector3 baseScale;
    bool captured;

    /// <summary>场上通电的还有几个（验证用）。</summary>
    public static int CountPowered()
    {
        int n = 0;
        for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].powered) n++;
        return n;
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        // Start 里记原样：Highlighter / ArtOverride 之类可能在 Awake 之后才改渲染器
        Capture();
        ApplyVisual();
    }

    void Capture()
    {
        if (captured) return;
        captured = true;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        baseScale = transform.localScale;
    }

    /// <summary>
    /// 通电 / 断电。<paramref name="announce"/> = false 时不广播 <see cref="GameEvent.PowerChanged"/>
    /// —— 电线剪断时会一次性广播「这一片断电了」，它身上的机器就不必再各自广播一遍（免得一次停电算两次）。
    /// 断电时顺带弄出停机噪音（村民会来看出了什么事）。
    /// </summary>
    public void SetPower(bool on, bool announce = true)
    {
        if (powered == on) return;
        powered = on;
        ApplyVisual();

        if (announce) GameEvent.RaisePowerChanged(transform.position, on);

        if (!on)
        {
            // 停机是有动静的（比啃食响一点），所以「拉了电闸」会把附近的人引过来
            GameEvent.RaiseNoise(transform.position, 0.9f, NoiseKind.Break);
            Debug.Log("[Power] " + name + " 断电停机。");
        }
        else
        {
            Debug.Log("[Power] " + name + " 恢复供电。");
        }
    }

    void ApplyVisual()
    {
        Capture();
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].color = powered ? baseColors[i] : offColor;
        }

        if (shrinkWhenOff)
            transform.localScale = powered ? baseScale : baseScale * 0.94f;
    }
}

/// <summary>
/// 电线：**被吃掉 / 被腐蚀 / 被打碎就剪断这条线路** —— 它会把 <see cref="linkRadius"/> 内的
/// <see cref="PoweredProp"/> 全部断电。这是设计文档 §9 那条连锁链的第一环
/// （“吃掉电线 → 电脑断电”在村庄里的等价物）。
///
/// 注意：**只在「被玩家弄坏」时断电**，区块回收 / 场景卸载不会（见 <see cref="Edible.onConsumed"/>）——
/// 否则玩家走远一趟回来，全村的电都断了。
/// </summary>
public class ElectricWire : MonoBehaviour
{
    [Tooltip("这条线管着多远的机器")]
    public float linkRadius = 6f;

    [Tooltip("剪断后自己的样子（变暗）")]
    public Color cutColor = new Color(0.35f, 0.33f, 0.3f, 1f);

    public bool IsCut { get; private set; }

    /// <summary>剪断：把自己和射程内的机器一起断电。返回断了几台。</summary>
    public int CutPower()
    {
        if (IsCut) return 0;
        IsCut = true;

        Vector2 self = transform.position;
        float sqr = linkRadius * linkRadius;
        int cut = 0;

        for (int i = 0; i < PoweredProp.All.Count; i++)
        {
            PoweredProp prop = PoweredProp.All[i];
            if (prop == null || prop == GetComponent<PoweredProp>()) continue;
            if (((Vector2)prop.transform.position - self).sqrMagnitude > sqr) continue;
            if (!prop.HasPower) continue;

            prop.SetPower(false, false);      // 静默：这条线路统一广播一次就够了
            cut++;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = cutColor;

        // **不管射程内有没有机器，都要广播「这一片断电了」**：
        // 否则玩家啃断一根孤零零的电线会「什么都没发生」，任务和混乱值也都不会动 —— 反馈断了。
        GameEvent.RaisePowerChanged(self, false);
        GameEvent.RaiseNoise(self, 1.4f, NoiseKind.Break);
        Debug.Log("[Power] 电线被剪断，连带断电 " + cut + " 台机器。");
        return cut;
    }
}
