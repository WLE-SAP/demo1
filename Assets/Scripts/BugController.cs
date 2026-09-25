using UnityEngine;

/// <summary>
/// 2D 俯视玩法控制：鼠标左键点击地图移动，空格进食，F 交互（拾取/放下可搬动物品）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BugController : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("移动速度（单位/秒）")]
    public float walkSpeed = 4f;
    [Tooltip("搬运物品时的速度倍率（搬的东西自带了倍率时以它为准，见 Draggable.speedMultiplier）")]
    public float dragSpeedMultiplier = 0.4f;
    public float acceleration = 26f;
    [Tooltip("到达目标点的判定半径")]
    public float arrivalRadius = 0.14f;
    [Tooltip("被挡住多久后放弃当前目标点")]
    public float stuckTimeout = 1.2f;

    [Header("朝向")]
    public float turnSpeed = 14f;
    public float facingSmooth = 10f;

    [Header("点击移动")]
    public Camera aimCamera;
    [Tooltip("目标点标记")]
    public Transform moveMarker;
    [Tooltip("可点击范围（以场景中心为原点，只在 useWorldLimit 打开时生效）")]
    public Vector2 worldLimit = new Vector2(16f, 16f);
    [Tooltip("把点击目标限制在 worldLimit 内；无限地图请关掉")]
    public bool useWorldLimit = false;

    [Header("引用")]
    public FollowCamera followCamera;
    public WormBody wormBody;
    public BugEat eat;
    public DragController interact;

    [Header("地洞")]
    [Tooltip("站在离洞口多近才能钻进去")]
    public float burrowRange = 0.9f;
    [Tooltip("两次钻洞之间的最短间隔")]
    public float burrowCooldown = 0.5f;

    [Header("冲刺（Shift）")]
    [Tooltip("冲刺时的速度")]
    public float dashSpeed = 11f;
    [Tooltip("冲刺持续多久")]
    public float dashDuration = 0.22f;
    [Tooltip("两次冲刺之间的间隔")]
    public float dashCooldown = 0.9f;

    float dashTimer;
    float nextDashTime;
    Vector2 dashDirection;
    float nextDashNoiseTime;

    /// <summary>1 = 正在冲刺（尾巴摆到最快）。</summary>
    public float DashBlend { get { return dashTimer > 0f ? 1f : 0f; } }
    public bool IsDashing { get { return dashTimer > 0f; } }

    /// <summary>Shift：朝当前目标点（没有目标就朝朝向）冲一小段距离。</summary>
    public void TryDash()
    {
        if (hidden || Time.time < nextDashTime) return;

        dashTimer = Mathf.Max(0.05f, dashDuration);
        nextDashTime = Time.time + dashTimer + Mathf.Max(0.1f, dashCooldown);

        Vector2 direction = hasDestination ? destination - rb.position : facing;
        if (direction.sqrMagnitude < 0.0001f) direction = facing;
        dashDirection = direction.normalized;

        // 冲刺是「跑起来」的动静：立刻开始出声（实际发声在 FixedUpdate 里按间隔发）
        nextDashNoiseTime = Time.time;
    }

    Rigidbody2D rb;
    Vector2 velocity;
    Vector2 facing = Vector2.right;
    Vector2 destination;
    bool hasDestination;
    float aimAngle;
    float stuckTimer;
    Vector2 lastPosition;
    bool isDragging;
    bool hidden;
    Burrow hiddenBurrow;
    float nextBurrowTime;
    Transform headVisual;
    Collider2D bodyCollider;

    /// <summary>是否正搬运物品（由 DragController 写入）。</summary>
    public bool IsDragging
    {
        get { return isDragging; }
        set { isDragging = value; }
    }

    /// <summary>
    /// 手里那件东西自带的速度倍率（&lt;= 0 = 用 <see cref="dragSpeedMultiplier"/>）。
    /// 由 <see cref="DragController"/> 在拾取 / 放下时写 —— 越大的石头给得越小，所以搬大石头明显更慢。
    /// </summary>
    public float HeldSpeedMultiplier { get; set; }

    /// <summary>搬运时的实际速度倍率（物品自己带了就用物品的）。</summary>
    public float CarrySpeedMultiplier
    {
        get { return HeldSpeedMultiplier > 0.01f ? HeldSpeedMultiplier : dragSpeedMultiplier; }
    }

    public Vector2 Velocity { get { return velocity; } }
    public float CurrentSpeed { get { return velocity.magnitude; } }
    public float TargetSpeed { get { return isDragging ? walkSpeed * CarrySpeedMultiplier : walkSpeed; } }
    /// <summary>头部朝向（单位向量）。</summary>
    public Vector2 Facing { get { return facing; } }
    public bool HasDestination { get { return hasDestination; } }
    public Vector2 Destination { get { return destination; } }
    public float Speed01 { get { return Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.01f, walkSpeed)); } }

    /// <summary>场上唯一的小虫。</summary>
    public static BugController Instance { get; private set; }

    /// <summary>是否躲在地洞里（躲着的时候村民看不见它）。</summary>
    public bool IsHidden { get { return hidden; } }
    /// <summary>
    /// 是否正伪装着（<see cref="AbilitySet"/> 的能力）。
    /// 村民的 <see cref="Villager.CanSeeBug"/> 会问这个值：伪装期间除非贴到脸上，否则认不出来。
    /// </summary>
    public bool IsDisguised { get { return AbilitySet.Instance != null && AbilitySet.Instance.IsDisguised; } }
    /// <summary>转身整圈里有没有正在遮住小虫的伪装（给 HUD / 调试看）。</summary>
    public bool IsInvisibleToVillagers { get { return hidden || IsDisguised; } }
    /// <summary>躲进去的那个地洞。</summary>
    public Burrow CurrentBurrow { get { return hiddenBurrow; } }
    /// <summary>身边有没有可以钻的地洞（HUD 提示用）。</summary>
    public Burrow NearbyBurrow
    {
        get
        {
            if (hidden) return hiddenBurrow;
            // 组件 Awake 顺序不保证：HUD 可能比小虫先跑，这时 rb 还没赋值，用 transform 顶上
            Vector2 self = rb != null ? rb.position : (Vector2)transform.position;
            return Burrow.Nearest(self, burrowRange);
        }
    }

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        if (aimCamera == null) aimCamera = Camera.main;
        if (followCamera == null) followCamera = FindObjectOfType<FollowCamera>();
        if (wormBody == null) wormBody = GetComponentInChildren<WormBody>();
        if (eat == null) eat = GetComponent<BugEat>();
        if (interact == null) interact = FindObjectOfType<DragController>();
        headVisual = transform.Find("Head");
        bodyCollider = GetComponent<Collider2D>();
        aimAngle = 0f;
        lastPosition = rb.position;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (moveMarker != null) moveMarker.gameObject.SetActive(false);
    }

    void Update()
    {
        // 躲在地洞里时点不动，只能按交互键（F）出来
        if (!hidden && Input.GetMouseButtonDown(0)) SetDestination(ScreenToWorld(Input.mousePosition));

        if (Input.GetKeyDown(KeyCode.Space) && eat != null) eat.TryEat();

        // 交互键统一成 F（拾取/放下、钻地洞/出洞、走地道传送）
        if (GameInput.InteractDown) Interact();

        if (GameInput.DashDown) TryDash();

        if (wormBody != null) wormBody.SetLocomotion(DashBlend > 0f ? 1f : Speed01);
        UpdateMarker();
    }

    /// <summary>
    /// F 键统一入口，按优先级来：
    /// 躲着 → 出洞；手里拿着东西 → 放下；站在地洞上 → 钻洞 / 走地道；附近有可搬物品 → 拾取。
    /// </summary>
    public void Interact()
    {
        if (hidden)
        {
            UseBurrow();
            return;
        }

        if (interact != null && interact.IsHolding)
        {
            interact.ToggleInteract();     // 放下手里的东西
            return;
        }

        if (Burrow.Nearest(rb.position, burrowRange) != null)
        {
            UseBurrow();
            return;
        }

        if (interact != null) interact.ToggleInteract();
    }

    /// <summary>F 键：钻进地洞躲起来 / 从地洞出来 / 用地道传送。</summary>
    public void UseBurrow()
    {
        // 出洞永远允许（不受冷却限制，免得按了没反应）
        if (hidden)
        {
            SetHidden(false, null);
            nextBurrowTime = Time.time + burrowCooldown;
            return;
        }

        if (Time.time < nextBurrowTime) return;

        Burrow burrow = Burrow.Nearest(rb.position, burrowRange);
        if (burrow == null) return;

        if (burrow.IsTunnel)
        {
            // 地道：直接从另一头钻出来
            Burrow exit = burrow.partner;
            Teleport(exit.Position + new Vector2(0f, -0.35f));
        }
        else
        {
            SetHidden(true, burrow);
        }
        nextBurrowTime = Time.time + burrowCooldown;
    }

    /// <summary>躲进 / 离开地洞。</summary>
    public void SetHidden(bool value, Burrow burrow)
    {
        hidden = value;
        hiddenBurrow = burrow;

        if (headVisual != null) headVisual.gameObject.SetActive(!value);
        if (wormBody != null) wormBody.SetVisible(!value);
        if (bodyCollider != null) bodyCollider.enabled = !value;

        ClearDestination();
        velocity = Vector2.zero;
        if (rb != null) rb.velocity = Vector2.zero;
        if (moveMarker != null) moveMarker.gameObject.SetActive(false);
        if (interact != null) interact.Drop();
    }

    /// <summary>瞬移（走地道用）。</summary>
    void Teleport(Vector2 world)
    {
        ClearDestination();
        velocity = Vector2.zero;
        rb.velocity = Vector2.zero;
        rb.position = world;
        transform.position = new Vector3(world.x, world.y, transform.position.z);
        lastPosition = world;
        if (moveMarker != null) moveMarker.gameObject.SetActive(false);
        if (followCamera != null) followCamera.Snap();
    }

    void FixedUpdate()
    {
        Vector2 desired = Vector2.zero;

        if (dashTimer > 0f)
        {
            // 冲刺：短时间内直接沿冲刺方向高速移动，目标点保留（冲完接着走）
            dashTimer -= Time.fixedDeltaTime;
            desired = dashDirection * dashSpeed;

            // 冲刺期间每隔一小段就出一声（响度比脚步大，附近的村民会注意到）
            if (Time.time >= nextDashNoiseTime)
            {
                nextDashNoiseTime = Time.time + 0.25f;
                GameEvent.RaiseNoise(rb.position, NoiseKind.Dash);
            }
        }
        else if (hasDestination)
        {
            Vector2 toTarget = destination - rb.position;
            float distance = toTarget.magnitude;
            if (distance <= arrivalRadius) hasDestination = false;
            else desired = toTarget / distance * Mathf.Min(TargetSpeed, distance * 4f);
        }

        velocity = Vector2.MoveTowards(velocity, desired, acceleration * Time.fixedDeltaTime);
        rb.velocity = velocity;

        if (velocity.sqrMagnitude > 0.02f)
            facing = Vector2.Lerp(facing, velocity.normalized, 1f - Mathf.Exp(-facingSmooth * Time.fixedDeltaTime)).normalized;

        aimAngle = Mathf.LerpAngle(aimAngle, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg,
            1f - Mathf.Exp(-turnSpeed * Time.fixedDeltaTime));
        // freezeRotation 为 true 时 MoveRotation 会被约束吞掉，必须用 SetRotation 直接设置
        rb.SetRotation(aimAngle);

        // 被障碍物卡住时放弃目标点，避免一直顶墙
        if (hasDestination)
        {
            stuckTimer = Vector2.Distance(rb.position, lastPosition) < 0.002f ? stuckTimer + Time.fixedDeltaTime : 0f;
            lastPosition = rb.position;
            if (stuckTimer >= stuckTimeout) hasDestination = false;
        }
    }

    void UpdateMarker()
    {
        if (moveMarker == null) return;
        bool show = hasDestination;
        if (moveMarker.gameObject.activeSelf != show) moveMarker.gameObject.SetActive(show);
        if (show) moveMarker.position = new Vector3(destination.x, destination.y, 0f);
    }

    /// <summary>屏幕坐标转世界坐标（2D 平面）。</summary>
    public Vector2 ScreenToWorld(Vector3 screen)
    {
        if (aimCamera == null) return Vector2.zero;
        Vector3 w = aimCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -aimCamera.transform.position.z));
        return new Vector2(w.x, w.y);
    }

    public void SetDestination(Vector2 world)
    {
        if (useWorldLimit)
        {
            world.x = Mathf.Clamp(world.x, -worldLimit.x, worldLimit.x);
            world.y = Mathf.Clamp(world.y, -worldLimit.y, worldLimit.y);
        }
        destination = world;
        hasDestination = true;
        stuckTimer = 0f;
        lastPosition = rb.position;
    }

    public void ClearDestination()
    {
        hasDestination = false;
    }

    /// <summary>播放一次捕食（啃食）动画。</summary>
    public void PlayEatAnimation()
    {
        if (wormBody != null) wormBody.TriggerEat();
    }
}
