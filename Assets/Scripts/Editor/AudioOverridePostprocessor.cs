using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入后处理：放进 <c>Assets/Resources/AudioOverride/</c> 的音频会被自动配置好，
/// 不需要手改 Import Settings。
///
/// <list type="bullet">
/// <item>BGM（<c>bgm</c> / <c>bgm_menu</c>）：<b>流式</b>载入（不一次性占内存，适合长音频）；</item>
/// <item>音效：<b>解压到内存 + 预载</b>（播放时不会因为解码卡一下）；</item>
/// <item>都不做单声道强制、不改压缩格式（保持工程默认的音质）。</item>
/// </list>
/// 已经配置过的文件（meta 里带标记）不再改动，方便手动微调。
/// </summary>
public class AudioOverridePostprocessor : AssetPostprocessor
{
    const string FolderToken = "/Resources/" + AudioOverride.ResourceFolder + "/";
    const string ConfiguredTag = "audiooverride";

    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.IndexOf(FolderToken, System.StringComparison.OrdinalIgnoreCase) < 0) return;

        AudioImporter importer = assetImporter as AudioImporter;
        if (importer == null || importer.userData == ConfiguredTag) return;

        string key = AudioOverride.ResolveKey(path);
        bool music = key == AudioKeys.Bgm || key == AudioKeys.BgmMenu;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.preloadAudioData = !music;      // 流式载入不能预载
        importer.defaultSampleSettings = settings;

        importer.forceToMono = false;
        importer.loadInBackground = true;
        importer.userData = ConfiguredTag;

        UnityEngine.Debug.Log("[AudioOverride] " + Path.GetFileName(path)
            + (key.Length == 0
                ? " 按音效导入（没对上 key，游戏不会用到它——名字写错了？）"
                : " 按 " + (music ? "BGM（流式载入）" : "音效（预载）") + " 导入，key = " + key));
    }
}
