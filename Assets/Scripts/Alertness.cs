using UnityEngine;

/// <summary>
/// 本局的「警觉值」：0~100，设计文档 §6。
/// 和 <see cref="ChaosMeter"/> 的区别：**混乱是「场景被搞坏了多少」（降得慢），
/// 警觉是「现在有多紧张、村民多盯着你」（降得快）**。
///
/// <list type="bullet">
/// <item>**怎么涨**：被村民看到（<see cref="GameEvent.Spotted"/>，每个目击者 +18）、
///       以及大动静（按噪音响度加一点）；</item>
/// <item>**怎么降**：**没人看得见小虫时** -1.2/s（躲起来一会儿就冷静了）；</item>
/// <item>**高了会怎样**：≥40 村民视野 ×1.15、听觉 ×1.35；≥70 起每 <see cref="searchInterval"/> 秒
///       让最近的 <see cref="searchCount"/> 个村民进入 <see cref="VillagerState.Search"/>（主动来搜）。</item>
/// </list>
/// </summary>
public class Alertness : MonoBehaviour
{
    [Header("增量")]
    [Tooltip("被一个村民看到加多少")]
    public float spottedGain = 18f;
    [Tooltip("噪音每 1 点响度加多少（脚步这种几乎不加）")]
    public float noisePerLoudness = 2f;

    [Header("衰减")]
    [Tooltip("没人看得见小虫时每秒掉多少")]
    public float decayPerSecond = 1.2f;
    [Tooltip("看得见时是否不衰减（true = 被盯着就一直紧张）")]
    public bool freezeDecayWhenSeen = true;

    [Header("阈值")]
    [Tooltip("超过这条线：村民视野听觉都提高")]
    public float alertLine = 40f;
    [Tooltip("超过这条线：村民开始主动搜索")]
    public float searchLine = 70f;

    [Header("主动搜索")]
    public float searchInterval = 6f;
    public int searchCount = 2;

    public static Alertness Instance { get; private set; }

    [Tooltip("当前警觉值（0~100）")]
    public float alert;
    /// <summary>本局出现过的最高警觉值（结算面板展示）。</summary>
    public float MaxAlert { get; private set; }

    float nextSearchTime;
    float nextSightCheck;      // 「有没有人看得见」不必每帧问一遍（村民可能有二三十个）
    bool seenRecently;
    bool wasTense;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        if (Object.FindObjectOfType<Alertness>() != null) return;
        GameObject host = GameObject.Find("GameDirector");
        if (host == null)
        {
            BugController bug = Object.FindObjectOfType<BugController>();
            if (bug == null) return;
            host = bug.gameObject;
        }
        host.AddComponent<Alertness>();
    }

    void Awake()
    {
        Instance = this;
        MaxAlert = alert;
        wasTense = alert >= alertLine;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvent.Spotted += OnSpotted;
        GameEvent.Noise += OnNoise;
    }

    void OnDisable()
    {
        GameEvent.Spotted -= OnSpotted;
        GameEvent.Noise -= OnNoise;
    }

    void OnSpotted(Villager by) { Add(spottedGain); }
    void OnNoise(NoiseEvent e) { Add(e.loudness * noisePerLoudness); }

    void Update()
    {
        // 「有没有人看得见小虫」最多每 0.2 秒问一遍（村民可能有二三十个，别每帧全扫）
        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + 0.2f;
            seenRecently = AnyoneSeesBug();
        }

        if (!seenRecently || !freezeDecayWhenSeen)
            Add(-decayPerSecond * Time.deltaTime);

        if (alert >= searchLine && Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + Mathf.Max(1f, searchInterval);
            SendSearchers();
        }
    }

    /// <summary>加 / 减警觉值，并在跨过阈值时广播。</summary>
    public void Add(float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;

        alert = Mathf.Clamp(alert + amount, 0f, 100f);
        if (alert > MaxAlert) MaxAlert = alert;

        bool tense = alert >= alertLine;
        if (tense == wasTense) return;
        wasTense = tense;
        GameEvent.RaiseAlertnessChanged(tense);
    }

    /// <summary>读档用：直接设定（不广播）。</summary>
    public void Restore(float value)
    {
        alert = Mathf.Clamp(value, 0f, 100f);
        if (alert > MaxAlert) MaxAlert = alert;
        wasTense = alert >= alertLine;
    }

    static bool AnyoneSeesBug()
    {
        Villager[] all = Villager.All.ToArray();
        for (int i = 0; i < all.Length; i++)
        {
            Villager v = all[i];
            if (v == null) continue;
            if (v.CanSeeBug()) return true;
        }
        return false;
    }

    /// <summary>让最近的两个村民去搜（设计文档 §6 的「NPC 开始搜索」）。</summary>
    void SendSearchers()
    {
        BugController bug = BugController.Instance;
        if (bug == null) return;

        Vector2 self = bug.transform.position;
        Villager[] all = Villager.All.ToArray();
        int sent = 0;

        // 就近挑（不受冻结影响，因为 All 里本来就只有没冻结的）
        while (sent < Mathf.Max(1, searchCount))
        {
            Villager best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                Villager v = all[i];
                if (v == null || v.IsFrozen || v.State == VillagerState.Search) continue;
                float d = ((Vector2)v.transform.position - self).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = v;
            }
            if (best == null) break;
            best.BeginSearch(self);
            sent++;
        }

        if (sent > 0) Debug.Log("[Alert] 警觉值高，派了 " + sent + " 个人来搜。");
    }

    /// <summary>警觉值给村民视野的加成。</summary>
    public float VillagerViewBonus { get { return alert >= alertLine ? 1.15f : 1f; } }
    /// <summary>警觉值给村民听觉的加成。</summary>
    public float VillagerHearingBonus { get { return alert >= alertLine ? 1.35f : 1f; } }
}
