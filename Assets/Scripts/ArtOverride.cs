using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 美术覆盖（**一物一图**）：把玩家放进 <c>Assets/Resources/ArtOverride/</c> 的图片，
/// 按 key 精确替换游戏里**某一个**物件的程序化图元。
///
/// 规则：
/// <list type="bullet">
/// <item>一个 key 只管一个物件 —— <c>house_roof.png</c> 只换屋顶，不会连带换掉木箱或村民的身子；</item>
/// <item>目录里没有图片 → 全部保持程序化美术（默认状态，不报错）；</item>
/// <item>文件名（不含扩展名）= key，比较时忽略大小写、下划线、短横线和空格；</item>
/// <item>同一个 key 放了多张图 → 只用先载入的那张，其余在 Console 里点名。</item>
/// </list>
///
/// 每个 key 的「原素材 / 是否保留代码染色 / 是否九宫格」记在 <see cref="Slots"/> 里：
/// 编辑器导入时用它反推 Pixels Per Unit 与九宫格边框（见 <c>Editor/ArtOverridePostprocessor</c>），
/// 运行时用它决定套用图片后要不要把颜色刷成白色（见 <see cref="Apply"/>）。
/// </summary>
public static class ArtOverride
{
    /// <summary>图片存放目录（相对 Resources）。</summary>
    public const string ResourceFolder = "ArtOverride";

    /// <summary>一个 key 的定义。</summary>
    public struct Slot
    {
        /// <summary>对应的原素材（<c>Assets/Sprites</c> 里的名字），只在导入时用来反推尺寸与九宫格。</summary>
        public readonly string source;

        /// <summary>
        /// 套用图片后是否**保留**代码给的颜色。
        /// false（默认）= 图片原样显示（颜色刷成白色）；
        /// true = 图片只换形状，颜色仍由代码决定（村民职业色、按钮配色这类「颜色本身就是信息」的地方）。
        /// </summary>
        public readonly bool tint;

        /// <summary>是否按九宫格（Sliced）使用：导入时会把边框按原素材的比例放大到新图片上。</summary>
        public readonly bool sliced;

        public Slot(string source, bool tint = false, bool sliced = false)
        {
            this.source = source;
            this.tint = tint;
            this.sliced = sliced;
        }
    }

    /// <summary>
    /// 全部 key。**新增 / 改名 / 删除 key 时，要同时改三处**：
    /// 这里 + 根 <c>README.md</c> 的表格 + <c>Assets/Resources/ArtOverride/README.md</c>。
    /// </summary>
    public static readonly Dictionary<string, Slot> Slots = new Dictionary<string, Slot>
    {
        // —— 小虫与交互 ——
        { ArtKeys.BugHead,        new Slot("S_BugHead") },
        { ArtKeys.Crosshair,      new Slot("S_Crosshair") },
        { ArtKeys.Highlight,      new Slot("S_Disc") },
        { ArtKeys.HighlightRect,  new Slot("S_RoundRect", false, true) },

        // —— 地面 ——
        { ArtKeys.Ground,         new Slot("T_Grass") },
        { ArtKeys.Road,           new Slot("T_Road") },

        // —— 房屋与商店 ——
        { ArtKeys.HouseRoof,      new Slot("S_Rect", false, true) },
        { ArtKeys.HouseWall,      new Slot("S_Rect", false, true) },
        { ArtKeys.HouseDoor,      new Slot("S_Rect", false, true) },
        { ArtKeys.HouseWindow,    new Slot("S_Rect", false, true) },
        { ArtKeys.HouseChimney,   new Slot("S_Rect", false, true) },
        { ArtKeys.ShopSign,       new Slot("S_Rect", false, true) },
        { ArtKeys.Anvil,          new Slot("S_Rect") },
        { ArtKeys.Forge,          new Slot("S_Disc") },

        // —— 村民（颜色 = 职业 / 肤色，所以保留染色）——
        { ArtKeys.VillagerBody,   new Slot("S_Rect", true) },
        { ArtKeys.VillagerHead,   new Slot("S_Disc", true) },

        // —— 自然 ——
        { ArtKeys.TreeCanopy,     new Slot("S_Disc") },
        { ArtKeys.Berry,          new Slot("S_FoodBerry") },
        { ArtKeys.Leaf,           new Slot("S_FoodLeaf") },
        { ArtKeys.SpecialFood,    new Slot("S_Disc") },

        // —— 农田 / 牧场 / 花坛 ——
        { ArtKeys.FarmSoil,       new Slot("S_Rect", false, true) },
        { ArtKeys.FarmRow,        new Slot("S_Rect") },
        { ArtKeys.FarmSprout,     new Slot("S_Disc") },
        { ArtKeys.PenGrass,       new Slot("S_Rect", false, true) },
        { ArtKeys.Fence,          new Slot("S_Rect") },
        { ArtKeys.Sheep,          new Slot("S_Disc") },
        { ArtKeys.SheepHead,      new Slot("S_Disc") },
        { ArtKeys.GardenBed,      new Slot("S_Rect", false, true) },
        { ArtKeys.GardenEdge,     new Slot("S_Rect") },
        { ArtKeys.GardenFlower,   new Slot("S_Disc") },

        // —— 村里的小设施 ——
        { ArtKeys.WellRim,        new Slot("S_Disc") },
        { ArtKeys.WellWater,      new Slot("S_Disc") },
        { ArtKeys.WellPost,       new Slot("S_Disc") },
        { ArtKeys.StallCounter,   new Slot("S_Rect") },
        { ArtKeys.StallAwning,    new Slot("S_Rect") },
        { ArtKeys.StallPost,      new Slot("S_Rect") },
        { ArtKeys.StallGoods,     new Slot("S_Disc") },
        { ArtKeys.BenchSeat,      new Slot("S_Rect") },
        { ArtKeys.BenchBack,      new Slot("S_Rect") },
        { ArtKeys.BoardPost,      new Slot("S_Rect") },
        { ArtKeys.Board,          new Slot("S_Rect") },
        { ArtKeys.BoardPaper,     new Slot("S_Rect") },
        { ArtKeys.LampPost,       new Slot("S_Rect") },
        { ArtKeys.LampHead,       new Slot("S_Disc") },

        // —— 地洞 / 地道 / 道具 ——
        { ArtKeys.BurrowRim,      new Slot("S_Disc") },
        { ArtKeys.BurrowHole,     new Slot("S_Disc") },
        { ArtKeys.BurrowInner,    new Slot("S_Disc") },
        { ArtKeys.BurrowStone,    new Slot("S_Disc") },
        { ArtKeys.Crate,          new Slot("S_Rect", false, true) },

        // —— 开始界面（颜色沿用界面配色，所以保留染色）——
        { ArtKeys.MenuBackground, new Slot("T_Grass") },
        { ArtKeys.MenuPanel,      new Slot("S_RoundRect", true, true) },
        { ArtKeys.MenuButton,     new Slot("S_RoundRect", true, true) },
    };

