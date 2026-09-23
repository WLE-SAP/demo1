using UnityEngine;

/// <summary>
/// 小虫的成长：吃到「特殊食物」会长大一级。
/// 每升一级都会变大、跑得更快、捕食范围更大、体力上限更高——也就是能吃到更多、更「重」的东西。
/// 基础值在 <see cref="Awake"/> 里记下来，之后按等级乘算，所以不会越算越离谱。
/// </summary>
public class BugGrowth : MonoBehaviour
{
    /// <summary>
    /// 「长大到几级才吃得下」的门槛，用的是玩家看到的等级口径（见 <see cref="DisplayLevel"/>）：
    /// 木箱 2 级、树 3 级、村民 4 级（也就是满级）。果子、嫩叶和神奇果实不受限制。
    /// </summary>
    public const int CrateLevel = 2;
    public const int TreeLevel = 3;
    public const int VillagerLevel = 4;

    [Header("等级")]
    public int level;
    public int maxLevel = 3;

    [Header("每级的加成")]
    [Tooltip("体型放大比例（0.35 = 每级 +35%）")]
    public float scalePerLevel = 0.35f;
    [Tooltip("移速加成比例")]
    public float speedPerLevel = 0.12f;
    [Tooltip("捕食范围加成比例")]
    public float eatRangePerLevel = 0.28f;
    [Tooltip("体力上限加成（绝对值）")]
    public float staminaPerLevel = 25f;

    public bool CanGrow { get { return level < maxLevel; } }
    /// <summary>体型倍率（1 = 原始）。</summary>
    public float SizeMultiplier { get { return 1f + level * scalePerLevel; } }
    /// <summary>玩家看到的等级：1 = 还没长大（内部 level = 0），每长大一次 +1。</summary>
    public int DisplayLevel { get { return level + 1; } }

    BugController bug;
    WormBody worm;
    BugEat eat;
    BugVitality vitality;
    Transform headVisual;
    CircleCollider2D bodyCollider;

    float baseHeadScale;
    float baseColliderRadius;
    float baseWalkSpeed;
    float baseEatRadius;
    float baseVitalityMax;

    void Awake()
    {
        bug = GetComponent<BugController>();
        worm = GetComponentInChildren<WormBody>();
        eat = GetComponent<BugEat>();
        vitality = GetComponent<BugVitality>();
        headVisual = transform.Find("Head");
        bodyCollider = GetComponent<CircleCollider2D>();

        if (headVisual != null) baseHeadScale = headVisual.localScale.x;
        if (bodyCollider != null) baseColliderRadius = bodyCollider.radius;
        if (bug != null) baseWalkSpeed = bug.walkSpeed;
        if (eat != null) baseEatRadius = eat.eatRadius;
        if (vitality != null) baseVitalityMax = vitality.BaseMaxStamina;
    }

    /// <summary>长大一级（吃到特殊食物时调用）。返回是否真的长到了。</summary>
    public bool Grow(int levels = 1)
    {
        if (!CanGrow) return false;
        level = Mathf.Min(maxLevel, level + Mathf.Max(1, levels));
        Apply();
        return true;
    }

    /// <summary>读档用：直接设置等级。</summary>
    public void SetLevel(int value)
    {
        level = Mathf.Clamp(value, 0, maxLevel);
        Apply();
    }

    /// <summary>把当前等级的加成套用到体型 / 速度 / 捕食范围 / 体力上限。</summary>
    public void Apply()
    {
        float size = SizeMultiplier;

        // 头部缩放由 WormBody 负责（它每帧都在写头的缩放，交给它才不会被覆盖）
        if (worm != null) worm.ApplyScale(size);
        if (bodyCollider != null) bodyCollider.radius = baseColliderRadius * size;

        if (bug != null) bug.walkSpeed = baseWalkSpeed * (1f + level * speedPerLevel);
        if (eat != null) eat.eatRadius = baseEatRadius * (1f + level * eatRangePerLevel);
        if (vitality != null) vitality.SetMaxStamina(vitality.BaseMaxStamina + level * staminaPerLevel);
    }
}
