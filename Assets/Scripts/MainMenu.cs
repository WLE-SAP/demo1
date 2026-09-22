using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始界面：分辨率选择、全屏/窗口切换、继续游戏、开始新游戏。
/// 「继续游戏」只有在存在可用存档时才可点（游戏里每 5 秒自动存一次）。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("场景")]
    public string gameSceneName = "BugScene";

    [Header("UI")]
    public TMP_Text resolutionLabel;
    public TMP_Text fullscreenLabel;
    public Button prevButton;
    public Button nextButton;
    public Button fullscreenButton;
    public Button startButton;
    public Button continueButton;
    public TMP_Text continueLabel;

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
        if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
        if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);

        RefreshContinueButton();
    }

    /// <summary>有存档才让点「继续游戏」，并把状态写在按钮上。</summary>
    void RefreshContinueButton()
    {
        if (continueButton == null) return;

        GameSave save = SaveSystem.Load();
        bool hasSave = save != null;
        continueButton.interactable = hasSave;

        if (continueLabel == null) return;
        if (hasSave)
        {
            continueLabel.text = "继续游戏";
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
    }

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

    /// <summary>开始新的一局（会换一个新的世界种子，存档在进入游戏后马上被覆盖）。</summary>
    public void StartGame()
    {
        Apply();
        SaveSystem.ContinueRequested = false;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>读档继续上次的局面。</summary>
    public void ContinueGame()
    {
        Apply();
        SaveSystem.ContinueRequested = true;
        SceneManager.LoadScene(gameSceneName);
    }
}
