using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入后处理：放进 <c>Assets/Resources/ArtOverride/</c> 的图片会被自动配置成 Sprite，
/// 并按“被替换素材在世界里的宽度”反推 Pixels Per Unit。
///
/// 这样不管放进去的图片是 64px 还是 1024px，替换后占的位置都和原素材一致，不需要手改导入设置。
/// 找不到对应素材（名字没对上）时退回项目通用值：64px = 1 世界单位。
/// 已经配置过的图片（meta 里带标记）不再改动，方便手动微调。
/// </summary>
public class ArtOverridePostprocessor : AssetPostprocessor
{
    const string FolderToken = "/Resources/" + ArtOverride.ResourceFolder + "/";
    const string ConfiguredTag = "artoverride";
    const float FallbackPixelsPerUnit = 64f;

    static Dictionary<string, float> originalWidths;

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

        float worldWidth = OriginalWorldWidth(path);
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
        settings.wrapMode = TextureWrapMode.Clamp;
        importer.SetTextureSettings(settings);
        importer.userData = ConfiguredTag;

        Debug.Log("[ArtOverride] " + Path.GetFileName(path) + " 按 Sprite 导入，Pixels Per Unit = " + ppu.ToString("0.##")
            + (worldWidth > 0.0001f
                ? "（与原素材等宽：" + worldWidth.ToString("0.##") + " 世界单位）"
                : "（没有匹配到素材，使用通用值）"));
    }

    /// <summary>被替换素材在世界里的宽度（世界单位）。找不到返回 0。</summary>
    static float OriginalWorldWidth(string overridePath)
    {
        try
        {
            if (originalWidths == null) BuildOriginalWidths();
            float value;
            if (originalWidths.TryGetValue(ArtOverride.ResolveSpriteKey(Path.GetFileName(overridePath)), out value))
                return value;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArtOverride] 读取原素材尺寸失败：" + e.Message);
        }
        return 0f;
    }

    static void BuildOriginalWidths()
    {
        originalWidths = new Dictionary<string, float>();
        string[] guids = AssetDatabase.FindAssets("t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            if (path.IndexOf(FolderToken, System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string key = ArtOverride.NormalizeName(sprite.name);
            if (key.Length == 0 || originalWidths.ContainsKey(key)) continue;
            originalWidths[key] = sprite.bounds.size.x;
        }
    }
}
