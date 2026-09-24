using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 HUD。两部分：
/// <list type="bullet">
/// <item>**屏幕正下方**：三条「条」（从下往上 = **体力 / 混乱 / 警觉**），都用长度说话 ——
///       体力条最长、每升一级更长（等级越高体力上限越高），混乱与警觉是两条细一些的条；</item>
/// <item>**左上角**：操作说明 + 小虫状态（已吃数量、速度、能力、当前提示）。</item>
/// </list>
///
/// 版式规矩（<see cref="LayoutPanels"/> 负责，全部运行时算，改文案不会顶出面板）：
/// <list type="number">
/// <item>左上角两块面板的**高度 = 自己文字的高度 + 边距**，再按实际高度依次往下排，所以不会互相压住；</item>
/// <item>底部的条锚在屏幕底边居中，整体往上叠，不占游戏画面也不和右下角悬浮窗打架。</item>
/// </list>
/// </summary>
public class SimpleHUD : MonoBehaviour
{
    [Header("界面")]
    [Tooltip("左上角常驻的操作说明")]
    public TMP_Text instructions;
    [Tooltip("小虫状态 + 操作提示")]
    public TMP_Text status;

    [Header("面板（用于自动排布，避免重叠）")]
    public RectTransform helpPanel;
    public RectTransform statusPanel;
    [Tooltip("两块面板之间的间距")]
    public float panelGap = 10f;
    [Tooltip("操作说明面板比文字多留的高度（上下各一半）")]
    public float helpPadding = 28f;
    [Tooltip("状态面板比文字多留的高度（上下各一半）")]
    public float statusPadding = 20f;

    [Header("体力条（屏幕正下方居中）")]
    public RectTransform staminaBar;
    public Image staminaFill;
    [Tooltip("体力条初始长度（还没长大时），画布单位")]
    public float staminaBarWidth = 460f;
    [Tooltip("每长大一级，体力条加长多少 —— 等级越高血条越长")]
    public float staminaWidthPerLevel = 110f;
    [Tooltip("体力条离屏幕底边的距离")]
    public float staminaBottomMargin = 40f;

    [Header("混乱 / 警觉条（在体力条上方，运行时创建）")]
    [Tooltip("混乱条的高度")]
    public float meterBarHeight = 18f;
    [Tooltip("两条细条与体力条之间的间距")]
    public float meterGap = 7f;
    [Tooltip("低体力时的闪烁频率（每秒几次）")]
    public float lowStaminaBlinkSpeed = 4f;

    [Header("引用")]
    public BugController bug;
    public BugEat eat;
    public DragController interact;
    public AutoSave autoSave;
    public BugVitality vitality;
    public BugGrowth growth;
    [Tooltip("世界（用来显示当前在哪种地貌 / 聚落；留空自动找）")]
    public VillageWorld world;
    [Tooltip("能力栏（留空自动找；小虫身上由 AbilitySet 自动挂上）")]
    public AbilitySet abilitySet;

    [Header("刷新")]
    [Tooltip("状态行的刷新间隔（秒），交互检测比较费，不必每帧跑")]
    public float refreshInterval = 0.15f;

    [Header("村民反应提示")]
    [Tooltip("多远之内有人听到动静才算「附近」（世界的单位；比相机视野略大一点）")]
    public float noticeRadius = 12f;

    /// <summary>左上角常驻的操作说明（交互键统一是 F）。</summary>
    public const string InstructionsText =
        "操作说明\n" +
        "左键点击：移动到该处\n" +
        "空格：吃掉头附近的食物\n" +
        "F：拾取 / 放下、钻地洞 / 出洞、开关\n" +
        "Shift：朝目标冲一小段\n" +
        "数字键 1~5：使用能力（吃电池 / 破布团 / 孢子囊 / 齿轮 / 酸液瓶解锁）\n" +
        "H：本局战果    Esc：返回开始界面\n" +
        "长大后才吃得下：木箱 2 级 / 树 3 级 / 村民 4 级";

    float nextRefresh;
    string lastStatus;

    // 体力条的原始尺寸（场景里定的），运行时只改宽度
    float barHeight = 26f;
    float fillInset;
    float fillHeight = 18f;
    int lastBarLevel = -1;
    float lastStatusTextHeight = -1f;

