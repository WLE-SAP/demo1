using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可破坏物（木箱 / 水缸 / 木桶 / 油桶…）：被打、被撞、被腐蚀、被吃掉都会碎。
///
/// - 碎的瞬间：掉一地**碎片**（程序化小图元，不占美术 key）+ 响度 2.0 的噪音 + 广播
///   <see cref="GameEvent.Broken"/>（以后的混乱值 / 任务靠它计数）；
/// - 碎掉之后**不销毁自己**，而是关掉外观与碰撞、把控制权交给身上的别的组件：
///   <see cref="WaterSource"/>（碎了就漏水）、<see cref="Explosive"/>（碎了就炸）、
///   <see cref="ElectricWire"/>（碎了就断电）。都没有的话 `hideSeconds` 秒后销毁。
/// - 撞击破碎是可选的（<see cref="breakImpactSpeed"/>）：默认关闭，免得村民日常走动就把村子撞烂。
/// </summary>
public class Breakable : MonoBehaviour
{
    /// <summary>场上所有没碎的可破坏物（验证 / 调试用）。</summary>
    public static readonly List<Breakable> All = new List<Breakable>();

    [Header("耐久")]
    [Tooltip("要挨多少「力度」才碎（被吃掉算 1 点、爆炸算 3 点、腐蚀直接算碎）")]
    public float hp = 1f;

    [Header("碎裂表现")]
    [Tooltip("掉几块碎片")]
    public int debrisCount = 6;
    [Tooltip("碎片飞出去的散布半径")]
    public float debrisSpread = 0.8f;
    [Tooltip("碎片多大（世界单位）")]
    public float debrisSize = 0.12f;
    [Tooltip("碎片颜色（默认取本体颜色）")]
    public Color debrisColor = new Color(0.62f, 0.47f, 0.30f);
    [Tooltip("碎片多久后消失")]
    public float debrisSeconds = 1.4f;

    [Header("碎掉之后")]
    [Tooltip("碎裂的响度")]
    public float noiseLoudness = GameEvent.BreakLoudness;
    [Tooltip("没有别的组件接管时，多久后销毁自己（秒）")]
    public float hideSeconds = 0.4f;
    [Tooltip("撞上来多快就会碎（0 = 不会被撞碎）")]
    public float breakImpactSpeed = 0f;

    public bool IsBroken { get; private set; }

    SpriteRenderer[] renderers;
    Collider2D[] colliders;

    /// <summary>场上还有几个没碎的（验证用）。</summary>
    public static int CountAlive()
    {
        int n = 0;
        for (int i = 0; i < All.Count; i++) if (All[i] != null && !All[i].IsBroken) n++;
        return n;
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
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    /// <summary>挨一下。<paramref name="power"/> 够大（≥ 当前耐久）就碎。</summary>
    public bool TakeHit(float power)
    {
        if (IsBroken || power <= 0f) return false;

        hp -= power;
        if (hp > 0f) return false;

        Break();
        return true;
    }

    /// <summary>打碎（腐蚀 / 爆炸 / 撞碎都直接调它）。</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        Vector2 at = transform.position;
        SpawnDebris(at);

        // 关掉外观与碰撞：东西还在（可能还有水管 / 爆炸要接着演），但已经"不在了"
        if (renderers != null)
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = false;
        if (colliders != null)
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = false;

        // 让身上的其它组件接着演（这些是"关系"，见 Spec §4.8）
        bool handedOver = false;
        WaterSource water = GetComponent<WaterSource>();
        if (water != null) { water.StartLeak(); handedOver = true; }
        Explosive explosive = GetComponent<Explosive>();
        if (explosive != null) { explosive.Detonate(); handedOver = true; }
        ElectricWire wire = GetComponent<ElectricWire>();
        if (wire != null) { wire.CutPower(); handedOver = true; }

        GameEvent.RaiseNoise(at, noiseLoudness, NoiseKind.Break);
        GameEvent.RaiseBroken(gameObject, at);
        AudioOverridePlayer.Play(AudioKeys.Break);      // 没放音频就是安静的

        if (!handedOver) Destroy(gameObject, Mathf.Max(0.05f, hideSeconds));
    }

    void SpawnDebris(Vector2 at)
    {
        Sprite sprite = ArtShapes.Get(ArtShape.Rect);
        if (sprite == null) return;

        for (int i = 0; i < Mathf.Max(0, debrisCount); i++)
        {
            GameObject go = new GameObject("Debris");
            go.transform.SetParent(null, true);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = Vector3.one * debrisSize * Random.Range(0.7f, 1.4f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = debrisColor;
            sr.sortingOrder = SpawnKit.YOrder(at.y) + 2;

            Debris debris = go.AddComponent<Debris>();
            Vector2 dir = Random.insideUnitCircle.normalized;
            debris.Launch(dir * Random.Range(1.2f, 2.6f), debrisSeconds * Random.Range(0.8f, 1.2f));
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (breakImpactSpeed <= 0f || IsBroken) return;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < breakImpactSpeed) return;
        Break();
    }
}

/// <summary>
/// 一块碎片：飞出去、转两下、淡出消失。纯表现，不参与任何逻辑
/// （不挂碰撞体，免得村民踩到一地碎屑还会卡住）。
/// </summary>
public class Debris : MonoBehaviour
{
    Vector2 velocity;
    float life;
    float bornTime;
    SpriteRenderer sprite;
    float spin;

    /// <summary>给它一个初速度并设定存活时长。</summary>
    public void Launch(Vector2 initialVelocity, float lifeSeconds)
    {
        velocity = initialVelocity;
        life = Mathf.Max(0.1f, lifeSeconds);
        bornTime = Time.time;
        spin = Random.Range(-540f, 540f);
        sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float age = Time.time - bornTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        velocity = Vector2.Lerp(velocity, Vector2.zero, 4.5f * Time.deltaTime);
        transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.deltaTime;
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = Mathf.Clamp01(1f - age / life);
            sprite.color = c;
        }
    }
}
