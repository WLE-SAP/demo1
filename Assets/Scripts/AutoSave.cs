using UnityEngine;

/// <summary>
/// 存档管理（挂在游戏场景的 GameDirector 上）：
/// <list type="bullet">
/// <item>进入游戏时按主菜单的选择走「继续游戏」或「新开一局」；</item>
/// <item>游戏中每 <see cref="interval"/> 秒自动存一次；</item>
/// <item>窗口失焦 / 切后台 / 退出 / 返回主菜单时再补存一次，
///       所以玩家直接点右上角关掉游戏也不会丢进度。</item>
/// </list>
/// </summary>
public class AutoSave : MonoBehaviour
{
    [Header("自动存档")]
    [Tooltip("每多少秒自动存一次")]
    public float interval = 5f;
    [Tooltip("进游戏时是否读取存档（主菜单点了「继续游戏」才读）")]
    public bool applySaveOnStart = true;
    [Tooltip("退出 / 失焦 / 返回菜单时再存一次")]
    public bool saveOnExit = true;

    [Header("引用（留空自动找）")]
    public BugController bug;
    public BugEat eat;
    public VillageClock clock;
    public VillageWorld world;
    public BugVitality vitality;
    public BugGrowth growth;

    /// <summary>最近一次存档的时间（Time.time），HUD 用来显示「已保存」。</summary>
    public float LastSaveTime { get; private set; }
    public int SaveCount { get; private set; }
    public bool JustSaved { get { return SaveCount > 0 && Time.time - LastSaveTime < 2f; } }
    /// <summary>这一局已经玩了多久（秒）。</summary>
    public float PlaySeconds { get; private set; }

    float nextSave;

    void Awake()
    {
        if (bug == null) bug = FindObjectOfType<BugController>();
        if (eat == null) eat = FindObjectOfType<BugEat>();
        if (clock == null) clock = VillageClock.Instance != null ? VillageClock.Instance : FindObjectOfType<VillageClock>();
        if (world == null) world = FindObjectOfType<VillageWorld>();
        if (vitality == null) vitality = FindObjectOfType<BugVitality>();
        if (growth == null) growth = FindObjectOfType<BugGrowth>();
    }

    void Start()
    {
        if (applySaveOnStart) ApplyPendingState();
        nextSave = Time.time + Mathf.Max(1f, interval);
    }

    void Update()
    {
        PlaySeconds += Time.deltaTime;
        if (Time.time < nextSave) return;
        nextSave = Time.time + Mathf.Max(1f, interval);
        SaveNow();
    }

    void OnApplicationQuit()
    {
        if (saveOnExit) SaveNow();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && saveOnExit) SaveNow();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused && saveOnExit) SaveNow();
    }

    void OnDestroy()
    {
        // 返回主菜单（换场景）时也存一次；直接退出时这里会跟在 OnApplicationQuit 后面，重复存无害
        if (saveOnExit) SaveNow();
    }

    /// <summary>
    /// 读档：主菜单点「继续游戏」时由 Start 自动调用，也可以主动调用。
    /// 用存档里的世界种子重建世界，再把小虫位置、已吃数量、游戏内时间放回去。
    /// </summary>
    public void ApplyPendingState()
    {
        if (!SaveSystem.ContinueRequested) return;   // 新开一局：世界已经在 VillageWorld 里随机生成了

        SaveSystem.ContinueRequested = false;
        GameSave save = SaveSystem.Load();
        if (save == null)
        {
            Debug.Log("[Save] 没有可用的存档，按新开一局处理。");
            return;
        }

        // 1) 先用存档里的地图类型与种子重建世界（含出生点附近）；位置以存档为准，不要再把小虫放回出生点
        if (world != null)
        {
            if (world.builder != null) world.builder.placePlayerOnFirstChunk = false;
            world.LoadWorld(save.worldSeed, save.ResolvedMapKind);
        }

        // 2) 再把小虫放回原位
        Vector2 position = new Vector2(save.playerX, save.playerY);
        if (bug != null)
        {
            bug.transform.position = new Vector3(position.x, position.y, 0f);
            Rigidbody2D body = bug.GetComponent<Rigidbody2D>();
            if (body != null) body.position = position;
            FollowCamera camera = FindObjectOfType<FollowCamera>();
            if (camera != null) camera.Snap();
        }
        if (world != null) world.Stream(true);       // 按新位置补齐区块

        // 3) 恢复计数、体力、成长与时间
        if (eat != null) eat.RestoreEatenCount(save.eaten);
        if (growth != null) growth.SetLevel(save.growthLevel);
        if (vitality != null) vitality.Restore(save.stamina > 0.01f ? save.stamina : vitality.maxStamina * 0.6f);
        if (clock != null) clock.RestoreHours(save.clockHours);
        PlaySeconds = save.playSeconds;

        Debug.Log("[Save] 已读取存档：" + save.savedAt + " 地图=" + MapProfiles.Label(save.ResolvedMapKind)
            + " 种子=" + save.worldSeed
            + " 位置=(" + save.playerX.ToString("F1") + "," + save.playerY.ToString("F1") + ") 已吃=" + save.eaten
            + " 体力=" + save.stamina.ToString("F0") + " 成长=" + save.growthLevel + "级");
    }

    /// <summary>立刻存一次。</summary>
    public void SaveNow()
    {
        if (bug == null) return;

        GameSave save = new GameSave();
        save.worldSeed = world != null ? world.worldSeed : 0;
        save.mapKind = (int)(world != null ? world.mapKind : MapProfiles.Current);
        save.playerX = bug.transform.position.x;
        save.playerY = bug.transform.position.y;
        save.eaten = eat != null ? eat.EatenCount : 0;
        save.stamina = vitality != null ? vitality.Stamina : 100f;
        save.growthLevel = growth != null ? growth.level : 0;
        save.clockHours = clock != null ? clock.TotalHours : 7f;
        save.playSeconds = PlaySeconds;

        if (!SaveSystem.Save(save)) return;
        LastSaveTime = Time.time;
        SaveCount++;
    }
}