    // 运行时建的混乱 / 警觉条
    RectTransform chaosBar, chaosFill, alertBar, alertFill;
    TMP_Text staminaValue, chaosLabel, alertLabel;
    Image staminaBack;
    bool metersBuilt;

    void Awake()
    {
        if (bug == null) bug = FindObjectOfType<BugController>();
        if (eat == null) eat = FindObjectOfType<BugEat>();
        if (interact == null) interact = FindObjectOfType<DragController>();
        if (autoSave == null) autoSave = FindObjectOfType<AutoSave>();
        if (vitality == null) vitality = FindObjectOfType<BugVitality>();
        if (growth == null) growth = FindObjectOfType<BugGrowth>();
        if (world == null) world = FindObjectOfType<VillageWorld>();
        if (instructions != null) instructions.text = InstructionsText;
        if (status == null) Debug.LogWarning("[SimpleHUD] 没有指定状态文本，小虫状态不会显示。");

        // 记住场景里给体力条与 Fill 设的尺寸：运行时只改宽度，高度与内缩保持不变
        if (staminaBar != null)
        {
            barHeight = staminaBar.sizeDelta.y;
            staminaBack = staminaBar.GetComponent<Image>();
            if (staminaFill != null)
            {
                fillInset = Mathf.Max(0f, (staminaBar.sizeDelta.x - staminaFill.rectTransform.sizeDelta.x) * 0.5f);
                fillHeight = staminaFill.rectTransform.sizeDelta.y;
            }
        }

        BuildMeters();
        if (status != null) status.text = BuildStatus();
        LayoutPanels();
        UpdateBars();
    }

