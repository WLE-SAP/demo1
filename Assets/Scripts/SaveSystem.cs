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
    /// <summary>世界种子：决定这一局的世界长什么样（地貌与聚落都由它推出来）。</summary>
    public int worldSeed = 20260922;
    /// <summary>存档时小虫所在区块的自然体系（<see cref="NatureKind"/>）。**6 版新增**，只用于界面显示。</summary>
    public int natureKind;
    /// <summary>存档时小虫所在区块的聚落体系（<see cref="SettlementKind"/>）。**6 版新增**，只用于界面显示。</summary>
    public int settlementKind;
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

    /// <summary>已解锁的能力位掩码（bit = (int)AbilityId）。**3 版新增**。</summary>
    public int abilityMask;
    /// <summary>每个能力的剩余充能（下标 = (int)AbilityId）。**3 版新增**（老档读出来是 null）。</summary>
    public int[] abilityCharges;

    /// <summary>混乱值（0~100）。**4 版新增**。</summary>
    public float chaos;
    /// <summary>警觉值（0~100）。**4 版新增**。</summary>
    public float alert;
    /// <summary>本局统计：打碎数 / 吞噬数 / 被发现次数 / 滑倒次数 / 用能力次数 / 最长连锁。**4 版新增**。</summary>
    public int statBroken;
    public int statEaten;
    public int statSpotted;
    public int statSlips;
    public int statAbilityUses;
    public int statLongestChain;

    /// <summary>
    /// 老存档（6 版之前）没记「存档在哪种地貌」这件事 —— 那时候世界里还没有地貌体系。
    /// 所以界面要显示的话先看这个：为 false 就别显示，免得把默认值 0 当成「草原 · 荒野」。
    /// </summary>
    public bool HasBiomeInfo
    {
        get { return version >= 6; }
    }

    /// <summary>存档所在地貌的一句话，例如「森林 · 农村」（老存档返回空串）。</summary>
    public string BiomeText
    {
        get
        {
            if (!HasBiomeInfo) return "";
            return WorldBiome.Describe((NatureKind)Mathf.Clamp(natureKind, 0, 2),
                                       (SettlementKind)Mathf.Clamp(settlementKind, 0, 2));
        }
    }
}

/// <summary>
/// 存档读写：JSON 落到 <see cref="Application.persistentDataPath"/>。
/// 写入时先写临时文件再替换，避免正好在写入途中被强杀而留下半个坏档。
/// </summary>
public static class SaveSystem
{
    /// <summary>
    /// 6 版：**取消了「开局选地图」**，所以不再写 <c>mapKind</c>；改成记下「存档时小虫在哪种地貌」
    /// （<c>natureKind</c> / <c>settlementKind</c>，只给界面显示用，世界的真正依据仍然是种子）。
    /// 老存档里多出来的 <c>mapKind</c> 会被 JsonUtility 直接忽略，不会报错。
    /// 5 版是删掉任务模块，4 版是混乱 / 警觉 / 统计，3 版是能力，2 版是地图类型。
    /// 老存档按 <see cref="GameSave.version"/> 分支处理，**不要**用「字段是不是缺省值」当哨兵（红线 8）。
    /// </summary>
    public const int CurrentVersion = 6;
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
