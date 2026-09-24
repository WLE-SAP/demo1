using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算面板（设计文档 §17「关卡评分」）：屏幕中央的一块半透明面板，
/// 列出这一局的数据 —— 破坏程度 / 吞噬数量 / 被发现次数 / 最长连锁 / 最大混乱 / 用时 / 任务完成数。
///
/// **H 键随时开关**；**饿死时自动弹出**（<see cref="BugVitality"/> 会等一会儿再回菜单，
/// 这就是「关卡评分」那一刻）。Esc 的语义不变（还是返回开始界面）。
///
/// 面板是运行时建的（不改场景），底板的颜色 / 字体都照抄现有 HUD，保证观感一致。
/// </summary>
public class SettlementPanel : MonoBehaviour
{
    /// <summary>场上唯一（HUD 面板 / 饿死流程都找它）。</summary>
    public static SettlementPanel Instance { get; private set; }

    [Header("开关")]
    [Tooltip("手动开关结算面板的键")]
    public KeyCode toggleKey = KeyCode.H;

    [Header("版式")]
    [Tooltip("面板宽度（画布单位）")]
    public float panelWidth = 720f;
    [Tooltip("面板内边距")]
    public float padding = 36f;

    RectTransform panel;
    TMP_Text title;
    TMP_Text body;
    bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        if (Object.FindObjectOfType<SettlementPanel>() != null) return;
        GameObject host = GameObject.Find("GameDirector");
        if (host == null)
        {
            BugController bug = Object.FindObjectOfType<BugController>();
            if (bug == null) return;
            host = bug.gameObject;
        }
        host.AddComponent<SettlementPanel>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetVisible(!visible, false);
    }

    /// <summary>显示 / 隐藏结算面板。<paramref name="forced"/> = true 时忽略按键再触发一次。</summary>
    public void SetVisible(bool value, bool forced)
    {
        EnsureBuilt();
        if (panel == null) return;

        visible = value;
        panel.gameObject.SetActive(value);
        if (value) Refresh();
    }

    /// <summary>结算面板现在是开着的吗（饿死流程会问）。</summary>
    public bool IsVisible { get { return visible; } }

    /// <summary>面板本体（验证 / 别处要摆位置时用；面板是运行时建在 HUD 画布下的，不是挂在 GameDirector 上）。</summary>
    public RectTransform Panel
    {
        get { EnsureBuilt(); return panel; }
    }

    /// <summary>面板正文（验证用）。</summary>
    public string BodyText { get { return body != null ? body.text : ""; } }

    /// <summary>把这一局的数字重新算一遍并填进面板。</summary>
    public void Refresh()
    {
        if (body == null) return;

        RunStats stats = RunStats.Instance;
        ChaosMeter chaos = ChaosMeter.Instance;
        Alertness alert = Alertness.Instance;
        AutoSave save = FindObjectOfType<AutoSave>();

        int broken = stats != null ? stats.Broken : 0;
        int eaten = stats != null ? stats.Eaten : 0;
        int spotted = stats != null ? stats.Spotted : 0;
        int slips = stats != null ? stats.Slips : 0;
        int longest = stats != null ? stats.LongestChain : 0;
        int percent = stats != null ? stats.DestructionPercent : 0;
        int chaosLevel = chaos != null ? chaos.MaxLevel : 0;
        int alertMax = alert != null ? Mathf.RoundToInt(alert.MaxAlert) : 0;
        float seconds = save != null ? save.PlaySeconds : 0f;

        body.text =
            "破坏程度    " + percent + "%（打碎 " + broken + " 样）\n" +
            "吞噬数量    " + eaten + " 个\n" +
            "被发现次数  " + spotted + " 次\n" +
            "最长连锁    " + longest + " 连\n" +
            "滑倒的村民  " + slips + " 人\n" +
            "最大混乱    " + chaosLevel + " 级（" + ChaosMeter.LevelName(chaosLevel) + "）\n" +
            "最高警觉    " + alertMax + "%\n" +
            "用时        " + FormatTime(seconds);

        if (title != null) title.text = "这一局你干了些啥";
    }

    static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return (total / 60) + " 分 " + (total % 60) + " 秒";
    }

    /// <summary>按现有 HUD 的样子建面板（照抄底板的颜色与文字字体）。</summary>
    void EnsureBuilt()
    {
        if (panel != null) return;

        SimpleHUD hud = FindObjectOfType<SimpleHUD>();
        Transform parent = null;
        Image reference = null;
        TMP_Text template = null;

        if (hud != null)
        {
            if (hud.statusPanel != null)
            {
                parent = hud.statusPanel.parent;
                reference = hud.statusPanel.GetComponent<Image>();
            }
            template = hud.status != null ? hud.status : hud.instructions;
        }
        if (parent == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            parent = canvas.transform;
        }

        GameObject panelGo = new GameObject("SettlementPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(parent, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(panelWidth, 320f);

        Image image = panelGo.GetComponent<Image>();
        // 比左上角那两块更不透明一点：这是要读数字的面板
        image.color = reference != null
            ? new Color(reference.color.r, reference.color.g, reference.color.b, 0.88f)
            : new Color(0.05f, 0.09f, 0.06f, 0.88f);
        image.raycastTarget = false;

        title = MakeText(panelGo.transform, "Title", template, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(padding, -padding - 56f), new Vector2(-padding, -padding),
            TextAlignmentOptions.Center, 34f, new Vector2(0.5f, 1f));
        title.text = "这一局你干了些啥";

        body = MakeText(panelGo.transform, "Body", template, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(padding, padding + 30f), new Vector2(-padding, -padding - 56f),
            TextAlignmentOptions.TopLeft, 24f, new Vector2(0.5f, 1f));
        body.text = "";

        AddHint(panelGo.transform, template);

        panel.gameObject.SetActive(false);
    }

    /// <summary>按现有 HUD 的字体建一段文字；<paramref name="offsetMin"/>/<paramref name="offsetMax"/> 直接就是矩形边界。</summary>
    TMP_Text MakeText(Transform parent, string name, TMP_Text template, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions alignment, float fontSize, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = pivot;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
        }
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    void AddHint(Transform parent, TMP_Text template)
    {
        TMP_Text hint = MakeText(parent, "Hint", template, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(padding, padding - 6f), new Vector2(-padding, padding + 20f),
            TextAlignmentOptions.Right, 18f, new Vector2(0.5f, 0f));
        hint.color = new Color(1f, 1f, 1f, 0.65f);
        hint.text = "[" + toggleKey + "] 关闭";
    }
}
