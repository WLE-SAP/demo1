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
/// <item>**同一个 key 可以放多张图 = 这个物件的「变体」**：文件名结尾加数字区分
///       （<c>tree1.png</c> / <c>tree2.png</c>… 都是 <c>tree</c> 的变体），
///       运行时按地貌权重或位置随机取一张，所以一片林子里不会每棵树长得一模一样；
///       只放一张 = 全场都用这一张（和以前一样），放几张也不会再被当成「重名报错」；</item>
/// <item>**整图素材**（<see cref="Slot.whole"/>：树 / 石头 / 灌木这类「一张图 = 一个完整物件」）
///       导入时按不透明内容自动裁边，运行时按内容比例装进占地（见 <see cref="ArtShapes.AddWholeImage"/>）。</item>
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

        /// <summary>
        /// 是不是**平铺**素材（地面 / 路面）。导入时会把 Wrap Mode 设成 <c>Repeat</c>，
        /// 否则平铺时边缘会被拉伸出一道糊边（2026-09-25 修）。
        /// </summary>
        public readonly bool tiling;

        /// <summary>
        /// 是不是**整图素材**（一整棵树 / 一块石头 / 一丛灌木这种「一张图 = 一个完整物件」）。
        /// 导入时**按不透明内容自动裁掉透明边**，运行时再按内容比例装进占地
        /// （见 <see cref="ArtShapes.AddWholeImage"/>）——
        /// 美术习惯把物体画在画布中间，不裁的话物体会只有应有大小的三分之一，
        /// 而且脚下的影子 / 占地圈都比物体大出一圈。
        /// </summary>
        public readonly bool whole;

        public Slot(string source, bool tint = false, bool sliced = false, bool tiling = false, bool whole = false)
        {
            this.source = source;
            this.tint = tint;
            this.sliced = sliced;
            this.tiling = tiling;
            this.whole = whole;
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

        // —— 地面与道路（都是**平铺**素材，必须无缝接得上）——
        // 草地 / 沙漠 / 石铺路都可以放多张（grass1/2、sand1~4、gravel1/2）：同名多图 = 变体，
        // 草地按「区块」随机取一张（见 VillageGenerator.BuildGround），所以一片沙漠不会只有一种沙。
        { ArtKeys.Ground,         new Slot("T_Grass", false, false, true) },
        { ArtKeys.Road,           new Slot("T_Road", false, false, true) },
        { ArtKeys.GroundForest,   new Slot("T_Grass", false, false, true) },
        { ArtKeys.GroundDesert,   new Slot("T_Road", false, false, true) },
        { ArtKeys.RoadDirt,       new Slot("T_Road", false, false, true) },
        { ArtKeys.RoadPaved,      new Slot("T_Road", false, false, true) },

        // —— 路口瓦片（**一张图 = 一个路口形状**，尺寸由代码按「路面宽度」反推，不用管导入的 PPU）——
        // 每一张都画好了「哪里是路、哪里是地面」，路要在哪几边走由文件名说了算：
        // cross 四边、t_intersection_* 三边（缺的那边就是名字里的方向）、corner_* 相邻两边、
        // road_head_* 只有一边（路到这儿就断了）。**只在草原这类草地地面上铺**（原因见 README）。
        { ArtKeys.RoadCross,          new Slot("") },
        { ArtKeys.RoadTUp,            new Slot("") },
        { ArtKeys.RoadTDown,          new Slot("") },
        { ArtKeys.RoadTLeft,          new Slot("") },
        { ArtKeys.RoadTRight,         new Slot("") },
        { ArtKeys.RoadCornerUpLeft,   new Slot("") },
        { ArtKeys.RoadCornerUpRight,  new Slot("") },
        { ArtKeys.RoadCornerDownLeft, new Slot("") },
        { ArtKeys.RoadCornerDownRight, new Slot("") },
        { ArtKeys.RoadHeadUp,         new Slot("") },
        { ArtKeys.RoadHeadDown,       new Slot("") },
        { ArtKeys.RoadHeadLeft,       new Slot("") },
        { ArtKeys.RoadHeadRight,      new Slot("") },
        // 直路段瓦片（path1 = 竖直、path2 = 水平）：沿路平铺，缺了就用路面的贴图
        { ArtKeys.RoadStraightH,      new Slot("", false, false, true) },
        { ArtKeys.RoadStraightV,      new Slot("", false, false, true) },

        // —— 房屋与商店 ——
        { ArtKeys.HouseRoof,      new Slot("S_Rect", false, true) },
        { ArtKeys.HouseWall,      new Slot("S_Rect", false, true) },
        { ArtKeys.HouseDoor,      new Slot("S_Rect", false, true) },
        { ArtKeys.HouseWindow,    new Slot("S_Rect", false, true) },
        { ArtKeys.HouseChimney,   new Slot("S_Rect", false, true) },
        { ArtKeys.ShopSign,       new Slot("S_Rect", false, true) },
        { ArtKeys.Anvil,          new Slot("S_Rect") },
        { ArtKeys.Forge,          new Slot("S_Disc") },

        // —— 不同聚落的房型（房子随聚落换样子：农村农舍 / 两层小楼 / 谷仓，城市排屋 / 公寓，荒野木屋）——
        { ArtKeys.HouseUpperWall, new Slot("S_Rect", false, true) },
        { ArtKeys.ApartmentRoof,  new Slot("S_Rect", false, true) },
        { ArtKeys.ApartmentWindow, new Slot("S_Rect", false, true) },
        { ArtKeys.BarnRoof,       new Slot("S_Rect", false, true) },
        { ArtKeys.BarnDoor,       new Slot("S_Rect", false, true) },
        { ArtKeys.CabinWall,      new Slot("S_Rect", false, true) },
        { ArtKeys.CabinRoof,      new Slot("S_Rect", false, true) },

        // —— 整栋建筑（一张图 = 一整栋）：放了图就整栋用这张图，不再拼分件墙 / 门 / 窗 / 屋顶 / 烟囱 ——
        // 这些 key 一律**不**做九宫格：整栋图由代码按原比例缩放进占地矩形（VillageGenerator.AddWholeBuilding）。
        // 它们也是**整图素材**（`whole`）：美术习惯把房子画在画布中间，导入时按内容裁掉透明边，
        // 摆放时才「按房子本身的比例 + 底边贴地」，不然房子会悬在地上、或者只有应有大小的三分之二。
        { ArtKeys.HouseCottage,   new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.HouseTwoStory,  new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.HouseRowHouse,  new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.HouseBarn,      new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.HouseCabin,     new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.HouseApartment, new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.Stall,          new Slot("S_Rect", false, false, false, true) },
        { ArtKeys.Windmill,       new Slot("S_Rect", false, false, false, true) },
        // 城堡（城市里的稀有地标）与「杂项建筑」（中世纪建筑包这类拆分不出墙/门/窗的整图）：
        // 后者是**变体**（house_extra 放几张就随机挑几张），当村里的杂项建筑用。
        { ArtKeys.Castle,         new Slot("", false, false, false, true) },
        { ArtKeys.HouseExtra,     new Slot("", false, false, false, true) },

        // —— 聚落特有的地标（风车 / 钟楼 / 喷泉 / 篝火）——
        { ArtKeys.WindmillBody,   new Slot("S_Rect", false, true) },
        { ArtKeys.WindmillBlade,  new Slot("S_Rect") },
        { ArtKeys.BellTower,      new Slot("S_Rect", false, true) },
        { ArtKeys.Bell,           new Slot("S_Disc") },
        { ArtKeys.FountainRim,    new Slot("S_Disc") },
        { ArtKeys.FountainWater,  new Slot("S_Disc") },
        { ArtKeys.CampfireStone,  new Slot("S_Disc") },
        { ArtKeys.CampfireFire,   new Slot("S_Disc") },

        // —— 村民（颜色 = 职业 / 肤色，所以保留染色）——
        { ArtKeys.VillagerBody,   new Slot("S_Rect", true) },
        { ArtKeys.VillagerHead,   new Slot("S_Disc", true) },
        // **整身村民**（man1~7 / woman1~4 这类一张图画完整个人的）：放了它就不再拼「方块身子 + 圆头」，
        // 每个村民按区块确定性随机挑一张（变体），图原样显示（不染色）。
        { ArtKeys.Villager,       new Slot("", false, false, false, true) },

        // —— 村民头顶的表情气泡（emoji/*）——
        // 按状态显示（追人 = 生气、被电麻 = 晕、翻找 = 困惑…），同名多图 = 变体（如 angry1 / angry2）。
        // 尺寸由代码给定（与导入的 PPU 无关），所以要多大改 Villager.emojiSize 就行。
        { ArtKeys.EmojiAngry,       new Slot("") },
        { ArtKeys.EmojiAshamed,     new Slot("") },
        { ArtKeys.EmojiBulb,        new Slot("") },
        { ArtKeys.EmojiConfused,    new Slot("") },
        { ArtKeys.EmojiDizzy,       new Slot("") },
        { ArtKeys.EmojiExclamation, new Slot("") },
        { ArtKeys.EmojiHaha,        new Slot("") },
        { ArtKeys.EmojiHappy,       new Slot("") },
        { ArtKeys.EmojiHeartBroken, new Slot("") },
        { ArtKeys.EmojiLove,        new Slot("") },
        { ArtKeys.EmojiNo,          new Slot("") },
        { ArtKeys.EmojiSad,         new Slot("") },
        { ArtKeys.EmojiSleepy,      new Slot("") },
        { ArtKeys.EmojiSpeechless,  new Slot("") },

        // —— 自然 ——
        { ArtKeys.TreeCanopy,     new Slot("S_Disc") },
        // 整棵树（含树干）：交了这张图就整棵替换圆形树冠（`tree_canopy` 不用再交）
        { ArtKeys.Tree,           new Slot("S_Disc", false, false, false, true) },
        { ArtKeys.Berry,          new Slot("S_FoodBerry") },
        { ArtKeys.Leaf,           new Slot("S_FoodLeaf") },
        { ArtKeys.SpecialFood,    new Slot("S_Disc") },

        // —— 按自然体系换的景物（草原灌木 / 森林灌木 / 沙漠仙人掌、枯树、绿洲棕榈）——
        { ArtKeys.Bush,           new Slot("S_Bush", false, false, false, true) },
        { ArtKeys.CactusBody,     new Slot("S_Rect") },
        { ArtKeys.CactusArm,      new Slot("S_Rect") },
        { ArtKeys.DeadTree,       new Slot("S_Rect") },
        { ArtKeys.OasisWater,     new Slot("S_Disc") },
        { ArtKeys.PalmTrunk,      new Slot("S_Rect") },
        { ArtKeys.PalmLeaf,       new Slot("S_Disc") },

        // —— 按自然体系换的食物（森林蘑菇 / 松果，沙漠仙人掌果，草原麦穗）——
        { ArtKeys.Mushroom,       new Slot("S_Disc") },
        { ArtKeys.Pinecone,       new Slot("S_Disc") },
        { ArtKeys.CactusFruit,    new Slot("S_Disc") },
        { ArtKeys.Wheat,          new Slot("S_FoodLeaf") },

        // —— 按聚落 / 自然换的可交互物（石头四档、草垛、木料、垃圾桶、木桶、灯笼）——
        // 石头按大小分成四个 key：越大的越沉、要越高的等级才啃得动，出现的地貌与概率也不一样。
        // 每档都能放多张配色变体（文件名结尾加数字，见文件末尾「变体」的说明）。
        { ArtKeys.RockSmall,      new Slot("S_Rock", false, false, false, true) },
        { ArtKeys.RockMedium,     new Slot("S_Rock", false, false, false, true) },
        { ArtKeys.RockLarge,      new Slot("S_Rock", false, false, false, true) },
        { ArtKeys.RockHuge,       new Slot("S_Rock", false, false, false, true) },
        { ArtKeys.HayBale,        new Slot("S_Rect", false, true) },
        { ArtKeys.Log,            new Slot("S_Rect", false, true) },
        { ArtKeys.TrashCan,       new Slot("S_Rect", false, true) },
        { ArtKeys.Bucket,         new Slot("S_Rect", false, true) },
        { ArtKeys.Lantern,        new Slot("S_Disc") },

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

        // —— 能赋予能力的食物（吃掉解锁对应能力）——
        { ArtKeys.Battery,        new Slot("S_Rect") },
        { ArtKeys.Trash,          new Slot("S_Disc") },
        { ArtKeys.Spore,          new Slot("S_Disc") },
        { ArtKeys.Gear,           new Slot("S_Disc") },
        { ArtKeys.Acid,           new Slot("S_Disc") },

        // —— 会引发连锁的设施（电线 / 抽水泵 / 油桶 / 警报器）——
        { ArtKeys.Wire,           new Slot("S_Rect") },
        { ArtKeys.Pump,           new Slot("S_Rect", false, true) },
        { ArtKeys.OilBarrel,      new Slot("S_Rect", false, true) },
        { ArtKeys.Alarm,          new Slot("S_Disc") },

        // —— 发电站（电池聚在它旁边）——
        { ArtKeys.PowerPlant,     new Slot("S_Rect", false, true) },
        { ArtKeys.PowerCoil,      new Slot("S_Disc") },
        { ArtKeys.PowerPole,      new Slot("S_Rect") },

        // —— 开始界面（颜色沿用界面配色，所以保留染色）——
        { ArtKeys.MenuBackground, new Slot("T_Grass") },
        { ArtKeys.MenuPanel,      new Slot("S_RoundRect", true, true) },
        { ArtKeys.MenuButton,     new Slot("S_RoundRect", true, true) },

        // —— 备用（登记了 key 但游戏里还没用上；先放着，将来要用直接 ArtOverride.Get 取）——
        { ArtKeys.StatusIcon,     new Slot("", false, false, false, true) },
    };

    /// <summary>
    /// 别名表：把**不是正式 key 的文件名**映射到 key（两边都先归一化：忽略大小写、下划线、短横线、空格）。
    /// 和音频那边的 <c>AudioOverride.AliasSource</c> 是同一套思路 —— 玩家不必严格按 key 命名文件名。
    ///
    /// 例：<c>structure4.png</c> → <c>house_cottage</c>、<c>windmill1.png</c> → <c>windmill</c>。
    /// 想换映射改这里就行；把文件直接改名成正式 key 也可以（两种都认）。
    /// </summary>
    public static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
    {
        { "structure1",  ArtKeys.HouseApartment },
        { "structure2",  ArtKeys.Stall },          // 棚子（拼错成 sturcture2 的那张）
        { "sturcture2",  ArtKeys.Stall },
        { "structure3",  ArtKeys.HouseCabin },
        { "structure4",  ArtKeys.HouseCottage },
        { "structure5",  ArtKeys.HouseBarn },
        { "structure6",  ArtKeys.HouseTwoStory },
        { "structure7",  ArtKeys.HouseRowHouse },
        { "windmill1",   ArtKeys.Windmill },

        // 石头：stone1~4 是灰色的（小 → 巨），stone5~7 是土黄色的（小 → 大），按大小归到四档。
        // 同一档里的两张就是这一档的配色变体（走「变体」机制，运行时按地貌概率抽）。
        { "stone1",      ArtKeys.RockSmall },
        { "stone5",      ArtKeys.RockSmall },
        { "stone2",      ArtKeys.RockMedium },
        { "stone6",      ArtKeys.RockMedium },
        { "stone3",      ArtKeys.RockLarge },
        { "stone7",      ArtKeys.RockLarge },
        { "stone4",      ArtKeys.RockHuge },

        // 房屋：house1~6 / cottage1 按「占地大小」归到六种房型（最高的当公寓、最宽的当排屋、
        // 最小的当木屋…）。对不上就把文件改名成正式 key（house_apartment 这类），或者改这几行。
        { "house1",      ArtKeys.HouseApartment },
        { "house2",      ArtKeys.HouseRowHouse },
        { "house3",      ArtKeys.HouseCottage },
        { "house4",      ArtKeys.HouseCabin },
        { "house5",      ArtKeys.HouseBarn },
        { "house6",      ArtKeys.HouseTwoStory },
        { "cottage1",    ArtKeys.HouseCottage },          // 和 house3 是同一栋（同名多图 = 变体）

        // 城堡（城市稀有地标）与杂项建筑（中世纪建筑包：拆分不出墙 / 门 / 窗的整图，当村里的杂项建筑）
        { "castle1",               ArtKeys.Castle },
        { "castle2",               ArtKeys.Castle },
        { "medievalStructure_01",  ArtKeys.HouseExtra },
        { "medievalStructure_02",  ArtKeys.HouseExtra },
        { "medievalStructure_05",  ArtKeys.HouseExtra },
        { "medievalStructure_06",  ArtKeys.HouseExtra },
        { "medievalStructure_09",  ArtKeys.HouseExtra },
        { "medievalStructure_10",  ArtKeys.HouseExtra },
        { "medievalStructure_11",  ArtKeys.HouseExtra },
        { "medievalStructure_13",  ArtKeys.HouseExtra },
        { "medievalStructure_14",  ArtKeys.HouseExtra },
        { "medievalStructure_15",  ArtKeys.HouseExtra },
        { "medievalStructure_23",  ArtKeys.HouseExtra },
        { "status1",               ArtKeys.StatusIcon },   // 备用：登记了但游戏里还没用

        // 地面 / 路面贴图（同名多图 = 变体：草 2 张、沙 4 张、石铺路 2 张）
        { "grass1",      ArtKeys.Ground },
        { "grass2",      ArtKeys.Ground },
        { "sand1",       ArtKeys.GroundDesert },
        { "sand2",       ArtKeys.GroundDesert },
        { "sand3",       ArtKeys.GroundDesert },
        { "sand4",       ArtKeys.GroundDesert },
        { "gravel1",     ArtKeys.RoadPaved },
        { "gravel2",     ArtKeys.RoadPaved },

        // 路口瓦片（文件名写的就是「路在哪几边」，见 ArtKeys 里 road_* 的注释）
        { "cross",              ArtKeys.RoadCross },
        { "t-intersection-up",    ArtKeys.RoadTUp },
        { "t-intersection-down",  ArtKeys.RoadTDown },
        { "t-intersection-left",  ArtKeys.RoadTLeft },
        { "t-intersectiom-right", ArtKeys.RoadTRight },    // 文件名拼错了（intersectiom），照样认
        { "corner-up-left",       ArtKeys.RoadCornerUpLeft },
        { "corner-up-right",      ArtKeys.RoadCornerUpRight },
        { "corner-down-left",     ArtKeys.RoadCornerDownLeft },
        { "corner-down-right",    ArtKeys.RoadCornerDownRight },
        { "roadhead-up",          ArtKeys.RoadHeadUp },
        { "roadhead-down",        ArtKeys.RoadHeadDown },
        { "roadhead-left",        ArtKeys.RoadHeadLeft },
        { "roadhead-right",       ArtKeys.RoadHeadRight },
        { "path1",       ArtKeys.RoadStraightV },          // path1 = 竖直的直路瓦片
        { "path2",       ArtKeys.RoadStraightH },          // path2 = 水平的直路瓦片

        // 村民整身图（man1~7 / woman1~4 = 11 个变体，每个村民随机挑一张）
        { "man1",        ArtKeys.Villager },
        { "man2",        ArtKeys.Villager },
        { "man3",        ArtKeys.Villager },
        { "man4",        ArtKeys.Villager },
        { "man5",        ArtKeys.Villager },
        { "man6",        ArtKeys.Villager },
        { "man7",        ArtKeys.Villager },
        { "woman1",      ArtKeys.Villager },
        { "woman2",      ArtKeys.Villager },
        { "woman3",      ArtKeys.Villager },
        { "woman4",      ArtKeys.Villager },

        // 表情气泡（同名多图 = 变体：生气 / 惭愧 / 感叹号 / 喜欢各有两张）
        { "angry1",           ArtKeys.EmojiAngry },
        { "angry2",           ArtKeys.EmojiAngry },
        { "ashamed1",         ArtKeys.EmojiAshamed },
        { "ashamed2",         ArtKeys.EmojiAshamed },
        { "bulb",             ArtKeys.EmojiBulb },
        { "confused",         ArtKeys.EmojiConfused },
        { "dizzy",            ArtKeys.EmojiDizzy },
        { "exclamation mark1", ArtKeys.EmojiExclamation },
        { "exclamation mark2", ArtKeys.EmojiExclamation },
        { "haha",             ArtKeys.EmojiHaha },
        { "happy",            ArtKeys.EmojiHappy },
        { "heart-broken",     ArtKeys.EmojiHeartBroken },
        { "love1",            ArtKeys.EmojiLove },
        { "love2",            ArtKeys.EmojiLove },
        { "no",               ArtKeys.EmojiNo },
        { "sad",              ArtKeys.EmojiSad },
        { "sleepy",           ArtKeys.EmojiSleepy },
        { "speechless",       ArtKeys.EmojiSpeechless },
    };

    /// <summary>
    /// 一张变体图。**同一个 key 可以放多张图**（文件名结尾加数字区分：
    /// <c>tree1.png</c> / <c>tree2.png</c> / <c>tree3.png</c> 都是 <c>tree</c> 的变体），
    /// 运行时按地貌权重或位置随机取一张 —— 一片林子里就不会每棵树长得一模一样。
    /// </summary>
    struct Variant
    {
        public Sprite sprite;
        public string file;

        /// <summary>文件名结尾的数字（没写数字 = 0）。变体的排列顺序按它从小到大排，
        /// 权重数组的下标就是它，所以「第几张」由美术的文件名说了算。</summary>
        public int number;
    }

    static Dictionary<string, List<Variant>> table;     // 归一化 key → 变体表（按结尾数字从小到大）
    static Dictionary<string, Variant> primary;         // 归一化 key → 第一个变体（Get(key) 给的就是它）
    static Dictionary<string, string> canonical;        // 归一化 key → 正式 key（写日志用）
    static Dictionary<string, string> aliasLookup;      // 归一化别名 → 正式 key

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
        bool viaAlias;
        return ResolveNormalized(NormalizeName(Path.GetFileNameWithoutExtension(fileName)), out viaAlias);
    }

    /// <summary>
    /// 归一化文件名 → 正式 key。顺序：**正式 key → 别名表 → 去掉结尾的数字再试一次**
    /// （<c>windmill1</c> → <c>windmill</c>、<c>house_wall2</c> → <c>house_wall</c>）。
    /// <paramref name="viaAlias"/> 为 true 表示这个 key 是「靠别名 / 去数字」猜出来的（载入时会打日志）。
    /// </summary>
    static string ResolveNormalized(string norm, out bool viaAlias)
    {
        viaAlias = false;
        EnsureCanonical();
        if (string.IsNullOrEmpty(norm)) return null;

        string key;
        if (canonical.TryGetValue(norm, out key)) return key;

        viaAlias = true;
        if (aliasLookup.TryGetValue(norm, out key)) return key;

        int end = norm.Length;
        while (end > 0 && char.IsDigit(norm[end - 1])) end--;
        if (end > 0 && end < norm.Length)
        {
            string trimmed = norm.Substring(0, end);
            if (canonical.TryGetValue(trimmed, out key)) return key;
            if (aliasLookup.TryGetValue(trimmed, out key)) return key;
        }

        viaAlias = false;
        return null;
    }

    /// <summary>取用来替换某个 key 的图片（= 第一个变体）；没放图片时返回 null。</summary>
    public static Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureLoaded();
        if (table.Count == 0) return null;

        Variant variant;
        string norm = NormalizeName(key);
        if (!primary.TryGetValue(norm, out variant) || variant.sprite == null) return null;
        used.Add(norm);
        return variant.sprite;
    }

    /// <summary>这个 key 放了几个变体（0 = 没放图片）。</summary>
    public static int VariantCount(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        EnsureLoaded();

        List<Variant> list;
        return table.TryGetValue(NormalizeName(key), out list) ? list.Count : 0;
    }

    /// <summary>
    /// 取**指定编号**的变体：<paramref name="number"/> = 文件名结尾的数字（<c>bush2.png</c> → 2，没写数字 = 0）。
    /// 这个编号没交图时退回第一个变体（交了一张总比退回程序化美术强）；整个 key 都没图时返回 null。
    /// </summary>
    public static Sprite Get(string key, int number)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureLoaded();

        string norm = NormalizeName(key);
        List<Variant> list;
        if (!table.TryGetValue(norm, out list) || list.Count == 0) return null;

        used.Add(norm);
        for (int i = 0; i < list.Count; i++)
            if (list[i].number == number) return list[i].sprite;
        return list[0].sprite;
    }

    /// <summary>
    /// 按权重抽一个变体（同一片地貌里换着用，见 <c>VillageGenerator.TreeVariantWeights</c>）。
    /// <paramref name="weights"/> 的下标 = 变体顺序（文件名结尾数字从小到大），
    /// 写少了的部分按 1 算；权重全为 0 时退化成均匀抽。
    /// <paramref name="roll"/> ∈ [0,1) 由调用方给（用生成器的确定性随机数，走远回头才一模一样）。
    /// </summary>
    public static Sprite PickVariant(string key, float[] weights, float roll)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureLoaded();

        string norm = NormalizeName(key);
        List<Variant> list;
        if (!table.TryGetValue(norm, out list) || list.Count == 0) return null;

        used.Add(norm);
        if (list.Count == 1) return list[0].sprite;

        float total = 0f;
        for (int i = 0; i < list.Count; i++) total += WeightAt(weights, i);
        if (total <= 0f) return list[Mathf.Clamp((int)(Mathf.Clamp01(roll) * list.Count), 0, list.Count - 1)].sprite;

        float pick = Mathf.Clamp01(roll) * total;
        for (int i = 0; i < list.Count; i++)
        {
            pick -= WeightAt(weights, i);
            if (pick <= 0f) return list[i].sprite;
        }
        return list[list.Count - 1].sprite;
    }

    static float WeightAt(float[] weights, int index)
    {
        if (weights == null) return 1f;
        if (index >= weights.Length) return 1f;                  // 权重写少了的部分按 1 算
        return Mathf.Max(0f, weights[index]);
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

    /// <summary>这个 key 是不是平铺素材（导入时要把 Wrap Mode 设成 Repeat）。</summary>
    public static bool TilingOf(string key)
    {
        Slot slot;
        return Slots.TryGetValue(key, out slot) && slot.tiling;
    }

    /// <summary>这个 key 是不是整图素材（导入时按内容裁边、运行时按内容比例装进占地）。</summary>
    public static bool WholeOf(string key)
    {
        Slot slot;
        return Slots.TryGetValue(key, out slot) && slot.whole;
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
        primary = null;
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

        aliasLookup = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> pair in Aliases)
        {
            string norm = NormalizeName(pair.Key);
            if (norm.Length == 0 || aliasLookup.ContainsKey(norm)) continue;
            aliasLookup[norm] = pair.Value;
        }
    }

    static void EnsureLoaded()
    {
        if (table != null) return;

        EnsureCanonical();
        table = new Dictionary<string, List<Variant>>();
        primary = new Dictionary<string, Variant>();
        files.Clear();
        unmatched.Clear();

        Sprite[] sprites = Resources.LoadAll<Sprite>(ResourceFolder);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null) continue;

            // 表按**正式 key**索引（不是按文件名）—— 这样「别名 / 去数字」认出来的图也能被 Get(key) 取到
            bool viaAlias;
            string key = ResolveNormalized(NormalizeName(sprite.name), out viaAlias);
            if (key == null)
            {
                unmatched.Add(sprite.name);
                continue;
            }

            string keyNorm = NormalizeName(key);
            List<Variant> list;
            if (!table.TryGetValue(keyNorm, out list))
            {
                list = new List<Variant>();
                table[keyNorm] = list;
            }

            Variant variant;
            variant.sprite = sprite;
            variant.file = sprite.name;
            variant.number = TrailingNumber(NormalizeName(sprite.name));
            list.Add(variant);
            files.Add(sprite.name);

            if (viaAlias)
                Debug.Log("[ArtOverride] " + sprite.name + " 不是正式 key，按别名当成 " + key
                    + "（想换映射就改 ArtOverride.Aliases，或者把文件改名成 " + key + "）。");
        }

        // 变体顺序 = 文件名结尾的数字从小到大（没写数字的排最前）。
        // **必须显式排序**：Resources.LoadAll 的先后顺序不保证，而「第几张」是玩家看得见的约定
        //（权重数组按这个顺序给，见 PickVariant）。
        int multi = 0;
        foreach (KeyValuePair<string, List<Variant>> pair in table)
        {
            pair.Value.Sort(CompareVariant);
            primary[pair.Key] = pair.Value[0];
            if (pair.Value.Count > 1) multi++;
        }

        if (files.Count > 0)
            Debug.Log("[ArtOverride] 从 Resources/" + ResourceFolder + " 载入 " + files.Count + " 张图片"
                + (multi > 0 ? "（其中 " + multi + " 个 key 有多个变体，运行时按地貌 / 位置取用）" : "")
                + "。");
    }

    /// <summary>变体排序：结尾数字小的在前，数字相同按文件名（保证结果稳定）。</summary>
    static int CompareVariant(Variant a, Variant b)
    {
        if (a.number != b.number) return a.number.CompareTo(b.number);
        return string.CompareOrdinal(a.file, b.file);
    }

    /// <summary>归一化文件名结尾的数字（<c>tree3</c> → 3、<c>bush</c> → 0）。</summary>
    static int TrailingNumber(string norm)
    {
        if (string.IsNullOrEmpty(norm)) return 0;

        int end = norm.Length;
        while (end > 0 && char.IsDigit(norm[end - 1])) end--;
        if (end >= norm.Length) return 0;

        int value;
        return int.TryParse(norm.Substring(end), out value) ? value : 0;
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

    // 地面（都是平铺素材）
    public const string Ground = "ground";
    public const string Road = "road";
    /// <summary>森林地貌的地面（草原直接用 <see cref="Ground"/>）。</summary>
    public const string GroundForest = "ground_forest";
    /// <summary>沙漠地貌的地面。</summary>
    public const string GroundDesert = "ground_desert";
    /// <summary>荒野的土路。</summary>
    public const string RoadDirt = "road_dirt";
    /// <summary>城市的石铺路。</summary>
    public const string RoadPaved = "road_paved";

    // 路口瓦片（一张图 = 一个路口形状；文件名以 road_ 开头，别名表把 cross / corner-* 这类名字接进来）
    /// <summary>十字路口（四边都有路）。</summary>
    public const string RoadCross = "road_cross";
    /// <summary>丁字路口：**缺下边**（左右 + 上）。</summary>
    public const string RoadTUp = "road_t_up";
    /// <summary>丁字路口：**缺上边**（左右 + 下）。</summary>
    public const string RoadTDown = "road_t_down";
    /// <summary>丁字路口：**缺右边**（上下 + 左）。</summary>
    public const string RoadTLeft = "road_t_left";
    /// <summary>丁字路口：**缺左边**（上下 + 右）。</summary>
    public const string RoadTRight = "road_t_right";
    /// <summary>转角：连**上 + 左**两边。</summary>
    public const string RoadCornerUpLeft = "road_corner_up_left";
    /// <summary>转角：连**上 + 右**两边（游戏里的 L 型路口默认是这一张）。</summary>
    public const string RoadCornerUpRight = "road_corner_up_right";
    /// <summary>转角：连**下 + 左**两边。</summary>
    public const string RoadCornerDownLeft = "road_corner_down_left";
    /// <summary>转角：连**下 + 右**两边。</summary>
    public const string RoadCornerDownRight = "road_corner_down_right";
    /// <summary>路尽头：路**往上收口**（图里路在下半张）。</summary>
    public const string RoadHeadUp = "road_head_up";
    /// <summary>路尽头：路**往下收口**。</summary>
    public const string RoadHeadDown = "road_head_down";
    /// <summary>路尽头：路**往左收口**。</summary>
    public const string RoadHeadLeft = "road_head_left";
    /// <summary>路尽头：路**往右收口**。</summary>
    public const string RoadHeadRight = "road_head_right";
    /// <summary>直路瓦片（横向）：沿路平铺；没交就用路面贴图。</summary>
    public const string RoadStraightH = "road_straight_h";
    /// <summary>直路瓦片（纵向）。</summary>
    public const string RoadStraightV = "road_straight_v";

    // 房屋与商店
    public const string HouseRoof = "house_roof";
    public const string HouseWall = "house_wall";
    public const string HouseDoor = "house_door";
    public const string HouseWindow = "house_window";
    public const string HouseChimney = "house_chimney";
    public const string ShopSign = "shop_sign";
    public const string Anvil = "anvil";
    public const string Forge = "forge";

    // 房型（两层小楼 / 公寓 / 谷仓 / 木屋）
    /// <summary>两层小楼与排屋、公寓的**上层墙体**（下层用 <see cref="HouseWall"/>）。</summary>
    public const string HouseUpperWall = "house_upper_wall";
    /// <summary>公寓的平屋顶。</summary>
    public const string ApartmentRoof = "apartment_roof";
    /// <summary>公寓的窗（比 <see cref="HouseWindow"/> 小、成排）。</summary>
    public const string ApartmentWindow = "apartment_window";
    /// <summary>谷仓的高屋顶。</summary>
    public const string BarnRoof = "barn_roof";
    /// <summary>谷仓的双开大门。</summary>
    public const string BarnDoor = "barn_door";
    /// <summary>木屋的圆木墙。</summary>
    public const string CabinWall = "cabin_wall";
    /// <summary>木屋的屋顶。</summary>
    public const string CabinRoof = "cabin_roof";

    // 整栋建筑（一张图 = 一整栋，**含**屋顶 / 墙 / 门 / 窗：放了图就不再拼那些分件）
    /// <summary>农舍的整栋外观。</summary>
    public const string HouseCottage = "house_cottage";
    /// <summary>两层小楼的整栋外观。</summary>
    public const string HouseTwoStory = "house_two_story";
    /// <summary>排屋的整栋外观。</summary>
    public const string HouseRowHouse = "house_rowhouse";
    /// <summary>谷仓的整栋外观。</summary>
    public const string HouseBarn = "house_barn";
    /// <summary>木屋的整栋外观。</summary>
    public const string HouseCabin = "house_cabin";
    /// <summary>公寓的整栋外观。</summary>
    public const string HouseApartment = "house_apartment";
    /// <summary>集市摊位的整栋外观（柜台 + 篷 + 货物一图搞定）。</summary>
    public const string Stall = "stall";
    /// <summary>风车的整栋外观（**叶片要画在图里**：放了这张图就不再生成会转的叶片）。</summary>
    public const string Windmill = "windmill";
    /// <summary>城堡：城市里的**稀有地标**（一张图 = 一栋，见 <c>VillageGenerator.BuildCastle</c>）。</summary>
    public const string Castle = "castle";
    /// <summary>
    /// **杂项建筑**：拆分不出墙 / 门 / 窗的整图建筑（中世纪建筑包那类），
    /// 放几张就是几个变体，按区块确定性随机挑一张当村里的杂项建筑（见 <c>VillageGenerator.BuildHouses</c>）。
    /// </summary>
    public const string HouseExtra = "house_extra";

    // 聚落地标
    /// <summary>风车的塔身。</summary>
    public const string WindmillBody = "windmill_body";
    /// <summary>风车的叶片（四片共用，会旋转）。</summary>
    public const string WindmillBlade = "windmill_blade";
    /// <summary>钟楼的塔身（含顶）。</summary>
    public const string BellTower = "bell_tower";
    /// <summary>钟楼里的钟。</summary>
    public const string Bell = "bell";
    /// <summary>喷泉的池边。</summary>
    public const string FountainRim = "fountain_rim";
    /// <summary>喷泉的水面（中央水柱也用它）。</summary>
    public const string FountainWater = "fountain_water";
    /// <summary>篝火的石头圈。</summary>
    public const string CampfireStone = "campfire_stone";
    /// <summary>篝火的火苗（夜里亮、会闪）。</summary>
    public const string CampfireFire = "campfire_fire";

    // 自然景物（按自然体系换）
    /// <summary>灌木（草原 / 森林）。</summary>
    public const string Bush = "bush";
    /// <summary>仙人掌的主干（沙漠）。</summary>
    public const string CactusBody = "cactus_body";
    /// <summary>仙人掌的侧枝。</summary>
    public const string CactusArm = "cactus_arm";
    /// <summary>枯树（沙漠）。</summary>
    public const string DeadTree = "dead_tree";
    /// <summary>绿洲的水面（沙漠里的稀有景点）。</summary>
    public const string OasisWater = "oasis_water";
    /// <summary>棕榈树干（绿洲旁）。</summary>
    public const string PalmTrunk = "palm_trunk";
    /// <summary>棕榈树叶。</summary>
    public const string PalmLeaf = "palm_leaf";

    // 按自然体系换的食物
    /// <summary>蘑菇（森林）。</summary>
    public const string Mushroom = "mushroom";
    /// <summary>松果（森林）。</summary>
    public const string Pinecone = "pinecone";
    /// <summary>仙人掌果（沙漠）。</summary>
    public const string CactusFruit = "cactus_fruit";
    /// <summary>麦穗（草原）。</summary>
    public const string Wheat = "wheat";

    // 按聚落 / 自然换的可交互物
    //
    // 石头按大小分成四档（越大的越沉、搬运越慢、要越高的等级才啃得动，出现的地貌与概率也不同）。
    // 每一档都可以放多张配色变体（文件名结尾加数字）。
    /// <summary>石头·小：到处都是，一开始就搬得动、啃得动。</summary>
    public const string RockSmall = "rock_small";
    /// <summary>石头·中：要长到 <see cref="BugGrowth.CrateLevel"/> 才啃得动。</summary>
    public const string RockMedium = "rock_medium";
    /// <summary>石头·大：很沉，要 <see cref="BugGrowth.TreeLevel"/> 级。</summary>
    public const string RockLarge = "rock_large";
    /// <summary>石头·巨石：沙漠里最多，最沉，要满级（<see cref="BugGrowth.VillagerLevel"/>）才啃得动。</summary>
    public const string RockHuge = "rock_huge";
    /// <summary>草垛（草原，能搬能吃）。</summary>
    public const string HayBale = "hay_bale";
    /// <summary>木料堆（森林，能搬能吃）。</summary>
    public const string Log = "log";
    /// <summary>垃圾桶（城市，能搬能吃）。</summary>
    public const string TrashCan = "trash_can";
    /// <summary>木桶（村庄 / 城市，能搬能吃）。</summary>
    public const string Bucket = "bucket";
    /// <summary>灯笼（村庄 / 城市，能搬，夜里发亮）。</summary>
    public const string Lantern = "lantern";

    // 村民
    public const string VillagerBody = "villager_body";
    public const string VillagerHead = "villager_head";
    /// <summary>**整身村民**（一张图画完整个人的）：放了它就不再拼「方块身子 + 圆头」，
    /// 每个村民按区块确定性随机挑一张（man1~7 / woman1~4 就是 11 个变体）。</summary>
    public const string Villager = "villager";

    // 村民头顶的表情气泡（按状态显示，见 Villager.EmojiFor）
    /// <summary>生气（追小虫）。</summary>
    public const string EmojiAngry = "emoji_angry";
    /// <summary>惭愧（回过神 / 尴尬）。</summary>
    public const string EmojiAshamed = "emoji_ashamed";
    /// <summary>灯泡（想到什么了：去查看动静）。</summary>
    public const string EmojiBulb = "emoji_bulb";
    /// <summary>困惑（翻找 / 找不到）。</summary>
    public const string EmojiConfused = "emoji_confused";
    /// <summary>头晕（被电麻 / 摔倒）。</summary>
    public const string EmojiDizzy = "emoji_dizzy";
    /// <summary>感叹号（听到动静、起疑）——玩家最眼熟的那个提示。</summary>
    public const string EmojiExclamation = "emoji_exclamation";
    /// <summary>大笑（闲聊）。</summary>
    public const string EmojiHaha = "emoji_haha";
    /// <summary>开心（孩子玩耍）。</summary>
    public const string EmojiHappy = "emoji_happy";
    /// <summary>心碎（看着同伴被吃掉）。</summary>
    public const string EmojiHeartBroken = "emoji_heart_broken";
    /// <summary>喜欢（闲聊时的小心思）。</summary>
    public const string EmojiLove = "emoji_love";
    /// <summary>不要 / 拒绝（吓得摆手）。</summary>
    public const string EmojiNo = "emoji_no";
    /// <summary>难过（逃跑时的表情）。</summary>
    public const string EmojiSad = "emoji_sad";
    /// <summary>犯困（干活干累了）。</summary>
    public const string EmojiSleepy = "emoji_sleepy";
    /// <summary>无语（回过神）。</summary>
    public const string EmojiSpeechless = "emoji_speechless";

    // 自然
    /// <summary>树冠（圆形）；交了整棵树的图（<see cref="Tree"/>）就用整棵树的。</summary>
    public const string TreeCanopy = "tree_canopy";
    /// <summary>**整棵树**（含树干）：交了这张图就整棵替换圆形树冠，不用再交 <see cref="TreeCanopy"/>。</summary>
    public const string Tree = "tree";
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

    // 能赋予能力的食物（吃掉解锁对应能力，见 AbilityId）
    public const string Battery = "battery";
    public const string Trash = "trash";
    public const string Spore = "spore";
    public const string Gear = "gear";
    public const string Acid = "acid";

    // 发电站（电池聚在它旁边）
    public const string PowerPlant = "power_plant";
    public const string PowerCoil = "power_coil";
    public const string PowerPole = "power_pole";

    // 会引发连锁的设施
    public const string Wire = "wire";
    public const string Pump = "pump";
    public const string OilBarrel = "oilbarrel";
    public const string Alarm = "alarm";

    // 开始界面
    public const string MenuBackground = "menu_background";
    public const string MenuPanel = "menu_panel";
    public const string MenuButton = "menu_button";

    // 备用（登记了 key，但游戏里暂时没用到）
    /// <summary>备用：状态图标竖条（还没想好挂在哪，先占一个 key，免得被当成「名字写错」点名）。</summary>
    public const string StatusIcon = "status_icon";
}