    static Dictionary<string, Sprite> table;            // 归一化 key → 图片
    static Dictionary<string, string> owners;           // 归一化 key → 图片文件名
    static Dictionary<string, string> canonical;        // 归一化 key → 正式 key（写日志用）

    static readonly List<string> files = new List<string>();
    static readonly List<string> unmatched = new List<string>();
    static readonly HashSet<string> used = new HashSet<string>();

    /// <summary>已载入的图片张数（0 = 全部使用程序化美术）。</summary>
    public static int Count
    {
        get { EnsureLoaded(); return files.Count; }
    }

    /// <summary>已经生效的 key 数量。</summary>
    public static int UsedKeyCount { get { return used.Count; } }

    /// <summary>放了但名字没对上任何 key 的图片（多半是拼错了），用来提示。</summary>
    public static List<string> UnmatchedFiles
    {
        get { EnsureLoaded(); return new List<string>(unmatched); }
    }

    /// <summary>把名字归一化：去掉分隔符并转小写，用于匹配（<c>HouseRoof</c> 与 <c>house_roof</c> 等价）。</summary>
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

    /// <summary>某个图片文件名对应哪个 key（找不到返回 null）。编辑器导入时用来推算尺寸。</summary>
    public static string ResolveKey(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        EnsureCanonical();
        string key;
        return canonical.TryGetValue(NormalizeName(Path.GetFileNameWithoutExtension(fileName)), out key) ? key : null;
    }

    /// <summary>取用来替换某个 key 的图片；没放图片时返回 null。</summary>
    public static Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureLoaded();
        if (table.Count == 0) return null;

