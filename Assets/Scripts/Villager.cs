using System.Collections.Generic;
using UnityEngine;

/// <summary>村民状态机的状态。</summary>
public enum VillagerState
{
    Idle,        // 原地待着（休息 / 发呆 / 夜里在家）
    Commute,     // 正在走向目标点
    Work,        // 在工作点干活（或守卫站岗、长者晒太阳）
    Socialize,   // 在社交点闲聊（会和身边的邻居面对面）
    Play,        // 孩子玩耍（在广场跑来跑去）
    Chase,       // 看到小虫，追上去
    Flee,        // 看到小虫，躲开
    Alert,       // 听到动静：站住、转身朝向声源（起疑，还没决定要不要去看）
    Investigate, // 到声源附近了，在原地张望（看有没有异常）
    Search,      // 追丢了：在最后看到小虫的地方附近一处一处翻找
    Recover,     // 异常处理完了：回过神，准备回去干活
    Stunned,     // 被电麻了：动不了、也看不见（电击能力）
    Slip         // 踩到水洼滑倒了：摔在地上起不来（漏水 → 地面变滑）
}

/// <summary>
/// 村民 NPC：有自己的职业、名字、家和工作点，按 <see cref="VillageClock"/> 的作息在
/// 「干活 → 中午去广场闲聊 → 下午再干活 → 傍晚饭后闲逛 → 夜里回家」之间循环，
/// 大家一起把村庄的生活演出来。
///
/// 还有「视野」：以朝向前方的一个扇形（村民只会左右翻转，所以视野也是左右两边的扇形），
/// 看到小虫后按职业做不同反应——守卫/樵夫/孩子会追，长者/摊贩/面包师/牧羊人会躲，
/// 农夫和铁匠不理会。反应时会降低移速（<see cref="reactSpeed"/>）。
///
/// 但**亲眼看到小虫吃掉一个村民**之后就不一样了（见 <see cref="ReportEaten"/>）：
/// 目击者从此见小虫就躲（<see cref="fearsBug"/>），不管自己是哪个职业，
/// 而且更警觉（看得更远）、跑得更远更久。
///
/// 还有「听觉」（见 <see cref="OnNoise"/>）：小虫走路、冲刺、啃东西、把箱子砸在地上都会发出噪音，
/// 噪音**不受朝向限制**（声音是全向的，跟视野扇形不一样），能传多远 = <see cref="hearingBase"/> × 响度 ×
/// <see cref="VillagerJobs.Curiosity"/>。听到动静后走这一条链：
/// <c>Alert（站住转身起疑）→ Investigate（走过去张望）→ 没发现 → Recover（回过神）</c>；
/// 追人追丢了则 <c>Chase → Search（在最后看见的地方附近翻找）→ Recover</c>。
/// 好奇心低（农夫 / 铁匠）与 <see cref="fearsBug">怕了小虫</see>的人只会原地起疑，不会凑过去看。
///
/// 朝向：整体美术始终「头朝上」，不旋转刚体，只用 Visual 的 X 缩放做左右翻转。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Villager : MonoBehaviour
{
    [Header("身份")]
    public VillagerJob job = VillagerJob.Farmer;
    public string displayName = "村民";

    [Header("据点")]
    public VillageMap map;
    [Tooltip("自己的家（夜里回去待着）")]
    public Vector2 home;
    [Tooltip("主要工作点（摊位 / 铺子 / 农田…）")]
    public Vector2 workplace;
    [Tooltip("平时聚集闲逛的地方（自己那片村庄的中心）")]
    public Vector2 hangout;

    [Header("视野与反应")]
    [Tooltip("视野半径（小虫在这个距离内、且在面前的扇形里就会被看到）")]
    public float viewRadius = 5.5f;
    [Tooltip("视野扇形的半角（度），以朝向为中轴")]
    public float viewHalfAngle = 55f;
    [Tooltip("贴脸距离：这么近不管朝哪都能发现小虫")]
    public float awareRadius = 1.4f;
    [Tooltip("发现小虫后的反应持续时间")]
    public float reactMin = 3f;
    public float reactMax = 6f;
    [Tooltip("反应时（追 / 躲）的移动速度倍率，降低移速")]
    [Range(0.2f, 1.5f)] public float reactSpeed = 0.72f;
    [Tooltip("追逐时保持的距离，不会贴到脸上")]
    public float chaseDistance = 1.2f;
    [Tooltip("躲避时往反方向跑多远")]
    public float fleeDistance = 7f;
    [Tooltip("选中时在 Scene 视图里画出视野扇形（仅编辑器）")]
    public bool drawViewGizmo = true;

    [Header("亲眼见到小虫吃人之后")]
    [Tooltip("目睹小虫吃掉一个村民之后，就一直躲着小虫（不再按职业反应，看到就跑）")]
    public bool fearsBug;
    [Tooltip("离吃人现场多近算「亲眼看到」")]
    public float witnessRadius = 11f;
    [Tooltip("离受害者这么近，就算没正对着也算看到（来不及躲）")]
    public float witnessCloseRadius = 4.5f;
    [Tooltip("怕了小虫之后更警觉：视野半径的倍率")]
    public float afraidViewBonus = 1.25f;
    [Tooltip("怕了小虫之后躲得更远更久：距离与持续时间的倍率")]
    public float afraidFleeBonus = 1.35f;

    [Header("听觉（噪音）")]
    [Tooltip("听觉基准半径：能听到的最大距离 = 基准 × 噪音响度 × 职业好奇倍率（见 VillagerJobs.Curiosity）")]
    public float hearingBase = 6f;
    [Tooltip("同一个人两次「听到动静」之间的最短间隔；否则连续脚步会让它一直起疑")]
    public float hearingCooldown = 1f;
    [Tooltip("起疑（站住、转身朝向声源）持续多久，之后才决定要不要去看")]
    public float alertMin = 0.6f;
    public float alertMax = 1.2f;
    [Tooltip("走到声源附近后，在原地张望多久")]
    public float lookMin = 1.2f;
    public float lookMax = 2.5f;
    [Tooltip("好奇倍率低于这个数的职业只会原地起疑，不会离开岗位去看（农夫 / 铁匠 / 长者）")]
    public float leavePostThreshold = 1f;
    [Tooltip("异常处理完之后「回过神」的停顿，然后再回到日常作息")]
    public float recoverDuration = 0.5f;

    [Header("追丢之后")]
    [Tooltip("追丢后在最后看见小虫的地方附近翻找几个点")]
    public int searchPoints = 4;
    [Tooltip("每个翻找点离最后看见的位置多远")]
    public float searchRadius = 3.5f;

    [Header("摔跤（踩到水洼）")]
    [Tooltip("摔一跤要在地上躺多久")]
    public float slipSeconds = 1.6f;
    [Tooltip("摔完之后多久不会再滑（刚爬起来不会立刻又摔）")]
    public float slipCooldown = 3f;
    [Tooltip("摔跤时把身边多远的可搬物品撞翻")]
    public float slipScatterRadius = 1.6f;
    [Tooltip("撞翻的力度")]
    public float slipScatterImpulse = 4f;

    [Header("远距离冻结")]
    [Tooltip("被冻结时连精灵渲染一起关掉，进一步省资源")]
    public bool freezeVisuals = true;

    [Header("头顶表情（emoji_* 的图）")]
    [Tooltip("表情气泡的大小（世界单位，按图的宽高比缩放）；0 = 不显示")]
    public float emojiSize = 0.46f;
    [Tooltip("表情挂在头顶多高（原来那个程序化的「!」在 0.62）")]
    public float emojiHeight = 0.9f;
    [Tooltip("「刚进入某个状态」时冒的表情显示多久（秒），到点自己消失")]
    public float emojiSeconds = 1.6f;

    [Header("移动")]
    public float moveSpeed = 1.6f;
    public float acceleration = 12f;
    public float arrivalRadius = 0.16f;
    [Tooltip("被挡住多久后放弃这个目标")]
    public float stuckTimeout = 1.1f;

    [Header("作息节奏")]
    [Tooltip("一次干活持续多久（秒）")]
    public float workMin = 5f;
    public float workMax = 10f;
    [Tooltip("一次闲聊持续多久（秒）")]
    public float chatMin = 5f;
    public float chatMax = 11f;
    [Tooltip("一次原地停留持续多久（秒）")]
    public float idleMin = 1.5f;
    public float idleMax = 4f;
    [Tooltip("这个距离内有邻居就会面对面聊起来")]
    public float chatRadius = 2.2f;
    [Tooltip("孩子玩耍的活动半径")]
    public float playRadius = 4.5f;

    /// <summary>场景里所有村民（村民之间互相找聊天对象用）。</summary>
    public static readonly List<Villager> All = new List<Villager>();

    Rigidbody2D rb;
    Transform visual;
    Vector3 visualBaseScale;
    VillageClock clock;
    SpriteRenderer[] visualRenderers;

    VillagerState state = VillagerState.Idle;
    VillagerState pendingState = VillagerState.Idle;
    float pendingDuration;
    float stateTimer;
    Vector2 destination;
    float stuckTimer;
    Vector2 lastPosition;
    float bobPhase;
    float facing = 1f;
    int patrolIndex;
    Villager neighbour;
    float nextNeighbourScan;
    /// <summary>上一次做决定时的时间段，用来发现“换班了”。</summary>
    VillageClock.Phase decidedPhase = VillageClock.Phase.Morning;

    // ---- 听觉与异常处理 ----
    /// <summary>听到了还没处理的动静（真正的状态切换留给状态机，噪音回调只写字段）。</summary>
    bool hasNoise;
    /// <summary>这次动静要不要亲自去看（好奇心够高、而且还没怕小虫）。</summary>
    bool shouldInvestigate;
    Vector2 lastNoisePoint;
    /// <summary>最后一次正面看到小虫的位置（追丢后在这里附近搜索）。</summary>
    Vector2 lastSeenBug;
    float nextHearTime;
    int searchLeft;
    Transform alertMark;

    // ---- 头顶的表情（一个槽位：听动静的「!」优先，其次是刚进入状态时冒的表情，见 UpdateAlertMark）----
    SpriteRenderer emojiRenderer;
    /// <summary>现在这个槽里显示的是哪个 key（null = 什么都没显示）。</summary>
    string emojiKey;
    /// <summary>「刚进入某个状态」要冒的表情，以及它到什么时候消失。</summary>
    string stateEmojiKey;
    float stateEmojiUntil;
    /// <summary>这个时间点之前不会再滑倒（刚爬起来的人不会立刻又摔）。</summary>
    float nextSlipTime;

    public VillagerState State { get { return state; } }
    /// <summary>1 = 朝右，-1 = 朝左。</summary>
    public float Facing { get { return facing; } }
    public string JobLabel { get { return VillagerJobs.Label(job); } }
    public bool IsWalking { get { return rb != null && rb.velocity.magnitude > 0.05f; } }
    /// <summary>是否已经亲眼见过小虫吃人（见过就一直躲着小虫）。</summary>
    public bool FearsBug { get { return fearsBug; } }
    /// <summary>视野半径：怕了小虫之后更警觉，看得更远；全村进入「警觉 / 混乱」状态时也会更远。</summary>
    public float EffectiveViewRadius
    {
        get
        {
            float bonus = fearsBug ? afraidViewBonus : 1f;
            if (Alertness.Instance != null) bonus *= Alertness.Instance.VillagerViewBonus;
            if (ChaosMeter.Instance != null) bonus *= ChaosMeter.Instance.VillagerViewBonus;
            return viewRadius * bonus;
        }
    }

    /// <summary>这么响的噪音，这个人听得到多远（响度 × 基准 × 职业好奇倍率 × 全村警觉加成）。</summary>
    public float HearingRadius(float loudness)
    {
        float bonus = 1f;
        if (Alertness.Instance != null) bonus *= Alertness.Instance.VillagerHearingBonus;
        if (ChaosMeter.Instance != null) bonus *= ChaosMeter.Instance.VillagerHearingBonus;
        return hearingBase * loudness * VillagerJobs.Curiosity(job) * bonus;
    }

    /// <summary>是不是正在处理「有动静 / 追丢了」这类异常（HUD 与验证用）。</summary>
    public bool IsReactingToNoise
    {
        get { return state == VillagerState.Alert || state == VillagerState.Investigate || state == VillagerState.Search; }
    }

    /// <summary>正在走去查看动静 / 去翻找（走的是 Commute，状态上还看不出来）。</summary>
    public bool IsHeadingToNoise
    {
        get
        {
            return state == VillagerState.Commute &&
                   (pendingState == VillagerState.Investigate || pendingState == VillagerState.Search);
        }
    }

    /// <summary>听到动静但还没处理的位置（验证时看它有没有记到），没有动静时是 Vector2.zero。</summary>
    public Vector2 LastNoisePoint { get { return hasNoise ? lastNoisePoint : Vector2.zero; } }
    /// <summary>有没有还没处理的动静。</summary>
    public bool HasPendingNoise { get { return hasNoise; } }

    /// <summary>
    /// 有人制造了噪音（订阅 <see cref="GameEvent.Noise"/>）。
    /// 这里**只记位置、不改状态**：状态切换统一交给 <see cref="Decide"/>，
    /// 否则噪音回调会在村民正在追人 / 睡觉的时候把它拽进错误的状态。
    /// </summary>
    void OnNoise(NoiseEvent e)
    {
        if (this == null || rb == null || IsFrozen) return;          // 区块回收后外部可能还持有引用
        if (state == VillagerState.Chase || state == VillagerState.Flee) return;   // 正在追 / 躲，顾不上别的动静
        if (state == VillagerState.Stunned) return;                  // 被电麻了，听不见
        if (state == VillagerState.Slip) return;                     // 摔在地上，顾不上
        if (Time.time < nextHearTime) return;                        // 节流：不被连续脚步声反复惊动

        float radius = HearingRadius(e.loudness);
        if (radius <= 0f) return;

        Vector2 self = rb.position;
        float distance = Vector2.Distance(self, e.position);
        if (distance > radius) return;

        nextHearTime = Time.time + hearingCooldown;
        hasNoise = true;
        // 近处直接知道在哪；边上只听到个大概（否则村民像雷达一样精准，反而不自然）
        lastNoisePoint = distance <= radius * 0.5f
            ? e.position
            : e.position + Random.insideUnitCircle * (radius * 0.35f);

        // 正在干闲事（发呆 / 干活 / 闲聊 / 玩 / 赶路）的人，当场被打断去看一眼。
        // 不打断的话，动静要等它手上这件事做完（干活最长十几秒）才处理 —— 分身引不走人、
        // 「扔个箱子把村民引开」这种玩法也就失效了。正在追 / 躲 / 起疑 / 被电麻的不受影响。
        if (state == VillagerState.Idle || state == VillagerState.Work ||
            state == VillagerState.Socialize || state == VillagerState.Play ||
            state == VillagerState.Commute)
        {
            Decide();
        }
    }

    /// <summary>
    /// 小虫吃掉了一个村民：把**亲眼看到现场**的村民标成「怕」。
    /// 看到 = 离现场够近，而且是正对着小虫（<see cref="CanSeeBug"/>），或者离受害者近到根本来不及躲。
    /// 只影响当场看见的人（村民会随区块回收 / 重建，不写进存档）。
    /// </summary>
    public static void ReportEaten(Villager victim)
    {
        if (victim == null) return;

        Vector2 scene = victim.transform.position;
        for (int i = 0; i < All.Count; i++)
        {
            Villager witness = All[i];
            if (witness == null || witness == victim) continue;

            float distance = Vector2.Distance(witness.transform.position, scene);
            if (distance > witness.witnessRadius) continue;
            if (!witness.CanSeeBug() && distance > witness.witnessCloseRadius) continue;

            witness.WitnessBugEating();
        }
    }

    /// <summary>亲眼看到小虫吃人：态度从此改成躲避（追人的、不管事的，一律变成跑）。</summary>
    public void WitnessBugEating()
    {
        if (fearsBug) return;
        fearsBug = true;

        Debug.Log("[Villager] " + displayName + "（" + JobLabel + "）亲眼看到小虫吃人，从此见到它就躲。");

        if (rb == null) return;

        // 现场就翻脸：放下手里的事，立刻往反方向跑
        RefreshFleeTarget();
        SetState(VillagerState.Flee, RandomRange(reactMin, reactMax) * afraidFleeBonus);
        Grieve();          // 盖上「心碎」，比 Flee 的一般表情持续更久（放在 SetState 之后才不会被顶掉）
    }

    /// <summary>是否被冻结（离玩家太远，停掉状态机与物理）。</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// 远处村民冻结：停状态机（enabled=false）与物理（rb.simulated=false），
    /// 需要时连渲染一起关掉。走回附近再解冻，状态与位置原样保留。
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        if (IsFrozen == frozen) return;
        IsFrozen = frozen;

        if (frozen) neighbour = null;

        if (rb != null)
        {
            if (frozen) rb.velocity = Vector2.zero;
            rb.simulated = !frozen;
        }
        if (freezeVisuals && visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
                if (visualRenderers[i] != null) visualRenderers[i].enabled = !frozen;
        }

        enabled = !frozen;     // 关掉 Update / FixedUpdate

        // 解冻时把头顶提示恢复到当前状态该有的样子（冻结期间这些渲染器被统一关过）
        if (!frozen) UpdateAlertMark();
    }

    /// <summary>当前在干什么（HUD 显示）。</summary>
    public string ActivityText
    {
        get
        {
            switch (state)
            {
                case VillagerState.Commute:
                    if (pendingState == VillagerState.Investigate) return "朝声响走过去了";
                    if (pendingState == VillagerState.Search) return "在附近翻找小虫";
                    return job == VillagerJob.Child ? "正跑向广场" : "正在赶路";
                case VillagerState.Work:
                    return VillagerJobs.WorkText(job);
                case VillagerState.Socialize:
                    return neighbour != null ? "在和人闲聊" : "在歇脚";
                case VillagerState.Play:
                    return "在广场上玩";
                case VillagerState.Chase:
                    return "看到小虫，追过来了！";
                case VillagerState.Flee:
                    return fearsBug ? "见过它吃人，拼命躲开！" : "被小虫吓跑了";
                case VillagerState.Alert:
                    return "听到动静，警觉地转过身";
                case VillagerState.Investigate:
                    return VillagerJobs.InvestigateText(job);
                case VillagerState.Search:
                    return "到处翻找刚才那个东西";
                case VillagerState.Recover:
                    return "没发现什么，回过神来了";
                case VillagerState.Stunned:
                    return "被电麻了，一动也动不了";
                case VillagerState.Slip:
                    return "踩到水滑了一跤，正爬起来";
                default:
                    return clock != null && clock.CurrentPhase == VillageClock.Phase.Night ? "回家休息了" : "在原地发呆";
            }
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        GameEvent.Noise += OnNoise;
    }

    void OnDisable()
    {
        All.Remove(this);

        // 红线 17：区块回收 / 冻结都会走 OnDisable，不退订就会留下指向已销毁对象的委托
        GameEvent.Noise -= OnNoise;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        // 睡眠中的刚体会忽略之后的 velocity 写入，村民必须保持唤醒
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        visual = transform.Find("Visual");
        if (visual != null) visualBaseScale = visual.localScale;

        // 头顶的「!」与表情要在缓存渲染器之前建好，这样它们也算进「冻结时一起关渲染」的范围里
        BuildAlertMark();
        BuildEmoji();
        visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        clock = VillageClock.Instance != null ? VillageClock.Instance : FindObjectOfType<VillageClock>();
        if (map == null) map = FindObjectOfType<VillageMap>();
        lastPosition = rb.position;
    }

    void Start()
    {
        Decide();
    }

    void Update()
    {
        UpdateVisual();
        UpdateAlertMark();          // 头顶表情有寿命，得每帧刷（只有「该显示哪张」变了才动渲染器）

        // 正在处理异常（起疑 / 查看 / 搜索 / 追 / 躲 / 被电麻）时，换班了也先把手上这件事做完
        bool handlingSomething = state == VillagerState.Commute || state == VillagerState.Chase ||
                                 state == VillagerState.Flee || state == VillagerState.Alert ||
                                 state == VillagerState.Investigate || state == VillagerState.Search ||
                                 state == VillagerState.Stunned || state == VillagerState.Slip;

        // 换班了（早晨→正午→傍晚→夜里）：正在干活/发呆的人别硬撑，很快转入下一件事
        VillageClock.Phase phase = clock != null ? clock.CurrentPhase : VillageClock.Phase.Morning;
        if (phase != decidedPhase && !handlingSomething && stateTimer > 2f)
            stateTimer = Random.Range(0.4f, 2f);

        // 不管手上在干什么，只要正面看到小虫就立刻反应：追 / 躲优先于一切日常安排。
        // 没这一条的话，**正在赶路的村民会径直从小虫身上走过去、当作没看见**
        // （原来是只在「到点了重新做决定」时才检查视野，最长要等十几秒）。
        if (state != VillagerState.Chase && state != VillagerState.Flee && CanSeeBug() && TryStartReaction()) return;

        // 正在追 / 正在躲：跟着小虫刷新目标，看不到或跑远了就开始倒计时收工
        if (state == VillagerState.Chase)
        {
            if (CanSeeBug())
            {
                lastSeenBug = BugPosition();
                stateTimer = Mathf.Max(stateTimer, 0.5f);      // 看得见就一直追
            }
            else
            {
                stateTimer -= Time.deltaTime;                  // 看不见了：追到「耐心」用完为止
                if (stateTimer <= 0f)
                {
                    StartSearch();                             // 跟丢了：在最后看见的地方附近翻找
                    return;
                }
            }
        }
        else if (state == VillagerState.Flee)
        {
            Vector2 bugPosition = BugPosition();
            float distance = Vector2.Distance(rb.position, bugPosition);
            float reach = fleeDistance * (fearsBug ? afraidFleeBonus : 1f);
            if (!CanSeeBug() && distance > reach * 0.8f)
            {
                // 已经跑远、也看不见了：躲够了就收工
                stateTimer = Mathf.Min(stateTimer, 0.4f);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    EndReaction();
                    return;
                }
            }
            else
            {
                stateTimer = Mathf.Max(stateTimer, 0.3f);      // 小虫还在附近：继续躲
                if (stateTimer < 0.35f || (distance < reach * 0.6f && CanSeeBug())) RefreshFleeTarget();
            }
        }

        if (state == VillagerState.Commute || state == VillagerState.Chase || state == VillagerState.Flee) return;

        if (state == VillagerState.Socialize) FaceNeighbour();

        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        // 这两步走完就到时间了：先看该不该继续处理异常，否则交回 Decide 排日常作息
        if (state == VillagerState.Alert)
        {
            AfterAlert();
            return;
        }
        if (state == VillagerState.Investigate)
        {
            EndReaction();                                     // 张望完了，什么都没发现
            return;
        }
        if (state == VillagerState.Search)
        {
            if (searchLeft > 0 && SearchNext()) return;        // 换个地方接着找
            EndReaction();
            return;
        }

        Decide();
    }

    void FixedUpdate()
    {
        Vector2 desired = Vector2.zero;

        if (state == VillagerState.Commute)
        {
            Vector2 toTarget = destination - rb.position;
            float distance = toTarget.magnitude;
            if (distance <= arrivalRadius)
            {
                Arrive();
            }
            else
            {
                desired = toTarget / distance * Mathf.Min(SpeedNow, distance * 3.5f);
            }
        }
        else if (state == VillagerState.Chase)
        {
            // 追小虫，但保持 chaseDistance，不会贴到脸上
            Vector2 toBug = BugPosition() - rb.position;
            float distance = toBug.magnitude;
            if (distance > chaseDistance + 0.15f)
                desired = toBug / distance * Mathf.Min(SpeedNow, (distance - chaseDistance) * 4f);
        }
        else if (state == VillagerState.Flee)
        {
            Vector2 toTarget = destination - rb.position;
            float distance = toTarget.magnitude;
            if (distance > arrivalRadius)
                desired = toTarget / distance * Mathf.Min(SpeedNow, distance * 3.5f);
        }

        rb.velocity = Vector2.MoveTowards(rb.velocity, desired, acceleration * Time.fixedDeltaTime);

        // 不旋转刚体（保持头朝上），只按前进方向左右翻转。
        // 用“想去的方向”而不是物理速度，避免被挤/撞时左右乱翻；站着不动时保留上一次朝向。
        if (Mathf.Abs(desired.x) > 0.02f) facing = desired.x > 0f ? 1f : -1f;

        if (state == VillagerState.Commute)
        {
            stuckTimer = Vector2.Distance(rb.position, lastPosition) < 0.002f ? stuckTimer + Time.fixedDeltaTime : 0f;
            lastPosition = rb.position;
            if (stuckTimer >= stuckTimeout)
            {
                // 被挡住过不去：当作到了，就地做该做的事
                Arrive();
            }
        }
    }

    /// <summary>反应时（追 / 躲）移速会降低。</summary>
    float SpeedNow
    {
        get { return moveSpeed * (state == VillagerState.Chase || state == VillagerState.Flee ? reactSpeed : 1f); }
    }

    // ---------------- 视野 ----------------

    static Vector2 BugPosition()
    {
        BugController bug = BugController.Instance;
        return bug != null ? (Vector2)bug.transform.position : new Vector2(float.MaxValue, float.MaxValue);
    }

    /// <summary>小虫是不是在视野里（朝向的扇形内；躲进地洞或伪装起来就看不见了）。</summary>
    public bool CanSeeBug()
    {
        if (this == null || rb == null) return false;      // 区块回收后可能被外部引用，别抛异常
        if (state == VillagerState.Stunned) return false;  // 被电麻了，什么都看不见
        if (state == VillagerState.Slip) return false;     // 摔在地上，先爬起来再说
        BugController bug = BugController.Instance;
        if (bug == null || bug.IsHidden) return false;

        Vector2 delta = (Vector2)bug.transform.position - rb.position;
        float distance = delta.magnitude;

        // 伪装：除了「贴到脸上」，别的距离一律认不出来（所以这一条要在贴脸判定之前）
        if (bug.IsDisguised && distance > awareRadius * DisguiseRevealFactor) return false;

        if (distance <= awareRadius) return true;
        if (distance > EffectiveViewRadius) return false;
        if (distance < 0.01f) return true;

        Vector2 look = new Vector2(facing, 0f);
        return Vector2.Angle(look, delta) <= viewHalfAngle;
    }

    /// <summary>伪装时贴多近会露馅（<see cref="AbilitySet.disguiseRevealFactor"/>，取不到就用 0.6）。</summary>
    float DisguiseRevealFactor
    {
        get
        {
            AbilitySet set = AbilitySet.Instance;
            return set != null ? set.disguiseRevealFactor : 0.6f;
        }
    }

    /// <summary>
    /// 被电麻（电击能力）：定住 <paramref name="seconds"/> 秒，期间看不见小虫、也听不见动静
    /// （正在追 / 躲的人会当场被打断）。
    /// </summary>
    public void Stun(float seconds)
    {
        if (this == null || rb == null) return;

        rb.velocity = Vector2.zero;
        hasNoise = false;
        SetState(VillagerState.Stunned, Mathf.Max(0.2f, seconds));
    }

    /// <summary>
    /// 被吓得跑开（强制抖动能力用）：往小虫的反方向跑一段。
    /// 和「看到小虫躲开」是同一条路（<see cref="VillagerState.Flee"/>），只是不由视野触发。
    /// </summary>
    public void Panic(float seconds)
    {
        if (this == null || rb == null) return;

        hasNoise = false;
        RefreshFleeTarget();
        SetState(VillagerState.Flee, Mathf.Max(0.5f, seconds));
    }

    /// <summary>
    /// 「渲染出错」：这个人的头 / 身子被随机推开几像素、排序也乱一下，同时冒一团洋红 —
    /// 看起来就是这个村民的模型在游戏里坏掉了（被电击时用，风格锚点见 Spec §4.10）。
    /// 结束后会自动复原（<see cref="GlitchJitter"/> 负责还回去）。
    /// </summary>
    public void GlitchParts(float seconds)
    {
        if (this == null) return;

        Transform target = visual != null ? visual : transform;
        AbilityFx.Jitter(target.gameObject, seconds, 0.06f, true);
        AbilityFx.Flash(transform.position, ArtShape.Round, 0.45f, AbilityFx.ErrorMagenta, Mathf.Min(seconds, 0.3f));
    }

    /// <summary>现在能不能滑倒（<see cref="Puddle"/> 会问这个）：被电麻 / 刚摔过 / 冻结的人不行。</summary>
    public bool CanSlip
    {
        get
        {
            return this != null && rb != null && !IsFrozen
                && state != VillagerState.Stunned && state != VillagerState.Slip
                && Time.time >= nextSlipTime;
        }
    }

    /// <summary>
    /// 踩到水滑倒：摔在地上 <see cref="slipSeconds"/> 秒起不来，摔得很响（会把附近的人引过来），
    /// 顺手把身边的可搬物品撞翻（设计文档 §4.2 那条「地面变滑 → NPC 摔倒 → 撞翻货架」的最后一环）。
    /// </summary>
    public void Slip()
    {
        if (!CanSlip) return;

        nextSlipTime = Time.time + Mathf.Max(0.5f, slipCooldown);
        rb.velocity = Vector2.zero;
        hasNoise = false;
        SetState(VillagerState.Slip, Mathf.Max(0.3f, slipSeconds));

        // 摔一跤很响（1.8）：附近的人会来看出了什么事
        GameEvent.RaiseNoise(rb.position, GameEvent.SlipLoudness, NoiseKind.Break);
        AudioOverridePlayer.Play(AudioKeys.Slip);       // 没放音频就是安静的
        int knocked = Draggable.Scatter(rb.position, slipScatterRadius, slipScatterImpulse);
        Debug.Log("[Villager] " + displayName + " 滑倒"
            + (knocked > 0 ? "，撞翻了 " + knocked + " 个东西" : "") + "。");
    }

    /// <summary>看到小虫了：按职业决定追还是躲；见过小虫吃人的人一律躲。</summary>
    bool TryStartReaction()
    {
        VillagerReaction reaction = fearsBug ? VillagerReaction.Flee : VillagerJobs.Reaction(job);
        if (reaction == VillagerReaction.Ignore) return false;

        float duration = RandomRange(reactMin, reactMax) * (fearsBug ? afraidFleeBonus : 1f);

        // 记下「最后一次看见它在哪」：追丢之后就在这附近翻找
        lastSeenBug = BugPosition();
        hasNoise = false;
        GameEvent.RaiseSpotted(this);

        if (reaction == VillagerReaction.Chase)
        {
            SetState(VillagerState.Chase, duration);
            return true;
        }

        RefreshFleeTarget();
        SetState(VillagerState.Flee, duration);
        return true;
    }

    /// <summary>往小虫的反方向跑一段（怕了小虫的人跑得更远）。</summary>
    void RefreshFleeTarget()
    {
        Vector2 away = rb.position - BugPosition();
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
        float reach = fleeDistance * (fearsBug ? afraidFleeBonus : 1f);
        destination = rb.position + away.normalized * reach;
    }

    // ---------------- 听觉与异常处理 ----------------

    /// <summary>
    /// 把记下来的动静转成状态：站住、转向声源、起疑。
    /// 返回是不是真的开始处理了（没动静就返回 false，让 Decide 继续排日常作息）。
    /// </summary>
    bool ConsumeNoise()
    {
        if (!hasNoise) return false;
        hasNoise = false;

        // 好奇心低的人（农夫 / 铁匠 / 长者）与见过小虫吃人的人，只原地起疑、不离开岗位
        shouldInvestigate = !fearsBug && VillagerJobs.Curiosity(job) >= leavePostThreshold;

        float dx = lastNoisePoint.x - rb.position.x;
        if (Mathf.Abs(dx) > 0.1f) facing = dx > 0f ? 1f : -1f;    // 转身朝向声源

        SetState(VillagerState.Alert, RandomRange(alertMin, alertMax));
        return true;
    }

    /// <summary>起疑完了：决定亲自去看，还是只是抬了下头就继续干活。</summary>
    void AfterAlert()
    {
        if (shouldInvestigate)
        {
            // 走过去看看：到了之后进入 Investigate 张望（张望途中看到小虫会转成追 / 躲）
            CommuteTo(lastNoisePoint, VillagerState.Investigate, RandomRange(lookMin, lookMax));
            return;
        }

        EndReaction();
    }

    /// <summary>异常处理完了：先「回过神」停一下，再交回 <see cref="Decide"/> 回到日常作息。</summary>
    void EndReaction()
    {
        shouldInvestigate = false;
        SetState(VillagerState.Recover, Mathf.Max(0.1f, recoverDuration));
    }

    /// <summary>追丢了：记下要找几处，然后立刻去第一处。</summary>
    void StartSearch()
    {
        searchLeft = Mathf.Max(1, searchPoints);
        if (!SearchNext()) EndReaction();
    }

    /// <summary>
    /// 被警觉值派出来搜索（<see cref="Alertness"/> 高的时候会调它）：把 <paramref name="around"/>
    /// 当成「怀疑小虫在这附近」，然后走现成的「追丢 → 翻找」那条路。
    /// </summary>
    public void BeginSearch(Vector2 around)
    {
        if (this == null || rb == null || IsFrozen) return;
        if (state == VillagerState.Stunned || state == VillagerState.Slip) return;

        lastSeenBug = around;
        searchLeft = Mathf.Max(1, searchPoints);
        StartSearch();
    }

    /// <summary>去下一个翻找点（在最后看见小虫的地方附近随机）。没有点了就返回 false。</summary>
    bool SearchNext()
    {
        if (searchLeft <= 0) return false;
        searchLeft--;
        Vector2 point = lastSeenBug + Random.insideUnitCircle * searchRadius;
        CommuteTo(point, VillagerState.Search, RandomRange(0.6f, 1.2f));
        return true;
    }

    // ---------------- 头顶的「!」提示 ----------------

    /// <summary>
    /// 头顶的「!」：一根竖条 + 一个点，用程序化的方块拼出来。
    /// **故意不占美术 key** —— 它不是场景物件，而是「这个人注意到动静了」的状态提示，
    /// 玩家不用盯着小地图也知道附近有人起疑了。
    /// </summary>
    void BuildAlertMark()
    {
        Sprite sprite = ArtShapes.Get(ArtShape.Rect);
        if (sprite == null) return;      // 没有图元就不做提示，不影响逻辑

        GameObject root = new GameObject("AlertMark");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        alertMark = root.transform;

        // 排序值比头（1）大，保证画在头顶上；YSort 会把这个偏移原样保留
        AddMarkPart(root.transform, "Bar", sprite, new Vector2(0.07f, 0.17f), new Vector2(0f, 0.09f));
        AddMarkPart(root.transform, "Dot", sprite, new Vector2(0.07f, 0.07f), new Vector2(0f, -0.04f));

        SetAlertMark(false);
    }

    void AddMarkPart(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 0.86f, 0.25f);      // 明黄：草地和夜里都看得见
        sr.sortingOrder = 6;
    }

    /// <summary>按当前状态刷新头顶提示（起疑 / 去看 / 翻找的时候才显示）。</summary>
    void UpdateAlertMark()
    {
        // 头顶只有**一个槽位**，优先级：听到动静的「!」 > 刚进入状态时冒的表情 > 不显示。
        // 每帧都跑：状态表情是有寿命的，到点要自己消失（但只有「该显示哪张」变了才动渲染器）。
        string want = null;
        if (IsReactingToNoise || IsHeadingToNoise) want = ArtKeys.EmojiExclamation;
        else if (stateEmojiKey != null && Time.time < stateEmojiUntil) want = stateEmojiKey;
        SetEmoji(want);
    }

    /// <summary>
    /// 头顶的表情气泡。美术在 <c>emoji_*</c> 下放一张图就有一张脸（见 <see cref="ArtKeys"/>）；
    /// 尺寸走 <c>SpriteRenderer.size</c>，所以和导入的 PPU 无关，要多大改 <see cref="emojiSize"/>。
    /// 没交图时：「!」退回程序化画的黄方块（<see cref="BuildAlertMark"/>），其它表情直接不显示。
    /// </summary>
    void BuildEmoji()
    {
        GameObject root = new GameObject("Emoji");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, emojiHeight, 0f);

        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(root.transform, false);
        emojiRenderer = bubble.AddComponent<SpriteRenderer>();
        emojiRenderer.color = Color.white;
        emojiRenderer.sortingOrder = 6;
        emojiRenderer.enabled = false;
    }

    void SetEmoji(string key)
    {
        if (key == emojiKey) return;               // 没变就不动：避免每帧切贴图
        emojiKey = key;

        Sprite sprite = string.IsNullOrEmpty(key) ? null : ArtOverride.Get(key);
        if (sprite == null)
        {
            // 这张没交图：「!」还能退回程序化画的那个，其它表情就干脆不显示
            if (emojiRenderer != null) emojiRenderer.enabled = false;
            SetAlertMark(key == ArtKeys.EmojiExclamation);
            return;
        }

        SetAlertMark(false);
        if (emojiRenderer == null) return;

        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        emojiRenderer.sprite = sprite;
        emojiRenderer.drawMode = SpriteDrawMode.Sliced;
        emojiRenderer.size = new Vector2(emojiSize * aspect, emojiSize);
        emojiRenderer.enabled = emojiSize > 0.001f;
    }

    /// <summary>进入某个状态时冒一下表情（过 <see cref="emojiSeconds"/> 秒自己消失）。传空 = 不冒。</summary>
    void ShowStateEmoji(string[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        stateEmojiKey = keys.Length == 1 ? keys[0] : keys[Random.Range(0, keys.Length)];
        stateEmojiUntil = Time.time + emojiSeconds;
    }

    // 每个状态冒哪几张表情（多张 = 随机挑一张，同一种状态也不至于每次都同一张脸）。
    // 表是静态的：状态切换时才查，不在 Update 里分配。
    static readonly string[] EmojiChase = { ArtKeys.EmojiAngry };
    static readonly string[] EmojiFlee = { ArtKeys.EmojiNo, ArtKeys.EmojiSad, ArtKeys.EmojiHeartBroken };
    static readonly string[] EmojiHurt = { ArtKeys.EmojiDizzy };
    static readonly string[] EmojiAlert = { ArtKeys.EmojiExclamation };
    static readonly string[] EmojiLook = { ArtKeys.EmojiBulb };
    static readonly string[] EmojiSearch = { ArtKeys.EmojiConfused };
    static readonly string[] EmojiChat = { ArtKeys.EmojiHaha, ArtKeys.EmojiLove, ArtKeys.EmojiHappy };
    static readonly string[] EmojiPlay = { ArtKeys.EmojiHappy, ArtKeys.EmojiHaha };
    static readonly string[] EmojiRecover = { ArtKeys.EmojiSpeechless, ArtKeys.EmojiAshamed };
    static readonly string[] EmojiWork = { ArtKeys.EmojiSleepy };
    static readonly string[] EmojiGrief = { ArtKeys.EmojiHeartBroken };

    /// <summary>这个状态该冒哪几张表情（null = 不冒）。</summary>
    static string[] EmojiFor(VillagerState state)
    {
        switch (state)
        {
            case VillagerState.Chase: return EmojiChase;
            case VillagerState.Flee: return EmojiFlee;
            case VillagerState.Stunned: return EmojiHurt;
            case VillagerState.Slip: return EmojiHurt;
            case VillagerState.Alert: return EmojiAlert;
            case VillagerState.Investigate: return EmojiLook;
            case VillagerState.Search: return EmojiSearch;
            case VillagerState.Socialize: return EmojiChat;
            case VillagerState.Play: return EmojiPlay;
            case VillagerState.Recover: return EmojiRecover;
            case VillagerState.Work: return EmojiWork;
            default: return null;      // 发呆 / 赶路不冒表情
        }
    }

    /// <summary>亲眼看到同伴被吃掉：冒一个心碎（只有真的看过的人才会被叫到这里）。</summary>
    public void Grieve()
    {
        ShowStateEmoji(EmojiGrief);
        stateEmojiUntil = Time.time + emojiSeconds * 1.6f;
    }

    void SetAlertMark(bool visible)
    {
        if (alertMark == null || alertMark.gameObject.activeSelf == visible) return;
        alertMark.gameObject.SetActive(visible);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawViewGizmo) return;
        Vector3 origin = transform.position;
        Vector3 look = new Vector3(facing, 0f, 0f);
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawLine(origin, origin + look * viewRadius);
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
        Vector3 a = Quaternion.Euler(0f, 0f, viewHalfAngle) * look * viewRadius;
        Vector3 b = Quaternion.Euler(0f, 0f, -viewHalfAngle) * look * viewRadius;
        Gizmos.DrawLine(origin, origin + a);
        Gizmos.DrawLine(origin, origin + b);
        Gizmos.DrawLine(origin + a, origin + b);
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(origin, awareRadius);
    }

    // ---------------- 状态机 ----------------

    /// <summary>按当前作息决定接下来做什么（外部也可以主动调用强制重新决策）。</summary>
    public void Decide()
    {
        VillageClock.Phase phase = clock != null ? clock.CurrentPhase : VillageClock.Phase.Morning;
        decidedPhase = phase;

        // 先把「看到小虫」处理掉：追 / 躲优先于一切日常安排
        if (state != VillagerState.Chase && state != VillagerState.Flee && CanSeeBug() && TryStartReaction()) return;

        // 再处理「听到动静」：也优先于日常安排（但低于「已经看到小虫」）
        if (ConsumeNoise()) return;

        // 夜里：回家待着
        if (phase == VillageClock.Phase.Night)
        {
            if (Vector2.Distance(rb.position, home) > 0.8f)
                CommuteTo(home, VillagerState.Idle, RandomRange(idleMin * 2f, idleMax * 2f));
            else
                SetState(VillagerState.Idle, RandomRange(4f, 8f));
            return;
        }

        // 孩子：白天到处跑，正午 / 傍晚去广场凑热闹
        if (job == VillagerJob.Child)
        {
            if (phase == VillageClock.Phase.Noon || phase == VillageClock.Phase.Evening)
            {
                if (Chance(0.45f)) SetState(VillagerState.Idle, RandomRange(idleMin, idleMax));
                else CommuteTo(RandomAround(hangout, playRadius * 0.7f), VillagerState.Play, RandomRange(2f, 5f));
            }
            else if (Chance(0.25f))
            {
                SetState(VillagerState.Idle, RandomRange(idleMin, idleMax));
            }
            else
            {
                CommuteTo(RandomAround(hangout, playRadius), VillagerState.Play, RandomRange(0.6f, 1.8f));
            }
            return;
        }

        // 正午 / 傍晚：全村社交时间（就近找人聊，也有一批人往广场/水井扎堆）
        if (phase == VillageClock.Phase.Noon || phase == VillageClock.Phase.Evening)
        {
            float roll = Random.value;
            if (roll < 0.2f) SetState(VillagerState.Idle, RandomRange(idleMin, idleMax));
            else if (roll < 0.65f) SetState(VillagerState.Socialize, RandomRange(chatMin, chatMax));
            else CommuteTo(SocialSpot(), VillagerState.Socialize, RandomRange(chatMin, chatMax));
            return;
        }

        // 上午 / 下午：干活
        if (Chance(0.08f)) SetState(VillagerState.Idle, RandomRange(idleMin, idleMax));
        else CommuteTo(WorkTarget(), VillagerState.Work, WorkDuration());
    }

    /// <summary>到达目的地后要进入的状态与停留时间。</summary>
    void Arrive()
    {
        state = pendingState;
        stateTimer = pendingDuration;

        // 走到半路换班了：先应付一会儿再去干新的事（比如天黑了就早点回家）
        if (clock != null && clock.CurrentPhase != decidedPhase)
        {
            decidedPhase = clock.CurrentPhase;
            stateTimer = Mathf.Min(stateTimer, 2f);
        }

        if (state == VillagerState.Socialize) neighbour = null;
        UpdateAlertMark();
        GameEvent.RaiseStateChanged(this);
    }

    void SetState(VillagerState next, float duration)
    {
        state = next;
        stateTimer = duration;
        ShowStateEmoji(EmojiFor(next));
        UpdateAlertMark();
        GameEvent.RaiseStateChanged(this);
    }

    void CommuteTo(Vector2 target, VillagerState after, float duration)
    {
        destination = target;
        pendingState = after;
        pendingDuration = duration;
        state = VillagerState.Commute;
        stuckTimer = 0f;
        lastPosition = rb.position;
        UpdateAlertMark();
        GameEvent.RaiseStateChanged(this);
    }

    /// <summary>这个职业的工作点在哪。</summary>
    Vector2 WorkTarget()
    {
        if (map == null) return workplace;

        switch (job)
        {
            case VillagerJob.Guard:            // 沿着主路一段一段地巡逻
                return NextPatrolPoint();
            case VillagerJob.Woodcutter:       // 找离自己最近的树
                return VillageMap.Nearest(map.trees, rb.position, workplace);
            case VillagerJob.Farmer:           // 在自家那块田里换个位置
                return RandomAround(workplace, 1.6f);
            case VillagerJob.Shepherd:         // 在畜栏里走动
                return RandomAround(workplace, 1.8f);
            case VillagerJob.Elder:            // 找张长椅坐下
                return RandomAround(VillageMap.Nearest(map.benches, rb.position, workplace), 0.9f);
            default:                           // 面包师 / 铁匠 / 摊贩守着自己的位子
                return workplace;
        }
    }

    float WorkDuration()
    {
        switch (job)
        {
            case VillagerJob.Guard: return RandomRange(1.2f, 2.8f);        // 站一会儿就继续走
            case VillagerJob.Woodcutter: return RandomRange(3f, 6f);
            case VillagerJob.Child: return RandomRange(0.6f, 1.8f);
            default: return RandomRange(workMin, workMax);
        }
    }

    /// <summary>守卫巡逻点：沿主路交替前进。</summary>
    Vector2 NextPatrolPoint()
    {
        if (map == null || map.roads.Count == 0) return workplace;
        patrolIndex = (patrolIndex + 1) % map.roads.Count;
        return map.roads[patrolIndex];
    }

    /// <summary>闲聊时去哪（就近的水井、长椅、摊位，都没有就在自家那片空地）。</summary>
    Vector2 SocialSpot()
    {
        if (map == null) return hangout;
        int roll = Random.Range(0, 4);
        if (roll == 0) return RandomAround(VillageMap.PickNear(map.wells, hangout, hangout, 30f), 1.8f);
        if (roll == 1) return RandomAround(VillageMap.PickNear(map.benches, hangout, hangout, 30f), 1.4f);
        if (roll == 2) return RandomAround(VillageMap.PickNear(map.stalls, hangout, hangout, 30f), 1.8f);
        return RandomAround(hangout, 3.4f);
    }

    Vector2 RandomAround(Vector2 origin, float radius)
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        return origin + offset;
    }

    float RandomRange(float min, float max)
    {
        return Random.Range(min, max);
    }

    static bool Chance(float probability)
    {
        return Random.value < probability;
    }

    // ---------------- 表现 ----------------

    void UpdateVisual()
    {
        if (visual == null) return;

        bool walking = IsWalking;
        bobPhase += Time.deltaTime * (walking ? 9f : 2.2f);

        // 干活 / 聊天时手上有动作，幅度小一点也更快
        float busy = state == VillagerState.Work || state == VillagerState.Play ? 1.4f : 1f;
        float bob = Mathf.Abs(Mathf.Sin(bobPhase * busy)) * (walking ? 0.07f : 0.02f);
        visual.localPosition = new Vector3(0f, bob, 0f);

        float squash = 1f + Mathf.Sin(bobPhase * 2f * busy) * (walking ? 0.05f : 0.025f);
        // 始终头朝上：不做旋转，只用 X 缩放做左右翻转
        visual.localScale = new Vector3(
            Mathf.Abs(visualBaseScale.x) * facing / squash,
            visualBaseScale.y * squash,
            visualBaseScale.z);
    }

    /// <summary>闲聊时把脸转向身边的邻居（只左右翻转）。</summary>
    void FaceNeighbour()
    {
        if (Time.time >= nextNeighbourScan)
        {
            nextNeighbourScan = Time.time + 0.3f;
            neighbour = FindNeighbour();
        }
        if (neighbour == null) return;

        float dx = neighbour.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.1f) facing = dx > 0f ? 1f : -1f;
    }

    Villager FindNeighbour()
    {
        Villager best = null;
        float bestDistance = chatRadius * chatRadius;
        Vector2 self = rb.position;
        for (int i = 0; i < All.Count; i++)
        {
            Villager other = All[i];
            if (other == null || other == this) continue;
            float distance = ((Vector2)other.transform.position - self).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }
        return best;
    }
}
