using UnityEngine;

/// <summary>
/// 钟楼：**每到整点敲一次钟**（用游戏内时间 <see cref="VillageClock"/>，240 秒 = 一天，所以约 10 秒一响）。
///
/// 敲钟做三件事，全部复用已有系统、不新造轮子：
/// <list type="number">
/// <item>放 <see cref="AudioKeys.Bell"/> 音效（没放音频文件就是静音，不报错）；</item>
/// <item>广播一次**很响的噪音**（<see cref="NoiseKind.Bell"/>）—— 附近村民会「听到钟声」抬头张望，
///       这是步骤① 听觉系统的现成复用，不用另写一套反应；</item>
/// <item>钟亮一下（<see cref="halo"/> 的光晕淡出），让玩家看得出**是这口钟在响**。</item>
/// </list>
///
/// 只有城市 / 少数农村有钟楼（见 <see cref="WorldBiome"/> 的 <c>bellTowerChance</c>），
/// 所以「听到钟声 = 附近有座城」也是一种导航线索。
/// </summary>
public class BellTower : MonoBehaviour
{
    [Header("敲钟")]
    [Tooltip("响度：村民的听觉半径 = hearingBase × 这个值 × 好奇倍率")]
    public float loudness = GameEvent.BellLoudness;
    [Tooltip("钟声之间的间隔（游戏内小时）")]
    public float ringEveryHours = 1f;

    [Header("表现")]
    [Tooltip("钟声的光晕（留空则只发声不出光）")]
    public SpriteRenderer halo;
    [Tooltip("光晕亮多久（秒）")]
    public float flashSeconds = 1.4f;

    VillageClock clock;
    float nextRingHour;
    float flashUntil;

    void Awake()
    {
        clock = VillageClock.Instance != null ? VillageClock.Instance : FindObjectOfType<VillageClock>();
        if (clock != null) nextRingHour = Mathf.Floor(clock.TotalHours / ringEveryHours) * ringEveryHours + ringEveryHours;

        ApplyHalo(0f);
    }

    void Update()
    {
        if (clock == null) return;

        // 整点到了就敲一次（游戏里 240 秒一天 → 每小时 10 秒）
        if (clock.TotalHours >= nextRingHour)
        {
            nextRingHour = Mathf.Floor(clock.TotalHours / ringEveryHours) * ringEveryHours + ringEveryHours;
            Ring();
        }

        if (halo == null) return;
        float left = Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(0.05f, flashSeconds));
        ApplyHalo(left);
    }

    /// <summary>敲一次钟（也会被外部 / 测试直接调用）。</summary>
    public void Ring()
    {
        AudioOverridePlayer.Play(AudioKeys.Bell);
        GameEvent.RaiseNoise(transform.position, loudness, NoiseKind.Bell);
        flashUntil = Time.time + Mathf.Max(0.05f, flashSeconds);
    }

    /// <summary>现在是不是正在响（验证用）。</summary>
    public bool IsRinging { get { return Time.time < flashUntil; } }

    void ApplyHalo(float amount)
    {
        if (halo == null) return;
        Color color = halo.color;
        color.a = 0.55f * Mathf.Clamp01(amount);
        halo.color = color;
    }
}
