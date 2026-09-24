using UnityEngine;

/// <summary>
/// 夜里亮、白天暗的东西（路灯、窗户）：按 <see cref="VillageClock.Night01"/> 在
/// <see cref="dayColor"/> 与 <see cref="nightColor"/> 之间插值。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NightGlow : MonoBehaviour
{
    [Tooltip("白天的颜色（想让白天看不见就把 alpha 设成 0）")]
    public Color dayColor = new Color(1f, 0.92f, 0.62f, 0f);

    [Tooltip("夜里的颜色")]
    public Color nightColor = new Color(1f, 0.88f, 0.52f, 0.95f);

    [Tooltip("夜里比白天放大多少（1 = 不变）")]
    public float nightScale = 1f;

    SpriteRenderer sprite;
    Vector3 baseScale;
    VillageClock clock;
    /// <summary>闪到什么时候（电击 / 抖动期间灯会乱闪）。</summary>
    float flickerUntil;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        clock = VillageClock.Instance != null ? VillageClock.Instance : FindObjectOfType<VillageClock>();
        Apply(clock != null ? clock.Night01 : 0f);
    }

    void Update()
    {
        Apply(clock != null ? clock.Night01 : 0f);
    }

    /// <summary>
    /// 闪一下（电击 / 抖动时用）：<paramref name="seconds"/> 秒内颜色在「亮」和「灭」之间乱跳，
    /// 结束后自动回到按昼夜算出来的颜色。多次调用取更长的那次。
    /// </summary>
    public void Flicker(float seconds)
    {
        flickerUntil = Mathf.Max(flickerUntil, Time.time + Mathf.Max(0.05f, seconds));
    }

    /// <summary>现在是不是正在闪（验证用）。</summary>
    public bool IsFlickering { get { return Time.time < flickerUntil; } }

    void Apply(float night)
    {
        if (sprite == null) return;

        Color color = Color.Lerp(dayColor, nightColor, night);

        if (Time.time < flickerUntil)
        {
            // 正弦 + 柏林噪声：不是规律地闪，看起来才像「电路出问题了」
            float wave = Mathf.Sin(Time.time * 57f) * 0.5f + 0.5f;
            float noise = Mathf.PerlinNoise(Time.time * 33f, 0.37f);
            float on = Mathf.Clamp01(wave * 0.7f + noise * 0.3f);
            Color lit = new Color(1f, 0.98f, 0.86f, Mathf.Max(color.a, 0.85f));
            color = Color.Lerp(color, lit, on);
        }

        sprite.color = color;
        if (!Mathf.Approximately(nightScale, 1f))
            transform.localScale = baseScale * Mathf.Lerp(1f, nightScale, night);
    }
}
