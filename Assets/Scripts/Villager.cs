using System.Collections.Generic;
using UnityEngine;

/// <summary>村民状态机的状态。</summary>
public enum VillagerState
{
    Idle,       // 原地待着（休息 / 发呆 / 夜里在家）
    Commute,    // 正在走向目标点
    Work,       // 在工作点干活（或守卫站岗、长者晒太阳）
    Socialize,  // 在社交点闲聊（会和身边的邻居面对面）
    Play,       // 孩子玩耍（在广场跑来跑去）
    Chase,      // 看到小虫，追上去
    Flee        // 看到小虫，躲开
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

    [Header("远距离冻结")]
    [Tooltip("被冻结时连精灵渲染一起关掉，进一步省资源")]
    public bool freezeVisuals = true;

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

    public VillagerState State { get { return state; } }
    /// <summary>1 = 朝右，-1 = 朝左。</summary>
    public float Facing { get { return facing; } }
    public string JobLabel { get { return VillagerJobs.Label(job); } }
    public bool IsWalking { get { return rb != null && rb.velocity.magnitude > 0.05f; } }
    /// <summary>是否已经亲眼见过小虫吃人（见过就一直躲着小虫）。</summary>
    public bool FearsBug { get { return fearsBug; } }
    /// <summary>视野半径：怕了小虫之后更警觉，看得更远。</summary>
    public float EffectiveViewRadius { get { return fearsBug ? viewRadius * afraidViewBonus : viewRadius; } }

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
    }

    /// <summary>当前在干什么（HUD 显示）。</summary>
    public string ActivityText
    {
        get
        {
            switch (state)
            {
                case VillagerState.Commute:
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
                default:
                    return clock != null && clock.CurrentPhase == VillageClock.Phase.Night ? "回家休息了" : "在原地发呆";
            }
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
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

        // 换班了（早晨→正午→傍晚→夜里）：正在干活/发呆的人别硬撑，很快转入下一件事
        VillageClock.Phase phase = clock != null ? clock.CurrentPhase : VillageClock.Phase.Morning;
        if (phase != decidedPhase && state != VillagerState.Commute && state != VillagerState.Chase && state != VillagerState.Flee && stateTimer > 2f)
            stateTimer = Random.Range(0.4f, 2f);

        // 正在追 / 正在躲：跟着小虫刷新目标，看不到或跑远了就收工
        if (state == VillagerState.Chase)
        {
            if (!CanSeeBug()) stateTimer = Mathf.Min(stateTimer, 0.8f);
            else stateTimer = Mathf.Max(stateTimer, 0.5f);
        }
        else if (state == VillagerState.Flee)
        {
            Vector2 bugPosition = BugPosition();
            float distance = Vector2.Distance(rb.position, bugPosition);
            float reach = fleeDistance * (fearsBug ? afraidFleeBonus : 1f);
            if (!CanSeeBug() && distance > reach * 0.8f) stateTimer = Mathf.Min(stateTimer, 0.4f);
            if (stateTimer < 0.35f || (distance < reach * 0.6f && CanSeeBug())) RefreshFleeTarget();
        }

        if (state == VillagerState.Commute || state == VillagerState.Chase || state == VillagerState.Flee) return;

        if (state == VillagerState.Socialize) FaceNeighbour();

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) Decide();
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

    /// <summary>小虫是不是在视野里（朝向的扇形内；躲进地洞就看不见了）。</summary>
    public bool CanSeeBug()
    {
        if (this == null || rb == null) return false;      // 区块回收后可能被外部引用，别抛异常
        BugController bug = BugController.Instance;
        if (bug == null || bug.IsHidden) return false;

        Vector2 delta = (Vector2)bug.transform.position - rb.position;
        float distance = delta.magnitude;
        if (distance <= awareRadius) return true;
        if (distance > EffectiveViewRadius) return false;
        if (distance < 0.01f) return true;

        Vector2 look = new Vector2(facing, 0f);
        return Vector2.Angle(look, delta) <= viewHalfAngle;
    }

    /// <summary>看到小虫了：按职业决定追还是躲；见过小虫吃人的人一律躲。</summary>
    bool TryStartReaction()
    {
        VillagerReaction reaction = fearsBug ? VillagerReaction.Flee : VillagerJobs.Reaction(job);
        if (reaction == VillagerReaction.Ignore) return false;

        float duration = RandomRange(reactMin, reactMax) * (fearsBug ? afraidFleeBonus : 1f);

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
    }

    void SetState(VillagerState next, float duration)
    {
        state = next;
        stateTimer = duration;
    }

    void CommuteTo(Vector2 target, VillagerState after, float duration)
    {
        destination = target;
        pendingState = after;
        pendingDuration = duration;
        state = VillagerState.Commute;
        stuckTimer = 0f;
        lastPosition = rb.position;
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
