using UnityEngine;

/// <summary>
/// 「小虫把游戏搞出 bug 了」的**世界级故障特效**（屏幕级的在 <see cref="GlitchOverlay"/>）。
///
/// 风格锚点：不用「魔法光效」，而是用**程序出错的样子**说话 ——
/// 缺图占位棋盘格、洋红错误材质、贴图撕裂、图块复制、排序错乱。小虫本来就是这只游戏里的 bug。
///
/// 全部程序化（<see cref="ArtShapes"/> 的图元拼出来），**不占美术 key**：
/// 它们是运行时表现，不是场景物件（同头顶「!」/ 碎片 / 水洼）。
/// 每个特效物体都自带 <see cref="GlitchLifetime"/> / <see cref="GlitchJitter"/>，到时间自己收工，
/// 所以**不会糊在屏幕上**。
/// </summary>
public static class AbilityFx
{
    /// <summary>Unity 里「材质丢了」的经典洋红，正好当错误材质的颜色。</summary>
    public static readonly Color ErrorMagenta = new Color(1f, 0f, 1f, 0.85f);
    /// <summary>缺图占位棋盘格的两种颜色（灰白 + 洋红）。</summary>
    public static readonly Color MissingGrey = new Color(0.72f, 0.72f, 0.72f, 0.95f);

    /// <summary>世界里的特效统一放在很高的排序值上（盖过 YSort 对象）。</summary>
    const int FxOrder = 4000;

    // ---------------- 基础件 ----------------

    /// <summary>一块纯色图元，存在一会儿就消失（<paramref name="fadeOut"/> = 是否淡出）。</summary>
    public static GameObject Flash(Vector2 position, ArtShape shape, float size, Color color, float seconds, bool fadeOut = true)
    {
        Sprite sprite = ArtShapes.Get(shape);
        if (sprite == null) return null;

        GameObject go = new GameObject("GlitchFlash");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = Vector3.one * size;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = FxOrder;

        GlitchLifetime life = go.AddComponent<GlitchLifetime>();
        life.Setup(seconds, fadeOut);
        return go;
    }

    /// <summary>一圈扩散的洋红冲击环（电击用）。</summary>
    public static GameObject Shockwave(Vector2 position, float fromSize, float toSize, float seconds)
    {
        GameObject go = Flash(position, ArtShape.Round, fromSize, new Color(ErrorMagenta.r, ErrorMagenta.g, ErrorMagenta.b, 0.55f), seconds, false);
        if (go == null) return null;

        GlitchLifetime life = go.GetComponent<GlitchLifetime>();
        if (life != null) life.GrowTo(toSize);
        return go;
    }

