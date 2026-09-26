using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限世界的流式加载器：以小虫为中心，按区块（chunk）生成 / 回收世界内容。
///
/// <list type="bullet">
/// <item>小虫走到哪，附近的区块就生成出来（地貌、建筑、设施、树、食物、村民都是按区块坐标 + 种子生成的）；</item>
/// <item>走出 <see cref="keepRadius"/> 之外的区块会被回收，内存不会无限涨；</item>
/// <item>同一个区块坐标 + 同一个世界种子永远生成一样的内容，所以走远再回头世界还是原样；</item>
/// <item>离小虫超过 <see cref="WorldBiome.FreezeRadius"/> 的村民会被「冻结」：停状态机、停物理、可选关渲染，
///       走回附近再自动解冻，用来省 CPU。</item>
/// </list>
///
/// **每个区块属于一种自然体系与一种聚落体系**（见 <see cref="WorldBiome"/>），
/// 所以「多远冻结村民」也跟着**当前区块的聚落**走 —— 城里人太多就冻得近一点。
/// **人口不是「屏幕里塞几个人」**：每个区块住几个人在生成时按**房屋密度**算好
/// （房子多 → 人多，见 <see cref="VillageGenerator.LastVillagerTarget"/>），
/// 少了就在**玩家看不见的地方**补（见 <see cref="IsHiddenFromPlayer"/>）。
/// </summary>
public class VillageWorld : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("跟随的目标（小虫）；留空自动找")]
    public Transform target;
    [Tooltip("区块生成器；留空自动找")]
    public VillageGenerator builder;

    [Header("区块流式加载")]
    [Tooltip("区块边长，要和 VillageGenerator.chunkSize 一致")]
    public float chunkSize = 32f;
    [Tooltip("以小虫所在区块为中心，生成 (2r+1)^2 个区块")]
    public int viewRadius = 1;
    [Tooltip("超出去这么多格的区块就回收（要 >= viewRadius）")]
    public int keepRadius = 2;
    [Tooltip("世界种子：决定整个世界长什么样")]
    public int worldSeed = 20260922;
    [Tooltip("新开一局时随机换个种子（读档时用存档里的种子）")]
    public bool randomSeedEachRun = true;
    [Tooltip("流式检查间隔（秒）")]
    public float streamInterval = 0.25f;

    [Header("村民冻结（省资源）")]
    public bool freezeFarVillagers = true;
    [Tooltip("冻结距离按当前区块的聚落取值，见 WorldBiome.FreezeRadius")]
    public float freezeCheckInterval = 0.5f;

    [Header("地道配对")]
    [Tooltip("两头地道的最近距离（太近没意思）")]
    public float tunnelMinGap = 24f;
    [Tooltip("两头地道的最远距离（太远不方便）")]
    public float tunnelMaxGap = 110f;

    [Header("按房屋密度维持人口（2026-09-26 重做）")]
    [Tooltip("每隔多久检查一次人口（秒）")]
    public float populationInterval = 1f;
    [Tooltip("给「这个区块该住几个人」的临时加成（混乱 4 级会让来看热闹的人变多）")]
    public int extraPopulation;
    [Tooltip("一次最多补几个人（免得一瞬间冒出来一堆）")]
    public int maxRespawnPerTick = 2;
    [Tooltip("「玩家看不见」的安全边距：相机视野往外再放宽这么多世界单位才算安全（免得贴着屏幕边冒人）")]
    public float hiddenMargin = 1.5f;

    readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    readonly List<Villager> villagers = new List<Villager>();
    readonly List<Burrow> burrows = new List<Burrow>();
    /// <summary>每个区块**该住几个人**（生成时按房屋密度算好，见 VillageGenerator.LastVillagerTarget）。</summary>
    readonly Dictionary<Vector2Int, int> chunkPopulation = new Dictionary<Vector2Int, int>();
    Vector2Int lastCenter;
    bool hasCenter;
    bool built;
    float nextStreamCheck;
    float nextFreezeCheck;
    float nextPopulationCheck;
    int walkerIndex = 9000;
    Camera cachedCamera;

    public int LoadedChunks { get { return chunks.Count; } }
    public int TrackedVillagers { get { return villagers.Count; } }
    public int FrozenVillagers { get; private set; }
    public int TrackedBurrows { get { return burrows.Count; } }
    public int TunnelPairs { get; private set; }
    /// <summary>按房屋密度补出来的村民总数（验证用）。</summary>
    public int RespawnedVillagers { get; private set; }
    /// <summary>补人时落在「玩家看得见」的位置上的次数 —— **恒该是 0**（验证用）。</summary>
    public int VisibleRespawnViolations { get; private set; }
    /// <summary>上一轮补人挑的落点（验证 / 调试用：每一个都该是「屏幕外，或从小虫那边被挡住」）。</summary>
    public IReadOnlyList<Vector2> LastRespawnPoints { get { return lastRespawnPoints; } }
    /// <summary>此刻落在相机视野里的村民数（调试 / 验证用）。</summary>
    public int VillagersInView { get; private set; }

    readonly List<Vector2> lastRespawnPoints = new List<Vector2>();

    /// <summary>验证 / 调试用：这个点现在是不是就画在屏幕上（相机视野之内）。</summary>
    public bool IsOnScreen(Vector2 point)
    {
        return IsInsideView(point, 0f);
    }

    /// <summary>验证 / 调试用：这个区块「按房屋密度该住几个人」（-1 = 这块没记录）。</summary>
    public int PopulationTargetOf(Vector2Int coord)
    {
        int value;
        return chunkPopulation.TryGetValue(coord, out value) ? value : -1;
    }
    public Vector2Int CurrentChunk { get { return ChunkOf(TargetPosition); } }

    /// <summary>小虫当前所在区块的自然体系（HUD 显示「森林 · 农村」用）。</summary>
    public NatureKind CurrentNature { get { return WorldBiome.NatureAt(CurrentChunk, worldSeed); } }
    /// <summary>小虫当前所在区块的聚落体系。</summary>
    public SettlementKind CurrentSettlement { get { return WorldBiome.SettlementAt(CurrentChunk, worldSeed); } }
    /// <summary>合起来的一句话，例如「森林 · 农村」。</summary>
    public string CurrentBiomeText { get { return WorldBiome.Describe(CurrentNature, CurrentSettlement); } }

    Vector2 TargetPosition
    {
        get { return target != null ? (Vector2)target.position : Vector2.zero; }
    }

    void Awake()
    {
        if (target == null)
        {
            BugController bug = FindObjectOfType<BugController>();
            if (bug != null) target = bug.transform;
        }
        if (builder == null) builder = FindObjectOfType<VillageGenerator>();
        if (builder != null && builder.chunkSize > 0.1f) chunkSize = builder.chunkSize;
        if (keepRadius < viewRadius) keepRadius = viewRadius;
    }

    void Start()
    {
        // AutoSave（如果在 GameDirector 上）可能已经先读过档并把世界建好了，别重复生成
        if (built) return;
        // 玩家在主菜单点了「继续游戏」：等 AutoSave 用存档里的种子来建
        if (SaveSystem.ContinueRequested) return;

        // 不再有「开局选地图」：世界每次都是一整片新的随机地图，
        // 地貌与聚落由「种子 + 区块坐标」决定（见 WorldBiome）
        if (randomSeedEachRun) worldSeed = Random.Range(1, int.MaxValue);
        Stream(true);
    }

    /// <summary>用指定种子重建整个世界（读档用）。</summary>
    public void LoadWorld(int seed)
    {
        worldSeed = seed;
        Rebuild();
    }

    /// <summary>换一个随机种子重新开一局。</summary>
    public void NewWorld()
    {
        worldSeed = Random.Range(1, int.MaxValue);
        Rebuild();
    }

    /// <summary>清空已加载的区块并立刻重建（会销毁所有区块对象与村民）。</summary>
    public void Rebuild()
    {
        ClearChunks();
        Stream(true);
    }

    void ClearChunks()
    {
        foreach (KeyValuePair<Vector2Int, GameObject> pair in chunks)
            if (pair.Value != null) Destroy(pair.Value);

        chunks.Clear();
        villagers.Clear();
        burrows.Clear();
        chunkPopulation.Clear();
        lastRespawnPoints.Clear();
        RespawnedVillagers = 0;
        VisibleRespawnViolations = 0;
        FrozenVillagers = 0;
        TunnelPairs = 0;
        VillagersInView = 0;
        hasCenter = false;
        if (builder != null && builder.map != null) builder.map.Clear();
    }

    void Update()
    {
        if (target == null) return;

        if (Time.time >= nextStreamCheck)
        {
            nextStreamCheck = Time.time + Mathf.Max(0.05f, streamInterval);
            Stream(false);
        }

        if (freezeFarVillagers && Time.time >= nextFreezeCheck)
        {
            nextFreezeCheck = Time.time + Mathf.Max(0.1f, freezeCheckInterval);
            UpdateFreeze();
        }

        if (Time.time >= nextPopulationCheck)
        {
            nextPopulationCheck = Time.time + Mathf.Max(0.25f, populationInterval);
            MaintainPopulation();
        }
    }

    /// <summary>
    /// **按房屋密度维持人口**（2026-09-26 取代原来的「保证视野里至少有 N 个村民」）：
    /// <list type="bullet">
    /// <item>每个区块「该住几个人」在生成时就算好了（房子数 × 每栋房入住率，见
    ///       <see cref="VillageGenerator.LastVillagerTarget"/>），这里只负责把被吃掉 / 走丢的人补回来；</item>
    /// <item>**补人只补在玩家看不见的地方**：要么在相机视野之外，要么虽然还在屏幕里、
    ///       但从小虫这边望过去被建筑 / 树挡着（<see cref="IsHiddenFromPlayer"/>）——
    ///       绝不会当着玩家的面凭空冒出一个人来；找不到这样的位置就先不补，下一轮再看。</item>
    /// </list>
    /// </summary>
    void MaintainPopulation()
    {
        if (builder == null || target == null) return;

        Vector2 self = target.position;
        Vector2Int coord = ChunkOf(self);

        // 只统计「相机看得见的人」，纯给调试 / 验证用（补人不会让它变大 = 没在屏幕里冒人）
        int visible = 0;
        for (int i = 0; i < villagers.Count; i++)
        {
            Villager villager = villagers[i];
            if (villager == null) continue;
            if (IsInsideView(villager.transform.position, 0f)) visible++;
        }
        VillagersInView = visible;

        int wanted;
        if (!chunkPopulation.TryGetValue(coord, out wanted)) return;
        wanted += Mathf.Max(0, extraPopulation);
        if (wanted <= 0) return;

        GameObject chunk;
        if (!chunks.TryGetValue(coord, out chunk) || chunk == null) return;

        // 「这个区块还有几口人」按**挂在它下面的村民**数（村民跑出自己的区块一点也不会被误判成缺人）
        Transform parent = chunk.transform.Find("Villagers");
        if (parent == null) return;
        int alive = CountVillagersUnder(parent);
        if (alive >= wanted) return;

        lastRespawnPoints.Clear();
        int need = Mathf.Min(wanted - alive, Mathf.Max(1, maxRespawnPerTick));
        for (int i = 0; i < need; i++)
        {
            Vector2 point;
            if (!TryFindHiddenSpawnPoint(coord, out point)) break;

            if (!IsHiddenFromPlayer(point)) VisibleRespawnViolations++;   // 恒该是 0（自检）
            lastRespawnPoints.Add(point);

            Villager villager = builder.SpawnWalkerAt(point, parent, walkerIndex++);
            if (villager == null) break;
            villagers.Add(villager);
            RespawnedVillagers++;
        }
    }

    /// <summary>这个父物体下面还活着几个村民（被吃掉的会自己从列表里消失）。</summary>
    static int CountVillagersUnder(Transform parent)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).GetComponent<Villager>() != null) count++;
        return count;
    }

    Rect ChunkBounds(Vector2Int coord)
    {
        Vector2 c = new Vector2(coord.x * chunkSize, coord.y * chunkSize);
        return new Rect(c.x - chunkSize * 0.5f, c.y - chunkSize * 0.5f, chunkSize, chunkSize);
    }

    /// <summary>
    /// 找一个**玩家看不见**的落点：① 先试「房子旁边」（拿本区块的房子当参照物，人像是从屋后绕出来的）；
    /// ② 不行就在这块区块里随机撒点。每一次候选都要过 <see cref="IsGoodSpawnPoint"/>。
    /// </summary>
    bool TryFindHiddenSpawnPoint(Vector2Int coord, out Vector2 point)
    {
        Rect bounds = ChunkBounds(coord);

        // ① 房子附近（有房子的地方才有人住 —— 和「人口跟房子走」是一回事）
        if (builder != null && builder.map != null && builder.map.houses.Count > 0)
        {
            List<Vector2> houses = builder.map.houses;
            int start = Random.Range(0, houses.Count);
            for (int i = 0; i < houses.Count; i++)
            {
                Vector2 home = houses[(start + i) % houses.Count];
                if (!bounds.Contains(home)) continue;
                for (int k = 0; k < 8; k++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    Vector2 candidate = home + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.6f, 4.6f);
                    if (!IsGoodSpawnPoint(candidate, bounds)) continue;
                    point = candidate;
                    return true;
                }
            }
        }

        // ② 区块里随便找
        for (int i = 0; i < 80; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(bounds.xMin + 1.5f, bounds.xMax - 1.5f),
                                            Random.Range(bounds.yMin + 1.5f, bounds.yMax - 1.5f));
            if (!IsGoodSpawnPoint(candidate, bounds)) continue;
            point = candidate;
            return true;
        }

        point = Vector2.zero;
        return false;
    }

    bool IsGoodSpawnPoint(Vector2 point, Rect bounds)
    {
        if (!bounds.Contains(point)) return false;
        if (!IsHiddenFromPlayer(point)) return false;
        // 别挤在别人身上 / 挤进墙里（静态与动态碰撞体都算）
        return Physics2D.OverlapCircle(point, 0.55f) == null;
    }

    /// <summary>这个位置「玩家看不见」：在相机视野之外，或者虽然还在屏幕里、但从小虫望过去被东西挡着。</summary>
    public bool IsHiddenFromPlayer(Vector2 point)
    {
        if (!IsInsideView(point, hiddenMargin)) return true;
        return IsLineOfSightBlocked(TargetPosition, point);
    }

    /// <summary>这个点在不在相机视野里（<paramref name="margin"/> 是往外放宽的世界单位）。</summary>
    bool IsInsideView(Vector2 point, float margin)
    {
        Camera cam = ViewCamera;
        if (cam == null) return false;                 // 找不到相机就当看不见（不至于卡住补人）

        Rect view = CameraWorldRect(cam);
        view.xMin -= margin; view.yMin -= margin; view.xMax += margin; view.yMax += margin;
        return view.Contains(point);
    }

    Rect CameraWorldRect(Camera cam)
    {
        Vector3 a = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 b = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    Camera ViewCamera
    {
        get
        {
            if (cachedCamera == null) cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            return cachedCamera;
        }
    }

    /// <summary>
    /// 小虫和这个点之间有没有「不会动的东西」挡着（房子 / 树 / 水井…）。
    /// 判定方式：沿线投射，命中**没有刚体**的碰撞体才算遮挡 —— 会动的（村民 / 木箱 / 小虫自己）
    /// 只是暂时路过，不算「视线被挡住」。
    /// </summary>
    static bool IsLineOfSightBlocked(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.8f) return false;

        Vector2 direction = delta / distance;
        Vector2 origin = from + direction * 0.6f;      // 往前挪一点，别把小虫自己算进去
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance - 0.6f, Physics2D.DefaultRaycastLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider == null || collider.isTrigger) continue;
            if (collider.attachedRigidbody != null) continue;
            return true;
        }
        return false;
    }

    Vector2Int ChunkOf(Vector2 world)
    {
        return new Vector2Int(Mathf.RoundToInt(world.x / chunkSize), Mathf.RoundToInt(world.y / chunkSize));
    }

    /// <summary>按当前小虫位置补齐 / 回收区块。</summary>
    public void Stream(bool force)
    {
        if (builder == null || target == null) return;
        built = true;

        Vector2Int center = ChunkOf(target.position);
        if (!force && hasCenter && center == lastCenter) return;
        lastCenter = center;
        hasCenter = true;

        // 先回收再生成：锚点表里只剩当前附近的内容，新区块生成时不会被旧锚点带偏
        List<Vector2Int> removing = null;
        foreach (KeyValuePair<Vector2Int, GameObject> pair in chunks)
        {
            int distance = Mathf.Max(Mathf.Abs(pair.Key.x - center.x), Mathf.Abs(pair.Key.y - center.y));
            if (distance <= keepRadius) continue;
            if (removing == null) removing = new List<Vector2Int>();
            removing.Add(pair.Key);
        }
        if (removing != null)
        {
            for (int i = 0; i < removing.Count; i++) UnloadChunk(removing[i]);
        }

        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        {
            for (int dy = -viewRadius; dy <= viewRadius; dy++)
            {
                Vector2Int coord = new Vector2Int(center.x + dx, center.y + dy);
                if (!chunks.ContainsKey(coord)) LoadChunk(coord);
            }
        }

        PairTunnels();
    }

    void LoadChunk(Vector2Int coord)
    {
        GameObject root = builder.BuildChunk(coord, worldSeed);
        if (root == null) return;
        chunks[coord] = root;
        // 这个区块「该住几个人」是生成时按房屋密度定好的（房子多 → 人多），补人时按它比
        chunkPopulation[coord] = builder.LastVillagerTarget;

        Villager[] found = root.GetComponentsInChildren<Villager>(true);
        for (int i = 0; i < found.Length; i++)
            if (found[i] != null) villagers.Add(found[i]);

        Burrow[] holes = root.GetComponentsInChildren<Burrow>(true);
        for (int i = 0; i < holes.Length; i++)
            if (holes[i] != null) burrows.Add(holes[i]);
    }

    void UnloadChunk(Vector2Int coord)
    {
        GameObject root;
        if (!chunks.TryGetValue(coord, out root)) return;
        chunks.Remove(coord);
        chunkPopulation.Remove(coord);

        if (root != null)
        {
            Villager[] found = root.GetComponentsInChildren<Villager>(true);
            for (int i = 0; i < found.Length; i++) villagers.Remove(found[i]);
            Destroy(root);
        }

        // 区块没了，锚点表里对应的一批也剪掉，别让列表越积越长
        if (builder != null && builder.map != null)
            builder.map.PruneFarFrom(target.position, (keepRadius + 1) * chunkSize);

        for (int i = villagers.Count - 1; i >= 0; i--)
            if (villagers[i] == null) villagers.RemoveAt(i);
        for (int i = burrows.Count - 1; i >= 0; i--)
            if (burrows[i] == null) burrows.RemoveAt(i);
    }

    /// <summary>
    /// 把已加载区块里的地道按距离两两配对（配好就能相互传送）。
    /// 另一头被回收时 <see cref="Burrow.OnDestroy"/> 会解开配对，这里再给剩下的重新找搭档。
    /// </summary>
    public void PairTunnels()
    {
        for (int i = burrows.Count - 1; i >= 0; i--)
            if (burrows[i] == null) burrows.RemoveAt(i);

        for (int i = 0; i < burrows.Count; i++)
        {
            Burrow a = burrows[i];
            if (a == null || !a.tunnel || a.partner != null) continue;

            Burrow best = null;
            float bestDistance = float.MaxValue;
            for (int j = 0; j < burrows.Count; j++)
            {
                Burrow b = burrows[j];
                if (b == null || b == a || !b.tunnel || b.partner != null) continue;
                float distance = Vector2.Distance(a.transform.position, b.transform.position);
                if (distance < tunnelMinGap || distance > tunnelMaxGap) continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = b;
                }
            }

            if (best == null) continue;
            a.partner = best;
            best.partner = a;
        }

        TunnelPairs = Burrow.CountTunnels() / 2;
    }

    /// <summary>远处的村民冻结，近的恢复（冻结距离按当前区块的聚落取值）。</summary>
    public void UpdateFreeze()
    {
        if (target == null) return;

        Vector2 self = target.position;
        float freezeRadius = WorldBiome.FreezeRadius(CurrentSettlement);
        float sqr = freezeRadius * freezeRadius;
        int frozen = 0;

        for (int i = villagers.Count - 1; i >= 0; i--)
        {
            Villager villager = villagers[i];
            if (villager == null)
            {
                villagers.RemoveAt(i);
                continue;
            }
            bool far = ((Vector2)villager.transform.position - self).sqrMagnitude > sqr;
            villager.SetFrozen(far);
            if (far) frozen++;
        }

        FrozenVillagers = frozen;
    }
}
