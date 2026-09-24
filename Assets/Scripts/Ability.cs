using UnityEngine;

/// <summary>
/// 小虫能获得的能力。**获得方式统一是「吃掉对应的东西」**（见 <see cref="AbilityPickup"/>），
/// 用完消耗一次充能，充能靠再吃同类东西补充。
/// </summary>
public enum AbilityId
{
    /// <summary>电击：电麻附近的村民、让路灯窗户乱闪、弄出很大动静（吃旧电池）。</summary>
    Shock,
    /// <summary>伪装：一段时间内村民看不见你，贴到脸上才会露馅（吃破布团）。</summary>
    Disguise,
    /// <summary>分裂：放出几只假小虫到处乱窜、不停出声，把村民引走（吃孢子囊）。</summary>
    Split,
    /// <summary>强制抖动：高速抖动把附近的箱子震飞、把村民吓跑（吃锈齿轮）。</summary>
    Shake,
    /// <summary>腐蚀：把面前的东西慢慢蚀穿（木箱、电线）—— 吃酸液瓶解锁。</summary>
    Corrode
}

/// <summary>
/// 一种能力的静态资料（名字 / 说明 / 默认键）。数值（半径、时长、冷却）在
/// <see cref="AbilitySet"/> 上是可调字段，不放这里。
/// </summary>
public class AbilityInfo
{
    public AbilityId id;
    /// <summary>中文名（HUD / 悬浮窗显示）。</summary>
    public string name;
    /// <summary>一句话说明它干什么。</summary>
    public string description;
    /// <summary>默认按键。</summary>
    public KeyCode key;
    /// <summary>能不能用（未实现的能力先关掉，HUD 里也不会出现）。</summary>
    public bool playable = true;
    /// <summary>这次使用要消耗几点体力（0 = 不消耗）。</summary>
    public float staminaCost;
}

/// <summary>
/// 能力资料表。**键位统一在 <see cref="GameInput.AbilityKeys"/> 里**（数字键 1~5），
/// 这里只记「第几个键」，不要各自写按键。
/// </summary>
public static class Abilities
{
    /// <summary>每种能力最多攒几发充能（吃同类东西补充，不能无限囤）。</summary>
    public const int MaxCharges = 3;

    /// <summary><see cref="AbilityId"/> 的个数（数组按它开）。</summary>
    public const int Count = 5;

    static readonly AbilityInfo[] Table = Build();

    /// <summary>取某个能力的资料（一定有值）。</summary>
    public static AbilityInfo Get(AbilityId id)
    {
        int index = (int)id;
        if (index < 0 || index >= Table.Length) return Table[0];
        return Table[index];
    }

    /// <summary>已经能玩的能力（HUD 只显示这些；数组顺序 = 数字键 1~5 的顺序）。</summary>
    public static readonly AbilityId[] Playable =
    {
        AbilityId.Shock, AbilityId.Disguise, AbilityId.Split, AbilityId.Shake, AbilityId.Corrode
    };

    /// <summary>这个能力对应第几个数字键（0 起）。</summary>
    public static int KeyIndex(AbilityId id)
    {
        for (int i = 0; i < Playable.Length; i++)
            if (Playable[i] == id) return i;
        return -1;
    }

    /// <summary>按键的显示名（HUD 提示用）。</summary>
    public static string KeyName(AbilityId id)
    {
        int index = KeyIndex(id);
        return index >= 0 ? GameInput.AbilityName(index) : "-";
    }

    /// <summary>
    /// 这个能力对应的音效 key（见 <see cref="AudioKeys"/>）。
    /// **没有往 <c>Resources/AudioOverride/</c> 放同名文件就是安静的**，接口先留着。
    /// </summary>
    public static string AudioKeyOf(AbilityId id)
    {
        switch (id)
        {
            case AbilityId.Shock: return AudioKeys.AbilityShock;
            case AbilityId.Disguise: return AudioKeys.AbilityDisguise;
            case AbilityId.Split: return AudioKeys.AbilitySplit;
            case AbilityId.Shake: return AudioKeys.AbilityShake;
            case AbilityId.Corrode: return AudioKeys.AbilityCorrode;
            default: return null;
        }
    }

    static AbilityInfo[] Build()
    {
        AbilityInfo[] table = new AbilityInfo[Count];

        table[(int)AbilityId.Shock] = new AbilityInfo
        {
            id = AbilityId.Shock,
            name = "电击",
            description = "把附近的村民电麻一段时间，路灯和窗户会乱闪，动静很大。",
            key = GameInput.AbilityKeys[0]
        };
        table[(int)AbilityId.Disguise] = new AbilityInfo
        {
            id = AbilityId.Disguise,
            name = "伪装",
            description = "装成一团没人在意的东西：村民看不见你，但贴到脸上还是会露馅。",
            key = GameInput.AbilityKeys[1]
        };
        table[(int)AbilityId.Split] = new AbilityInfo
        {
            id = AbilityId.Split,
            name = "分裂",
            description = "放出几只假小虫到处乱窜、不停出声，把村民引到别处去。",
            key = GameInput.AbilityKeys[2]
        };
        table[(int)AbilityId.Shake] = new AbilityInfo
        {
            id = AbilityId.Shake,
            name = "抖动",
            description = "高速抖动：把附近的箱子震飞，把人吓得四散跑开。",
            key = GameInput.AbilityKeys[3]
        };
        table[(int)AbilityId.Corrode] = new AbilityInfo
        {
            id = AbilityId.Corrode,
            name = "腐蚀",
            description = "蚀穿面前的东西：木箱会烂掉、电线会断（断了电，附近靠它供电的机器就停了）。",
            key = GameInput.AbilityKeys[4]
        };

        return table;
    }
}

/// <summary>
/// 「吃掉它就能获得能力」的东西挂这个组件（由 <see cref="FoodCatalog.Create"/> 按定义自动挂上）。
/// 挂在物体根上，吃的逻辑（<see cref="BugEat"/>）吃完就去问它，
/// 所以**加一种赋予能力的食物不需要改进食逻辑**。
/// </summary>
public class AbilityPickup : MonoBehaviour
{
    /// <summary>吃掉解锁哪个能力。</summary>
    public AbilityId ability = AbilityId.Shock;

    /// <summary>这次吃掉补几点充能（第一次吃同时会解锁）。</summary>
    public int charges = 2;
}
