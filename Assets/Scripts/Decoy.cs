using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分裂出来的「假小虫」：在小范围里乱窜，并且**每隔一会儿就发出一次噪音**。
///
/// 它身上没有任何战斗 / 碰撞逻辑 —— 作用就是**把村民引走**：
/// 村民会去调查它发出的声音（走 <see cref="GameEvent"/> 的噪音 → 步骤① 的调查状态机），
/// 到了地方什么也找不到，就白跑一趟。所以这个「分身」是纯干扰道具，
/// 不需要改 <see cref="Villager.CanSeeBug"/>（真小虫和假小虫不会混）。
///
/// 外观用 <see cref="ArtShapes"/> 程序化拼（小圆头 + 短身子），**不占美术 key**：
/// 它是运行时特效，不是场景里的物件。
/// </summary>
public class Decoy : MonoBehaviour
{
    /// <summary>场上所有假小虫（验证与「还剩几只」用）。</summary>
    public static readonly List<Decoy> All = new List<Decoy>();

    [Tooltip("活多久（秒）")]
    public float lifeSeconds = 8f;
    [Tooltip("乱窜的速度")]
    public float speed = 2.4f;
    [Tooltip("围着出生点多大范围乱窜")]
    public float wanderRadius = 4f;
    [Tooltip("每隔多久出一次声")]
    public float noiseInterval = 0.7f;
    [Tooltip("出声的响度（和玩家冲刺同一档：能把附近的人引过来）")]
    public float noiseLoudness = 0.6f;
    [Tooltip("出声位置的随机偏移，免得村民像追着定位器一样精准")]
    public float noiseJitter = 0.6f;

    Vector2 home;
    Vector2 target;
    float bornTime;
    float nextNoiseTime;
    float bobPhase;
    Transform visual;
    SpriteRenderer[] visualRenderers;
    Color[] visualColors;

    /// <summary>造一只假小虫；<paramref name="position"/> 是出生位置（世界坐标）。</summary>
    public static Decoy Spawn(Vector2 position, float lifeSeconds)
    {
        GameObject go = new GameObject("Decoy");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        Decoy decoy = go.AddComponent<Decoy>();
        decoy.lifeSeconds = Mathf.Max(0.5f, lifeSeconds);
        go.AddComponent<YSort>();      // 和别的 YSort 对象一样按世界 Y 排序
        return decoy;
    }

    /// <summary>场上还剩几只（验证用）。</summary>
    public static int CountAlive()
    {
        int n = 0;
        for (int i = 0; i < All.Count; i++) if (All[i] != null) n++;
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
        home = transform.position;
        bornTime = Time.time;
        nextNoiseTime = Time.time + Random.Range(0.05f, 0.3f);
        BuildVisual();
        PickTarget();
    }

    void Update()
    {
        // 到寿了：缩一下再消失（别「啪」地凭空不见）
        float age = Time.time - bornTime;
        if (age >= lifeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 self = transform.position;
        Vector2 toTarget = target - self;
        if (toTarget.magnitude <= 0.18f) PickTarget();
        else transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Time.time >= nextNoiseTime)
        {
            nextNoiseTime = Time.time + noiseInterval * Random.Range(0.8f, 1.25f);
            Vector2 at = (Vector2)transform.position + Random.insideUnitCircle * noiseJitter;
            GameEvent.RaiseNoise(at, noiseLoudness, NoiseKind.Dash);
        }

        UpdateVisual();
    }

    void PickTarget()
    {
        target = home + Random.insideUnitCircle * wanderRadius;
    }

    void BuildVisual()
    {
        // 「小圆当头 + 短身子」——和真小虫一个画风，但小一号，一眼看出是个假货
        GameObject root = new GameObject("Visual");
        root.transform.SetParent(transform, false);
        visual = root.transform;

        Color body = new Color(0.22f, 0.20f, 0.18f, 0.95f);
        Color head = new Color(0.30f, 0.27f, 0.23f);

        ArtShapes.AddSprite(visual, "Body", ArtShape.Rect, 0.10f, new Vector2(0f, -0.07f), body, 0);
        ArtShapes.AddSprite(visual, "Head", ArtShape.Round, 0.15f, new Vector2(0f, 0.04f), head, 1);

        // 淡出要改 alpha，所以先把「原本的颜色」记下来，否则每帧相乘会越淡越黑
        visualRenderers = visual.GetComponentsInChildren<SpriteRenderer>();
        visualColors = new Color[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
            visualColors[i] = visualRenderers[i] != null ? visualRenderers[i].color : Color.white;
    }

    void UpdateVisual()
    {
        if (visual == null) return;

        bobPhase += Time.deltaTime * 11f;
        visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.03f, 0f);

        // 最后半秒一边缩一边淡出
        float left = lifeSeconds - (Time.time - bornTime);
        float k = left < 0.5f ? Mathf.Clamp01(left / 0.5f) : 1f;
        visual.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);

        SpriteRenderer[] renderers = visualRenderers;
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = visualColors[i];
                c.a *= k;                                   // 基于原色淡出，不是每帧相乘
                renderers[i].color = c;
            }
        }
    }
}
