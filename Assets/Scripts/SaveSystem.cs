using System.IO;
using UnityEngine;

/// <summary>
/// 存档数据。世界是「种子决定」的（区块内容 = 坐标 + 种子），
/// 所以只要记住种子和小虫的状态，就能把局面完整还原出来；村民由区块重新生成，不需要存。
/// </summary>
[System.Serializable]
public class GameSave
{
    public int version = SaveSystem.CurrentVersion;
    /// <summary>世界种子：决定这一局的世界长什么样。</summary>
    public int worldSeed = 20260922;
    /// <summary>小虫的位置。</summary>
    public float playerX;
    public float playerY;
    /// <summary>已吃数量。</summary>
    public int eaten;
    /// <summary>当前体力（不吃东西会掉，掉光就饿死）。</summary>
    public float stamina = 100f;
    /// <summary>成长等级（吃特殊食物长大的次数）。</summary>
    public int growthLevel;
    /// <summary>游戏内累计小时数（VillageClock）。</summary>
    public float clockHours = 7f;
    /// <summary>这一局玩了多久（秒）。</summary>
    public float playSeconds;
    /// <summary>存档时间，便于排查。</summary>
    public string savedAt = "";
}

/// <summary>
/// 存档读写：JSON 落到 <see cref="Application.persistentDataPath"/>。
/// 写入时先写临时文件再替换，避免正好在写入途中被强杀而留下半个坏档。
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 1;
    const string FileName = "whatabug_save.json";

    /// <summary>主菜单点了「继续游戏」后置 true，游戏场景读档时用。</summary>
    public static bool ContinueRequested;

    public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

    /// <summary>有没有可用存档（真的能读出来才算）。</summary>
    public static bool HasSave { get { return Load() != null; } }

    public static GameSave Load()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return null;

            GameSave save = JsonUtility.FromJson<GameSave>(json);
            if (save == null || save.version > CurrentVersion) return null;
            return save;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] 读档失败：" + e.Message);
            return null;
        }
    }

    public static bool Save(GameSave save)
    {
        if (save == null) return false;

        save.version = CurrentVersion;
        save.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(save, true));

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] 存档失败：" + e.Message);
            return false;
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] 删档失败：" + e.Message);
        }
    }
}
