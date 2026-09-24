using UnityEngine;

/// <summary>
/// 本局的「混乱值」：0~100，分成 **6 个等级**（0~5），设计文档 §8。
///
/// <list type="bullet">
/// <item>**怎么涨**：全部靠订阅 <see cref="GameEvent"/>（吃、打碎、断电、漏水、滑倒、用能力、警报、吃村民），
///       所以任何新玩法只要发事件就自动计入，**不需要改这里**；</item>
/// <item>**怎么降**：随时间缓慢衰减，**等级越高降得越慢** —— 这就是设计文档那句
///       「破坏越多 → 风险越高 → 必须更聪明地破坏」；</item>
/// <item>**升级时会发生什么**（§8.2）：一级一级变夸张，全部复用已有系统（灯闪 / 噪音 / 村民视野听觉 /
///       补人 / 拉警报），不引入新表现层。</item>
/// </list>
/// </summary>
public class ChaosMeter : MonoBehaviour
{
    [Header("增量（每发生一次加多少）")]
    public float eatGain = 1f;
    public float breakGain = 6f;
    public float powerLostGain = 5f;
    public float leakGain = 3f;
    public float slipGain = 7f;
    public float abilityGain = 2f;
    public float alarmGain = 15f;
    public float villagerEatenGain = 12f;

    [Header("衰减（每秒掉多少；等级越高掉得越慢）")]
    public float decayAtLowLevel = 0.6f;
    public float decayAtHighLevel = 0.2f;

    [Header("等级阈值（0~100，共 6 级）")]
    public float[] thresholds = { 0f, 12f, 28f, 48f, 70f, 88f };

    [Header("阶段事件")]
    [Tooltip("L2「围观」的噪音半径")]
    public float curiousNoiseRadius = 6f;
    [Tooltip("L4：可见范围内至少要有几个村民（每升一级 +1）")]
    public int extraVillagersAtLevel4 = 1;
    [Tooltip("L5：全场灯闪多久")]
    public float finalFlickerSeconds = 6f;

    /// <summary>场上唯一的混乱表（HUD / 存档 / 阶段事件都读它）。</summary>
    public static ChaosMeter Instance { get; private set; }

    [Tooltip("当前混乱值（0~100）")]
    public float chaos;

    public int Level { get; private set; }
    /// <summary>本局出现过的最高等级（结算面板展示）。</summary>
    public int MaxLevel { get; private set; }
    /// <summary>本局出现过的最高混乱值。</summary>
    public float MaxChaos { get; private set; }

