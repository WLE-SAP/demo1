using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始界面：
/// <list type="bullet">
/// <item>「开始游戏」**直接开局**（2026-09-25 起不再先选地图 —— 世界每次都是一整片新的随机地图，
///       地貌与聚落由种子决定，见 <see cref="WorldBiome"/>）；</item>
/// <item>「继续游戏」读上次的存档，按钮上会写出存档所在地貌（例如「继续游戏（森林 · 农村）」），
///       没有存档就点不了；</item>
/// <item>「游戏设置」里放分辨率、屏幕模式与音量（音量是全局主音量，写 PlayerPrefs）。</item>
/// </list>
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("场景")]
    public string gameSceneName = "BugScene";

    [Header("主面板")]
    public GameObject mainPanel;
    public Button startButton;
    public Button continueButton;
    public Button settingsButton;
    public TMP_Text continueLabel;

    [Header("游戏设置面板")]
    public GameObject settingsPanel;
    public TMP_Text resolutionLabel;
    public TMP_Text fullscreenLabel;
    public TMP_Text volumeLabel;
    public Button prevButton;
    public Button nextButton;
    public Button fullscreenButton;
    public Button volumeDownButton;
    public Button volumeUpButton;
    public Button settingsBackButton;

    [Header("分辨率")]
    public int minWidth = 800;
    public int minHeight = 600;

    const string KeyWidth = "menu.resWidth";
    const string KeyHeight = "menu.resHeight";
    const string KeyFullscreen = "menu.fullscreen";

    readonly List<Vector2Int> options = new List<Vector2Int>();
    int index;
    bool fullscreen = true;

    void Awake()
    {
        BuildOptions();
        LoadSaved();
        WireButtons();
        GameSettings.Apply();
        ShowMainPanel();     // 打开时只显示主面板（设置面板先收起来）
        // 启动时就把上次保存的分辨率/屏幕模式应用上，避免显示与实际不一致
        Apply();
    }

    void BuildOptions()
    {
        options.Clear();
        Resolution[] resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width < minWidth || resolutions[i].height < minHeight) continue;
            Vector2Int v = new Vector2Int(resolutions[i].width, resolutions[i].height);
            if (!options.Contains(v)) options.Add(v);
        }

        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        if (!options.Contains(current)) options.Add(current);

        options.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
        index = Mathf.Max(0, options.IndexOf(current));
    }

    void LoadSaved()
    {
        fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

        int w = PlayerPrefs.GetInt(KeyWidth, 0);
        int h = PlayerPrefs.GetInt(KeyHeight, 0);
        if (w <= 0 || h <= 0) return;

        int found = options.FindIndex(o => o.x == w && o.y == h);
        if (found >= 0) index = found;
    }

    void WireButtons()
    {
        // 主面板：开始游戏直接开局（世界是随机生成的，不再先选地图）
        Bind(startButton, StartGame);
        Bind(continueButton, ContinueGame);
        Bind(settingsButton, OpenSettings);

        // 游戏设置
        Bind(prevButton, () => Step(-1));
        Bind(nextButton, () => Step(1));
        Bind(fullscreenButton, ToggleFullscreen);
        Bind(volumeDownButton, () => StepVolume(-GameSettings.VolumeStep));
        Bind(volumeUpButton, () => StepVolume(GameSettings.VolumeStep));
        Bind(settingsBackButton, ShowMainPanel);

        RefreshContinueButton();
    }

    /// <summary>绑定按钮：点任何按钮都先响一声 UI 音效（放了 <c>ui_click</c> 才响）。</summary>
    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(() =>
        {
            AudioOverridePlayer.Play(AudioKeys.UiClick);
            action();
        });
    }

    // ---------------- 面板切换 ----------------

    /// <summary>回到主面板（关掉设置）。</summary>
    public void ShowMainPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        RefreshLabels();
    }

    /// <summary>打开「游戏设置」（分辨率 / 屏幕模式 / 音量）。</summary>
    public void OpenSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        RefreshLabels();
    }

    /// <summary>有存档才让点「继续游戏」，并把存档所在地貌写在按钮上。</summary>
    void RefreshContinueButton()
    {
        if (continueButton == null) return;

        GameSave save = SaveSystem.Load();
        bool hasSave = save != null;
        continueButton.interactable = hasSave;

        if (continueLabel == null) return;
        if (hasSave)
        {
            string biome = save.BiomeText;
            continueLabel.text = string.IsNullOrEmpty(biome) ? "继续游戏" : "继续游戏（" + biome + "）";
            continueLabel.color = Color.white;
        }
        else
        {
            continueLabel.text = "还没有存档";
            continueLabel.color = new Color(1f, 1f, 1f, 0.4f);
        }
    }

    void RefreshLabels()
    {
        if (resolutionLabel != null)
        {
            Vector2Int r = options[Mathf.Clamp(index, 0, options.Count - 1)];
            resolutionLabel.text = r.x + " x " + r.y;
        }
        if (fullscreenLabel != null) fullscreenLabel.text = fullscreen ? "全屏" : "窗口";
        if (volumeLabel != null) volumeLabel.text = GameSettings.VolumeText;
    }

    // ---------------- 分辨率 / 屏幕模式 ----------------

    public void Step(int delta)
    {
        if (options.Count == 0) return;
        index = (index + delta + options.Count) % options.Count;
        Apply();
    }

    public void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        Apply();
    }

    /// <summary>把当前的分辨率 / 屏幕模式套用上并存起来。</summary>
    public void Apply()
    {
        if (options.Count == 0) return;
        Vector2Int r = options[Mathf.Clamp(index, 0, options.Count - 1)];
        Screen.SetResolution(r.x, r.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        PlayerPrefs.SetInt(KeyWidth, r.x);
        PlayerPrefs.SetInt(KeyHeight, r.y);
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.Save();

        RefreshLabels();
    }

    // ---------------- 音量 ----------------

    /// <summary>音量 ± 一档（直接听到效果，不需要确认）。</summary>
    public void StepVolume(float delta)
    {
        GameSettings.StepVolume(delta);
        RefreshLabels();
    }

    // ---------------- 开局 ----------------

    /// <summary>
    /// 开始新的一局：直接进游戏场景。**世界是随机生成的**（地貌 / 聚落由新的世界种子决定），
    /// 不再有「先选荒野 / 农村 / 城市」这一步。
    /// </summary>
    public void StartGame()
    {
        Apply();
        SaveSystem.ContinueRequested = false;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>读档继续上次的局面（世界用存档里的种子重建）。</summary>
    public void ContinueGame()
    {
        Apply();
        SaveSystem.ContinueRequested = true;
        SceneManager.LoadScene(gameSceneName);
    }
}
