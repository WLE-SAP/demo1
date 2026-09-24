using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **屏幕级故障**：小虫搞事时，整块屏幕会「像游戏出 bug 一样」闪一下 ——
/// 白闪 / 撕裂条 / 扫描线 / 局部黑屏 / **左下角红色滚动报错**。
///
/// 和世界级的 <see cref="AbilityFx"/> 分工：
/// <list type="bullet">
/// <item>世界级 = 场景里的东西坏了（缺图、撕裂、复制、排序乱）；</item>
/// <item>屏幕级 = **游戏本身**坏了（渲染层出问题、控制台刷报错）。</item>
/// </list>
///
/// 覆盖层运行时建在 HUD 画布上（不改场景），**订阅 <see cref="GameEvent"/>**：
/// 用能力 / **解锁新能力** / 警报 / 混乱升级都会自动播对应的组合 —— 加新能力不用改这里。
/// 所有效果都带时长，到点自己归零，**绝不会糊在屏幕上**。
/// </summary>
public class GlitchOverlay : MonoBehaviour
{
    /// <summary>场上唯一。</summary>
    public static GlitchOverlay Instance { get; private set; }

    [Header("强度（怕晃眼就把这些调小）")]
    [Tooltip("全屏闪帧的最大不透明度")]
    public float flashAlpha = 0.35f;
    [Tooltip("撕裂条最多几条")]
    public int maxTearBars = 5;
    [Tooltip("HUD / 屏幕整体抖动的最大像素")]
    public float shakePixels = 3f;
    [Tooltip("「剧烈抖动」时抖多少像素（解锁能力用）")]
    public float violentShakePixels = 9f;
    [Tooltip("局部黑屏最多几块")]
    public int maxBlackoutBands = 6;

    [Header("左下角报错控制台")]
    [Tooltip("红色报错最多同时显示几行（新行把旧行往上顶 = 滚动）")]
    public int consoleLines = 5;
    [Tooltip("每行报错留多久")]
    public float consoleLineSeconds = 2.2f;
    [Tooltip("每行文字从左边滑进来的距离（像素）")]
    public float consoleSlideFrom = 60f;

    [Header("总开关")]
    [Tooltip("关掉就完全没有屏幕级故障（只保留世界里的表现）")]
    public bool enabledFx = true;

    RectTransform root;
    Image flashImage;
    RectTransform[] tearBars;
    RectTransform[] blackoutBands;
    RectTransform[] scanlines;
    RectTransform consoleRoot;
    readonly List<TMP_Text> consoleTexts = new List<TMP_Text>();
    readonly List<float> consoleBorn = new List<float>();

