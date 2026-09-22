using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 头部靠近时给可交互 / 可拖拽的物品加高亮；
/// 当前的 F 拾取目标、Space 进食目标、正在搬运的物品用更强的颜色。
/// </summary>
public class ProximityHighlight : MonoBehaviour
{
    public Transform head;
    public BugController bug;
    public DragController interact;
    public BugEat eat;

    [Tooltip("头部多大范围内开始高亮（世界单位）")]
    public float radius = 1.4f;
    [Tooltip("每隔几帧扫描一次")]
    public int scanInterval = 2;

    readonly List<Highlighter> cached = new List<Highlighter>();
    int frame;
    int rebuildTimer;

    void Awake()
    {
        if (bug == null) bug = FindObjectOfType<BugController>();
        if (bug != null && head == null) head = bug.transform;
        if (interact == null) interact = FindObjectOfType<DragController>();
        if (eat == null) eat = FindObjectOfType<BugEat>();
    }

    void Update()
    {
        if (head == null) return;

        frame++;
        if (frame % Mathf.Max(1, scanInterval) != 0) return;

        if (--rebuildTimer <= 0)
        {
            Rebuild();
            rebuildTimer = 30;
        }

        Vector2 origin = head.position;
        float radiusSqr = radius * radius;

        Highlighter heldGlow = null;
        Highlighter grabTarget = null;
        Highlighter eatTarget = null;

        if (interact != null && interact.Held != null) heldGlow = interact.Held.GetComponent<Highlighter>();
        if (interact != null && !interact.IsHolding)
        {
            Draggable nearest = interact.FindInteractable();
            if (nearest != null) grabTarget = nearest.GetComponent<Highlighter>();
        }
        if (eat != null)
        {
            Edible food = eat.FindTarget();
            if (food != null) eatTarget = food.GetComponent<Highlighter>();
        }

        for (int i = 0; i < cached.Count; i++)
        {
            Highlighter h = cached[i];
            if (h == null) continue;

            int level;
            if (h == heldGlow || h == grabTarget || h == eatTarget) level = 2;
            else if (((Vector2)h.transform.position - origin).sqrMagnitude <= radiusSqr) level = 1;
            else level = 0;

            h.SetLevel(level);
        }
    }

    void Rebuild()
    {
        cached.Clear();
        Highlighter[] found = FindObjectsOfType<Highlighter>();
        for (int i = 0; i < found.Length; i++)
            if (found[i] != null) cached.Add(found[i]);
    }

    public int HighlightedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < cached.Count; i++) if (cached[i] != null && cached[i].Level > 0) n++;
            return n;
        }
    }
}
