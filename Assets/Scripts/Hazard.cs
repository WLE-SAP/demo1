using UnityEngine;

/// <summary>
/// 油桶：**被电击 / 被打碎就炸**。爆炸不是「伤害」（村民没有血量，Spec §4.8 的约定），
/// 而是设计文档 §10.2 那种「因果严重失衡」的物理喜剧：
///
/// - 把范围内的可搬物品全部震飞（<see cref="Draggable.Scatter"/>，冲量比抖动大得多）；
/// - 把范围内的村民吓得四散（<see cref="Villager.Panic"/>）；
/// - 顺带**引爆 / 触发附近的 <see cref="Alarm"/>**（警报一响，全村撤离 —— 链 B 的最后一环）；
/// - 广播全场最响的噪音（<see cref="GameEvent.ExplosionLoudness"/> = 3.0），
///   半径外的村民也会被吸过来看热闹。
/// </summary>
public class Explosive : MonoBehaviour
{
    [Header("爆炸范围")]
    [Tooltip("震飞东西的半径")]
    public float blastRadius = 4.5f;
    [Tooltip("震飞的冲量（比抖动能力大得多）")]
    public float blastImpulse = 11f;
    [Tooltip("吓跑村民的半径")]
    public float panicRadius = 6f;
    [Tooltip("被吓跑多久")]
    public float panicSeconds = 6f;
    [Tooltip("震动后多久把东西收回「固定不动」")]
    public float settleSeconds = 1.1f;

    [Header("连带效果")]
    [Tooltip("点亮 / 触发这个半径内的警报器")]
    public float alarmRadius = 7f;
    [Tooltip("爆炸时灯也会乱闪")]
    public float flickerRadius = 9f;

    public bool Exploded { get; private set; }

    /// <summary>爆炸（被电击 / 被打碎 / 被别的爆炸波及都会走到这里）。</summary>
    public void Detonate()
    {
        if (Exploded) return;
        Exploded = true;

        Vector2 self = transform.position;

        int flung = Draggable.Scatter(self, blastRadius, blastImpulse, settleSeconds);

        int panicked = 0;
        float panicSqr = panicRadius * panicRadius;
        Villager[] villagers = Villager.All.ToArray();
        for (int i = 0; i < villagers.Length; i++)
        {
            Villager v = villagers[i];
            if (v == null || v.IsFrozen) continue;
            if (((Vector2)v.transform.position - self).sqrMagnitude > panicSqr) continue;
            v.Panic(panicSeconds);
            panicked++;
        }

        // 波及警报器（链 B：爆炸 → 警报 → 全村撤离）
        int alarms = 0;
        float alarmSqr = alarmRadius * alarmRadius;
        Alarm[] all = FindObjectsOfType<Alarm>();
        for (int i = 0; i < all.Length; i++)
        {
            Alarm alarm = all[i];
            if (alarm == null || alarm.IsRinging) continue;
            if (((Vector2)alarm.transform.position - self).sqrMagnitude > alarmSqr) continue;
            alarm.Raise();
            alarms++;
        }

        float flickerSqr = flickerRadius * flickerRadius;
        NightGlow[] glows = FindObjectsOfType<NightGlow>();
        for (int i = 0; i < glows.Length; i++)
        {
            if (glows[i] == null) continue;
            if (((Vector2)glows[i].transform.position - self).sqrMagnitude > flickerSqr) continue;
            glows[i].Flicker(0.8f);
        }

        GameEvent.RaiseNoise(self, GameEvent.ExplosionLoudness, NoiseKind.Break);

        // 炸完自己就没了（桶也不见了）
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = false;
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;

        Debug.Log("[Hazard] 油桶爆炸：震飞 " + flung + " 个东西、吓跑 " + panicked + " 个村民、触发 "
            + alarms + " 个警报。");

        Destroy(gameObject, 0.1f);
    }
}

/// <summary>
/// 警报器：一响，**视野内的村民全部放下手里的事往外跑**（设计文档 §6/§8 的「NPC 开始搜索 / 封锁」
/// 在这里先做成最直接的一档：全村撤离）。
///
/// 触发方式：被电击（<see cref="AbilitySet"/> 的 CastShock 会遍历附近的警报器）、
/// 被爆炸波及（<see cref="Explosive"/>）、或者被腐蚀/打碎（接在 <see cref="Breakable"/> 里）。
/// </summary>
public class Alarm : MonoBehaviour
{
    [Header("响多久")]
    public float ringSeconds = 10f;

    [Header("撤离范围")]
    [Tooltip("这个半径内的村民全部往外跑")]
    public float panicRadius = 18f;
    [Tooltip("跑了多久才敢停下来")]
    public float panicSeconds = 10f;

    [Header("表现")]
    [Tooltip("响的时候灯会乱闪")]
    public float flickerRadius = 16f;

    public bool IsRinging { get { return Time.time < ringingUntil; } }
    float ringingUntil;
    SpriteRenderer sprite;
    Color baseColor;

    void Awake()
    {
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) baseColor = sprite.color;
    }

    void Update()
    {
        if (sprite == null) return;
        if (!IsRinging)
        {
            sprite.color = baseColor;
            return;
        }
        // 一红一白地闪，隔着半个村子也看得见
        float t = Mathf.Sin(Time.time * 16f) * 0.5f + 0.5f;
        sprite.color = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.3f), t);
    }

    /// <summary>拉响警报。返回这次是不是第一次响。</summary>
    public bool Raise()
    {
        bool first = !IsRinging;
        ringingUntil = Time.time + Mathf.Max(1f, ringSeconds);

        if (!first) return false;

        Vector2 self = transform.position;
        float sqr = panicRadius * panicRadius;
        int panicked = 0;
        Villager[] villagers = Villager.All.ToArray();
        for (int i = 0; i < villagers.Length; i++)
        {
            Villager v = villagers[i];
            if (v == null || v.IsFrozen) continue;
            if (((Vector2)v.transform.position - self).sqrMagnitude > sqr) continue;
            v.Panic(panicSeconds);
            panicked++;
        }

        float flickerSqr = flickerRadius * flickerRadius;
        NightGlow[] glows = FindObjectsOfType<NightGlow>();
        for (int i = 0; i < glows.Length; i++)
        {
            if (glows[i] == null) continue;
            if (((Vector2)glows[i].transform.position - self).sqrMagnitude > flickerSqr) continue;
            glows[i].Flicker(ringSeconds);
        }

        GameEvent.RaiseNoise(self, GameEvent.ExplosionLoudness, NoiseKind.Shock);
        GameEvent.RaiseAlarm(self);
        AudioOverridePlayer.Play(AudioKeys.Alarm);      // 没放音频就是安静的
        Debug.Log("[Hazard] 警报响起：附近 " + panicked + " 个村民撤离。");
        return true;
    }
}
