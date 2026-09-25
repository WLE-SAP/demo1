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
/// **整图 key**（树 / 石头 / 灌木，见 <see cref="ArtOverride.WholeOf"/>）会读源图的 alpha 包围盒，
/// **按不透明内容裁掉透明边**（2026-09-25 加）—— 美术习惯把物体画在画布正中，
/// 不裁的话物体只有应有大小的三分之一，脚下的影子还比物体大一圈；裁完运行时按内容比例装进占地。
/// 已经配置过的图片（meta 里带标记）不再改动，方便手动微调。
/// </summary>
public class ArtOverridePostprocessor : AssetPostprocessor
{
    const string FolderToken = "/Resources/" + ArtOverride.ResourceFolder + "/";
    const string ConfiguredTag = "artoverride";
    const float FallbackPixelsPerUnit = 64f;

    /// <summary>算内容包围盒时把 alpha 低于这个值的像素当成透明（0~255）。</summary>
    const byte AlphaCutoff = 8;

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

        // 整图 key：按不透明内容裁掉透明边（游戏按内容的宽高比把图装进占地）
        Rect content = new Rect();
        bool trimmed = false;
        if (known && slot.whole && TryMeasureContent(path, out content))
        {
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            settings.spriteBorder = Vector4.zero;
            trimmed = true;
        }

        importer.SetTextureSettings(settings);

        if (trimmed)
        {
            SpriteMetaData meta = new SpriteMetaData();
            // 名字必须**保持文件名**：ArtOverride 是靠 sprite.name 认 key 的（别名 / 结尾数字都从它解析）
            meta.name = Path.GetFileNameWithoutExtension(path);
            meta.rect = content;
            meta.alignment = (int)SpriteAlignment.Center;
            meta.pivot = new Vector2(0.5f, 0.5f);
            meta.border = Vector4.zero;

            // `spritesheet` 被 Unity 标成过时了（建议改用 ISpriteEditorDataProvider），但它仍然能用：
            // 真被移除的 API 会升级成 CS0619 编译错误（工程里 AudioImporter.preloadAudioData 就是），
            // 这里只是 CS0618 警告。**改这段前先确认 .meta 里真的写进了裁边后的 rect**（见 Spec §7 验证）。
#pragma warning disable 618
            importer.spritesheet = new SpriteMetaData[] { meta };
#pragma warning restore 618
        }

        importer.userData = ConfiguredTag;

        Debug.Log("[ArtOverride] " + Path.GetFileName(path) + " → key " + (known ? key : "（没对上）")
            + "，按 Sprite 导入，Pixels Per Unit = " + ppu.ToString("0.##")
            + (known && ArtOverride.TilingOf(key) ? "，平铺（Wrap Mode = Repeat）" : "")
            + (trimmed
                ? "，整图素材：已按内容裁掉透明边（" + content.width.ToString("0") + "×" + content.height.ToString("0")
                  + "，原图 " + width + "×" + height + "）"
                : "")
            + (known
                ? (worldWidth > 0.0001f
                    ? "（与原素材等宽：" + worldWidth.ToString("0.##") + " 世界单位）"
                    : "（这个 key 没有对应的原素材，尺寸由代码给定）")
                : "（没有匹配到 key，使用通用值）"));
    }

    /// <summary>
    /// 读源图的 alpha 包围盒（**Unity 的 Sprite 坐标：左下角为原点**，可直接当 <c>SpriteMetaData.rect</c>）。
    /// 这时候资源还没导入，所以直接从磁盘读原图；整张全透明或读失败时返回 false（那就保持原样导入）。
    /// </summary>
    static bool TryMeasureContent(string path, out Rect content)
    {
        content = new Rect();
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) return false;

            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();

            int minX = width, maxX = -1, minY = height, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= AlphaCutoff) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) return false;

            // GetPixels32 的第 0 行是图片**底部**，和 SpriteMetaData.rect 的坐标系一致
            content = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArtOverride] 读 " + Path.GetFileName(path) + " 的内容范围失败，保持原图导入："
                + e.Message);
            return false;
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
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
