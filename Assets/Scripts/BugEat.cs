using System.Collections;
using UnityEngine;

/// <summary>
/// 空格进食：播放啃食动画，并吃掉头部前方最近的食物。
/// </summary>
public class BugEat : MonoBehaviour
{
    public BugController bug;
    public WormBody wormBody;
    public BugVitality vitality;
    public BugGrowth growth;
    [Tooltip("嘴巴锚点（头部前方），用于判定进食范围与食物飞入方向")]
    public Transform mouth;

    [Header("进食判定")]
    [Tooltip("捕食半径：以头部为中心，只要靠近就能吃")]
    public float eatRadius = 0.7f;
    [Tooltip("捕食夹角；360 = 整圈（只要靠近头部就行，不要求朝向）")]
    public float eatAngle = 360f;
    [Tooltip("动画播到这一时刻才真正吃掉食物")]
    public float consumeDelay = 0.22f;
    [Tooltip("两次进食的最短间隔")]
    public float cooldown = 0.28f;

    public int EatenCount { get; private set; }

    /// <summary>读档用：恢复已吃数量。</summary>
    public void RestoreEatenCount(int value)
    {
        EatenCount = Mathf.Max(0, value);
    }

    float nextEatTime;

    void Awake()
    {
        if (bug == null) bug = GetComponent<BugController>();
        if (wormBody == null) wormBody = GetComponentInChildren<WormBody>();
        if (vitality == null) vitality = GetComponent<BugVitality>();
        if (growth == null) growth = GetComponent<BugGrowth>();
        if (mouth == null)
        {
            Transform h = transform.Find("Head");
            if (h != null) mouth = h;
        }
    }

    /// <summary>由 BugController 在按下空格时调用。</summary>
    public bool TryEat()
    {
        if (bug != null && bug.IsHidden) return false;   // 躲在地洞里吃不了东西
        if (Time.time < nextEatTime) return false;
        nextEatTime = Time.time + cooldown;

        if (wormBody != null) wormBody.TriggerEat();

        Edible target = FindTarget();
        if (target != null) StartCoroutine(ConsumeRoutine(target));
        return true;
    }

    /// <summary>当前头前方可吃到的食物（供 HUD / 提示使用）。等级不够的东西不算。</summary>
    public Edible FindTarget()
    {
        return FindNearest(true);
    }

    /// <summary>
    /// 头前方最近的、但**等级还不够**吃的东西（HUD 用来提示「长到几级才吃得下」）；
    /// 够级或附近没东西时返回 null。
    /// </summary>
    public Edible FindLockedTarget()
    {
        Edible near = FindNearest(false);
        return near != null && !CanEat(near) ? near : null;
    }

    /// <summary>小虫当前的「等级」（玩家看到的等级口径）。</summary>
    public int BugLevel { get { return growth != null ? growth.DisplayLevel : 1; } }

    /// <summary>
    /// 等级够不够吃它：物品和 npc 都挂了 <see cref="Edible.requiredLevel"/>，
    /// 没长到那个等级就啃不动（果子嫩叶是 1 级，一开始就能吃）。
    /// </summary>
    public bool CanEat(Edible food)
    {
        return food != null && food.CanBeEatenBy(BugLevel);
    }

    Edible FindNearest(bool respectLevel)
    {
        Vector2 origin = mouth != null ? (Vector2)mouth.position : (Vector2)transform.position;
        Vector2 forward = bug != null ? bug.Facing : Vector2.right;
        float halfAngle = eatAngle * 0.5f;

        Edible best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Edible.All.Count; i++)
        {
            Edible food = Edible.All[i];
            if (food == null || food.IsConsumed) continue;
            if (respectLevel && !CanEat(food)) continue;

            Vector2 delta = (Vector2)food.transform.position - origin;
            float distance = delta.magnitude;
            if (distance > eatRadius) continue;
            if (distance > 0.01f && Vector2.Angle(forward, delta) > halfAngle) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = food;
            }
        }
        return best;
    }

    IEnumerator ConsumeRoutine(Edible food)
    {
        yield return new WaitForSeconds(consumeDelay);

        if (food == null || food.IsConsumed) yield break;

        Vector2 origin = mouth != null ? (Vector2)mouth.position : (Vector2)transform.position;
        if (Vector2.Distance(food.transform.position, origin) > eatRadius * 1.6f) yield break;

        EatenCount++;

        // 吞下去的这一刻出声（没有放 eat 音频就是静音）
        AudioOverridePlayer.Play(AudioKeys.Eat);

        // 有些东西吃下去能获得能力（旧电池 → 电击、破布团 → 伪装……）
        AbilitySet.GrantFromPickup(food.GetComponent<AbilityPickup>());

        // 吃掉的是村民（后面的对话/连锁都靠这个事件知道）
        Villager victim = food.GetComponent<Villager>();

        // 吞东西是有动静的（村民听得见）：吃村民比吃果子响得多（那是出了大事）
        Vector2 noiseAt = mouth != null ? (Vector2)mouth.position : (Vector2)transform.position;
        GameEvent.RaiseNoise(noiseAt, victim != null ? GameEvent.DropLoudness : GameEvent.EatLoudness, NoiseKind.Eat);
        GameEvent.RaiseEaten(food, noiseAt, victim != null);

        // 恢复体力：不同食物（隐藏数值「分量」不同）恢复的量不一样
        if (vitality != null) vitality.AddStamina(food.SatietyAmount);

        // 特殊食物：长大一级（更大、更快、吃得更远、体力上限更高）
        if (food.growth > 0 && growth != null && growth.Grow(food.growth))
        {
            AudioOverridePlayer.Play(AudioKeys.Grow);
            Debug.Log("[Bug] 长大到 " + growth.DisplayLevel + " 级（体型 ×" + growth.SizeMultiplier.ToString("F2") + "）");
        }

        // 现场看到这一幕的村民从此会躲着小虫
        if (victim != null) Villager.ReportEaten(victim);

        food.Consume(mouth);
    }
}