    /// <summary>
    /// 建「混乱 / 警觉」两条条，并给体力条加**分段刻度 + 数字**（都在运行时建，不改场景）。
    /// 三条条一起摆在屏幕底边居中：从下往上 体力 → 混乱 → 警觉。
    /// </summary>
    void BuildMeters()
    {
        if (metersBuilt || staminaBar == null) return;
        metersBuilt = true;

        TMP_Text template = status != null ? status : instructions;

        // —— 体力条上的分段刻度（25% / 50% / 75%）——
        for (int i = 1; i <= 3; i++)
        {
            GameObject tickGo = new GameObject("Tick" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tickGo.transform.SetParent(staminaBar, false);
            RectTransform tick = tickGo.GetComponent<RectTransform>();
            tick.anchorMin = new Vector2(i * 0.25f, 0f);
            tick.anchorMax = new Vector2(i * 0.25f, 1f);
            tick.pivot = new Vector2(0.5f, 0.5f);
            tick.sizeDelta = new Vector2(2f, -6f);
            tick.anchoredPosition = Vector2.zero;
            Image tickImage = tickGo.GetComponent<Image>();
            tickImage.color = new Color(0f, 0f, 0f, 0.35f);
            tickImage.raycastTarget = false;
        }

        // —— 体力条上的数字（99/100）——
        staminaValue = MakeLabel(staminaBar, "StaminaValue", template, TextAlignmentOptions.Center, 16f,
            Vector2.zero, new Vector2(0f, 0f));

        // —— 混乱条 / 警觉条 ——
        Transform parent = staminaBar.parent != null ? staminaBar.parent : transform;
        chaosBar = MakeMeter(parent, "ChaosBar", template, out chaosFill, out chaosLabel);
        alertBar = MakeMeter(parent, "AlertBar", template, out alertFill, out alertLabel);
    }

    /// <summary>造一条「细条」：底衬 + 填充 + 居中的小字标签。</summary>
    RectTransform MakeMeter(Transform parent, string name, TMP_Text template, out RectTransform fill, out TMP_Text label)
    {
        GameObject backGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backGo.transform.SetParent(parent, false);
        RectTransform back = backGo.GetComponent<RectTransform>();
        back.anchorMin = new Vector2(0.5f, 0f);
        back.anchorMax = new Vector2(0.5f, 0f);
        back.pivot = new Vector2(0.5f, 0f);
        back.sizeDelta = new Vector2(staminaBarWidth, meterBarHeight);

        Image backImage = backGo.GetComponent<Image>();
        backImage.color = staminaBack != null ? staminaBack.color : new Color(0.08f, 0.10f, 0.09f, 0.62f);
        backImage.raycastTarget = false;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(backGo.transform, false);
        fill = fillGo.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = new Vector2(4f, 0f);
        fill.sizeDelta = new Vector2(staminaBarWidth - 8f, -4f);

        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(0.6f, 0.6f, 0.6f);
        fillImage.raycastTarget = false;

        label = MakeLabel(back, name + "Label", template, TextAlignmentOptions.Center, 14f, Vector2.zero, Vector2.zero);
        return back;
    }

    TMP_Text MakeLabel(RectTransform parent, string name, TMP_Text template, TextAlignmentOptions alignment,
        float fontSize, Vector2 position, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

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

    /// <summary>
    /// 排布 HUD：左上角两块面板按**各自文字的实际高度**依次往下排（不会互相压住）；
    /// 底部三条条居中往上叠。**新增面板一律走这里**，不要自己摆位置（红线 23）。
    /// </summary>
    void LayoutPanels()
    {
        float helpHeight = FitPanel(helpPanel, instructions, helpPadding);
        float statusHeight = FitPanel(statusPanel, status, statusPadding);

        float left = helpPanel != null ? helpPanel.anchoredPosition.x : 24f;
        float top = helpPanel != null ? helpPanel.anchoredPosition.y : -24f;

        float cursor = top;
        if (helpPanel != null)
        {
            helpPanel.anchoredPosition = new Vector2(left, cursor);
            cursor -= helpHeight + panelGap;
        }
        if (statusPanel != null)
        {
            statusPanel.anchoredPosition = new Vector2(left, cursor);
            cursor -= statusHeight + panelGap;
        }

        // 底部：体力在最下，混乱、警觉依次往上
        float y = staminaBottomMargin;
        if (staminaBar != null)
        {
            staminaBar.anchorMin = new Vector2(0.5f, 0f);
            staminaBar.anchorMax = new Vector2(0.5f, 0f);
            staminaBar.pivot = new Vector2(0.5f, 0f);
            staminaBar.anchoredPosition = new Vector2(0f, y);
            y += barHeight + meterGap;
        }
        if (chaosBar != null)
        {
            chaosBar.anchoredPosition = new Vector2(0f, y);
            y += meterBarHeight + meterGap;
        }
        if (alertBar != null)
        {
            alertBar.anchoredPosition = new Vector2(0f, y);
        }

        lastStatusTextHeight = status != null ? status.preferredHeight : 0f;
        ApplyBarWidth();
    }

    /// <summary>把面板高度收到「刚好包住文字 + 上下边距」，返回最终高度。</summary>
    float FitPanel(RectTransform panel, TMP_Text text, float padding)
    {
        if (panel == null) return 0f;

        float height = panel.sizeDelta.y;
        if (text != null)
        {
            text.ForceMeshUpdate();
            height = Mathf.Max(1f, text.preferredHeight + padding);
        }
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
        return height;
    }

    void Update()
    {
        UpdateBars();

        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);

        if (status == null) return;
        string text = BuildStatus();
        if (text == lastStatus) return;   // 文本没变就不重建 TMP 网格
        lastStatus = text;
        status.text = text;

        // 提示文字换行 / 变长时重新量一次，面板永远包得住文字、也不会压到下面的东西
        status.ForceMeshUpdate();
        if (Mathf.Abs(status.preferredHeight - lastStatusTextHeight) > 0.5f) LayoutPanels();
    }

    // ---------------- 三条条 ----------------

    /// <summary>当前体力条该有多长：等级越高越长（长大 = 体力上限更高）。</summary>
    float BarWidth
    {
        get { return staminaBarWidth + Mathf.Max(0, CurrentLevel) * staminaWidthPerLevel; }
    }

    int CurrentLevel
    {
        get { return growth != null ? growth.level : 0; }
    }

    void UpdateBars()
    {
        ApplyBarWidth();
        UpdateStaminaFill();
        UpdateMeter(chaosBar, chaosFill, chaosLabel, chaosRatio, chaosTextColor, chaosLabelText);
        UpdateMeter(alertBar, alertFill, alertLabel, alertRatio, alertTextColor, alertLabelText);
    }

    /// <summary>体力条：填充长度 + 低体力闪烁 + 刻度上的数字。</summary>
    void UpdateStaminaFill()
    {
        if (staminaFill == null) return;

        float ratio = vitality != null ? vitality.Ratio : 1f;
        Vector2 size = staminaFill.rectTransform.sizeDelta;
        size.x = Mathf.Max(0f, (BarWidth - fillInset * 2f) * ratio);
        staminaFill.rectTransform.sizeDelta = size;

        Color color;
        if (ratio > 0.6f) color = new Color(0.45f, 0.80f, 0.40f);
        else if (ratio > 0.3f) color = new Color(0.92f, 0.78f, 0.30f);
        else color = new Color(0.88f, 0.32f, 0.28f);

        // 快饿死的时候让条自己闪，玩家不可能没注意
        if (ratio <= 0.3f)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * lowStaminaBlinkSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            color.a = Mathf.Lerp(0.55f, 1f, pulse);
        }
        staminaFill.color = color;

        if (staminaValue != null && vitality != null)
            staminaValue.text = Mathf.CeilToInt(vitality.Stamina) + " / " + Mathf.RoundToInt(vitality.maxStamina);
    }

