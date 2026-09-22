using UnityEngine;

/// <summary>
/// 可被 F 拾取 / 搬运的物品标记。
/// </summary>
public class Draggable : MonoBehaviour
{
    [Tooltip("重量：数值越大拖动越迟滞")]
    public float weight = 1f;
}
