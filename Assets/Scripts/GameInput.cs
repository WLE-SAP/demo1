using UnityEngine;

/// <summary>
/// 按键统一表：**所有交互键默认用 F**（拾取 / 放下、钻地洞 / 出洞、地道传送……），
/// 以后加新的交互也直接读这里，别再各写各的键。
/// </summary>
public static class GameInput
{
    /// <summary>交互键（默认 F）：拾取 / 放下、钻地洞 / 出洞、地道传送。</summary>
    public static KeyCode Interact = KeyCode.F;

    /// <summary>冲刺键（默认 Shift）。</summary>
    public static KeyCode Dash = KeyCode.LeftShift;

    /// <summary>
    /// 能力键：**数字键 1~5**（下标 0 对应第一个能力，见 <see cref="Abilities.Playable"/>）。
    /// 不用字母键是因为 F/空格/Shift 已经各有用处，而数字键在点击式操作里最好按。
    /// </summary>
    public static readonly KeyCode[] AbilityKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5
    };

    public static bool InteractDown
    {
        get { return Input.GetKeyDown(Interact); }
    }

    public static bool DashDown
    {
        get { return Input.GetKeyDown(Dash) || Input.GetKeyDown(KeyCode.RightShift); }
    }

    /// <summary>第 <paramref name="index"/> 个能力键这一帧有没有按下（0 起）。</summary>
    public static bool AbilityDown(int index)
    {
        if (index < 0 || index >= AbilityKeys.Length) return false;
        return Input.GetKeyDown(AbilityKeys[index]);
    }

    /// <summary>按键的显示名（HUD 提示用）。</summary>
    public static string InteractName { get { return "F"; } }
    public static string DashName { get { return "Shift"; } }
    public static string AbilityName(int index)
    {
        return index >= 0 && index < AbilityKeys.Length ? (index + 1).ToString() : "?";
    }
}