    /// <summary>
    /// **缺图占位棋盘格**：一眼就知道「这张图没加载出来」。
    /// 用 <paramref name="cells"/>×<paramref name="cells"/> 个小方块拼出来（灰白与洋红相间）。
    /// </summary>
    public static GameObject MissingTextureBox(Transform parent, Vector2 localPosition, float size, float seconds, int cells = 4)
    {
        Sprite sprite = ArtShapes.Get(ArtShape.Rect);
        if (sprite == null) return null;

        GameObject root = new GameObject("MissingTexture");
        if (parent != null) root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        int n = Mathf.Clamp(cells, 2, 8);
        float cell = size / n;

        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++)
            {
                bool magenta = ((x + y) % 2) == 0;
                GameObject part = new GameObject("cell");
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = new Vector3(-size * 0.5f + cell * (x + 0.5f), -size * 0.5f + cell * (y + 0.5f), 0f);
                part.transform.localScale = new Vector3(cell, cell, 1f);

                SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = magenta ? MissingGrey : new Color(0.28f, 0.26f, 0.32f, 0.95f);
                sr.sortingOrder = FxOrder;
            }
        }

        GlitchLifetime life = root.AddComponent<GlitchLifetime>();
        life.Setup(seconds, true);
        return root;
    }

    /// <summary>
    /// 让一个物体（或它的一堆子精灵）**逐帧错位抖动** —— 就像贴图撕裂 / 顶点算错。
    /// 结束时会把位置原样还回去。同一个物体重复调用会刷新剩余时间。
    /// </summary>
    public static GlitchJitter Jitter(GameObject target, float seconds, float pixels = 0.03f, bool scrambleOrder = false)
    {
        if (target == null) return null;

        GlitchJitter jitter = target.GetComponent<GlitchJitter>();
        if (jitter == null) jitter = target.AddComponent<GlitchJitter>();
        jitter.Setup(seconds, pixels, scrambleOrder);
        return jitter;
    }

    /// <summary>
    /// 给一个物体挂上「复制粘贴多份」的残影（分裂出来的假小虫、被电麻的村民都用它）。
    /// 残影是半透明的同一个精灵，按固定像素偏移摆开 —— 看起来就是复制粘贴出了毛病。
    /// </summary>
    public static int AddGhosts(GameObject target, int count, float spacing, Color tint)
    {
        if (target == null || count <= 0) return 0;

        SpriteRenderer source = target.GetComponentInChildren<SpriteRenderer>();
        if (source == null || source.sprite == null) return 0;

        int made = 0;
        for (int i = 1; i <= count; i++)
        {
            GameObject ghost = new GameObject("Ghost" + i);
            ghost.transform.SetParent(source.transform.parent != null ? source.transform.parent : target.transform, false);
            ghost.transform.localPosition = source.transform.localPosition + new Vector3(spacing * i, -spacing * i * 0.4f, 0f);
            ghost.transform.localScale = source.transform.localScale;

            SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.color = new Color(tint.r, tint.g, tint.b, tint.a / (i + 1));
            sr.sortingOrder = source.sortingOrder - i;
            made++;
        }
        return made;
    }

    /// <summary>清掉一个物体身上所有残影（分身消失、伪装结束时用）。</summary>
    public static void ClearGhosts(GameObject target)
    {
        if (target == null) return;

        Transform root = target.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name.StartsWith("Ghost")) Object.Destroy(child.gameObject);
        }
    }

    // ---------------- 每种能力一套 ----------------

    /// <summary>电击：洋红冲击环 + 被电到的村民部件错位 + 一地洋红火花。</summary>
    public static void Shock(Vector2 position, float radius)
    {
        Shockwave(position, radius * 0.4f, radius * 2f, 0.35f);

        for (int i = 0; i < 5; i++)
        {
            Vector2 at = position + Random.insideUnitCircle * radius;
            Flash(at, ArtShape.Rect, Random.Range(0.10f, 0.22f), ErrorMagenta, Random.Range(0.15f, 0.35f));
        }
    }

    /// <summary>伪装：小虫身上盖一层缺图占位棋盘格（它「变成了一张没加载出来的图」）。</summary>
    public static GameObject DisguiseBox(BugController bug, float seconds)
    {
        if (bug == null) return null;
        return MissingTextureBox(bug.transform, new Vector2(0f, 0.02f), 0.62f, seconds, 4);
    }

    /// <summary>分裂：每只分身都复制粘贴出两份残影。</summary>
    public static void SplitGhosts(GameObject decoy)
    {
        AddGhosts(decoy, 2, 0.06f, new Color(0.5f, 0.5f, 0.55f, 0.5f));
        Jitter(decoy, 0.25f, 0.02f);
    }

    /// <summary>抖动：半径内的东西一起撕裂 + 排序错乱 + 一圈洋红。</summary>
    public static int Shake(Vector2 position, float radius, float seconds)
    {
        float sqr = radius * radius;
        int hit = 0;

        SpriteRenderer[] all = Object.FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sortingOrder > FxOrder - 100) continue;      // 不折腾特效自己
            if (((Vector2)sr.transform.position - position).sqrMagnitude > sqr) continue;

            // 直接抖渲染器所在的物体：整棵子树一起错位，看起来才像画面撕裂
            Transform owner = sr.transform.parent != null ? sr.transform.parent : sr.transform;
            Jitter(owner.gameObject, seconds, Random.Range(0.02f, 0.05f), true);
            hit++;
        }

        Flash(position, ArtShape.Round, radius * 1.6f, new Color(ErrorMagenta.r, ErrorMagenta.g, ErrorMagenta.b, 0.28f), 0.25f);
        return hit;
    }

    /// <summary>腐蚀：目标身上「像素一块块被挖掉」+ 绿色噪点 + 中心洋红。</summary>
    public static int Corrode(Vector2 position, float size)
    {
        int carved = PixelCarve(position, size, 22, new Color(0.16f, 0.16f, 0.18f, 0.9f));

        for (int i = 0; i < 6; i++)
        {
            Vector2 at = position + Random.insideUnitCircle * size * 0.6f;
            Flash(at, ArtShape.Rect, Random.Range(0.05f, 0.12f), new Color(0.45f, 0.95f, 0.35f, 0.8f), Random.Range(0.2f, 0.5f));
        }
        Flash(position, ArtShape.Round, size * 0.5f, ErrorMagenta, 0.3f);
        return carved;
    }

    /// <summary>在一块区域里随机「挖掉」若干个小方格（腐蚀的视觉核心）。</summary>
    public static int PixelCarve(Vector2 center, float size, int count, Color holeColor)
    {
        Sprite sprite = ArtShapes.Get(ArtShape.Rect);
        if (sprite == null) return 0;

        GameObject root = new GameObject("PixelCarve");
        root.transform.position = new Vector3(center.x, center.y, 0f);

        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            GameObject part = new GameObject("hole");
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = new Vector3(Random.Range(-size * 0.5f, size * 0.5f), Random.Range(-size * 0.5f, size * 0.5f), 0f);
            float cell = Random.Range(size * 0.08f, size * 0.2f);
            part.transform.localScale = new Vector3(cell, cell, 1f);

            SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = holeColor;
            sr.sortingOrder = FxOrder;
        }

        GlitchLifetime life = root.AddComponent<GlitchLifetime>();
        life.Setup(0.5f, true);
        return count;
    }
}

