using System;
using UnityEngine;

/// <summary>噪音的种类。只表示「这是什么动静」（响度由 <see cref="GameEvent"/> 的默认表给出）。</summary>
public enum NoiseKind
{
    Step,     // 走路脚步
    Dash,     // 冲刺
    Eat,      // 啃食
    Pickup,   // 拿起物品
    Drop,     // 放下 / 摔下物品
    Break,    // 东西被弄坏（可破坏物体用，暂未接入）
    Shock,    // 电击（能力系统用，暂未接入）
    Bell      // 钟楼的钟声（全村的动静，传得很远）
}

/// <summary>一次噪音：在哪、多响。<c>loudness</c> 决定能传多远（见 <see cref="Villager"/> 的听觉）。</summary>
public struct NoiseEvent
{
    public Vector2 position;
    public float loudness;
    public NoiseKind kind;
}

/// <summary>吃掉了一个东西（村民也算「东西」）。</summary>
public struct EatenEvent
{
    public Edible food;
    public Vector2 position;
    public bool wasVillager;
}

/// <summary>
/// 全局事件总线：**世界里的「发生了什么」都从这里广播**，谁关心谁订阅。
///
/// 为什么要有它：设计上「小虫搞事 → 村民有反应 → 连锁反应」这条主循环，
/// 靠各个脚本互相 FindObjectOfType / 直接调用是接不起来的（小虫在 A、NPC 在 B、任务是 C）。
/// 现在统一走这里，新增系统只要订阅，不用改触发方。
///
/// <list type="bullet">
/// <item><see cref="Noise"/>：噪音（脚步 / 冲刺 / 啃食 / 搬放物品 / 以后的破坏与电击）——村民靠它「听到动静」；</item>
/// <item><see cref="Eaten"/>：吃掉一个东西（含村民）——以后的混乱值 / 任务靠它计数；</item>
/// <item><see cref="Spotted"/>：某个村民正面看到小虫——以后的警觉值与「被发现的次数」靠它；</item>
/// <item><see cref="StateChanged"/>：村民的状态变了（HUD / 调试 / 以后的连锁条件用）。</item>
/// </list>
///
/// **两条规矩（红线 17）**：
/// <list type="number">
/// <item>订阅方（村民等）**必须在 <c>OnDisable</c> 里退订** —— 区块回收 / 冻结都会走 OnDisable，
///       不退订就会留下指向已销毁对象的委托；</item>
/// <item><see cref="Reset"/> 在每次进入 Play 前把订阅表清空（关掉「域重载」时静态字段会一直留着上一局的订阅者）。</item>
/// </list>
/// 载荷都是 struct、「有没有人订阅」先判空，所以广播本身不产生 GC。
/// 处理器（订阅方）**不要抛异常**：一次广播里前一个订阅者抛异常，后面的订阅者就收不到了，
/// 所以处理器里凡是可能为 null 的引用都要先兜底（村民的处理器就是这么写的）。
/// </summary>
public static class GameEvent
{
    /// <summary>有人制造了噪音。参数是噪音本身（位置 + 响度 + 种类）。</summary>
    public static event Action<NoiseEvent> Noise;
    /// <summary>吃掉了东西。<c>wasVillager</c> 为 true 时说明小虫吃了一只村民。</summary>
    public static event Action<EatenEvent> Eaten;
    /// <summary>某个村民正面看到了小虫（用于「被发现的次数」/ 以后的警觉值）。</summary>
    public static event Action<Villager> Spotted;
    /// <summary>某个村民的状态变了（HUD / 调试 / 以后的连锁条件）。</summary>
    public static event Action<Villager> StateChanged;
    /// <summary>小虫用了一次能力（以后的混乱值 / 任务 / 连锁条件用）。</summary>
    public static event Action<AbilityId, Vector2> AbilityUsed;
    /// <summary>某个可破坏物被打碎了（<see cref="Breakable"/>）。</summary>
    public static event Action<GameObject, Vector2> Broken;
    /// <summary>某处的电通了 / 断了（<see cref="ElectricWire"/> / <see cref="PoweredProp"/>）。</summary>
    public static event Action<Vector2, bool> PowerChanged;
    /// <summary>哪里开始漏水了（<see cref="WaterSource"/>）。</summary>
    public static event Action<Vector2> Leaked;
    /// <summary>警报响了（<see cref="Alarm"/>）。</summary>
    public static event Action<Vector2> AlarmRaised;
    /// <summary>混乱值升到新的一级（<see cref="ChaosMeter"/>）。参数是新等级 0~5。</summary>
    public static event Action<int> ChaosLevelChanged;
    /// <summary>警觉值跨过某条线（<see cref="Alertness"/>）。参数是「现在是紧张还是放松」。</summary>
    public static event Action<bool> AlertnessChanged;
    /// <summary>（任务模块 2026-09-25 已删）留下来给以后的「目标 / 成就」用：完成一件事时广播。</summary>
    public static event Action<string> TaskCompleted;

