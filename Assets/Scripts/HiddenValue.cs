using UnityEngine;

/// <summary>
/// 不显示给玩家的隐藏数值。每个物品和村民都有一个，但**永远不会出现在 HUD 或信息窗里**。
/// <list type="bullet">
/// <item>物品：<c>value</c> = 分量（决定吃下去恢复多少体力、以及要不要长大才吃得下）</item>
/// <item>村民：<c>value</c> = 体魄（影响追逐的耐心与一点点速度）</item>
/// </list>
/// <c>note</c> 只是给编辑器里看备注用的，运行时不参与任何逻辑。
/// </summary>
public class HiddenValue : MonoBehaviour
{
    [Tooltip("隐藏数值（不显示）")]
    public int value = 1;

    [Tooltip("备注：只给编辑器看，运行时没用")]
    public string note = "";

    /// <summary>取值；没有挂组件时返回 fallback。</summary>
    public static int Of(GameObject target, int fallback)
    {
        if (target == null) return fallback;
        HiddenValue hidden = target.GetComponent<HiddenValue>();
        return hidden != null ? hidden.value : fallback;
    }
}