    float flashUntil;
    float tearUntil;
    float scanUntil;
    float blackoutUntil;
    float shakeUntil;
    float shakeStrength = 3f;
    Color flashColor = Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        if (Object.FindObjectOfType<GlitchOverlay>() != null) return;
        GameObject host = GameObject.Find("GameDirector");
        if (host == null)
        {
            BugController bug = Object.FindObjectOfType<BugController>();
            if (bug == null) return;
            host = bug.gameObject;
        }
        host.AddComponent<GlitchOverlay>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvent.AbilityUsed += OnAbilityUsed;
        GameEvent.AlarmRaised += OnAlarm;
        GameEvent.ChaosLevelChanged += OnChaosLevel;
    }

    void OnDisable()
    {
        GameEvent.AbilityUsed -= OnAbilityUsed;
        GameEvent.AlarmRaised -= OnAlarm;
        GameEvent.ChaosLevelChanged -= OnChaosLevel;
    }

    // ---------------- 事件 → 故障组合 ----------------

    void OnAbilityUsed(AbilityId id, Vector2 position)
    {
        if (!enabledFx) return;

        switch (id)
        {
            case AbilityId.Shock:
                Flash(Color.white, 0.10f);
                Tear(0.35f, 4);
                LogError("NullReferenceException: Villager.body");
                break;
            case AbilityId.Disguise:
                Scanlines(0.5f);
                LogError("Texture 'bug_head' not found");
                break;
            case AbilityId.Split:
                Tear(0.45f, 5);
                LogError("Duplicate entity detected");
                break;
            case AbilityId.Shake:
                Tear(0.6f, 5);
                Flash(new Color(1f, 0f, 1f, 1f), 0.18f);
                LogError("Transform jitter overflow");
                break;
            case AbilityId.Corrode:
                Scanlines(0.45f);
                LogError("IndexOutOfRangeException: pixel");
                break;
        }
    }

    void OnAlarm(Vector2 position)
    {
        if (!enabledFx) return;
        Flash(new Color(1f, 0.25f, 0.2f, 1f), 0.2f);
        Tear(0.5f, 5);
        Scanlines(1.2f);
        LogError("FATAL: alarm subsystem");
    }

    void OnChaosLevel(int level)
    {
        if (!enabledFx || level < 4) return;
        Scanlines(level >= 5 ? 3f : 1.5f);
        Tear(0.8f, 3);
        LogError("World state unstable (chaos " + level + ")");
    }

    // ---------------- 播放接口 ----------------

    /// <summary>全屏闪一帧颜色。</summary>
    public void Flash(Color color, float seconds)
    {
        EnsureBuilt();
        if (flashImage == null) return;
        flashColor = color;
        flashUntil = Mathf.Max(flashUntil, Time.time + Mathf.Max(0.02f, seconds));
    }

    /// <summary>画几条横向撕裂条（错位的色块），一会儿自己消失。</summary>
    public void Tear(float seconds, int count)
    {
        EnsureBuilt();
        if (tearBars == null) return;

        tearUntil = Mathf.Max(tearUntil, Time.time + Mathf.Max(0.05f, seconds));
        for (int i = 0; i < tearBars.Length; i++)
        {
            if (tearBars[i] == null) continue;
            bool on = i < Mathf.Min(count, maxTearBars);
            tearBars[i].gameObject.SetActive(on);
            if (on) RandomizeBand(tearBars[i], 3f, 10f);
        }
    }

    /// <summary>**局部黑屏**：几块黑色横条不规则地盖住画面一部分（解锁新能力时用）。</summary>
    public void Blackout(float seconds, int count)
    {
        EnsureBuilt();
        if (blackoutBands == null) return;

        blackoutUntil = Mathf.Max(blackoutUntil, Time.time + Mathf.Max(0.05f, seconds));
        for (int i = 0; i < blackoutBands.Length; i++)
        {
            if (blackoutBands[i] == null) continue;
            bool on = i < Mathf.Min(count, maxBlackoutBands);
            blackoutBands[i].gameObject.SetActive(on);
            if (on) RandomizeBand(blackoutBands[i], Screen.height * 0.05f, Screen.height * 0.22f);
        }
    }

    void RandomizeBand(RectTransform band, float minHeight, float maxHeight)
    {
        band.anchoredPosition = new Vector2(Random.Range(-40f, 40f), Random.Range(-Screen.height * 0.45f, Screen.height * 0.45f));
        band.sizeDelta = new Vector2(Screen.width + 80f, Random.Range(minHeight, maxHeight));
    }

    /// <summary>扫描线：一层细横线，像老显示器 / 渲染出错。</summary>
    public void Scanlines(float seconds)
    {
        EnsureBuilt();
        if (scanlines == null) return;
        scanUntil = Mathf.Max(scanUntil, Time.time + Mathf.Max(0.05f, seconds));
    }

    /// <summary>
    /// 在**左下角**的控制台里压一行红色报错（新行把旧行往上顶 = 滚动）。
    /// 所有「故障场景」都走这里，别自己到处飘字。
    /// </summary>
    public void LogError(string message)
    {
        EnsureBuilt();
        if (consoleTexts.Count == 0) return;

        // 整列往上挪一行，然后把新行放到最底下
        for (int i = 0; i < consoleTexts.Count - 1; i++)
        {
            consoleTexts[i].text = consoleTexts[i + 1].text;
            consoleBorn[i] = consoleBorn[i + 1];
        }
        int last = consoleTexts.Count - 1;
        consoleTexts[last].text = message;
        consoleBorn[last] = Time.time;
        consoleTexts[last].rectTransform.anchoredPosition = new Vector2(consoleSlideFrom, consoleTexts[last].rectTransform.anchoredPosition.y);
    }

    /// <summary>旧名字（世界里的故障也用它），等价于 <see cref="LogError"/>。</summary>
    public void FakeError(string message) { LogError(message); }

    /// <summary>
    /// **解锁新能力**的那一下：局部黑屏 + 撕裂加剧烈抖动 + 洋红闪 + 一行报错。
    /// 这是「小虫又长本事了」的仪式感，也是用户点名要的效果。
    /// </summary>
    public void AbilityUnlocked(string abilityName)
    {
        if (!enabledFx) return;

        Blackout(0.55f, 5);
        Tear(0.75f, maxTearBars);
        Flash(new Color(1f, 0f, 1f, 1f), 0.14f);
        ViolentShake(0.75f);
        LogError("AbilityUnlockedException: " + abilityName);
        LogError("MemoryPatch applied: " + abilityName);
    }

    /// <summary>剧烈抖动（解锁能力 / 大事故用）。</summary>
    public void ViolentShake(float seconds)
    {
        EnsureBuilt();
        shakeUntil = Mathf.Max(shakeUntil, Time.time + Mathf.Max(0.05f, seconds));
        shakeStrength = violentShakePixels;
    }

    /// <summary>现在屏幕上有故障吗（验证用）。</summary>
    public bool IsGlitching
    {
        get
        {
            return Time.time < flashUntil || Time.time < tearUntil || Time.time < scanUntil
                || Time.time < blackoutUntil || Time.time < shakeUntil;
        }
    }

    /// <summary>左下角控制台现在显示的最后一行（验证用）。</summary>
    public string LastError
    {
        get
        {
            for (int i = consoleTexts.Count - 1; i >= 0; i--)
                if (consoleTexts[i] != null && !string.IsNullOrEmpty(consoleTexts[i].text)) return consoleTexts[i].text;
            return "";
        }
    }

    /// <summary>控制台现在有几行字（验证用）。</summary>
    public int ErrorLineCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < consoleTexts.Count; i++)
                if (consoleTexts[i] != null && !string.IsNullOrEmpty(consoleTexts[i].text)) n++;
            return n;
        }
    }

    /// <summary>控制台锚点在左下角吗（验证用）。</summary>
    public bool ConsoleAtBottomLeft
    {
        get
        {
            if (consoleRoot == null) return false;
            return consoleRoot.anchorMin.x < 0.01f && consoleRoot.anchorMin.y < 0.01f
                && consoleRoot.anchorMax.x < 0.01f && consoleRoot.anchorMax.y < 0.01f;
        }
    }

    void Update()
    {
        if (root == null) return;

        UpdateFlash();
        UpdateBands(tearBars, tearUntil, 3f, 10f);
        UpdateBands(blackoutBands, blackoutUntil, Screen.height * 0.05f, Screen.height * 0.22f);
        UpdateScanlines();
        UpdateConsole();
        UpdateShake();
    }

    void UpdateFlash()
    {
        if (flashImage == null) return;

        bool on = Time.time < flashUntil;
        flashImage.enabled = on;
        if (!on) return;
        Color c = flashColor;
        c.a = flashAlpha;
        flashImage.color = c;
    }

    void UpdateBands(RectTransform[] bands, float until, float minHeight, float maxHeight)
    {
        if (bands == null) return;

        bool on = Time.time < until;
        for (int i = 0; i < bands.Length; i++)
        {
            if (bands[i] == null || !bands[i].gameObject.activeSelf) continue;
            if (!on)
            {
                bands[i].gameObject.SetActive(false);
                continue;
            }
            // 期间每帧换个位置：看起来才像画面被撕开
            RandomizeBand(bands[i], minHeight, maxHeight);
        }
    }

    void UpdateScanlines()
    {
        if (scanlines == null) return;

        bool on = Time.time < scanUntil;
        for (int i = 0; i < scanlines.Length; i++)
        {
            if (scanlines[i] == null) continue;
            if (scanlines[i].gameObject.activeSelf != on) scanlines[i].gameObject.SetActive(on);
            if (on)
                scanlines[i].anchoredPosition = new Vector2(0f,
                    Mathf.Repeat(Time.unscaledTime * 60f + i * 6f, Screen.height) - Screen.height * 0.5f);
        }
    }

    void UpdateConsole()
    {
        for (int i = 0; i < consoleTexts.Count; i++)
        {
            TMP_Text line = consoleTexts[i];
            if (line == null) continue;

            float age = Time.time - consoleBorn[i];
            bool alive = age < consoleLineSeconds && !string.IsNullOrEmpty(line.text);
            if (line.gameObject.activeSelf != alive) line.gameObject.SetActive(alive);
            if (!alive) continue;

            // 新行从左边滑进来 + 最后半秒淡出
            Vector2 pos = line.rectTransform.anchoredPosition;
            pos.x = Mathf.MoveTowards(pos.x, 0f, consoleSlideFrom * 4f * Time.deltaTime);
            line.rectTransform.anchoredPosition = pos;

            Color c = line.color;
            c.a = age > consoleLineSeconds - 0.5f ? Mathf.Clamp01((consoleLineSeconds - age) / 0.5f) : 1f;
            line.color = c;
        }
    }

    void UpdateShake()
    {
        bool shaking = Time.time < shakeUntil;
        float amount = shaking ? shakeStrength : (Time.time < tearUntil ? shakePixels : 0f);
        // 抖完必须归零，否则整个 HUD 会一直偏着
        root.anchoredPosition = amount > 0f
            ? new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount))
            : Vector2.zero;
        if (!shaking) shakeStrength = shakePixels;
    }

    /// <summary>建覆盖层（挂在 HUD 画布下，不改场景）。</summary>
    void EnsureBuilt()
    {
        if (root != null) return;

        Canvas canvas = null;
        SimpleHUD hud = FindObjectOfType<SimpleHUD>();
        if (hud != null && hud.statusPanel != null && hud.statusPanel.parent != null)
            canvas = hud.statusPanel.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        TMP_Text template = hud != null && hud.status != null ? hud.status : null;

        GameObject rootGo = new GameObject("GlitchOverlay", typeof(RectTransform));
        rootGo.transform.SetParent(canvas.transform, false);
        root = rootGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();          // 盖在所有 HUD 之上

        // 全屏闪帧
        GameObject flashGo = MakeBand(root, "Flash", new Color(1f, 1f, 1f, 0f));
        flashImage = flashGo.GetComponent<Image>();
        flashImage.enabled = false;

        // 撕裂条
        tearBars = new RectTransform[maxTearBars];
        for (int i = 0; i < tearBars.Length; i++)
        {
            Color color = i % 2 == 0
                ? new Color(1f, 0f, 1f, 0.35f)         // 洋红：错误材质
                : new Color(0.4f, 1f, 0.4f, 0.28f);    // 绿：老显示器的错位
            GameObject bar = MakeBand(root, "TearBar" + i, color);
            tearBars[i] = bar.GetComponent<RectTransform>();
            bar.SetActive(false);
        }

        // 局部黑屏（不透明黑条）
        blackoutBands = new RectTransform[maxBlackoutBands];
        for (int i = 0; i < blackoutBands.Length; i++)
        {
            GameObject band = MakeBand(root, "Blackout" + i, Color.black);
            blackoutBands[i] = band.GetComponent<RectTransform>();
            band.SetActive(false);
        }

        // 扫描线
        scanlines = new RectTransform[24];
        for (int i = 0; i < scanlines.Length; i++)
        {
            GameObject line = MakeBand(root, "Scanline" + i, new Color(0f, 0f, 0f, 0.16f));
            scanlines[i] = line.GetComponent<RectTransform>();
            scanlines[i].sizeDelta = new Vector2(Screen.width, 2f);
            line.SetActive(false);
        }

        BuildConsole(template);
    }

    GameObject MakeBand(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Screen.width, 6f);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    /// <summary>左下角的红色报错控制台：若干行等高等宽的文本，从下往上排。</summary>
    void BuildConsole(TMP_Text template)
    {
        GameObject consoleGo = new GameObject("ErrorConsole", typeof(RectTransform));
        consoleGo.transform.SetParent(root, false);
        consoleRoot = consoleGo.GetComponent<RectTransform>();
        consoleRoot.anchorMin = new Vector2(0f, 0f);      // ← 左下角
        consoleRoot.anchorMax = new Vector2(0f, 0f);
        consoleRoot.pivot = new Vector2(0f, 0f);
        consoleRoot.anchoredPosition = new Vector2(18f, 18f);
        consoleRoot.sizeDelta = new Vector2(760f, 24f * consoleLines);

        consoleTexts.Clear();
        consoleBorn.Clear();

        for (int i = 0; i < consoleLines; i++)
        {
            GameObject lineGo = new GameObject("Error" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lineGo.transform.SetParent(consoleRoot, false);
            RectTransform rect = lineGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(760f, 24f);
            rect.anchoredPosition = new Vector2(0f, i * 24f);   // 第 0 行在最下面

            TextMeshProUGUI text = lineGo.GetComponent<TextMeshProUGUI>();
            if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
            }
            text.fontSize = 20f;
            text.color = new Color(1f, 0.25f, 0.25f);           // 红
            text.alignment = TextAlignmentOptions.BottomLeft;
            text.raycastTarget = false;
            text.text = "";
            lineGo.SetActive(false);

            consoleTexts.Add(text);
            consoleBorn.Add(-999f);
        }
    }
}
