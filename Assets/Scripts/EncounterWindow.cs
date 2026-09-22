using UnityEngine;
using TMPro;

/// <summary>
/// 右下角的悬浮窗：小虫**遇到物品或 NPC** 时弹出对它的介绍，走开后淡出。
/// 内容来自 <see cref="EntityInfo"/>（物品）或村民本身（名字 / 职业 / 当前活动）。
/// 注意：这里不会显示 <see cref="HiddenValue"/> 的隐藏数值。
/// </summary>
public class EncounterWindow : MonoBehaviour
{
    [Header("界面")]
    public CanvasGroup group;
    public TMP_Text title;
    public TMP_Text body;

    [Header("引用")]
    public BugController bug;

    [Header("遇到判定")]
    [Tooltip("离得多近算「遇到」")]
    public float radius = 1.8f;
    [Tooltip("扫描间隔（秒）")]
    public float scanInterval = 0.15f;
    [Tooltip("走开后多久淡出")]
    public float hideDelay = 1.2f;
    [Tooltip("淡入淡出速度")]
    public float fadeSpeed = 8f;

    readonly Collider2D[] buffer = new Collider2D[24];
    float nextScan;
    float hideTimer;
    float targetAlpha;
    string lastTitle;
    string lastBody;

    void Awake()
    {
        if (bug == null) bug = FindObjectOfType<BugController>();
        if (group != null) group.alpha = 0f;
    }

    void Update()
    {
        if (group == null) return;
        if (bug == null) bug = FindObjectOfType<BugController>();

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + Mathf.Max(0.05f, scanInterval);
            Scan();
        }

        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f) targetAlpha = 0f;
        }

        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }

    void Scan()
    {
        if (bug == null) return;

        Vector2 self = bug.transform.position;
        string bestTitle = null;
        string bestBody = null;
        float bestDistance = float.MaxValue;

        // 村民（静态表里都是没被冻结的）
        for (int i = 0; i < Villager.All.Count; i++)
        {
            Villager villager = Villager.All[i];
            if (villager == null) continue;
            float distance = ((Vector2)villager.transform.position - self).sqrMagnitude;
            if (distance > radius * radius || distance >= bestDistance) continue;

            // 躲在地洞里时不会「遇到」村民（村民也看不见你）
            if (bug.IsHidden) continue;

            bestDistance = distance;
            bestTitle = villager.displayName + " · " + villager.JobLabel;
            bestBody = VillagerJobs.Intro(villager.job) + "\n现在：" + villager.ActivityText;
        }

        // 物品 / 设施：用一次圆形检测拿到所有碰撞体，再看它（或父物体）上有没有介绍
        if (bestTitle == null)
        {
            int count = Physics2D.OverlapCircleNonAlloc(self, radius, buffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = buffer[i];
                if (collider == null) continue;

                float distance = ((Vector2)collider.transform.position - self).sqrMagnitude;
                if (distance >= bestDistance) continue;

                EntityInfo info = collider.GetComponentInParent<EntityInfo>();
                if (info == null) continue;

                bestDistance = distance;
                bestTitle = info.FullTitle;
                bestBody = info.description;
            }

            // 地洞没有碰撞体，单独找
            Burrow burrow = Burrow.Nearest(self, radius);
            if (burrow != null)
            {
                float distance = ((Vector2)burrow.transform.position - self).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTitle = burrow.tunnel ? "地道（设施）" : "地洞（设施）";
                    bestBody = burrow.IsTunnel
                        ? "和另一头连通的土洞，钻进去会直接传送到对面。"
                        : "小虫能钻进去躲起来，躲着的时候村民看不见你。";
                }
            }
        }

        if (bestTitle == null)
        {
            if (hideTimer <= 0f) hideTimer = Mathf.Max(0.05f, hideDelay);
            return;
        }

        hideTimer = 0f;
        targetAlpha = 1f;

        if (bestTitle == lastTitle && bestBody == lastBody) return;
        lastTitle = bestTitle;
        lastBody = bestBody;
        if (title != null) title.text = bestTitle;
        if (body != null) body.text = bestBody;
    }
}