        Sprite sprite;
        if (!table.TryGetValue(NormalizeName(key), out sprite) || sprite == null) return null;
        used.Add(NormalizeName(key));
        return sprite;
    }

    /// <summary>这个 key 有没有对应的图片。</summary>
    public static bool Has(string key)
    {
        return Get(key) != null;
    }

    /// <summary>有图片就用图片，没有就用传入的程序化精灵。</summary>
    public static Sprite Or(string key, Sprite fallback)
    {
        Sprite sprite = Get(key);
        return sprite != null ? sprite : fallback;
    }

    /// <summary>
    /// 把某个 key 的图片套到这个渲染器上。返回是否真的替换了。
    /// 除 <see cref="Slot.tint"/> 为 true 的 key（村民、界面）外，替换后颜色会刷成白色，让图片原样显示。
    /// </summary>
    public static bool Apply(SpriteRenderer renderer, string key)
    {
        if (renderer == null || string.IsNullOrEmpty(key)) return false;

        Sprite replacement = Get(key);
        if (replacement == null) return false;

        renderer.sprite = replacement;
        if (!TintOf(key)) renderer.color = Color.white;
        return true;
    }

    /// <summary>这个 key 是否保留代码染色。</summary>
    public static bool TintOf(string key)
    {
        Slot slot;
        return Slots.TryGetValue(key, out slot) && slot.tint;
    }

    /// <summary>把场景里所有 <see cref="ArtSlot"/>（含未激活对象）套用一遍，返回被替换的渲染器数量。</summary>
    public static int ApplyAll()
    {
        EnsureLoaded();

        int applied = 0;
        ArtSlot[] slots = Object.FindObjectsOfType<ArtSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            applied += slots[i].Apply();
        }
        return applied;
    }

    /// <summary>清空缓存（换文件夹内容后可手动调用一次）。</summary>
    public static void Reset()
    {
        table = null;
        owners = null;
        files.Clear();
        unmatched.Clear();
        used.Clear();
    }

    static void EnsureCanonical()
    {
        if (canonical != null) return;
        canonical = new Dictionary<string, string>();
        foreach (KeyValuePair<string, Slot> pair in Slots)
        {
            string norm = NormalizeName(pair.Key);
            if (norm.Length == 0 || canonical.ContainsKey(norm)) continue;
            canonical[norm] = pair.Key;
        }
    }

    static void EnsureLoaded()
    {
        if (table != null) return;

        EnsureCanonical();
        table = new Dictionary<string, Sprite>();
        owners = new Dictionary<string, string>();
        files.Clear();
        unmatched.Clear();

        Sprite[] sprites = Resources.LoadAll<Sprite>(ResourceFolder);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null) continue;

            string norm = NormalizeName(sprite.name);
            string key;
            if (norm.Length == 0 || !canonical.TryGetValue(norm, out key))
            {
                unmatched.Add(sprite.name);
                continue;
            }

            if (table.ContainsKey(norm))
            {
                Debug.LogWarning("[ArtOverride] key " + key + " 放了多张图片，只用先载入的 "
                    + owners[norm] + "，" + sprite.name + " 被忽略。");
                continue;
            }

            files.Add(sprite.name);
            table[norm] = sprite;
            owners[norm] = sprite.name;
        }

        if (files.Count > 0)
            Debug.Log("[ArtOverride] 从 Resources/" + ResourceFolder + " 载入 " + files.Count + " 张图片。");
    }
}

/// <summary>
/// 全部美术 key 的常量表（写法与 <see cref="AudioKeys"/> 一致）。
/// 用常量而不是散落的字符串，改名时编译器会帮你找出所有调用点。
/// </summary>
public static class ArtKeys
{
    // 小虫与交互
    public const string BugHead = "bug_head";
    public const string Crosshair = "crosshair";
    public const string Highlight = "highlight";
    public const string HighlightRect = "highlight_rect";

    // 地面
    public const string Ground = "ground";
    public const string Road = "road";

    // 房屋与商店
    public const string HouseRoof = "house_roof";
    public const string HouseWall = "house_wall";
    public const string HouseDoor = "house_door";
    public const string HouseWindow = "house_window";
    public const string HouseChimney = "house_chimney";
    public const string ShopSign = "shop_sign";
    public const string Anvil = "anvil";
    public const string Forge = "forge";

    // 村民
    public const string VillagerBody = "villager_body";
    public const string VillagerHead = "villager_head";

    // 自然
    public const string TreeCanopy = "tree_canopy";
    public const string Berry = "berry";
    public const string Leaf = "leaf";
    public const string SpecialFood = "special_food";

    // 农田 / 牧场 / 花坛
    public const string FarmSoil = "farm_soil";
    public const string FarmRow = "farm_row";
    public const string FarmSprout = "farm_sprout";
    public const string PenGrass = "pen_grass";
    public const string Fence = "fence";
    public const string Sheep = "sheep";
    public const string SheepHead = "sheep_head";
    public const string GardenBed = "garden_bed";
    public const string GardenEdge = "garden_edge";
    public const string GardenFlower = "garden_flower";

    // 村里的小设施
    public const string WellRim = "well_rim";
    public const string WellWater = "well_water";
    public const string WellPost = "well_post";
    public const string StallCounter = "stall_counter";
    public const string StallAwning = "stall_awning";
    public const string StallPost = "stall_post";
    public const string StallGoods = "stall_goods";
    public const string BenchSeat = "bench_seat";
    public const string BenchBack = "bench_back";
    public const string BoardPost = "board_post";
    public const string Board = "board";
    public const string BoardPaper = "board_paper";
    public const string LampPost = "lamp_post";
    public const string LampHead = "lamp_head";

    // 地洞 / 地道 / 道具
    public const string BurrowRim = "burrow_rim";
    public const string BurrowHole = "burrow_hole";
    public const string BurrowInner = "burrow_inner";
    public const string BurrowStone = "burrow_stone";
    public const string Crate = "crate";

    // 开始界面
    public const string MenuBackground = "menu_background";
    public const string MenuPanel = "menu_panel";
    public const string MenuButton = "menu_button";
}
