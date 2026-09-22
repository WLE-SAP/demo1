using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一条黑色曲线身体：用 LineRenderer 跟随头部轨迹（链式跟随），叠加横向正弦摆动。
/// 摆动幅度/频率由移动速度驱动，捕食时切换为「咬合」节奏并让身体收紧。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class WormBody : MonoBehaviour
{
    [Tooltip("头部（角色根节点），曲线从它身后拖出")]
    public Transform head;

    [Header("身体")]
    public int pointCount = 26;
    [Tooltip("相邻采样点的间距")]
    public float spacing = 0.14f;
    [Tooltip("身体粗细（全程等宽）")]
    public float width = 0.38f;

    [Header("摆动")]
    public float idleAmplitude = 0.045f;
    public float idleFrequency = 1.6f;
    public float walkAmplitude = 0.16f;
    public float walkFrequency = 3.4f;
    [Tooltip("摆动波长（世界单位）")]
    public float waveLength = 1.9f;
    public float smoothSpeed = 8f;

    [Header("捕食")]
    public float eatDuration = 0.6f;
    public float eatAmplitude = 0.22f;
    [Tooltip("捕食时身体收紧的比例")]
    [Range(0f, 0.8f)] public float eatCoil = 0.4f;
    public float eatHeadScale = 1.4f;

    LineRenderer line;
    Transform headVisual;
    Vector3[] buffer;
    readonly List<Vector2> chain = new List<Vector2>();

    float speed01;
    float phase;
    float amplitude;
    float frequency;
    float spacingMul = 1f;
    float eatTimer;
    float baseHeadScale = 1f;
    float sizeMultiplier = 1f;

    // 基础尺寸：小虫长大时按这些值乘算，避免反复乘同一份数据越算越大
    float baseWidth;
    float baseSpacing;
    float baseIdleAmplitude;
    float baseWalkAmplitude;
    float baseEatAmplitude;
    float baseWaveLength;

    public bool IsEating { get { return eatTimer > 0f; } }

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (head == null) head = transform.root;
        headVisual = head.Find("Head");

        baseWidth = width;
        baseSpacing = spacing;
        baseIdleAmplitude = idleAmplitude;
        baseWalkAmplitude = walkAmplitude;
        baseEatAmplitude = eatAmplitude;
        baseWaveLength = waveLength;

        // 头部基准缩放要在 Awake 里取（此时还没长过），否则会被成长后的值当成基准、反复放大
        if (headVisual != null) baseHeadScale = headVisual.localScale.x;
    }

    void Start()
    {
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.positionCount = pointCount;
        // 等宽曲线：从头到尾粗细一致
        line.widthMultiplier = width;
        line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        buffer = new Vector3[pointCount];
        amplitude = idleAmplitude;
        frequency = idleFrequency;

        if (headVisual != null) baseHeadScale = headVisual.localScale.x;

        // 初始时身体笔直地拖在头后面
        Vector2 start = head.position;
        Vector2 behind = -head.right;
        chain.Clear();
        for (int i = 0; i < pointCount; i++) chain.Add(start + behind * (i * spacing));
        WriteLine();
    }

    void Update()
    {
        if (line != null && !line.enabled) return;   // 躲进地洞时整条尾巴不画

        float dt = Time.deltaTime;
        bool eating = eatTimer > 0f;
        if (eating) eatTimer -= dt;

        float ampTarget = eating ? eatAmplitude : Mathf.Lerp(idleAmplitude, walkAmplitude, speed01);
        float freqTarget = eating ? walkFrequency * 1.7f : Mathf.Lerp(idleFrequency, walkFrequency, speed01);
        float spacingTarget = eating ? 1f - eatCoil * Chomp() : 1f;

        float k = 1f - Mathf.Exp(-smoothSpeed * dt);
        amplitude = Mathf.Lerp(amplitude, ampTarget, k);
        frequency = Mathf.Lerp(frequency, freqTarget, k);
        spacingMul = Mathf.Lerp(spacingMul, spacingTarget, k);

        phase += frequency * Mathf.PI * 2f * dt;
        if (phase > Mathf.PI * 200f) phase -= Mathf.PI * 200f;

        UpdateChain();
        WriteLine();

        if (headVisual != null)
        {
            float s = baseHeadScale * sizeMultiplier * (1f + (eatHeadScale - 1f) * Chomp());
            headVisual.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>捕食进度 0..1。</summary>
    float EatProgress()
    {
        return eatDuration <= 0f ? 1f : Mathf.Clamp01(1f - eatTimer / eatDuration);
    }

    /// <summary>咬合节奏：一次捕食内咬两下。</summary>
    float Chomp()
    {
        return IsEating ? Mathf.Abs(Mathf.Sin(EatProgress() * Mathf.PI * 2f)) : 0f;
    }

    void UpdateChain()
    {
        if (chain.Count != pointCount)
        {
            chain.Clear();
            Vector2 s = head.position;
            for (int i = 0; i < pointCount; i++) chain.Add(s - (Vector2)head.right * (i * spacing));
        }

        chain[0] = head.position;
        float step = Mathf.Max(0.01f, spacing * spacingMul);
        for (int i = 1; i < chain.Count; i++)
        {
            Vector2 prev = chain[i - 1];
            Vector2 d = chain[i] - prev;
            float dist = d.magnitude;
            if (dist < 1e-5f) d = -((Vector2)head.right);
            else d /= dist;
            chain[i] = prev + d * step;
        }
    }

    void WriteLine()
    {
        for (int i = 0; i < pointCount; i++)
        {
            Vector2 tangent;
            if (i == 0) tangent = chain[1] - chain[0];
            else if (i == pointCount - 1) tangent = chain[i] - chain[i - 1];
            else tangent = chain[i + 1] - chain[i - 1];

            if (tangent.sqrMagnitude < 1e-8f) tangent = Vector2.right;
            tangent.Normalize();
            Vector2 normal = new Vector2(-tangent.y, tangent.x);

            float t = pointCount > 1 ? i / (float)(pointCount - 1) : 0f;
            // 两端摆动收一点，避免头部起步处偏摆（与粗细无关）
            float waveFade = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.06f));
            float wave = Mathf.Sin(phase - i * (spacing * Mathf.PI * 2f / waveLength)) * waveFade;

            Vector2 p = chain[i] + normal * (amplitude * wave);
            buffer[i] = new Vector3(p.x, p.y, 0f);
        }
        line.SetPositions(buffer);
    }

    /// <summary>0 = 静止，1 = 全速移动。</summary>
    public void SetLocomotion(float value)
    {
        speed01 = Mathf.Clamp01(value);
    }

    /// <summary>整条尾巴是否显示（钻地洞时隐藏）。</summary>
    public void SetVisible(bool visible)
    {
        if (line != null) line.enabled = visible;
    }

    /// <summary>
    /// 整体缩放（小虫长大时用）：粗细、节距、摆幅、波长一起乘（基准值在 Awake 里记下，不会越乘越大）。
    /// 会把新的粗细写回 LineRenderer，所以运行中改也立刻生效。
    /// </summary>
    public void ApplyScale(float multiplier)
    {
        if (multiplier <= 0.0001f) return;

        sizeMultiplier = multiplier;
        width = baseWidth * multiplier;
        spacing = baseSpacing * multiplier;
        idleAmplitude = baseIdleAmplitude * multiplier;
        walkAmplitude = baseWalkAmplitude * multiplier;
        eatAmplitude = baseEatAmplitude * multiplier;
        waveLength = baseWaveLength * multiplier;

        if (line != null) line.widthMultiplier = width;
    }

    /// <summary>当前体型倍率（1 = 原始）。</summary>
    public float SizeMultiplier { get { return sizeMultiplier; } }

    public void TriggerEat()
    {
        eatTimer = eatDuration;
    }
}
