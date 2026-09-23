using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限世界的流式加载器：以小虫为中心，按区块（chunk）生成 / 回收村庄内容。
///
/// <list type="bullet">
/// <item>小虫走到哪，附近的区块就生成出来（建筑、设施、树、食物、村民都是随机刷的）；</item>
/// <item>走出 <see cref="keepRadius"/> 之外的区块会被回收，内存不会无限涨；</item>
/// <item>同一个区块坐标 + 同一个世界种子永远生成一样的内容，所以走远再回头村庄还是原样；</item>
/// <item>离小虫超过 <see cref="freezeRadius"/> 的村民会被「冻结」：停状态机、停物理、可选关渲染，
///       走回附近再自动解冻，用来省 CPU。</item>
/// </list>
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

    [Header("地图类型（荒野 / 农村 / 城市）")]
    [Tooltip("决定村子密度与 npc 多少；主菜单选完地图后由 ApplyMapKind 覆盖")]
    public MapKind mapKind = MapKind.Village;

    [Header("村民冻结（省资源）")]
    public bool freezeFarVillagers = true;
    [Tooltip("离小虫超过这个距离的村民会被冻结")]
    public float freezeRadius = 26f;
    public float freezeCheckInterval = 0.5f;

    [Header("地道配对")]
    [Tooltip("两头地道的最近距离（太近没意思）")]
    public float tunnelMinGap = 24f;
    [Tooltip("两头地道的最远距离（太远不方便）")]
    public float tunnelMaxGap = 110f;

    [Header("保证视野里有村民")]
    public bool keepVillagersInView = true;
    [Tooltip("可见范围内至少要有这么多村民")]
    public int minVillagersInView = 2;
    [Tooltip("可见范围的一半（宽 / 高，比相机略小一圈）")]
    public Vector2 viewHalfSize = new Vector2(13f, 7.5f);
    [Tooltip("每个区块最多为此补几个村民，防止人越补越多")]
    public int maxExtraPerChunk = 4;

    readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    readonly List<Villager> villagers = new List<Villager>();
    readonly List<Burrow> burrows = new List<Burrow>();
    readonly Dictionary<Vector2Int, int> extraSpawned = new Dictionary<Vector2Int, int>();
    Vector2Int lastCenter;
    bool hasCenter;
    bool built;
    float nextStreamCheck;
    float nextFreezeCheck;
    int walkerIndex = 9000;

    public int LoadedChunks { get { return chunks.Count; } }
    public int TrackedVillagers { get { return villagers.Count; } }
    public int FrozenVillagers { get; private set; }
    public int TrackedBurrows { get { return burrows.Count; } }
    public int TunnelPairs { get; private set; }
    /// <summary>为了「视野里不少于两个村民」额外补出来的村民数量。</summary>
    public int ExtraVillagersSpawned { get; private set; }
    /// <summary>当前可见范围内的村民数（HUD / 调试用）。</summary>
    public int VillagersInView { get; private set; }
    public Vector2Int CurrentChunk { get { return ChunkOf(TargetPosition); } }

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
        // 玩家在主菜单点了「继续游戏」：等 AutoSave 用存档里的地图类型与种子来建
        if (SaveSystem.ContinueRequested) return;

        if (randomSeedEachRun) worldSeed = Random.Range(1, int.MaxValue);
        ApplyMapKind(MapProfiles.Current);
        Stream(true);
    }

    /// <summary>
    /// 套用地图类型（必须在建区块之前调用）：把荒野 / 农村 / 城市的密度参数
    /// 覆盖到生成器与流式加载器上，之后每个区块都按这套参数生成。
    /// </summary>
    public void ApplyMapKind(MapKind kind)
    {
        mapKind = kind;
        MapProfiles.Current = kind;
        MapProfiles.Apply(kind, builder, this);
    }

    /// <summary>用指定地图与种子重建整个世界（读档用）。</summary>
    public void LoadWorld(int seed, MapKind kind)
    {
        worldSeed = seed;
        ApplyMapKind(kind);
        Rebuild();
    }

    /// <summary>用指定种子重建整个世界（地图类型沿用当前选中的）。</summary>
    public void LoadWorld(int seed)
    {
        LoadWorld(seed, MapProfiles.Current);
    }

    /// <summary>换一个随机种子重新开一局。</summary>
    public void NewWorld()
    {
        worldSeed = Random.Range(1, int.MaxValue);
        ApplyMapKind(MapProfiles.Current);
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
        extraSpawned.Clear();
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
            EnsureVillagersInView();
        }
    }

    /// <summary>
    /// 保证小虫视野里不少于 <see cref="minVillagersInView"/> 个村民：
    /// 不够时就近补人（挂到当前区块下，区块回收时一起销毁），每个区块最多补 <see cref="maxExtraPerChunk"/> 个。
    /// </summary>
    void EnsureVillagersInView()
    {
        if (!keepVillagersInView || builder == null || target == null) return;

        Vector2 self = target.position;
        int count = 0;
        for (int i = 0; i < villagers.Count; i++)
        {
            Villager villager = villagers[i];
            if (villager == null) continue;
            Vector2 delta = (Vector2)villager.transform.position - self;
            if (Mathf.Abs(delta.x) <= viewHalfSize.x && Mathf.Abs(delta.y) <= viewHalfSize.y) count++;
        }
        VillagersInView = count;
        if (count >= minVillagersInView) return;

        Vector2Int coord = ChunkOf(self);
        GameObject chunk;
        if (!chunks.TryGetValue(coord, out chunk) || chunk == null) return;

        int spawned;
        extraSpawned.TryGetValue(coord, out spawned);
        if (spawned >= maxExtraPerChunk) return;

        Transform parent = chunk.transform.Find("Villagers");
        if (parent == null) return;

        int need = minVillagersInView - count;
        for (int i = 0; i < need && spawned < maxExtraPerChunk; i++)
        {
            Villager villager = builder.SpawnWalkerNear(self, parent, walkerIndex++);
            if (villager == null) break;
            villagers.Add(villager);
            spawned++;
            ExtraVillagersSpawned++;
        }
        extraSpawned[coord] = spawned;
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

    /// <summary>远处的村民冻结，近的恢复。</summary>
    public void UpdateFreeze()
    {
        if (target == null) return;

        Vector2 self = target.position;
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
