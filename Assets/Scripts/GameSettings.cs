using UnityEngine;

/// <summary>
/// 玩家设置：目前只有「音量」（主音量）。
/// 主音量直接写全局的 <see cref="AudioListener.volume"/>，并存进 PlayerPrefs，
/// 所以换场景、重开游戏都保留。分辨率 / 屏幕模式由 <see cref="MainMenu"/> 自己管
/// （它需要先拿到这台机器可用的分辨率列表，不适合放在这里）。
/// </summary>
public static class GameSettings
{
    const string KeyVolume = "game.volume";

    public const float VolumeStep = 0.1f;
    const float DefaultVolume = 0.8f;

    static float volume = -1f;

    /// <summary>主音量 0 ~ 1。</summary>
    public static float MasterVolume
    {
        get
        {
            if (volume < 0f) volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, DefaultVolume));
            return volume;
        }
        set
        {
            volume = Mathf.Clamp01(Mathf.Round(value / VolumeStep) * VolumeStep);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(KeyVolume, volume);
            PlayerPrefs.Save();
        }
    }

    /// <summary>显示用：形如 80%。</summary>
    public static string VolumeText { get { return Mathf.RoundToInt(MasterVolume * 100f) + "%"; } }

    /// <summary>± 一档（默认一档 10%）。</summary>
    public static void StepVolume(float delta)
    {
        MasterVolume = MasterVolume + delta;
    }

    /// <summary>把存下来的音量套用到音频系统。</summary>
    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }

    /// <summary>每次进入播放（含编辑器里重进 Play）都套用一次，免得音量设置被重置。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        Apply();
    }
}