    void UpdateMeter(RectTransform bar, RectTransform fill, TMP_Text label, float ratio, Color color, string text)
    {
        if (bar == null || fill == null) return;

        float width = staminaBarWidth + Mathf.Max(0, CurrentLevel) * staminaWidthPerLevel;
        bar.sizeDelta = new Vector2(width, meterBarHeight);
        fill.sizeDelta = new Vector2(Mathf.Max(0f, (width - 8f) * Mathf.Clamp01(ratio)), -4f);
        fill.GetComponent<Image>().color = color;
        if (label != null) label.text = text;
    }

    float chaosRatio
    {
        get
        {
            ChaosMeter chaos = ChaosMeter.Instance;
            return chaos != null ? Mathf.Clamp01(chaos.chaos / 100f) : 0f;
        }
    }

    Color chaosTextColor
    {
        get
        {
            int level = ChaosMeter.Instance != null ? ChaosMeter.Instance.Level : 0;
            switch (level)
            {
                case 0: return new Color(0.45f, 0.72f, 0.45f);
                case 1: return new Color(0.72f, 0.75f, 0.38f);
                case 2: return new Color(0.88f, 0.66f, 0.30f);
                case 3: return new Color(0.90f, 0.45f, 0.28f);
                case 4: return new Color(0.88f, 0.30f, 0.45f);
                default: return new Color(1f, 0.20f, 1f);       // 5 级：洋红，正好呼应「游戏出 bug 了」
            }
        }
    }

    string chaosLabelText
    {
        get
        {
            ChaosMeter chaos = ChaosMeter.Instance;
            if (chaos == null) return "";
            return "混乱 " + chaos.Level + " 级 · " + ChaosMeter.LevelName(chaos.Level);
        }
    }

    float alertRatio
    {
        get
        {
            Alertness alert = Alertness.Instance;
            return alert != null ? Mathf.Clamp01(alert.alert / 100f) : 0f;
        }
    }

    Color alertTextColor
    {
        get
        {
            float value = Alertness.Instance != null ? Alertness.Instance.alert : 0f;
            if (value >= alertLine) return new Color(0.92f, 0.35f, 0.32f);
            if (value >= 40f) return new Color(0.92f, 0.76f, 0.35f);
            return new Color(0.48f, 0.68f, 0.88f);
        }
    }

    float alertLine { get { return Alertness.Instance != null ? Alertness.Instance.alertLine : 40f; } }

    string alertLabelText
    {
        get
        {
            Alertness alert = Alertness.Instance;
            if (alert == null) return "";
            string line = "警觉 " + Mathf.RoundToInt(alert.alert) + "%";
            if (alert.alert >= alert.searchLine) line += " · 他们在找你";
            else if (alert.alert >= alertLine) line += " · 村民更警觉了";
            return line;
        }
    }

    /// <summary>等级变了才改长度，避免每帧改 RectTransform 触发重排。</summary>
    void ApplyBarWidth()
    {
        if (staminaBar == null) return;

        int level = CurrentLevel;
        if (level == lastBarLevel) return;

        lastBarLevel = level;
        staminaBar.sizeDelta = new Vector2(BarWidth, barHeight);
    }

