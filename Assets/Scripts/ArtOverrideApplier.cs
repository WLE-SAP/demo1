using UnityEngine;

/// <summary>
/// 启动时把 <c>Resources/ArtOverride</c> 里的图片套用到场景里那几个**手工摆的**精灵上
/// （小虫的头、点击标记、地面…见场景对象上的 <see cref="ArtSlot"/>）。
/// 执行顺序排在很后面，保证 VillageGenerator 已经生成完毕。
///
/// 村庄里的物件（房屋 / 树 / 食物 / 木箱 / 村民）是区块反复生成 / 回收的，
/// 它们在创建的那一刻就自己套好图片了，不靠这里。
/// 图片目录为空时什么都不做，游戏保持程序化美术。
/// </summary>
[DefaultExecutionOrder(1000)]
public class ArtOverrideApplier : MonoBehaviour
{
    [Tooltip("启动时自动套用一次")]
    public bool applyOnStart = true;

    [Tooltip("把载入 / 替换 / 没对上的图片数量打到 Console")]
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

        string message = "[ArtOverride] 图片 " + count + " 张，场景精灵替换了 " + applied
            + " 个（村庄里的物件在生成时已各自替换）。";
        System.Collections.Generic.List<string> unmatched = ArtOverride.UnmatchedFiles;
        if (unmatched.Count > 0)
            message += " 没对上 key（名字写错了？）：" + string.Join("、", unmatched.ToArray());
        Debug.Log(message);
    }
}
