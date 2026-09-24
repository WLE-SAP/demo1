using UnityEngine;

/// <summary>
/// 按 F 交互：拾取 / 放下头前方的可搬动物品。
/// 搬运期间物品停在头前方，并通知 <see cref="BugController"/> 降低移动速度。
/// </summary>
public class DragController : MonoBehaviour
{
    public BugController bug;

    [Header("交互判定")]
    [Tooltip("以角色为圆心能交互到的距离")]
    public float interactRange = 2.6f;
    [Tooltip("以头朝向为中轴的交互夹角（度）")]
    public float interactAngle = 150f;

    [Header("搬运")]
    [Tooltip("搬运时物品停在头前方多远")]
    public float holdDistance = 0.95f;
    [Tooltip("跟随强度：越大越跟手")]
    public float followStrength = 12f;
    [Tooltip("物品离角色超过这个距离就自动脱手")]
    public float maxHoldRange = 4.5f;
    public LayerMask interactMask = ~0;

    Draggable held;

    public bool IsHolding { get { return held != null; } }
    public Draggable Held { get { return held; } }

    void Awake()
    {
        if (bug == null) bug = FindObjectOfType<BugController>();
    }

    void Update()
    {
        // 手里的东西可能已经被小虫啃掉了（Unity 的 == 对已销毁对象也返回 true）：
        // 这时要清掉引用并恢复小虫的移速，否则它会一直处于「搬运中」的慢速状态
        if (held == null)
        {
            if (bug != null) bug.IsDragging = false;
            return;
        }
        Carry();
    }

    void OnDisable()
    {
        Drop();
    }

    /// <summary>交互键（F）：没拿东西就拾取最近的，拿着就先放下。由 <see cref="BugController"/> 统一分发。</summary>
    public void ToggleInteract()
    {
        if (held != null)
        {
            Drop();
            return;
        }

        Draggable target = FindInteractable();
        if (target == null) return;

        held = target;
        SetSimulated(held, false);
        if (bug != null) bug.IsDragging = true;
        AudioOverridePlayer.Play(AudioKeys.Pickup);

        // 搬东西是有动静的（拿起很轻）
        GameEvent.RaiseNoise(held.transform.position, NoiseKind.Pickup);
    }

    /// <summary>头前方最合适的可搬动物品。</summary>
    public Draggable FindInteractable()
    {
        Vector2 origin = Origin();
        Vector2 face = bug != null ? bug.Facing : Vector2.right;
        float halfAngle = interactAngle * 0.5f;

        Draggable best = null;
        float bestDistance = float.MaxValue;
        Draggable[] all = FindObjectsOfType<Draggable>();
        for (int i = 0; i < all.Length; i++)
        {
            Vector2 delta = (Vector2)all[i].transform.position - origin;
            float distance = delta.magnitude;
            if (distance > interactRange) continue;
            if (distance > 0.01f && Vector2.Angle(face, delta) > halfAngle) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = all[i];
            }
        }
        return best;
    }

    void Carry()
    {
        Vector2 anchor = Origin();
        Vector2 face = bug != null ? bug.Facing : Vector2.right;
        Vector2 target = anchor + face * holdDistance;

        Vector2 offset = target - anchor;
        float distance = offset.magnitude;
        if (distance > maxHoldRange) target = anchor + offset / distance * maxHoldRange;

        float k = 1f - Mathf.Exp(-(followStrength / Mathf.Max(0.2f, held.weight)) * Time.deltaTime);
        Vector2 next = Vector2.Lerp(held.transform.position, target, k);

        // 搬运中的物品已关闭物理模拟，直接写位置即可，不会被刚体回写覆盖
        held.transform.position = new Vector3(next.x, next.y, held.transform.position.z);

        if (Vector2.Distance(next, anchor) > maxHoldRange) Drop();
    }

    /// <summary>搬运时暂停物品的物理模拟，放下时恢复。</summary>
    static void SetSimulated(Draggable target, bool simulated)
    {
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body == null) return;
        if (simulated) body.position = target.transform.position;
        body.simulated = simulated;
    }

    Vector2 Origin()
    {
        return bug != null ? (Vector2)bug.transform.position : (Vector2)transform.position;
    }

    public void Drop()
    {
        if (held == null) return;

        // 放下的位置先记下来：held 置空之后就取不到了
        Vector2 dropAt = held.transform.position;
        SetSimulated(held, true);
        held = null;
        if (bug != null) bug.IsDragging = false;
        AudioOverridePlayer.Play(AudioKeys.Drop);

        // 把箱子砸在地上是很响的 —— 「扔给村民听」本身就是玩法
        GameEvent.RaiseNoise(dropAt, NoiseKind.Drop);
    }
}
