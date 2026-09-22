using UnityEngine;

/// <summary>
/// 启动时把 <c>Resources/ArtOverride</c> 里的图片套用到场景里的精灵上。
/// 执行顺序排在很后面，保证 VillageGenerator 生成的村庄（房屋 / 树 / 食物 / 木箱 / 村民）
/// 和高亮底衬都已经创建完毕，这样它们也能一起被替换。
/// 文件夹为空时什么都不做，游戏保持原有的程序化美术。
/// </summary>
[DefaultExecutionOrder(1000)]
public class ArtOverrideApplier : MonoBehaviour
{
    [Tooltip("启动时自动套用一次")]
    public bool applyOnStart = true;

    [Tooltip("把载入 / 替换 / 没生效的图片数量打到 Console")]
    public bool logToConsole = true;

    void Start()
    {
        if (applyOnStart) ApplyNow();
    }

    /// <summary>立即套用一次（运行中新放了图片时可手动调用）。</summary>
    public void ApplyNow()
    {
        int count = ArtOverride.Count;
        int applied = ArtOverride.ApplyAll();
        if (!logToConsole) return;

        if (count == 0)
        {
            Debug.Log("[ArtOverride] Resources/" + ArtOverride.ResourceFolder + " 里没有图片，使用程序化美术。");
            return;
        }

        string message = "[ArtOverride] 图片 " + count + " 张，替换了 " + applied + " 个精灵。";
        System.Collections.Generic.List<string> unused = ArtOverride.UnusedFiles;
        if (unused.Count > 0)
            message += " 未匹配到素材（名字写错了？）：" + string.Join("、", unused.ToArray());
        Debug.Log(message);
    }
}
