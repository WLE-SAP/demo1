using UnityEngine;

/// <summary>
/// 悬浮窗要展示的介绍文本。物品（食物 / 木箱 / 树 / 地洞…）都挂一个，
/// 村民不用挂（窗口会直接读村民的名字、职业和当前活动）。
/// 这里**不放隐藏数值**——数值是不显示给玩家的。
/// </summary>
public class EntityInfo : MonoBehaviour
{
    [Tooltip("标题，比如「果子」")]
    public string title = "物品";

    [Tooltip("分类，比如「食物」「道具」「设施」")]
    public string kind = "";

    [TextArea(2, 4)]
    [Tooltip("一段介绍")]
    public string description = "";

    /// <summary>带分类的标题，形如 果子（食物）。</summary>
    public string FullTitle
    {
        get { return string.IsNullOrEmpty(kind) ? title : title + "（" + kind + "）"; }
    }

    /// <summary>取某个物体上的介绍；没有返回 null。</summary>
    public static EntityInfo Of(GameObject target)
    {
        return target == null ? null : target.GetComponentInParent<EntityInfo>();
    }
}
