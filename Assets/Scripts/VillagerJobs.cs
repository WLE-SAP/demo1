using UnityEngine;

/// <summary>村民的职业。</summary>
public enum VillagerJob
{
    Farmer,      // 农夫：下地干活
    Baker,       // 面包师：守着面包房
    Merchant,    // 摊贩：守着自己的摊位
    Guard,       // 守卫：沿主路巡逻
    Smith,       // 铁匠：守着铁匠铺
    Woodcutter,  // 樵夫：去树林砍柴
    Shepherd,    // 牧羊人：在畜栏放牧
    Child,       // 孩子：在广场到处跑
    Elder        // 长者：在井边 / 长椅晒太阳闲聊
}

/// <summary>村民看到小虫时的反应。</summary>
public enum VillagerReaction
{
    Ignore,   // 懒得理（干活要紧）
    Flee,     // 躲开
    Chase     // 追上去（好奇或要把它赶走）
}

/// <summary>
/// 职业资料表：中文名、衣服颜色、工作时的描述、看到小虫的反应。
/// 衣服颜色就是村民的职业标识（同一个职业一个色系，个体略有深浅差异）。
/// </summary>
public static class VillagerJobs
{
    /// <summary>所有职业，按生成时的抽取顺序排列。</summary>
    public static readonly VillagerJob[] All =
    {
        VillagerJob.Farmer, VillagerJob.Farmer, VillagerJob.Merchant, VillagerJob.Child,
        VillagerJob.Woodcutter, VillagerJob.Guard, VillagerJob.Elder, VillagerJob.Baker,
        VillagerJob.Smith, VillagerJob.Shepherd, VillagerJob.Farmer, VillagerJob.Child,
        VillagerJob.Merchant, VillagerJob.Woodcutter, VillagerJob.Elder, VillagerJob.Guard,
        VillagerJob.Shepherd, VillagerJob.Child, VillagerJob.Farmer, VillagerJob.Merchant,
        VillagerJob.Woodcutter, VillagerJob.Elder, VillagerJob.Child, VillagerJob.Farmer
    };

    public static string Label(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Farmer: return "农夫";
            case VillagerJob.Baker: return "面包师";
            case VillagerJob.Merchant: return "摊贩";
            case VillagerJob.Guard: return "守卫";
            case VillagerJob.Smith: return "铁匠";
            case VillagerJob.Woodcutter: return "樵夫";
            case VillagerJob.Shepherd: return "牧羊人";
            case VillagerJob.Child: return "孩子";
            default: return "长者";
        }
    }

    /// <summary>职业的衣服颜色（也就是视觉上的职业标识）。</summary>
    public static Color Shirt(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Farmer: return new Color(0.78f, 0.66f, 0.30f);      // 麦黄
            case VillagerJob.Baker: return new Color(0.93f, 0.91f, 0.86f);       // 面粉白
            case VillagerJob.Merchant: return new Color(0.86f, 0.42f, 0.20f);    // 橙红
            case VillagerJob.Guard: return new Color(0.26f, 0.35f, 0.56f);       // 制服蓝
            case VillagerJob.Smith: return new Color(0.36f, 0.32f, 0.34f);       // 铁灰
            case VillagerJob.Woodcutter: return new Color(0.24f, 0.42f, 0.24f);  // 林绿
            case VillagerJob.Shepherd: return new Color(0.36f, 0.61f, 0.55f);    // 草青
            case VillagerJob.Child: return new Color(0.80f, 0.60f, 0.85f);       // 亮紫
            default: return new Color(0.58f, 0.48f, 0.62f);                      // 长者灰紫
        }
    }

    /// <summary>看到小虫时的反应：守卫/樵夫会追，长者/摊贩/面包师/牧羊人躲，农夫铁匠不理会。</summary>
    public static VillagerReaction Reaction(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Guard: return VillagerReaction.Chase;       // 巡逻守卫要把虫子赶走
            case VillagerJob.Woodcutter: return VillagerReaction.Chase;  // 樵夫拿家伙追
            case VillagerJob.Child: return VillagerReaction.Chase;       // 孩子好奇，追着看
            case VillagerJob.Elder: return VillagerReaction.Flee;        // 长者怕虫
            case VillagerJob.Merchant: return VillagerReaction.Flee;     // 摊贩怕虫子吓跑客人
            case VillagerJob.Baker: return VillagerReaction.Flee;        // 面包师躲开
            case VillagerJob.Shepherd: return VillagerReaction.Flee;     // 牧羊人躲开
            default: return VillagerReaction.Ignore;                     // 农夫 / 铁匠：干活要紧
        }
    }

    /// <summary>悬浮窗里给村民的一段介绍（按职业）。</summary>
    public static string Intro(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Farmer: return "天天下地干活，不太搭理小虫，看见了也当没看见。";
            case VillagerJob.Baker: return "面包房的主人，闻到虫味就往屋里躲。";
            case VillagerJob.Merchant: return "守着摊位的生意人，最怕虫子吓跑客人。";
            case VillagerJob.Guard: return "村里的守卫，看见虫子会一路追着赶出村口。";
            case VillagerJob.Smith: return "打铁的，胆子大，虫子走到脚边也不抬眼皮。";
            case VillagerJob.Woodcutter: return "在林子里砍柴，手里有家伙，会追着虫子跑。";
            case VillagerJob.Shepherd: return "看羊的，虫子一靠近就先躲开。";
            case VillagerJob.Child: return "村里的孩子，好奇心重，会追着小虫看。";
            default: return "上了年纪的村民，怕虫，喜欢在井边晒太阳。";
        }
    }

    /// <summary>工作时在做什么（HUD 显示用）。</summary>
    public static string WorkText(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Farmer: return "在田里劳作";
            case VillagerJob.Baker: return "在面包房烤面包";
            case VillagerJob.Merchant: return "守着自己的摊位";
            case VillagerJob.Guard: return "沿街巡逻";
            case VillagerJob.Smith: return "在铁匠铺打铁";
            case VillagerJob.Woodcutter: return "在林子里砍柴";
            case VillagerJob.Shepherd: return "在畜栏放牧";
            case VillagerJob.Child: return "在广场上玩";
            default: return "在井边晒太阳";
        }
    }

    /// <summary>上班地点属于哪一类（决定找锚点的方式）。</summary>
    public static string WorkPlace(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Farmer: return "farm";
            case VillagerJob.Baker: return "bakery";
            case VillagerJob.Merchant: return "stall";
            case VillagerJob.Guard: return "road";
            case VillagerJob.Smith: return "smithy";
            case VillagerJob.Woodcutter: return "tree";
            case VillagerJob.Shepherd: return "pen";
            case VillagerJob.Child: return "plaza";
            default: return "well";
        }
    }
}