    // 默认响度：噪音能传多远由「响度 × 听觉基准」乘出来（见 Villager.hearingBase）
    /// <summary>脚步：最轻，只有贴得很近的人听得见。</summary>
    public const float StepLoudness = 0.35f;
    /// <summary>冲刺：比走路明显，但还不算「出事」。</summary>
    public const float DashLoudness = 0.6f;
    /// <summary>啃食：近处的人能听见。</summary>
    public const float EatLoudness = 0.5f;
    /// <summary>拿起物品：很轻。</summary>
    public const float PickupLoudness = 0.5f;
    /// <summary>放下 / 摔下物品：很响（「把箱子砸在地上给村民听」是玩法）。</summary>
    public const float DropLoudness = 1.2f;
    /// <summary>东西被弄坏（可破坏物体用）。</summary>
    public const float BreakLoudness = 2f;
    /// <summary>电击（能力系统用）。</summary>
    public const float ShockLoudness = 2.5f;
    /// <summary>摔一跤（滑倒）：比啃食响、比倒塌轻。</summary>
    public const float SlipLoudness = 1.8f;
    /// <summary>被腐蚀 / 慢慢蚀穿：介于吃与破坏之间。</summary>
    public const float CorrodeLoudness = 1.5f;
    /// <summary>爆炸：全场最响。</summary>
    public const float ExplosionLoudness = 3f;
    /// <summary>钟声：比爆炸轻一点，但一响半个村子都听得见。</summary>
    public const float BellLoudness = 2.6f;

    /// <summary>这个动作默认多响。</summary>
    public static float LoudnessOf(NoiseKind kind)
    {
        switch (kind)
        {
            case NoiseKind.Step: return StepLoudness;
            case NoiseKind.Dash: return DashLoudness;
            case NoiseKind.Eat: return EatLoudness;
            case NoiseKind.Pickup: return PickupLoudness;
            case NoiseKind.Drop: return DropLoudness;
            case NoiseKind.Break: return BreakLoudness;
            case NoiseKind.Bell: return BellLoudness;
            default: return ShockLoudness;
        }
    }

    /// <summary>广播过多少次噪音（调试 / 验证用）。</summary>
    public static int NoiseRaised { get; private set; }
    /// <summary>广播过多少次「吃掉东西」（调试 / 验证用）。</summary>
    public static int EatenRaised { get; private set; }
    /// <summary>用过多少次能力（调试 / 验证用）。</summary>
    public static int AbilityUseCount { get; private set; }
    /// <summary>打碎过多少东西（调试 / 验证用）。</summary>
    public static int BrokenCount { get; private set; }
    /// <summary>响过多少次警报（调试 / 验证用）。</summary>
    public static int AlarmCount { get; private set; }
    /// <summary>最近一次噪音（调试用）。</summary>
    public static NoiseEvent LastNoise { get; private set; }

    /// <summary>
    /// 现在有多少个订阅者在听噪音（调试 / 验证用）。
    /// 用来抓「区块反复回收重建后订阅者越积越多」——正常情况下它就是没被冻结的村民数量。
    /// </summary>
    public static int NoiseSubscriberCount
    {
        get { return Noise != null ? Noise.GetInvocationList().Length : 0; }
    }

