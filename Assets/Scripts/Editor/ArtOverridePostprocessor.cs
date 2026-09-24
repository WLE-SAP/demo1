using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入后处理：放进 <c>Assets/Resources/ArtOverride/</c> 的图片会被自动配置成 Sprite，
/// 并按「这个 key 对应的原素材在世界里的宽度」反推 Pixels Per Unit。
///
/// 这样不管放进去的图片是 64px 还是 1024px，替换后占的位置都和原素材一致，不需要手改导入设置。
/// 九宫格 key（屋顶 / 木箱 / 面板…）还会把边框按原素材的比例自动放大到新图片上，
/// 四角因此不会跟着被拉伸。名字没对上 key 的图片退回通用值：64px = 1 世界单位。
/// **平铺 key**（草地 / 路面，见 <see cref="ArtOverride.TilingOf"/>）会额外把 Wrap Mode 设成 Repeat。
/// 已经配置过的图片（meta 里带标记）不再改动，方便手动微调。
/// </summary>
public class ArtOverridePostprocessor : AssetPostprocessor
{
    const string FolderToken = "/Resources/" + ArtOverride.ResourceFolder + "/";
    const string ConfiguredTag = "artoverride";
    const float FallbackPixelsPerUnit = 64f;

    struct SpriteInfo
    {
        public float worldWidth;
        public float borderRatio;
    }

    static Dictionary<string, SpriteInfo> originals;

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.IndexOf(FolderToken, System.StringComparison.OrdinalIgnoreCase) < 0) return;

        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null || importer.userData == ConfiguredTag) return;

        int width = 0;
        int height = 0;
        importer.GetSourceTextureWidthAndHeight(out width, out height);
        if (width <= 0) return;

        string key = ArtOverride.ResolveKey(path);
        ArtOverride.Slot slot = default;
        bool known = key != null && ArtOverride.Slots.TryGetValue(key, out slot);

        SpriteInfo info = default;
        bool hasInfo = known && TryGetOriginal(slot.source, out info);
        float worldWidth = hasInfo ? info.worldWidth : 0f;
        float ppu = worldWidth > 0.0001f ? width / worldWidth : FallbackPixelsPerUnit;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePixelsPerUnit = ppu;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        // 平铺素材（草地 / 路面）必须 Repeat，否则平铺时边缘会被拉伸出一道糊边；
        // 其余素材用 Clamp，免得边缘的透明像素把邻居吸进来
        settings.wrapMode = known && ArtOverride.TilingOf(key) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        // 九宫格 key：把原素材的圆角/边框比例搬到新图片上，四角才不会被拉伸
        if (known && slot.sliced && hasInfo && info.borderRatio > 0.0001f)
        {
            int side = Mathf.Min(width, height);
            int border = Mathf.Clamp(Mathf.RoundToInt(side * info.borderRatio), 1, Mathf.Max(1, side / 2));
            settings.spriteBorder = new Vector4(border, border, border, border);
        }

        importer.SetTextureSettings(settings);
        importer.userData = ConfiguredTag;

        Debug.Log("[ArtOverride] " + Path.GetFileName(path) + " → key " + (known ? key : "（没对上）")
            + "，按 Sprite 导入，Pixels Per Unit = " + ppu.ToString("0.##")
            + (known && ArtOverride.TilingOf(key) ? "，平铺（Wrap Mode = Repeat）" : "")
            + (worldWidth > 0.0001f
                ? "（与原素材等宽：" + worldWidth.ToString("0.##") + " 世界单位）"
                : "（没有匹配到 key，使用通用值）"));
    }

    /// <summary>取原素材的「世界宽度」与「边框比例」；找不到返回 false。</summary>
    static bool TryGetOriginal(string spriteName, out SpriteInfo info)
    {
        info = new SpriteInfo();
        if (string.IsNullOrEmpty(spriteName)) return false;

        try
        {
            if (originals == null) BuildOriginals();
            return originals.TryGetValue(ArtOverride.NormalizeName(spriteName), out info);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArtOverride] 读取原素材尺寸失败：" + e.Message);
            return false;
        }
    }

    static void BuildOriginals()
    {
        originals = new Dictionary<string, SpriteInfo>();
        string[] guids = AssetDatabase.FindAssets("t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            if (path.IndexOf(FolderToken, System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string key = ArtOverride.NormalizeName(sprite.name);
            if (key.Length == 0 || originals.ContainsKey(key)) continue;

            SpriteInfo info = new SpriteInfo();
            info.worldWidth = sprite.bounds.size.x;
            Texture2D texture = sprite.texture;
            info.borderRatio = texture != null && texture.width > 0 ? sprite.border.x / texture.width : 0f;
            originals[key] = info;
        }
    }
}
