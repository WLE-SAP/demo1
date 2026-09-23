using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可被吃掉的food标记。被吃掉时缩小并飞向嘴巴后销毁。
/// </summary>
public class Edible : MonoBehaviour
{
    /// <summary>场上所有未被吃掉的食物，供捕食逻辑就近查找。</summary>
    public static readonly List<Edible> All = new List<Edible>();

    [Tooltip("吃掉后获得的营养/分数")]
    public int nutrition = 1;

    [Tooltip("吃掉恢复多少体力；-1 = 按隐藏数值（分量）自动算")]
    public int satiety = -1;

    [Tooltip("吃掉让小虫长大几级（特殊食物 > 0）")]
    public int growth;

    [Tooltip("需要小虫长到几级才吃得下（玩家看到的等级口径：1 = 还没长大，每长一次 +1；0 = 不限）")]
    public int requiredLevel;

    public bool IsConsumed { get; private set; }

    /// <summary>小虫这个等级（<see cref="BugGrowth.DisplayLevel"/> 的口径）吃不吃得下它。</summary>
    public bool CanBeEatenBy(int bugLevel)
    {
        return bugLevel >= requiredLevel;
    }

    /// <summary>这个食物恢复体力的量：没单独指定就按隐藏数值（分量）算。</summary>
    public int SatietyAmount
    {
        get { return satiety >= 0 ? satiety : Mathf.Max(1, HiddenValue.Of(gameObject, 8)); }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public void Consume(Transform mouth)
    {
        if (IsConsumed) return;
        IsConsumed = true;
        All.Remove(this);
        StartCoroutine(ConsumeRoutine(mouth));
    }

    IEnumerator ConsumeRoutine(Transform mouth)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.position;

        const float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float ease = k * k;
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.05f, ease);
            if (mouth != null) transform.position = Vector3.Lerp(startPosition, mouth.position, ease);
            yield return null;
        }

        Destroy(gameObject);
    }
}
