using UnityEngine;

/// <summary>
/// 给**手工摆在场景里**的精灵标一个美术 key（一物一图）。
/// <see cref="VillageGenerator"/> 生成的物件在创建时就自己套好了图片，不需要这个组件；
/// 只有场景里本来就有的那几个（小虫的头、点击标记、地面）挂它。
///
/// 没放对应图片时什么都不做，保持程序化美术。
/// </summary>
public class ArtSlot : MonoBehaviour
{
    [Tooltip("美术 key，取值见 Assets/Resources/ArtOverride/README.md")]
    public string key;

    [Tooltip("勾上则连子物体上的 SpriteRenderer 一起替换")]
    public bool includeChildren = false;

    void Awake()
    {
        Apply();
    }

    /// <summary>套用一次，返回被替换的渲染器数量。</summary>
    public int Apply()
    {
        if (string.IsNullOrEmpty(key)) return 0;
        if (!ArtOverride.Has(key)) return 0;      // 没放图片就别动它

        int count = 0;
        if (includeChildren)
        {
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
                if (ArtOverride.Apply(all[i], key)) count++;
            return count;
        }

        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        return ArtOverride.Apply(sprite, key) ? 1 : 0;
    }
}
