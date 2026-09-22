using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 小虫的体力：**不吃东西就会持续下降，降到 0 就饿死**。
/// 吃到食物恢复体力（不同食物不一样），吃特殊食物还会顺便成长（见 <see cref="BugGrowth"/>）。
/// 饿死后会把当前进度存下来（体力回一点，免得一读档又立刻饿死），然后回开始界面。
/// </summary>
public class BugVitality : MonoBehaviour
{
    [Header("体力")]
    public float maxStamina = 100f;
    public float startStamina = 100f;
    [Tooltip("每秒掉多少体力（站着不动也在掉）")]
    public float drainPerSecond = 1f;
    [Tooltip("饿死后停留多久再回开始界面（秒）")]
    public float deathDelay = 1.8f;
    [Tooltip("饿死后存档里保留的体力比例")]
    [Range(0f, 1f)] public float respawnRatio = 0.6f;

    [Header("场景")]
    public string menuSceneName = "MainMenu";

    public float Stamina { get; private set; }
    public float Ratio { get { return maxStamina > 0.01f ? Mathf.Clamp01(Stamina / maxStamina) : 0f; } }
    public bool IsDead { get; private set; }
    /// <summary>体力偏低（HUD 可以变红）。</summary>
    public bool IsLow { get { return Ratio <= 0.3f; } }

    BugController bug;
    Rigidbody2D body;
    AutoSave autoSave;
    float deathTimer = -1f;
    float baseMax = -1f;

    void Awake()
    {
        bug = GetComponent<BugController>();
        body = GetComponent<Rigidbody2D>();
        autoSave = FindObjectOfType<AutoSave>();
        Stamina = Mathf.Clamp(startStamina, 0f, maxStamina);
    }

    void Update()
    {
        if (IsDead)
        {
            deathTimer -= Time.deltaTime;
            if (deathTimer <= 0f) GoToMenu();
            return;
        }

        Stamina = Mathf.Max(0f, Stamina - drainPerSecond * Time.deltaTime);
        if (Stamina <= 0f) Die();
    }

    /// <summary>吃东西恢复体力（不同食物量不同）。</summary>
    public void AddStamina(float amount)
    {
        if (IsDead) return;
        Stamina = Mathf.Clamp(Stamina + amount, 0f, maxStamina);
    }

    /// <summary>读档用。</summary>
    public void Restore(float stamina)
    {
        Stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    /// <summary>成长时提高上限（保持当前比例）。</summary>
    public void SetMaxStamina(float max, bool keepRatio = true)
    {
        float ratio = maxStamina > 0.01f ? Ratio : 1f;
        maxStamina = Mathf.Max(10f, max);
        Stamina = keepRatio ? maxStamina * ratio : Mathf.Min(Stamina, maxStamina);
    }

    /// <summary>
    /// 回到成长前的基础上限（用于重新计算）。
    /// 第一次取值时把场景里的 maxStamina 当作基准记下来——不能放在 Awake 里，
    /// 因为组件 Awake 顺序不保证，BugGrowth 可能先跑。
    /// </summary>
    public float BaseMaxStamina
    {
        get
        {
            if (baseMax < 0f) baseMax = maxStamina;
            return baseMax;
        }
    }

    void Die()
    {
        IsDead = true;
        deathTimer = Mathf.Max(0.2f, deathDelay);
        Stamina = 0f;

        // 停住小虫：不能再走、不能再交互
        if (bug != null) bug.enabled = false;
        if (body != null) body.velocity = Vector2.zero;

        // 把进度存下来，体力留一点，免得读档后立刻又饿死
        if (autoSave != null)
        {
            Stamina = maxStamina * respawnRatio;
            autoSave.SaveNow();
        }
        Debug.Log("[Bug] 饿死了…… 回到开始界面。");
    }

    void GoToMenu()
    {
        if (!string.IsNullOrEmpty(menuSceneName)) SceneManager.LoadScene(menuSceneName);
    }
}
