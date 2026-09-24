using UnityEngine;

/// <summary>
/// 本局统计（设计文档 §17「关卡评分」的数据来源）：
/// 破坏数、吞噬数、被发现次数、滑倒次数、用能力次数、最大混乱 / 最大警觉、**最长连锁**、用时。
///
/// **全部靠订阅 <see cref="GameEvent"/>**，任何系统都不需要知道自己被统计了
/// （这也是步骤① 建事件总线的最大回报：加统计不用改任何触发方）。
///
/// 「连锁」的定义：两次事件之间间隔不超过 <see cref="chainWindow"/> 秒就算同一条链，
/// 取本局最长的那条 —— 也就是文档里那句「本局最长连锁：14 次」。
/// </summary>
public class RunStats : MonoBehaviour
{
    /// <summary>场上唯一的统计器。</summary>
    public static RunStats Instance { get; private set; }

    [Tooltip("两次事件间隔不超过这么多秒，就算同一条连锁")]
    public float chainWindow = 3f;

    /// <summary>打碎过多少东西。</summary>
    public int Broken { get; private set; }
    /// <summary>吃掉过多少东西（含村民）。</summary>
    public int Eaten { get; private set; }
    /// <summary>被村民看到过多少次。</summary>
    public int Spotted { get; private set; }
    /// <summary>村民摔倒过多少次。</summary>
    public int Slips { get; private set; }
    /// <summary>用过多少次能力。</summary>
    public int AbilityUses { get; private set; }
    /// <summary>响过多少次警报。</summary>
    public int Alarms { get; private set; }
    /// <summary>本局最长连锁（连续发生的事件数）。</summary>
    public int LongestChain { get; private set; }
    /// <summary>当前这条连锁有多长。</summary>
    public int CurrentChain { get; private set; }

    float lastEventTime = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        if (Object.FindObjectOfType<RunStats>() != null) return;
        GameObject host = GameObject.Find("GameDirector");
        if (host == null)
        {
            BugController bug = Object.FindObjectOfType<BugController>();
            if (bug == null) return;
            host = bug.gameObject;
        }
        host.AddComponent<RunStats>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvent.Broken += OnBroken;
        GameEvent.Eaten += OnEaten;
        GameEvent.Spotted += OnSpotted;
        GameEvent.AlarmRaised += OnAlarm;
        GameEvent.AbilityUsed += OnAbility;
        GameEvent.StateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        GameEvent.Broken -= OnBroken;
        GameEvent.Eaten -= OnEaten;
        GameEvent.Spotted -= OnSpotted;
        GameEvent.AlarmRaised -= OnAlarm;
        GameEvent.AbilityUsed -= OnAbility;
        GameEvent.StateChanged -= OnStateChanged;
    }

    void OnBroken(GameObject what, Vector2 at) { Broken++; MarkEvent(); }
    void OnEaten(EatenEvent e) { Eaten++; MarkEvent(); }
    void OnSpotted(Villager by) { Spotted++; MarkEvent(); }
    void OnAlarm(Vector2 at) { Alarms++; MarkEvent(); }
    void OnAbility(AbilityId id, Vector2 at) { AbilityUses++; MarkEvent(); }

    void OnStateChanged(Villager who)
    {
        if (who == null || who.State != VillagerState.Slip) return;
        Slips++;
        MarkEvent();
    }

    /// <summary>记一次「有事发生」，顺便维护连锁长度。</summary>
    void MarkEvent()
    {
        float now = Time.time;
        CurrentChain = (now - lastEventTime) <= chainWindow ? CurrentChain + 1 : 1;
        lastEventTime = now;
        if (CurrentChain > LongestChain) LongestChain = CurrentChain;
    }

    /// <summary>读档用：把这些数恢复回去。</summary>
    public void Restore(int broken, int eaten, int spotted, int slips, int abilityUses, int longestChain)
    {
        Broken = Mathf.Max(0, broken);
        Eaten = Mathf.Max(0, eaten);
        Spotted = Mathf.Max(0, spotted);
        Slips = Mathf.Max(0, slips);
        AbilityUses = Mathf.Max(0, abilityUses);
        LongestChain = Mathf.Max(0, longestChain);
        CurrentChain = 0;
        lastEventTime = -999f;
    }

    /// <summary>
    /// 「破坏程度」百分比：文档 §17 的示例指标。
    /// 无尽村庄没有「场景总物件数」的概念，所以用绝对量换算（打碎 12 个 = 100%），封顶 100。
    /// </summary>
    public int DestructionPercent
    {
        get { return Mathf.Clamp(Broken * 8, 0, 100); }
    }
}
