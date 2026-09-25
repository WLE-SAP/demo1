using UnityEngine;

/// <summary>
/// 可被 F 拾取 / 搬运的物品标记。
/// </summary>
public class Draggable : MonoBehaviour
{
    [Tooltip("重量：数值越大拖动越迟滞")]
    public float weight = 1f;

    [Tooltip("搬着它走路的速度倍率（越小越慢）：<= 0 = 用 BugController 上的默认值。"
        + "用来做「越大的石头搬起来越慢」这种逐件手感")]
    public float speedMultiplier = -1f;

    /// <summary>被震飞后多久收回「固定不动」的默认时长。</summary>
    public const float DefaultSettleSeconds = 0.9f;

    /// <summary>
    /// 把 <paramref name="center"/> 附近的可搬物品震飞（**抖动能力 / 摔跤 / 爆炸 都走这里**，
    /// 不要在各自的地方再抄一份）。返回推了几个。
    ///
    /// 为什么这么绕（红线 20）：可搬物品平时是 **`RigidbodyType2D.Kinematic`**（只由
    /// <see cref="DragController"/> 直接写位置，不会自己动），而**运动学刚体完全不吃 `AddForce` / `velocity`**。
    /// 所以要：① 临时切成 `Dynamic`；② `WakeUp()`（睡眠中的刚体也忽略力的写入）；③ 加冲量；
    /// ④ 由物品自己起协程，<paramref name="settleSeconds"/> 秒后把速度归零、切回 `Kinematic`
    /// —— 不收回去的话村庄会被越推越乱，木箱原本的手感也没了。
    /// </summary>
    public static int Scatter(Vector2 center, float radius, float impulse, float settleSeconds = DefaultSettleSeconds)
    {
        float sqr = radius * radius;
        int flung = 0;
        Draggable[] all = FindObjectsOfType<Draggable>();
        for (int i = 0; i < all.Length; i++)
        {
            Draggable d = all[i];
            if (d == null) continue;

            Vector2 delta = (Vector2)d.transform.position - center;
            if (delta.sqrMagnitude > sqr) continue;

            Rigidbody2D body = d.GetComponent<Rigidbody2D>();
            if (body == null || !body.simulated) continue;            // 正被人搬在手里的不推
            if (body.bodyType == RigidbodyType2D.Dynamic) continue;   // 已经在飞了

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.drag = Mathf.Max(body.drag, 2.5f);                  // 加点阻尼，滑一段就停
            body.WakeUp();

            Vector2 push = delta.sqrMagnitude > 0.01f ? delta.normalized : Random.insideUnitCircle.normalized;
            push = (push + Random.insideUnitCircle * 0.6f).normalized;
            body.AddForce(push * impulse, ForceMode2D.Impulse);

            d.StartCoroutine(d.SettleLater(settleSeconds));
            flung++;
        }
        return flung;
    }

    /// <summary>飞一段之后停下来，并收回「运动学 + 速度归零」的常态。</summary>
    System.Collections.IEnumerator SettleLater(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null || !body.simulated) yield break;            // 中途被搬起来 / 被销毁了
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
    }
}
