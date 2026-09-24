using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕左上角的 HUD —— **只放小虫自己的信息和操作指引**，不放世界 / 村民的调试信息。
/// <list type="bullet">
/// <item>左上角：常驻操作说明 + 小虫状态行（已吃数量、体力、移动速度、成长等级、当前状态）；</item>
/// <item>屏幕正下方：体力条（不吃东西会一直掉，掉光就饿死）—— **等级越高、条越长**，代表体力上限变大；</item>
/// <item>右下角：靠近东西时的悬浮窗（<see cref="EncounterWindow"/>）。</item>
/// </list>
/// 版式的两条规矩（<see cref="LayoutPanels"/> 负责，都是运行时算的，改文案不会顶出面板）：
/// <list type="number">
/// <item>左上角两块面板的**高度永远等于自己文字的高度 + 边距**，再按实际高度依次往下排，所以不会互相压住；</item>
/// <item>体力条锚在屏幕底边居中，两侧留空，不占游戏画面也不和右下角悬浮窗打架。</item>
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

    [Header("引用")]
    public BugController bug;
    public BugEat eat;
    public DragController interact;
    public AutoSave autoSave;
    public BugVitality vitality;
    public BugGrowth growth;

    [Header("刷新")]
    [Tooltip("状态行的刷新间隔（秒），交互检测比较费，不必每帧跑")]
    public float refreshInterval = 0.15f;

    /// <summary>左上角常驻的操作说明（交互键统一是 F）。</summary>
    public const string InstructionsText =
        "操作说明\n" +
        "左键点击：移动到该处\n" +
        "空格：吃掉头附近的食物\n" +
        "F：拾取 / 放下、钻地洞 / 出洞\n" +
        "Shift：朝目标冲一小段\n" +
        "Esc：返回开始界面\n" +
        "长大后才吃得下：木箱 2 级 / 树 3 级 / 村民 4 级\n" +
        "每 5 秒自动保存一次";

    float nextRefresh;
    string lastStatus;

    // 体力条的原始尺寸（场景里定的），运行时只改宽度的「初始值」与「每级增量」
    float barHeight = 26f;
    float fillInset;
    float fillHeight = 18f;
    int lastBarLevel = -1;
    float lastStatusTextHeight = -1f;

    void Awake()
    {
        if (bug == null) bug = FindObjectOfType<BugController>();
        if (eat == null) eat = FindObjectOfType<BugEat>();
        if (interact == null) interact = FindObjectOfType<DragController>();
        if (autoSave == null) autoSave = FindObjectOfType<AutoSave>();
        if (vitality == null) vitality = FindObjectOfType<BugVitality>();
        if (growth == null) growth = FindObjectOfType<BugGrowth>();
        if (instructions != null) instructions.text = InstructionsText;
        if (status == null) Debug.LogWarning("[SimpleHUD] 没有指定状态文本，小虫状态不会显示。");

        // 记住场景里给体力条与 Fill 设的尺寸：运行时只改宽度，高度与内缩保持不变
        if (staminaBar != null)
        {
            barHeight = staminaBar.sizeDelta.y;
            if (staminaFill != null)
            {
                fillInset = Mathf.Max(0f, (staminaBar.sizeDelta.x - staminaFill.rectTransform.sizeDelta.x) * 0.5f);
                fillHeight = staminaFill.rectTransform.sizeDelta.y;
            }
        }

        // 先给状态行一个初值，第一帧的面板高度才是对的
        if (status != null) status.text = BuildStatus();
        LayoutPanels();
        UpdateStaminaBar();
    }

    /// <summary>
    /// 排布 HUD：左上角两块面板按**各自文字的实际高度**依次往下排（不会互相压住），
    /// 体力条钉在屏幕底边居中。
    /// </summary>
    void LayoutPanels()
    {
        float helpHeight = FitPanel(helpPanel, instructions, helpPadding);
        float statusHeight = FitPanel(statusPanel, status, statusPadding);

        if (helpPanel != null && statusPanel != null)
        {
            Vector2 helpPosition = helpPanel.anchoredPosition;
            statusPanel.anchoredPosition = new Vector2(helpPosition.x, helpPosition.y - helpHeight - panelGap);
        }

        if (staminaBar != null)
        {
            staminaBar.anchorMin = new Vector2(0.5f, 0f);
            staminaBar.anchorMax = new Vector2(0.5f, 0f);
            staminaBar.pivot = new Vector2(0.5f, 0f);
            staminaBar.anchoredPosition = new Vector2(0f, staminaBottomMargin);
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
        UpdateStaminaBar();

        if (status == null) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);

        string text = BuildStatus();
        if (text == lastStatus) return;   // 文本没变就不重建 TMP 网格
        lastStatus = text;
        status.text = text;

        // 提示文字换行 / 变长时重新量一次，面板永远包得住文字、也不会压到下面的东西
        status.ForceMeshUpdate();
        if (Mathf.Abs(status.preferredHeight - lastStatusTextHeight) > 0.5f) LayoutPanels();
    }

    /// <summary>当前体力条该有多长：等级越高越长（长大 = 体力上限更高）。</summary>
    float BarWidth
    {
        get { return staminaBarWidth + Mathf.Max(0, CurrentLevel) * staminaWidthPerLevel; }
    }

    int CurrentLevel
    {
        get { return growth != null ? growth.level : 0; }
    }

    void UpdateStaminaBar()
    {
        ApplyBarWidth();
        if (staminaFill == null) return;

        float ratio = vitality != null ? vitality.Ratio : 1f;
        Vector2 size = staminaFill.rectTransform.sizeDelta;
        size.x = Mathf.Max(0f, (BarWidth - fillInset * 2f) * ratio);
        staminaFill.rectTransform.sizeDelta = size;

        if (ratio > 0.6f) staminaFill.color = new Color(0.45f, 0.80f, 0.40f);
        else if (ratio > 0.3f) staminaFill.color = new Color(0.92f, 0.78f, 0.30f);
        else staminaFill.color = new Color(0.88f, 0.32f, 0.28f);
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

    string BuildStatus()
    {
        return DescribeBug() + "\n" + BuildHint();
    }

    /// <summary>小虫自己的信息：已吃数量 / 体力 / 速度 / 成长 / 状态。</summary>
    string DescribeBug()
    {
        if (bug == null) return "";

        string line = "已吃 " + (eat != null ? eat.EatenCount : 0) + " 个";
        if (vitality != null) line += " · 体力 " + Mathf.CeilToInt(vitality.Stamina) + "/" + Mathf.RoundToInt(vitality.maxStamina);
        line += " · 速度 " + bug.CurrentSpeed.ToString("0.0");
        if (growth != null && growth.level > 0) line += " · " + growth.DisplayLevel + " 级";

        if (bug.IsHidden) line += " · 躲在地洞里";
        else if (bug.IsDragging) line += " · 搬运中（变慢）";
        else if (bug.IsDashing) line += " · 冲刺";

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
        else if (interact != null && interact.FindInteractable() != null) hint = "[F] 拾取箱子";
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
