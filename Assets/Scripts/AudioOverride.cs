using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 音频覆盖：把音频文件放进 <c>Assets/Resources/AudioOverride/</c>，游戏就会在对应事件上自动播放。
///
/// 规则：
/// <list type="bullet">
/// <item>文件夹里没有音频 → 全程静音（不报错、不影响玩法），这就是默认状态。</item>
/// <item>文件名（不含扩展名）= 逻辑 key。<c>eat.wav</c> / <c>SFX_Eat.ogg</c> / <c>se_eat.mp3</c> 都算 <c>eat</c>。</item>
/// <item>比较时忽略大小写、下划线、短横线和空格（<c>ui_click</c> = <c>UIClick</c> = <c>ui-click</c>），
///       并自动忽略 <c>sfx_</c> / <c>se_</c> / <c>sound_</c> / <c>audio_</c> 前缀。</item>
/// <item>同一个 key 只放一个文件；放了多个时，谁先被载入就用谁。</item>
/// </list>
///
/// 哪些 key 会在什么时候响、BGM 怎么随场景切换，见 <see cref="AudioOverridePlayer"/>；
/// key 常量见 <see cref="AudioKeys"/>。音量统一由「游戏设置 → 音量」（<see cref="GameSettings.MasterVolume"/>）
/// 控制，这里不做单独的音频音量。
/// </summary>
public static class AudioOverride
{
    /// <summary>音频存放目录（相对 Resources）。</summary>
    public const string ResourceFolder = "AudioOverride";

    /// <summary>逻辑 key → 允许的文件名（都会归一化后比较，所以怎么拼都认）。</summary>
    static readonly Dictionary<string, string[]> AliasSource = new Dictionary<string, string[]>
    {
        { AudioKeys.Eat,     new[] { "eat", "eat_food", "eating", "chew", "chewing", "bite" } },
        { AudioKeys.Grow,    new[] { "grow", "growth", "levelup", "powerup", "evolve" } },
        { AudioKeys.Pickup,  new[] { "pickup", "grab", "take" } },
        { AudioKeys.Drop,    new[] { "drop", "put", "putdown", "place", "release" } },
        { AudioKeys.Step,    new[] { "step", "steps", "footstep", "footsteps", "walk" } },
        { AudioKeys.UiClick, new[] { "uiclick", "click", "button", "ui", "menuclick" } },
        { AudioKeys.Bgm,     new[] { "bgm", "bgmgame", "bgmgameplay", "gamemusic", "gameplaymusic", "music" } },
        { AudioKeys.BgmMenu, new[] { "bgmmenu", "menubgm", "menumusic", "musicmenu" } },
    };

    /// <summary>自动忽略的常见前缀（归一化之后的名字再比较，所以 sfx_eat → eat）。</summary>
    static readonly string[] Prefixes = { "sfx", "se", "sound", "audio" };

    static Dictionary<string, string> aliases;
    static Dictionary<string, AudioClip> table;
    static readonly List<string> files = new List<string>();

    /// <summary>已载入的音频文件数（0 = 全程静音）。</summary>
    public static int Count
    {
        get { EnsureLoaded(); return files.Count; }
    }

