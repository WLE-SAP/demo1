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

    /// <summary>当前头前方可吃到的食物（供 HUD / 提示使用）。</summary>
    public Edible FindTarget()
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

        // 恢复体力：不同食物（隐藏数值「分量」不同）恢复的量不一样
        if (vitality != null) vitality.AddStamina(food.SatietyAmount);

        // 特殊食物：长大一级（更大、更快、吃得更远、体力上限更高）
        if (food.growth > 0 && growth != null && growth.Grow(food.growth))
            Debug.Log("[Bug] 长大到 " + (growth.level + 1) + " 级（体型 ×" + growth.SizeMultiplier.ToString("F2") + "）");

        food.Consume(mouth);
    }
}