    /// <summary>广播一次噪音（响度用 <see cref="LoudnessOf"/> 的默认值）。</summary>
    public static void RaiseNoise(Vector2 position, NoiseKind kind)
    {
        RaiseNoise(position, LoudnessOf(kind), kind);
    }

    /// <summary>广播一次噪音。<paramref name="loudness"/> &lt;= 0 时什么都不做。</summary>
    public static void RaiseNoise(Vector2 position, float loudness, NoiseKind kind)
    {
        if (loudness <= 0f) return;

        NoiseEvent e = new NoiseEvent { position = position, loudness = loudness, kind = kind };
        NoiseRaised++;
        LastNoise = e;
        if (Noise != null) Noise(e);
    }

    /// <summary>广播一次「吃掉东西」。</summary>
    public static void RaiseEaten(Edible food, Vector2 position, bool wasVillager)
    {
        EatenRaised++;
        if (Eaten == null) return;
        Eaten(new EatenEvent { food = food, position = position, wasVillager = wasVillager });
    }

    /// <summary>广播一次「某个村民看到了小虫」。</summary>
    public static void RaiseSpotted(Villager by)
    {
        if (by == null || Spotted == null) return;
        Spotted(by);
    }

    /// <summary>广播一次「某个村民的状态变了」。订阅方要自己判空（对象可能已被区块回收）。</summary>
    public static void RaiseStateChanged(Villager who)
    {
        if (who == null || StateChanged == null) return;
        StateChanged(who);
    }

    /// <summary>广播一次「小虫用了一次能力」。</summary>
    public static void RaiseAbilityUsed(AbilityId id, Vector2 position)
    {
        AbilityUseCount++;
        if (AbilityUsed == null) return;
        AbilityUsed(id, position);
    }

    /// <summary>广播一次「某个可破坏物被打碎了」。</summary>
    public static void RaiseBroken(GameObject what, Vector2 position)
    {
        BrokenCount++;
        if (Broken == null) return;
        Broken(what, position);
    }

    /// <summary>广播一次「这里的电通了 / 断了」。</summary>
    public static void RaisePowerChanged(Vector2 position, bool powered)
    {
        if (PowerChanged == null) return;
        PowerChanged(position, powered);
    }

    /// <summary>广播一次「这里开始漏水了」。</summary>
    public static void RaiseLeaked(Vector2 position)
    {
        if (Leaked == null) return;
        Leaked(position);
    }

    /// <summary>广播一次「警报响了」。</summary>
    public static void RaiseAlarm(Vector2 position)
    {
        AlarmCount++;
        if (AlarmRaised == null) return;
        AlarmRaised(position);
    }

    /// <summary>广播一次「混乱值升到新的一级」。</summary>
    public static void RaiseChaosLevel(int level)
    {
        if (ChaosLevelChanged == null) return;
        ChaosLevelChanged(level);
    }

    /// <summary>广播一次「警觉值跨过某条线」。</summary>
    public static void RaiseAlertnessChanged(bool tense)
    {
        if (AlertnessChanged == null) return;
        AlertnessChanged(tense);
    }

    /// <summary>广播一次「完成了一个任务」。</summary>
    public static void RaiseTaskCompleted(string taskId)
    {
        if (TaskCompleted == null) return;
        TaskCompleted(taskId);
    }

    /// <summary>
    /// 进入 Play 前清空订阅表。**不写这个的话**，编辑器里关掉「域重载（Domain Reload）」时
    /// 上一局残留的订阅者会一直挂在静态事件上（指向已销毁的对象），越玩越慢、还报空引用。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Noise = null;
        Eaten = null;
        Spotted = null;
        StateChanged = null;
        AbilityUsed = null;
        Broken = null;
        PowerChanged = null;
        Leaked = null;
        AlarmRaised = null;
        ChaosLevelChanged = null;
        AlertnessChanged = null;
        TaskCompleted = null;
        NoiseRaised = 0;
        EatenRaised = 0;
        AbilityUseCount = 0;
        BrokenCount = 0;
        AlarmCount = 0;
        LastNoise = default(NoiseEvent);
    }
}
