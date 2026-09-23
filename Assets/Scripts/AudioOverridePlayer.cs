using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 音频播放器：全局唯一、跨场景常驻（<c>DontDestroyOnLoad</c>），负责
/// <list type="bullet">
/// <item><b>音效</b>：<see cref="Play(string)"/>，由各个玩法脚本在事件里直接调用；</item>
/// <item><b>背景音乐</b>：进游戏场景放 <see cref="AudioKeys.Bgm"/>，回开始界面放 <see cref="AudioKeys.BgmMenu"/>，
///       两者都是循环播放，切场景时自动换。</item>
/// </list>
///
/// 对象是运行时自动创建的（<c>RuntimeInitializeOnLoadMethod</c>），不需要往场景里拖任何东西；
/// <c>Resources/AudioOverride</c> 里没有音频时全部调用都是空操作，游戏照常静音运行。
/// 音量统一受「游戏设置 → 音量」（<see cref="GameSettings.MasterVolume"/> → <c>AudioListener.volume</c>）控制。
/// </summary>
public class AudioOverridePlayer : MonoBehaviour
{
    /// <summary>进这个场景就放游戏的 BGM（其余场景放菜单的 BGM）。</summary>
    public const string GameSceneName = "BugScene";

    /// <summary>音效音量（0~1，叠在全局主音量之上）。</summary>
    public float sfxVolume = 1f;
    /// <summary>背景音乐音量（0~1，一般比音效低一点）。</summary>
    public float musicVolume = 0.7f;

    static AudioOverridePlayer instance;

    AudioSource sfxSource;
    AudioSource musicSource;
    string currentBgmKey = "";

    /// <summary>当前是否已经在播放音频（没有音频文件时为 false）。</summary>
    public static bool HasAudio { get { return instance != null && AudioOverride.Count > 0; } }

    void Awake()
    {
        instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;      // 2D：不受相机位置影响

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;

        LogLoaded();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>进游戏自动建一个常驻播放器，并套用第一个场景的 BGM。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;

        GameObject root = new GameObject("AudioOverridePlayer");
        root.AddComponent<AudioOverridePlayer>();
        DontDestroyOnLoad(root);

        SceneManager.sceneLoaded += OnSceneLoaded;
        if (instance != null) instance.ApplyScene(SceneManager.GetActiveScene().name);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null) instance.ApplyScene(scene.name);
    }

    // ---------------- 音效 ----------------

    /// <summary>播一个音效 key（没提供这个 key 就什么都不做）。</summary>
    public static void Play(string key)
    {
        if (instance == null) return;
        instance.PlaySfx(key, 1f, 1f);
    }

    /// <summary>播一个音效 key，可以单独调这一声的音量和音高。</summary>
    public static void Play(string key, float volume, float pitch)
    {
        if (instance == null) return;
        instance.PlaySfx(key, volume, pitch);
    }

    void PlaySfx(string key, float volume, float pitch)
    {
        if (sfxSource == null) return;
        AudioClip clip = AudioOverride.Get(key);
        if (clip == null) return;

        sfxSource.pitch = Mathf.Clamp(pitch, 0.2f, 3f);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volume));
    }

    // ---------------- 背景音乐 ----------------

    /// <summary>按场景切 BGM：游戏场景用 <see cref="AudioKeys.Bgm"/>，其余用 <see cref="AudioKeys.BgmMenu"/>。</summary>
    void ApplyScene(string sceneName)
    {
        PlayBgm(sceneName == GameSceneName ? AudioKeys.Bgm : AudioKeys.BgmMenu);
    }

    /// <summary>播放指定 key 的 BGM（循环）；没提供就停掉当前音乐。同一首正在播时不重头再来。</summary>
    public void PlayBgm(string key)
    {
        if (musicSource == null || (currentBgmKey == key && musicSource.isPlaying)) return;

        AudioClip clip = AudioOverride.Get(key);
        currentBgmKey = key;

        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    /// <summary>停掉背景音乐。</summary>
    public void StopBgm()
    {
        currentBgmKey = "";
        if (musicSource != null) musicSource.Stop();
    }

    void LogLoaded()
    {
        int count = AudioOverride.Count;
        if (count == 0)
        {
            Debug.Log("[AudioOverride] Resources/" + AudioOverride.ResourceFolder + " 里没有音频，静音运行。");
            return;
        }

        List<string> keys = AudioOverride.AvailableKeys;
        string message = "[AudioOverride] 音频 " + count + " 个，可用 key：" + string.Join("、", keys.ToArray()) + "。";
        List<string> unmatched = AudioOverride.UnmatchedFiles;
        if (unmatched.Count > 0)
            message += " 没对上 key（名字写错了？）：" + string.Join("、", unmatched.ToArray());
        Debug.Log(message);
    }
}