/// <summary>
/// 特效物体的「寿命」：到时间淡出并销毁；也可以让它一边长大一边淡出（冲击环）。
/// </summary>
public class GlitchLifetime : MonoBehaviour
{
    float life;
    float bornTime;
    bool fade;
    float startScale;
    float growTo = -1f;
    SpriteRenderer[] renderers;
    Color[] colors;

    public void Setup(float seconds, bool fadeOut)
    {
        life = Mathf.Max(0.05f, seconds);
        bornTime = Time.time;
        fade = fadeOut;
        startScale = transform.localScale.x;
        Cache();
    }

    /// <summary>在寿命内把整体放大到 <paramref name="size"/>（冲击环用）。</summary>
    public void GrowTo(float size) { growTo = size; }

    void Cache()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            colors[i] = renderers[i] != null ? renderers[i].color : Color.white;
    }

    void Update()
    {
        float age = Time.time - bornTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        float k = Mathf.Clamp01(age / life);
        if (growTo > 0f && startScale > 0.0001f)
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, growTo, k);

        if (!fade || renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = colors[i];
            c.a = colors[i].a * (1f - k);
            renderers[i].color = c;
        }
    }
}

/// <summary>
/// 「贴图撕裂 / 顶点算错」：把物体下面所有精灵**逐帧错位**，可选把排序值也打乱。
/// 结束（或组件被移除）时把位置与排序**原样还回去**，所以不会留下烂摊子。
/// </summary>
public class GlitchJitter : MonoBehaviour
{
    float until;
    float pixels;
    bool scramble;
    Vector3[] basePositions;
    int[] baseOrders;
    Transform[] targets;
    SpriteRenderer[] renderers;
    bool captured;

    /// <summary>开始（或刷新）抖动。<paramref name="pixels"/> 是世界单位的错位幅度。</summary>
    public void Setup(float seconds, float pixels, bool scrambleOrder)
    {
        this.pixels = Mathf.Max(0.001f, pixels);
        scramble = scrambleOrder;
        until = Mathf.Max(until, Time.time + Mathf.Max(0.05f, seconds));
        Capture();
        enabled = true;
    }

    void Capture()
    {
        if (captured) return;
        captured = true;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        targets = new Transform[renderers.Length];
        basePositions = new Vector3[renderers.Length];
        baseOrders = new int[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            targets[i] = renderers[i] != null ? renderers[i].transform : null;
            basePositions[i] = targets[i] != null ? targets[i].localPosition : Vector3.zero;
            baseOrders[i] = renderers[i] != null ? renderers[i].sortingOrder : 0;
        }
    }

    void Update()
    {
        if (targets == null) return;

        if (Time.time >= until)
        {
            Restore();
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;

            // 逐行（逐精灵）独立错位：横向为主，看起来就是画面被撕开
            float jitterX = Random.Range(-pixels, pixels);
            float jitterY = Random.Range(-pixels, pixels) * 0.35f;
            targets[i].localPosition = basePositions[i] + new Vector3(jitterX, jitterY, 0f);

            if (scramble && renderers[i] != null && Random.value < 0.25f)
                renderers[i].sortingOrder = baseOrders[i] + Random.Range(-25, 26);
        }
    }

    void Restore()
    {
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null) targets[i].localPosition = basePositions[i];
                if (renderers != null && renderers[i] != null) renderers[i].sortingOrder = baseOrders[i];
            }
        }
        Destroy(this);
    }

    /// <summary>对象被销毁 / 组件被移除时也要把位置还回去（不然会残留在错位状态）。</summary>
    void OnDisable()
    {
        if (targets == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null) targets[i].localPosition = basePositions[i];
            if (renderers != null && renderers[i] != null) renderers[i].sortingOrder = baseOrders[i];
        }
    }
}
