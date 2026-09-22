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

    void Apply(float night)
    {
        if (sprite == null) return;
        sprite.color = Color.Lerp(dayColor, nightColor, night);
        if (!Mathf.Approximately(nightScale, 1f))
            transform.localScale = baseScale * Mathf.Lerp(1f, nightScale, night);
    }
}
