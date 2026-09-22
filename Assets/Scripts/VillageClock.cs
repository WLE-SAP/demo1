using UnityEngine;

/// <summary>
/// 村庄的一天：把现实秒数映射成游戏内时刻，给出时间段（作息）和夜晚程度。
/// 村民按 <see cref="CurrentPhase"/> 决定现在是去干活、去广场闲聊，还是回家待着；
/// 路灯和窗户用 <see cref="Night01"/> 决定亮不亮。
/// </summary>
public class VillageClock : MonoBehaviour
{
    /// <summary>一天里的时间段。</summary>
    public enum Phase
    {
        Night,      // 22:00 - 05:00 回家休息
        Morning,    // 05:00 - 11:00 干活
        Noon,       // 11:00 - 14:00 广场社交
        Afternoon,  // 14:00 - 18:00 干活
        Evening     // 18:00 - 22:00 饭后闲聊
    }

    [Tooltip("一天（24 游戏小时）等于多少现实秒")]
    public float dayLengthSeconds = 240f;

    [Tooltip("开局时刻（0-24）")]
    public float startHour = 7f;

    [Tooltip("时间倍率，调大就是快进")]
    public float speed = 1f;

    float totalHours;

    /// <summary>当前场景里的时钟（村里只有一个）。</summary>
    public static VillageClock Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        totalHours = Mathf.Repeat(startHour, 24f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        totalHours += 24f * Mathf.Max(0.01f, speed) * Time.deltaTime / Mathf.Max(5f, dayLengthSeconds);
    }

    /// <summary>当天时刻，0-24。</summary>
    public float Hour { get { return Mathf.Repeat(totalHours, 24f); } }

    /// <summary>第几天，从 1 开始。</summary>
    public int Day { get { return 1 + Mathf.FloorToInt(totalHours / 24f); } }

    /// <summary>形如 09:24。</summary>
    public string TimeText
    {
        get
        {
            int h = Mathf.FloorToInt(Hour);
            int m = Mathf.FloorToInt((Hour - h) * 60f);
            return (h < 10 ? "0" : "") + h + ":" + (m < 10 ? "0" : "") + m;
        }
    }

    /// <summary>形如 第1天 09:24。</summary>
    public string DayTimeText { get { return "第" + Day + "天 " + TimeText; } }

    public Phase CurrentPhase { get { return PhaseAt(Hour); } }

    public static Phase PhaseAt(float hour)
    {
        if (hour < 5f) return Phase.Night;
        if (hour < 11f) return Phase.Morning;
        if (hour < 14f) return Phase.Noon;
        if (hour < 18f) return Phase.Afternoon;
        if (hour < 22f) return Phase.Evening;
        return Phase.Night;
    }

    public static string PhaseLabel(Phase phase)
    {
        switch (phase)
        {
            case Phase.Morning: return "早晨";
            case Phase.Noon: return "正午";
            case Phase.Afternoon: return "下午";
            case Phase.Evening: return "傍晚";
            default: return "夜里";
        }
    }

    /// <summary>夜晚程度：0 = 大白天，1 = 深夜。</summary>
    public float Night01
    {
        get
        {
            float h = Hour;
            if (h >= 8f && h <= 17f) return 0f;
            if (h > 17f && h < 21f) return Mathf.InverseLerp(17f, 21f, h);
            if (h >= 21f || h < 5f) return 1f;
            return 1f - Mathf.InverseLerp(5f, 8f, h);
        }
    }

    /// <summary>把时间往前拨若干小时（调试 / 测试用）。</summary>
    public void SkipHours(float hours)
    {
        totalHours += hours;
    }

    /// <summary>直接跳到某个时刻（0-24，调试 / 测试用）。</summary>
    public void SetHour(float hour)
    {
        totalHours = Mathf.Floor(totalHours / 24f) * 24f + Mathf.Repeat(hour, 24f);
    }

    /// <summary>存档用：累计的总小时数（含天数）。</summary>
    public float TotalHours { get { return totalHours; } }

    /// <summary>读档用：恢复累计小时数。</summary>
    public void RestoreHours(float hours)
    {
        totalHours = Mathf.Max(0f, hours);
    }
}
