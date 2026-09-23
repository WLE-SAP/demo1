using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕左上角的 HUD —— **只放小虫自己的信息和操作指引**，不放世界 / 村民的调试信息。
/// <list type="bullet">
/// <item>常驻操作说明；</item>
/// <item>小虫状态行：已吃数量、体力、移动速度、成长等级、当前状态（搬运中 / 躲在地洞）；</item>
/// <item>随场合变化的操作提示（F / 空格 / Shift）与自动存档提示；</item>
/// <item>体力条（不吃东西会一直掉，掉光就饿死）。</item>
/// </list>
/// 面板位置在 <see cref="Awake"/> 里按前一块的实际高度往下排，避免两块 HUD 叠在一起互相遮挡。
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

    [Header("体力条")]
    public RectTransform staminaBar;
    public Image staminaFill;
    public float staminaBarWidth = 460f;

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

        LayoutPanels();
    }

    /// <summary>把状态面板和体力条依次排到操作说明下面，避免互相遮挡。</summary>
    void LayoutPanels()
    {
        if (helpPanel == null || statusPanel == null) return;

        Vector2 helpPosition = helpPanel.anchoredPosition;
        float helpHeight = helpPanel.sizeDelta.y;

        statusPanel.anchoredPosition = new Vector2(helpPosition.x, helpPosition.y - helpHeight - panelGap);

        if (staminaBar != null)
            staminaBar.anchoredPosition = new Vector2(helpPosition.x, statusPanel.anchoredPosition.y - statusPanel.sizeDelta.y - panelGap);
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
    }

    void UpdateStaminaBar()
    {
        if (staminaFill == null) return;

        float ratio = vitality != null ? vitality.Ratio : 1f;
        Vector2 size = staminaFill.rectTransform.sizeDelta;
        size.x = Mathf.Max(0f, staminaBarWidth * ratio);
        staminaFill.rectTransform.sizeDelta = size;

        if (ratio > 0.6f) staminaFill.color = new Color(0.45f, 0.80f, 0.40f);
        else if (ratio > 0.3f) staminaFill.color = new Color(0.92f, 0.78f, 0.30f);
        else staminaFill.color = new Color(0.88f, 0.32f, 0.28f);
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
