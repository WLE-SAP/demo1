using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 美术覆盖：把玩家放进 <c>Assets/Resources/ArtOverride/</c> 的图片，按“素材名”替换游戏里
/// 原本用程序化图元画的精灵。
///
/// 规则：
/// <list type="bullet">
/// <item>文件夹里没有图片 → 覆盖表为空，游戏完全使用原有的程序化美术（默认状态）。</item>
/// <item>有图片 → 凡是使用“被替换精灵”的渲染器都会换成图片（<see cref="Apply"/> / <see cref="ApplyAll"/>）。</item>
/// <item>文件名可以直接用原始精灵名（<c>S_BugHead.png</c>），也可以用友好别名（<c>bug_head.png</c>）。</item>
/// <item>比较时忽略大小写、下划线、短横线和空格，所以 <c>BugHead.png</c> 也能匹配 <c>S_BugHead</c>。</item>
/// </list>
/// </summary>
public static class ArtOverride
{
    /// <summary>图片存放目录（相对 Resources）。</summary>
    public const string ResourceFolder = "ArtOverride";

    /// <summary>
    /// 友好文件名 → 原始精灵名。两张表都按“归一化名字”比较，写起来可读即可。
    /// 只列当前游戏真正用到的精灵：木箱、房屋、村民身子共用 S_Rect，所以没有单独的 crate 别名。
    /// （S_Bush / S_Rock / S_Eye / S_Crate / S_Fence 在 Assets/Sprites 里存在，但本游戏已经没用到，
    ///   放同名图片不会生效，运行时会被列进“未匹配到素材”。）
    /// </summary>
    static readonly Dictionary<string, string> AliasSource = new Dictionary<string, string>
    {
        { "bug_head", "S_BugHead" },
        { "crosshair", "S_Crosshair" },
        { "marker", "S_Crosshair" },
        { "ground", "T_Grass" },
        { "grass", "T_Grass" },
        { "road", "T_Road" },
        { "rect", "S_Rect" },
        { "roundrect", "S_RoundRect" },
        { "disc", "S_Disc" },
        { "berry", "S_FoodBerry" },
        { "leaf", "S_FoodLeaf" },
    };

    static Dictionary<string, string> aliases;
    static Dictionary<string, Sprite> table;
    static Dictionary<string, string> owners;
    static readonly List<string> files = new List<string>();
    static readonly HashSet<string> usedFiles = new HashSet<string>();

    /// <summary>归一化后的“友好别名 → 精灵名”。</summary>
    static Dictionary<string, string> Aliases
    {
        get
        {
            if (aliases != null) return aliases;
            aliases = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in AliasSource)
                aliases[NormalizeName(pair.Key)] = NormalizeName(pair.Value);
            return aliases;
        }
    }

    /// <summary>已载入的图片张数（0 = 全部使用程序化美术）。</summary>
    public static int Count
    {
        get { EnsureLoaded(); return files.Count; }
    }

    /// <summary>放了但没有任何渲染器用到、也就是没生效的图片，用来提示命名写错了。</summary>
    public static List<string> UnusedFiles
    {
        get
        {
            EnsureLoaded();
            List<string> rest = new List<string>();
            for (int i = 0; i < files.Count; i++)
                if (!usedFiles.Contains(files[i])) rest.Add(files[i]);
            return rest;
        }
    }

    /// <summary>把名字归一化：去掉分隔符并转小写，用于匹配（S_BugHead 与 bughead 都可比较）。</summary>
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

    /// <summary>某个图片文件名要替换的原始精灵名（归一化）。编辑器导入时用来推算尺寸。</summary>
    public static string ResolveSpriteKey(string fileName)
    {
        string key = NormalizeName(System.IO.Path.GetFileNameWithoutExtension(fileName));
        string mapped;
        if (Aliases.TryGetValue(key, out mapped)) return mapped;
        return key;
    }

    /// <summary>取用来替换某个精灵的图片；没有对应图片时返回 null。</summary>
    public static Sprite Get(Sprite original)
    {
        if (original == null) return null;
        EnsureLoaded();
        if (table.Count == 0) return null;

        string key = NormalizeName(original.name);
        Sprite replacement;
        if (!table.TryGetValue(key, out replacement)) return null;

        string owner;
        if (owners.TryGetValue(key, out owner)) usedFiles.Add(owner);
        return replacement;
    }

    /// <summary>如果该渲染器用的是被替换的精灵，就换成图片。返回是否发生了替换。</summary>
    public static bool Apply(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return false;
        Sprite replacement = Get(renderer.sprite);
        if (replacement == null || replacement == renderer.sprite) return false;
        renderer.sprite = replacement;
        return true;
    }

    /// <summary>把覆盖表套用到场景里所有精灵（含未激活对象）。返回被替换的渲染器数量。</summary>
    public static int ApplyAll()
    {
        EnsureLoaded();
        if (table.Count == 0) return 0;

        int applied = 0;
        SpriteRenderer[] all = Object.FindObjectsOfType<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
            if (Apply(all[i])) applied++;
        return applied;
    }

    /// <summary>清空缓存（换文件夹内容后可手动调用一次）。</summary>
    public static void Reset()
    {
        table = null;
        owners = null;
        files.Clear();
        usedFiles.Clear();
    }

    static void EnsureLoaded()
    {
        if (table != null) return;

        table = new Dictionary<string, Sprite>();
        owners = new Dictionary<string, string>();
        files.Clear();
        usedFiles.Clear();

        Sprite[] sprites = Resources.LoadAll<Sprite>(ResourceFolder);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null) continue;

            string key = NormalizeName(sprite.name);
            if (key.Length == 0) continue;

            files.Add(sprite.name);
            table[key] = sprite;
            owners[key] = sprite.name;

            // 友好别名：bug_head.png 也能替换 S_BugHead
            string spriteName;
            if (Aliases.TryGetValue(key, out spriteName) && spriteName.Length > 0)
            {
                table[spriteName] = sprite;
                owners[spriteName] = sprite.name;
            }
        }

        if (files.Count > 0)
            Debug.Log("[ArtOverride] 从 Resources/" + ResourceFolder + " 载入 " + files.Count + " 张图片。");
    }
}