    VillageWorld world;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        if (Object.FindObjectOfType<ChaosMeter>() != null) return;
        GameObject host = GameObject.Find("GameDirector");
        if (host == null)
        {
            BugController bug = Object.FindObjectOfType<BugController>();
            if (bug == null) return;
            host = bug.gameObject;
        }
        host.AddComponent<ChaosMeter>();
    }

    void Awake()
    {
        Instance = this;
        world = FindObjectOfType<VillageWorld>();
        Level = ComputeLevel(chaos);
        MaxLevel = Level;
        MaxChaos = chaos;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvent.Eaten += OnEaten;
        GameEvent.Broken += OnBroken;
        GameEvent.PowerChanged += OnPowerChanged;
        GameEvent.Leaked += OnLeaked;
        GameEvent.AlarmRaised += OnAlarm;
        GameEvent.AbilityUsed += OnAbility;
        GameEvent.StateChanged += OnVillagerStateChanged;
    }

    void OnDisable()
    {
        GameEvent.Eaten -= OnEaten;
        GameEvent.Broken -= OnBroken;
        GameEvent.PowerChanged -= OnPowerChanged;
        GameEvent.Leaked -= OnLeaked;
        GameEvent.AlarmRaised -= OnAlarm;
        GameEvent.AbilityUsed -= OnAbility;
        GameEvent.StateChanged -= OnVillagerStateChanged;
    }

    void Update()
    {
        // 衰减：等级越高越慢（低等级 0.6/s → 高等级 0.2/s）
        float k = thresholds != null && thresholds.Length > 5 ? Mathf.Clamp01(Level / 5f) : 0f;
        float decay = Mathf.Lerp(decayAtLowLevel, decayAtHighLevel, k);
        Add(-decay * Time.deltaTime);
    }

    // ---------------- 事件来源 ----------------

    void OnEaten(EatenEvent e)
    {
        Add(e.wasVillager ? villagerEatenGain : eatGain);
    }

    void OnBroken(GameObject what, Vector2 at) { Add(breakGain); }
    void OnLeaked(Vector2 at) { Add(leakGain); }
    void OnAlarm(Vector2 at) { Add(alarmGain); }
    void OnAbility(AbilityId id, Vector2 at) { Add(abilityGain); }
    void OnPowerChanged(Vector2 at, bool powered) { if (!powered) Add(powerLostGain); }

    /// <summary>村民摔一跤也算「混乱」（他是被你弄出来的水洼滑倒的）。</summary>
    void OnVillagerStateChanged(Villager who)
    {
        if (who == null || who.State != VillagerState.Slip) return;
        Add(slipGain);
    }

    // ---------------- 数值与等级 ----------------

    /// <summary>加 / 减混乱值，并处理跨等级时的阶段事件。</summary>
    public void Add(float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;

        chaos = Mathf.Clamp(chaos + amount, 0f, 100f);
        if (chaos > MaxChaos) MaxChaos = chaos;

        int level = ComputeLevel(chaos);
        if (level == Level) return;

        bool rising = level > Level;
        Level = level;
        if (level > MaxLevel) MaxLevel = level;
        if (rising) OnLevelUp(level);          // 只在往上跨的时候放阶段事件
        GameEvent.RaiseChaosLevel(level);
    }

    /// <summary>读档用：直接设定混乱值（不放阶段事件）。</summary>
    public void Restore(float value)
    {
        chaos = Mathf.Clamp(value, 0f, 100f);
        Level = ComputeLevel(chaos);
        if (Level > MaxLevel) MaxLevel = Level;
        if (chaos > MaxChaos) MaxChaos = chaos;
    }

    int ComputeLevel(float value)
    {
        int level = 0;
        for (int i = 0; i < thresholds.Length; i++)
            if (value >= thresholds[i]) level = i;
        return Mathf.Clamp(level, 0, 5);
    }

    /// <summary>等级 → 中文（HUD / 结算面板）。</summary>
    public static string LevelName(int level)
    {
        switch (level)
        {
            case 0: return "正常";
            case 1: return "小异常";
            case 2: return "有点乱";
            case 3: return "全村警觉";
            case 4: return "场景失控";
            default: return "彻底乱了";
        }
    }

    // ---------------- 阶段事件（§8.2）----------------

    void OnLevelUp(int level)
    {
        Vector2 self = transform.position;
        BugController bug = BugController.Instance;
        if (bug != null) self = bug.transform.position;

        switch (level)
        {
            case 1:
                // 小异常：附近的灯闪一下就算「有反应」了
                FlickerLamps(self, 14f, 1.2f);
                Debug.Log("[Chaos] 混乱 1 级（小异常）：附近有灯闪了一下。");
                break;

            case 2:
                // 有点乱：放一次「围观噪音」，附近的村民会转头 / 走过来看
                GameEvent.RaiseNoise(self, 0.9f, NoiseKind.Break);
                Debug.Log("[Chaos] 混乱 2 级（有点乱）：附近的人开始往这边看。");
                break;

            case 3:
                // 全村警觉：村民看得更远、听得更远（数值在 Villager 里读这里）
                Debug.Log("[Chaos] 混乱 3 级（全村警觉）：村民视野与听觉都提高了。");
                break;

            case 4:
                // 场景失控：人越聚越多（每个区块要几个人由聚落决定，这里只加一个「临时加成」）
                if (world == null) world = FindObjectOfType<VillageWorld>();
                if (world != null)
                {
                    world.extraMinVillagers += extraVillagersAtLevel4;
                    Debug.Log("[Chaos] 混乱 4 级（场景失控）：来看热闹的人变多了（下限 +"
                        + world.extraMinVillagers + "）。");
                }
                break;

            default:
                // 彻底乱了：全村警报 + 灯乱闪
                Alarm[] alarms = FindObjectsOfType<Alarm>();
                for (int i = 0; i < alarms.Length; i++)
                    if (alarms[i] != null) alarms[i].Raise();
                FlickerLamps(self, 60f, finalFlickerSeconds);
                Debug.Log("[Chaos] 混乱 5 级（彻底乱了）：全村警报响起，灯闪个不停。");
                break;
        }
    }

    static void FlickerLamps(Vector2 center, float radius, float seconds)
    {
        float sqr = radius * radius;
        NightGlow[] glows = FindObjectsOfType<NightGlow>();
        for (int i = 0; i < glows.Length; i++)
        {
            if (glows[i] == null) continue;
            if (((Vector2)glows[i].transform.position - center).sqrMagnitude > sqr) continue;
            glows[i].Flicker(seconds);
        }
    }

    /// <summary>
    /// 混乱值给村民的加成（等级越高，村里越「紧张」）：
    /// 3 级起视野 +15%、听觉 +25%。村民读这里，不需要各自订阅事件。
    /// </summary>
    public float VillagerViewBonus
    {
        get { return Level >= 3 ? 1.15f : 1f; }
    }

    public float VillagerHearingBonus
    {
        get { return Level >= 3 ? 1.25f : 1f; }
    }
}