    // ---------------- 文字 ----------------

    string BuildStatus()
    {
        return DescribeBug() + NoticeHint() + AbilityHint() + "\n" + BuildHint();
    }

    /// <summary>
    /// 能力栏：已解锁的能力 + 剩余充能 + 键位（一个都没解锁时返回空串，不占行）。
    /// 文案由 <see cref="AbilitySet.HudLine"/> 生成（能力自己最清楚有几个充能、冷却还剩多久）。
    /// </summary>
    string AbilityHint()
    {
        if (abilitySet == null) abilitySet = FindObjectOfType<AbilitySet>();
        if (abilitySet == null) return "";

        string line = abilitySet.HudLine();
        return string.IsNullOrEmpty(line) ? "" : "\n" + line;
    }

    /// <summary>
    /// 附近有没有人注意到动静 —— 玩家得能知道自己吵到人了（不然「搞事要小心」这条玩法就没有反馈）。
    /// 只统计没被冻结的村民（<see cref="Villager.All"/> 里就是这些），并且限定在
    /// <see cref="noticeRadius"/> 之内，免得屏幕外的人也让提示一直挂着。
    /// </summary>
    string NoticeHint()
    {
        if (bug == null) return "";

        Vector2 self = bug.transform.position;
        float radiusSqr = noticeRadius * noticeRadius;
        int alert = 0;
        int coming = 0;

        for (int i = 0; i < Villager.All.Count; i++)
        {
            Villager villager = Villager.All[i];
            if (villager == null || !villager.IsReactingToNoise) continue;
            if (((Vector2)villager.transform.position - self).sqrMagnitude > radiusSqr) continue;

            if (villager.State == VillagerState.Alert) alert++;
            else coming++;      // Investigate / Search：已经动身了
        }

        if (coming > 0) return "\n有人听到动静，正朝这边过来（" + coming + " 个）";
        if (alert > 0) return "\n附近有人听到了动静（" + alert + " 个）";
        return "";
    }

    /// <summary>小虫自己的信息：所在地貌 / 聚落 + 已吃数量 / 体力 / 速度 / 成长 / 状态。</summary>
    string DescribeBug()
    {
        if (bug == null) return "";

        string line = "";
        if (world != null) line += world.CurrentBiomeText + " · ";     // 例如「森林 · 农村」
        line += "已吃 " + (eat != null ? eat.EatenCount : 0) + " 个";
        if (vitality != null) line += " · 体力 " + Mathf.CeilToInt(vitality.Stamina) + "/" + Mathf.RoundToInt(vitality.maxStamina);
        line += " · 速度 " + bug.CurrentSpeed.ToString("0.0");
        if (growth != null && growth.level > 0) line += " · " + growth.DisplayLevel + " 级";

        if (bug.IsHidden) line += " · 躲在地洞里";
        else if (bug.IsDragging) line += " · 搬运中（变慢）";
        else if (bug.IsDashing) line += " · 冲刺";
        else if (bug.IsDisguised) line += " · 伪装中";

        if (autoSave != null && autoSave.JustSaved) line += " · 已保存";
        return line;
    }

    /// <summary>随场合变化的操作提示（交互统一 F）。</summary>
    string BuildHint()
    {
        if (bug != null && bug.IsHidden) return "[F] 出洞（躲着的时候村民看不见你）";

        Burrow burrow = bug != null ? bug.NearbyBurrow : null;
        if (burrow != null && (interact == null || !interact.IsHolding))
            return burrow.IsTunnel ? "[F] 钻进地道，从另一头出来" : "[F] 钻地洞躲起来";

        string hint;
        bool holding = interact != null && interact.IsHolding;
        if (holding) hint = "[F] 放下物品";
        else if (interact != null && interact.FindInteractable() != null) hint = "[F] 拾取物品";
        else hint = "[F] 附近没有可搬物品";

        if (eat != null && eat.FindTarget() != null) hint += " · [空格] 进食";
        else
        {
            // 附近有东西但等级不够：直接告诉玩家要长到几级
            Edible locked = eat != null ? eat.FindLockedTarget() : null;
            hint += locked != null
                ? " · [空格] 长到 " + locked.requiredLevel + " 级才吃得下这个"
                : " · [空格] 没东西可吃";
        }

        return hint;
    }
}