    /// <summary>文件夹里已经能用上的 key（没匹配上的文件不算）。</summary>
    public static List<string> AvailableKeys
    {
        get
        {
            EnsureLoaded();
            List<string> keys = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                string key = ResolveKey(files[i]);
                if (key.Length > 0 && !keys.Contains(key)) keys.Add(key);
            }
            return keys;
        }
    }

    /// <summary>文件名没对上任何 key 的音频，用来提示命名写错了。</summary>
    public static List<string> UnmatchedFiles
    {
        get
        {
            EnsureLoaded();
            List<string> rest = new List<string>();
            for (int i = 0; i < files.Count; i++)
                if (ResolveKey(files[i]).Length == 0) rest.Add(files[i]);
            return rest;
        }
    }

    /// <summary>把名字归一化：只留下小写字母和数字，用于匹配（SFX_Eat 与 sfxeat 等价）。</summary>
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        StringBuilder sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = char.ToLowerInvariant(name[i]);
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>把文件名（或路径）解析成逻辑 key；不认识时返回空字符串。</summary>
    public static string ResolveKey(string fileNameOrPath)
    {
        if (string.IsNullOrEmpty(fileNameOrPath)) return "";
        if (aliases == null) BuildAliases();

        string key = NormalizeName(System.IO.Path.GetFileNameWithoutExtension(fileNameOrPath));
        if (key.Length == 0) return "";

        string found;
        if (aliases.TryGetValue(key, out found)) return found;

        // 去掉 sfx_ / se_ / sound_ / audio_ 前缀再试一次
        for (int i = 0; i < Prefixes.Length; i++)
        {
            if (key.Length <= Prefixes[i].Length || !key.StartsWith(Prefixes[i])) continue;
            if (aliases.TryGetValue(key.Substring(Prefixes[i].Length), out found)) return found;
        }
        return "";
    }

    /// <summary>取某个 key 的音频；没有提供这个 key 时返回 null（调用方直接不发声即可）。</summary>
    public static AudioClip Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureLoaded();
        if (table.Count == 0) return null;

        AudioClip clip;
        table.TryGetValue(NormalizeName(key), out clip);
        return clip;
    }

    /// <summary>清空缓存（运行中换了文件夹内容后可手动调用一次）。</summary>
    public static void Reset()
    {
        table = null;
        files.Clear();
    }

    static void BuildAliases()
    {
        aliases = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string[]> pair in AliasSource)
        {
            aliases[NormalizeName(pair.Key)] = pair.Key;
            for (int i = 0; i < pair.Value.Length; i++)
                aliases[NormalizeName(pair.Value[i])] = pair.Key;
        }
    }

    static void EnsureLoaded()
    {
        if (table != null) return;

        table = new Dictionary<string, AudioClip>();
        files.Clear();

        AudioClip[] clips = Resources.LoadAll<AudioClip>(ResourceFolder);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null) continue;

            files.Add(clip.name);

            string key = ResolveKey(clip.name);
            if (key.Length == 0) continue;

            // 同时用「文件名」和「逻辑 key」两个名字登记，取的时候两种写法都能拿到
            string fileName = NormalizeName(clip.name);
            if (!table.ContainsKey(fileName)) table[fileName] = clip;

            string keyName = NormalizeName(key);
            if (!table.ContainsKey(keyName)) table[keyName] = clip;
        }
    }
}

/// <summary>
/// 游戏会去取的音频 key。<c>Assets/Resources/AudioOverride/</c> 里的文件名只要能被
/// <see cref="AudioOverride.ResolveKey"/> 认成下面任意一个 key，就会用在对应的地方。
/// </summary>
public static class AudioKeys
{
    /// <summary>吃掉东西（果子 / 树叶 / 木箱 / 树 / 村民）时响一次。</summary>
    public const string Eat = "eat";
    /// <summary>吃到神奇果实长大一级时，紧跟在 <see cref="Eat"/> 后面再响一次。</summary>
    public const string Grow = "grow";
    /// <summary>F 键拾取物品。</summary>
    public const string Pickup = "pickup";
    /// <summary>F 键放下物品（含搬运中被删掉时自动脱手）。</summary>
    public const string Drop = "drop";
    /// <summary>小虫走路时的脚步，按速度控制间隔。</summary>
    public const string Step = "step";
    /// <summary>开始界面里点任何按钮。</summary>
    public const string UiClick = "ui_click";
    /// <summary>游戏中（BugScene）的背景音乐，循环播放。</summary>
    public const string Bgm = "bgm";
    /// <summary>开始界面（MainMenu）的背景音乐，循环播放。</summary>
    public const string BgmMenu = "bgm_menu";
}
