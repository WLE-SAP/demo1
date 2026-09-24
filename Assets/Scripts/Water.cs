using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 水源 / 漏水点：默认一直漏（破掉的水缸），也可以设成**断电才漏**
/// （<see cref="leakWhenUnpowered"/> + 同一物体上的 <see cref="PoweredProp"/>，也就是「水泵停了、管子开始漏」）。
///
/// 漏水 = 每 <see cref="leakInterval"/> 秒在脚下生成一个 <see cref="Puddle"/>，
/// 并且广播 <see cref="GameEvent.Leaked"/>。这就是设计文档 §4.2 那条
/// 「腐蚀水管 → 漏水 → 地面变滑 → NPC 路过摔倒」在村庄里的等价物。
/// </summary>
public class WaterSource : MonoBehaviour
{
    [Tooltip("自动开始漏水（破掉的水缸开着这个）")]
    public bool leakOnStart;
    [Tooltip("断电才漏（抽水泵：通电时正常，电一断就开始漏）")]
    public bool leakWhenUnpowered = true;
    [Tooltip("每隔多久积一摊水")]
    public float leakInterval = 0.8f;
    [Tooltip("最多同时积几摊水（防止越积越多）")]
    public int maxPuddles = 4;
    [Tooltip("水洼出现的位置偏移范围")]
    public float spread = 0.9f;

    public bool IsLeaking { get; private set; }

    float nextLeakTime;
    readonly List<Puddle> spawned = new List<Puddle>();
    PoweredProp power;

    void Awake()
    {
        power = GetComponent<PoweredProp>();
        if (leakOnStart) StartLeak();
    }

    void Update()
    {
        if (!IsLeaking)
        {
            // 泵停了才开始漏
            if (leakWhenUnpowered && power != null && !power.HasPower) StartLeak();
            return;
        }

        if (Time.time < nextLeakTime) return;
        nextLeakTime = Time.time + Mathf.Max(0.2f, leakInterval);
        SpawnPuddle();
    }

    /// <summary>开始漏水（被打碎 / 断电都会走到这里）。</summary>
    public void StartLeak()
    {
        if (IsLeaking) return;
        IsLeaking = true;
        nextLeakTime = Time.time;
        GameEvent.RaiseLeaked(transform.position);
        Debug.Log("[Water] " + name + " 开始漏水。");
    }

    /// <summary>停止漏水（留给以后的「修好它」用）。</summary>
    public void StopLeak()
    {
        IsLeaking = false;
    }

    void SpawnPuddle()
    {
        // 先把已经消失的清理掉，再按上限决定要不要新积一摊
        for (int i = spawned.Count - 1; i >= 0; i--)
            if (spawned[i] == null) spawned.RemoveAt(i);
        if (spawned.Count >= maxPuddles) return;

        Vector2 at = (Vector2)transform.position + Random.insideUnitCircle * spread;
        Puddle puddle = Puddle.Spawn(at);
        if (puddle != null) spawned.Add(puddle);
    }
}

/// <summary>
/// 地上的水洼：**村民踩上去会滑倒**（<see cref="Villager.Slip"/>），摔得很响，
/// 于是把附近的人引过来看，还可能把旁边的东西撞翻 —— 这是链 A 里「地面变滑」那一环。
///
/// 只做「地面贴图 + 圆形判定」，没有任何流体模拟（Spec §4.8 的约定）。
/// 水洼属于地面层（画在角色下面），所以不会挡住人。
/// </summary>
public class Puddle : MonoBehaviour
{
    /// <summary>场上所有水洼。</summary>
    public static readonly List<Puddle> All = new List<Puddle>();

    [Tooltip("过多久干掉")]
    public float lifeSeconds = 30f;
    [Tooltip("判定半径（世界单位）")]
    public float radius = 0.9f;
    [Tooltip("踩上去滑倒的概率")]
    [Range(0f, 1f)] public float slipChance = 0.65f;
    [Tooltip("多久检查一次有谁踩上来")]
    public float checkInterval = 0.25f;

    float bornTime;
    float nextCheck;
    SpriteRenderer sprite;

    /// <summary>场上还有几摊水（验证用）。</summary>
    public static int CountAlive()
    {
        int n = 0;
        for (int i = 0; i < All.Count; i++) if (All[i] != null) n++;
        return n;
    }

    /// <summary>造一摊水。</summary>
    public static Puddle Spawn(Vector2 position)
    {
        Sprite sprite = ArtShapes.Get(ArtShape.Round);
        if (sprite == null) return null;

        GameObject go = new GameObject("Puddle");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.32f, 0.46f, 0.62f, 0.55f);
        sr.sortingLayerID = GroundLayerId;              // 和草地/道路同一层，永远在角色下面
        sr.sortingOrder = -820;

        go.transform.localScale = new Vector3(1.5f, 1.1f, 1f);
        return go.AddComponent<Puddle>();
    }

    static int groundLayerId = -1;
    static int GroundLayerId
    {
        get
        {
            if (groundLayerId >= 0) return groundLayerId;
            groundLayerId = 0;                          // 找不到就用 Default，不会出错
            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != "Ground") continue;
                groundLayerId = layers[i].id;
                break;
            }
            return groundLayerId;
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
        bornTime = Time.time;
        sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float age = Time.time - bornTime;
        if (age >= lifeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        // 最后 3 秒淡出（看起来像「干了」）
        if (sprite != null && lifeSeconds - age < 3f)
        {
            Color c = sprite.color;
            c.a = 0.55f * Mathf.Clamp01((lifeSeconds - age) / 3f);
            sprite.color = c;
        }

        if (Time.time < nextCheck) return;
        nextCheck = Time.time + Mathf.Max(0.05f, checkInterval);
        CheckSlip();
    }

    void CheckSlip()
    {
        Vector2 self = transform.position;
        float sqr = radius * radius;
        Villager[] villagers = Villager.All.ToArray();
        for (int i = 0; i < villagers.Length; i++)
        {
            Villager v = villagers[i];
            if (v == null || v.IsFrozen || !v.CanSlip) continue;
            if (((Vector2)v.transform.position - self).sqrMagnitude > sqr) continue;
            if (slipChance < 1f && Random.value > slipChance) continue;

            v.Slip();
        }
    }
}
