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

    public static bool InteractDown
    {
        get { return Input.GetKeyDown(Interact); }
    }

    public static bool DashDown
    {
        get { return Input.GetKeyDown(Dash) || Input.GetKeyDown(KeyCode.RightShift); }
    }

    /// <summary>按键的显示名（HUD 提示用）。</summary>
    public static string InteractName { get { return "F"; } }
    public static string DashName { get { return "Shift"; } }
}
