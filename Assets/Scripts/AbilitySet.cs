using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小虫的能力栏：谁解锁了、还剩几发充能、什么时候能用，以及**四个能力的实际效果**都在这里。
///
/// <list type="bullet">
/// <item>**怎么获得**：吃掉带 <see cref="AbilityPickup"/> 的东西（旧电池 / 破布团 / 孢子囊 / 锈齿轮）。
///       第一次吃**永久解锁**，之后每次吃补充满能（上限 <see cref="Abilities.MaxCharges"/>）。</item>
/// <item>**怎么用**：数字键 1~4（键位统一在 <see cref="GameInput.AbilityKeys"/>），
///       每次使用消耗 1 点充能并进入冷却；没充能就用不了，得再去吃。</item>
/// <item>**效果**：全部复用已有系统 —— 村民状态机（麻 / 逃）、噪音事件（<see cref="GameEvent"/>）、
///       昼夜灯（<see cref="NightGlow"/>）、可搬物品的刚体（震飞）。</item>
/// </list>
/// 组件由 <see cref="Boot"/> 在场景加载后自动挂到小虫身上（和 <see cref="BugFootsteps"/> 同一套路），
/// **不需要改场景**。
/// </summary>
public class AbilitySet : MonoBehaviour
{
    [Header("电击")]
    [Tooltip("电击半径（世界单位）")]
    public float shockRadius = 3.2f;
    [Tooltip("被电麻多久")]
    public float shockStunSeconds = 3.5f;
    [Tooltip("电击冷却（秒）")]
    public float shockCooldown = 6f;
    [Tooltip("电击会点亮/闪动范围内的灯（半径比电击半径大一圈）")]
    public float flickerRadius = 8f;

    [Header("伪装")]
    [Tooltip("伪装持续多久")]
    public float disguiseSeconds = 8f;
    public float disguiseCooldown = 10f;
    [Tooltip("伪装时贴多近会露馅（村民贴脸距离 awareRadius 的倍率）")]
    public float disguiseRevealFactor = 0.6f;

    [Header("分裂")]
    [Tooltip("放出几只假小虫（Decoy）")]
    public int splitCount = 2;
    [Tooltip("假小虫活多久")]
    public float splitSeconds = 8f;
    public float splitCooldown = 12f;

    [Header("抖动")]
    [Tooltip("震飞东西的半径")]
    public float shakeRadius = 3.5f;
    [Tooltip("抖动持续多久")]
    public float shakeSeconds = 0.6f;
    [Tooltip("震飞木箱用的冲量大小")]
    public float shakeImpulse = 5.5f;
    [Tooltip("被震飞的东西多久后收回「固定不动」（木箱平时是运动学刚体）")]
    public float shakeSettleSeconds = 0.9f;
    [Tooltip("把多远的村民吓跑")]
    public float shakePanicRadius = 4.5f;
    [Tooltip("被吓跑多久")]
    public float shakePanicSeconds = 4f;
    public float shakeCooldown = 8f;

    [Header("腐蚀")]
    [Tooltip("能够到多远的可腐蚀目标（木箱 / 电线）")]
    public float corrodeRange = 2.2f;
    public float corrodeCooldown = 5f;

    [Header("表现")]
    [Tooltip("伪装时身上那层「破烂」的颜色")]
    public Color disguiseColor = new Color(0.42f, 0.36f, 0.26f, 0.92f);

    /// <summary>场上的小虫能力栏（只有一只小虫，所以是单例）。</summary>
    public static AbilitySet Instance { get; private set; }

    readonly bool[] unlocked = new bool[Abilities.Count];
    readonly int[] charges = new int[Abilities.Count];
    readonly float[] nextUseTime = new float[Abilities.Count];

    float disguiseUntil;
    float shakingUntil;
    bool disguiseFlicker;
    BugController bug;
    BugVitality vitality;
    Transform disguiseVisual;

    /// <summary>是否正伪装着（村民 <see cref="Villager.CanSeeBug"/> 会问它）。</summary>
    public bool IsDisguised { get { return Time.time < disguiseUntil; } }
    /// <summary>是否正在抖动（HUD 显示用）。</summary>
    public bool IsShaking { get { return Time.time < shakingUntil; } }
    /// <summary>伪装还剩多少秒。</summary>
    public float DisguiseLeft { get { return Mathf.Max(0f, disguiseUntil - Time.time); } }

    /// <summary>场景加载后自动挂到小虫身上（已经在上面就跳过）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        BugController target = FindObjectOfType<BugController>();
        if (target == null) return;
        if (target.GetComponent<AbilitySet>() != null) return;
        target.gameObject.AddComponent<AbilitySet>();
    }

    void Awake()
    {
        Instance = this;
        bug = GetComponent<BugController>();
        vitality = GetComponent<BugVitality>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 数字键 1~5 直接试放（没解锁 / 没充能 / 冷却中都会被 TryUse 挡掉）
        for (int i = 0; i < Abilities.Playable.Length; i++)
            if (GameInput.AbilityDown(i)) TryUse(Abilities.Playable[i]);

        UpdateDisguiseVisual();
    }

    // ---------------- 查询 / 授予 ----------------

    public bool IsUnlocked(AbilityId id) { return unlocked[(int)id]; }
    public int Charges(AbilityId id) { return charges[(int)id]; }
    public bool IsReady(AbilityId id)
    {
        return unlocked[(int)id] && charges[(int)id] > 0 && Time.time >= nextUseTime[(int)id];
    }
    /// <summary>还剩多少秒冷却。</summary>
    public float CooldownLeft(AbilityId id) { return Mathf.Max(0f, nextUseTime[(int)id] - Time.time); }

    /// <summary>解锁（第一次）并补充充能。返回这次是不是首次解锁。</summary>
    public bool Grant(AbilityId id, int amount)
    {
        int i = (int)id;
        bool firstTime = !unlocked[i];
        unlocked[i] = true;
        charges[i] = Mathf.Clamp(charges[i] + Mathf.Max(1, amount), 0, Abilities.MaxCharges);

        if (firstTime)
        {
            AbilityInfo info = Abilities.Get(id);
            Debug.Log("[Ability] 解锁【" + info.name + "】—— 按 " + Abilities.KeyName(id) + " 使用。");

            // 解锁新能力的那一下：局部黑屏 + 撕裂加剧烈抖动 + 左下角报错（见 GlitchOverlay）
            GlitchOverlay overlay = GlitchOverlay.Instance;
            if (overlay != null) overlay.AbilityUnlocked(info.name);

            // 世界里也「坏一下」：小虫身上冒一团洋红 + 自己抖一下
            AbilityFx.Flash(transform.position, ArtShape.Round, 1.2f, AbilityFx.ErrorMagenta, 0.4f);
            if (bug != null)
            {
                Transform target = bug.transform.Find("Head");
                if (target != null) AbilityFx.Jitter(target.gameObject, 0.5f, 0.06f, true);
            }

            // 音效接口（没放 Resources/AudioOverride/ability_unlock 就是安静的）
            AudioOverridePlayer.Play(AudioKeys.AbilityUnlock);
        }

        return firstTime;
    }

    /// <summary>吃掉赋予能力的食物时调用（<see cref="BugEat"/> 里接的）。找不到能力栏就什么都不做。</summary>
    public static void GrantFromPickup(AbilityPickup pickup)
    {
        if (pickup == null || Instance == null) return;
        Instance.Grant(pickup.ability, pickup.charges);
    }

    // ---------------- 使用 ----------------

    /// <summary>用一次能力。没解锁 / 没充能 / 冷却中都返回 false（并说明原因）。</summary>
    public bool TryUse(AbilityId id)
    {
        int i = (int)id;
        AbilityInfo info = Abilities.Get(id);

        if (!unlocked[i])
        {
            Debug.Log("[Ability] 还没学会【" + info.name + "】：吃掉" + UnlockHint(id) + "就能解锁。");
            return false;
        }
        if (charges[i] <= 0)
        {
            Debug.Log("[Ability] 【" + info.name + "】没有充能了：再吃一个" + UnlockHint(id) + "。");
            return false;
        }
        if (Time.time < nextUseTime[i])
        {
            Debug.Log("[Ability] 【" + info.name + "】还在冷却（还有 " + CooldownLeft(id).ToString("F1") + " 秒）。");
            return false;
        }
        if (bug != null && bug.IsHidden)
        {
            Debug.Log("[Ability] 躲在地洞里用不了能力。");
            return false;
        }

        // 腐蚀：面前没有能蚀穿的东西就不消耗充能、也不进冷却（否则玩家会白扔一发）
        if (id == AbilityId.Corrode && FindCorrodeTarget() == null)
        {
            Debug.Log("[Ability] 【腐蚀】面前没有能蚀穿的东西（木箱 / 电线）。");
            return false;
        }

        charges[i]--;
        switch (id)
        {
            case AbilityId.Shock:
                nextUseTime[i] = Time.time + shockCooldown;
                CastShock();
                break;
            case AbilityId.Disguise:
                nextUseTime[i] = Time.time + disguiseCooldown;
                CastDisguise();
                break;
            case AbilityId.Split:
                nextUseTime[i] = Time.time + splitCooldown;
                CastSplit();
                break;
            case AbilityId.Shake:
                nextUseTime[i] = Time.time + shakeCooldown;
                CastShake();
                break;
            case AbilityId.Corrode:
                nextUseTime[i] = Time.time + corrodeCooldown;
                CastCorrode();
                break;
            default:
                charges[i]++;               // 没实现的能力：不扣充能
                Debug.Log("[Ability] 【" + info.name + "】还没实现（留给「可破坏物体」那一步）。");
                return false;
        }

        GameEvent.RaiseAbilityUsed(id, transform.position);

        // 放技能的音效：没往 Resources/AudioOverride/ 放对应文件时就是安静的（接口先留着）
        AudioOverridePlayer.Play(Abilities.AudioKeyOf(id));
        return true;
    }

    static string UnlockHint(AbilityId id)
    {
        switch (id)
        {
            case AbilityId.Shock: return "旧电池";
            case AbilityId.Disguise: return "破布团";
            case AbilityId.Split: return "孢子囊";
            case AbilityId.Shake: return "锈齿轮";
            default: return "对应的东西";
        }
    }

    // ---------------- 四种效果 ----------------

    /// <summary>
    /// 电击：把半径内的村民麻住、让附近的灯乱闪，并弄出很大的动静。
    /// **还会引爆附近的油桶、拉响附近的警报器**（链 B 的起点：电击 → 爆炸 → 警报 → 全村撤离）。
    /// </summary>
    void CastShock()
    {
        Vector2 self = transform.position;
        float sqr = shockRadius * shockRadius;
        int stunned = 0;

        Villager[] villagers = Villager.All.ToArray();
        for (int i = 0; i < villagers.Length; i++)
        {
            Villager v = villagers[i];
            if (v == null || v.IsFrozen) continue;
            if (((Vector2)v.transform.position - self).sqrMagnitude > sqr) continue;
            v.Stun(shockStunSeconds);
            v.GlitchParts(0.6f);        // 被电到的人「渲染出错」：部件错位 + 洋红闪
            stunned++;
        }

        // 世界级故障：洋红冲击环 + 一地错误材质火花（屏幕级由 GlitchOverlay 自动播）
        AbilityFx.Shock(self, shockRadius);

        int boom = 0;
        Explosive[] explosives = FindObjectsOfType<Explosive>();
        for (int i = 0; i < explosives.Length; i++)
        {
            if (explosives[i] == null || explosives[i].Exploded) continue;
            if (((Vector2)explosives[i].transform.position - self).sqrMagnitude > sqr) continue;
            explosives[i].Detonate();
            boom++;
        }

        int alarms = 0;
        Alarm[] alarmList = FindObjectsOfType<Alarm>();
        for (int i = 0; i < alarmList.Length; i++)
        {
            if (alarmList[i] == null || alarmList[i].IsRinging) continue;
            if (((Vector2)alarmList[i].transform.position - self).sqrMagnitude > sqr) continue;
            alarmList[i].Raise();
            alarms++;
        }

        FlickerLamps(flickerRadius, 0.7f);

        // 电击是最响的动静（响度 2.5）：附近没被电到的人也都会跑来看
        GameEvent.RaiseNoise(self, GameEvent.ShockLoudness, NoiseKind.Shock);
        Debug.Log("[Ability] 电击：麻住 " + stunned + " 个村民、引爆 " + boom + " 个油桶、拉响 " + alarms + " 个警报。");
    }

    /// <summary>伪装：一段时间内村民看不见小虫（贴脸除外）。</summary>
    void CastDisguise()
    {
        disguiseUntil = Time.time + disguiseSeconds;
        EnsureDisguiseVisual();
        Debug.Log("[Ability] 伪装 " + disguiseSeconds.ToString("F0") + " 秒。");
    }

    /// <summary>分裂：放出几只假小虫（它们自己会到处乱窜并出声）。</summary>
    void CastSplit()
    {
        Vector2 self = transform.position;
        for (int i = 0; i < Mathf.Max(1, splitCount); i++)
        {
            float angle = (i / (float)Mathf.Max(1, splitCount)) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.8f, 1.4f);
            Decoy decoy = Decoy.Spawn(self + offset, splitSeconds);

            // 每只分身都「复制粘贴」出两份半透明残影，并且抖一下
            if (decoy != null) AbilityFx.SplitGhosts(decoy.gameObject);
        }

        GameEvent.RaiseNoise(self, GameEvent.EatLoudness, NoiseKind.Eat);
        Debug.Log("[Ability] 分裂出 " + splitCount + " 只假小虫。");
    }

    /// <summary>强制抖动：把附近的箱子震飞、把村民吓跑（响度 2.0）。</summary>
    void CastShake()
    {
        Vector2 self = transform.position;
        shakingUntil = Time.time + shakeSeconds;

        // 震飞可搬物品（运动学刚体的坑统一由 Draggable.Scatter 处理，见红线 20）
        int flung = Draggable.Scatter(self, shakeRadius, shakeImpulse, shakeSettleSeconds);

        // 吓跑附近的村民
        int panicked = 0;
        float panicSqr = shakePanicRadius * shakePanicRadius;
        Villager[] villagers = Villager.All.ToArray();
        for (int i = 0; i < villagers.Length; i++)
        {
            Villager v = villagers[i];
            if (v == null || v.IsFrozen) continue;
            if (((Vector2)v.transform.position - self).sqrMagnitude > panicSqr) continue;
            v.Panic(shakePanicSeconds);
            panicked++;
        }

        FlickerLamps(flickerRadius, shakeSeconds + 0.3f);
        GameEvent.RaiseNoise(self, GameEvent.BreakLoudness, NoiseKind.Break);

        // 世界级故障：半径内的东西一起「贴图撕裂 + 排序错乱」
        int torn = AbilityFx.Shake(self, shakeRadius, shakeSeconds);
        Debug.Log("[Ability] 抖动：震飞 " + flung + " 个箱子，撕裂 " + torn + " 个精灵，吓跑 " + panicked + " 个村民。");
    }

    /// <summary>让附近的灯/窗户闪一下（电击、抖动都会用）。</summary>
    void FlickerLamps(float radius, float seconds)    {
        float sqr = radius * radius;
        Vector2 self = transform.position;
        NightGlow[] glows = FindObjectsOfType<NightGlow>();
        for (int i = 0; i < glows.Length; i++)
        {
            NightGlow glow = glows[i];
            if (glow == null) continue;
            if (((Vector2)glow.transform.position - self).sqrMagnitude > sqr) continue;
            glow.Flicker(seconds);
        }
    }

    // ---------------- 腐蚀 ----------------

    /// <summary>
    /// 头附近最近的可腐蚀目标：优先 <see cref="Breakable"/>（木箱 / 油桶），其次是
    /// <see cref="ElectricWire"/>（单独一根电线）。没有就返回 null。
    /// </summary>
    Breakable FindCorrodeBreakable()
    {
        Vector2 origin = transform.position;
        float sqr = corrodeRange * corrodeRange;
        Breakable best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < Breakable.All.Count; i++)
        {
            Breakable b = Breakable.All[i];
            if (b == null || b.IsBroken) continue;
            float d = ((Vector2)b.transform.position - origin).sqrMagnitude;
            if (d > sqr || d >= bestDistance) continue;
            bestDistance = d;
            best = b;
        }
        return best;
    }

    ElectricWire FindCorrodeWire()
    {
        Vector2 origin = transform.position;
        float sqr = corrodeRange * corrodeRange;
        ElectricWire best = null;
        float bestDistance = float.MaxValue;

        ElectricWire[] wires = FindObjectsOfType<ElectricWire>();
        for (int i = 0; i < wires.Length; i++)
        {
            ElectricWire w = wires[i];
            if (w == null || w.IsCut) continue;
            float d = ((Vector2)w.transform.position - origin).sqrMagnitude;
            if (d > sqr || d >= bestDistance) continue;
            bestDistance = d;
            best = w;
        }
        return best;
    }

    /// <summary>
    /// 面前最近的可腐蚀目标：<see cref="Breakable"/>（木箱 / 油桶 / 泵）和 <see cref="ElectricWire"/>（电线）
    /// **放在一起按距离挑最近的那个** —— 不这么做的话，站在电线旁边也会先去蚀远处的泵，玩家会觉得「不听话」。
    /// </summary>
    Component FindCorrodeTarget()
    {
        Breakable breakable = FindCorrodeBreakable();
        ElectricWire wire = FindCorrodeWire();
        if (breakable == null) return wire;
        if (wire == null) return breakable;

        Vector2 self = transform.position;
        float breakableDistance = ((Vector2)breakable.transform.position - self).sqrMagnitude;
        float wireDistance = ((Vector2)wire.transform.position - self).sqrMagnitude;
        return breakableDistance <= wireDistance ? (Component)breakable : wire;
    }

    /// <summary>腐蚀：蚀穿面前最近的东西 —— 木箱会烂掉，电线会断（断了电，机器就停）。</summary>
    void CastCorrode()
    {
        Vector2 self = transform.position;
        Component target = FindCorrodeTarget();
        if (target == null) return;

        GameEvent.RaiseNoise(self, GameEvent.CorrodeLoudness, NoiseKind.Break);

        // 世界级故障：目标身上「像素一块块被挖掉」+ 绿色噪点 + 中心洋红
        AbilityFx.Corrode(target.transform.position, 0.9f);

        Breakable breakable = target as Breakable;
        if (breakable != null)
        {
            // 直接算蚀穿（Break 会把控制权交给身上的水管 / 爆炸 / 电线）
            breakable.TakeHit(breakable.hp);
            Debug.Log("[Ability] 腐蚀掉了 " + breakable.name + "。");
            return;
        }

        ElectricWire wire = target as ElectricWire;
        if (wire != null)
        {
            int cut = wire.CutPower();
            Debug.Log("[Ability] 腐蚀断了电线，连带断电 " + cut + " 台机器。");
        }
    }

    // ---------------- 伪装外观 ----------------

    /// <summary>
    /// 伪装时在身上盖一层**缺图占位棋盘格** —— 小虫干脆「变成一张没加载出来的图」，
    /// 这就是这只游戏里的 bug 该有的样子（风格锚点见 Spec §4.10）。
    /// **延迟创建**：用的 <see cref="ArtShapes"/> 图元由生成器在 Awake 里登记，早于它建就没图了。
    /// </summary>
    void EnsureDisguiseVisual()
    {
        if (disguiseVisual != null) return;

        GameObject box = AbilityFx.MissingTextureBox(transform, new Vector2(0f, 0.02f), 0.62f, disguiseSeconds + 0.3f, 4);
        if (box != null)
        {
            disguiseVisual = box.transform;
            return;
        }

        // 图元还没登记好时的兜底：至少有个东西盖在身上
        Sprite sprite = ArtShapes.Get(ArtShape.Round);
        if (sprite == null) return;

        GameObject go = new GameObject("Disguise");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        go.transform.localScale = Vector3.one * 0.62f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = disguiseColor;
        sr.sortingOrder = 40;
        disguiseVisual = go.transform;
    }

    void UpdateDisguiseVisual()
    {
        if (disguiseVisual == null) return;

        bool show = IsDisguised;
        if (disguiseVisual.gameObject.activeSelf != show)
            disguiseVisual.gameObject.SetActive(show);

        if (!show)
        {
            disguiseFlicker = false;
            return;
        }

        // 快结束时开始「一格一格地闪」：提示玩家马上要露馅了
        if (!disguiseFlicker && DisguiseLeft < 1.5f)
        {
            disguiseFlicker = true;
            AbilityFx.Jitter(disguiseVisual.gameObject, 1.6f, 0.035f, true);
        }
    }

    // ---------------- 存档 ----------------

    /// <summary>已解锁的能力（位掩码，bit = (int)AbilityId）。</summary>
    public int SaveMask()
    {
        int mask = 0;
        for (int i = 0; i < unlocked.Length; i++)
            if (unlocked[i]) mask |= 1 << i;
        return mask;
    }

    /// <summary>每个能力的剩余充能（下标 = (int)AbilityId）。</summary>
    public int[] SaveCharges()
    {
        int[] copy = new int[charges.Length];
        System.Array.Copy(charges, copy, charges.Length);
        return copy;
    }

    /// <summary>读档：恢复解锁状态与充能。<paramref name="savedCharges"/> 可以是 null（老存档）。</summary>
    public void Restore(int mask, int[] savedCharges)
    {
        for (int i = 0; i < unlocked.Length; i++)
        {
            unlocked[i] = (mask & (1 << i)) != 0;
            int value = savedCharges != null && i < savedCharges.Length ? savedCharges[i] : 0;
            charges[i] = Mathf.Clamp(unlocked[i] ? value : 0, 0, Abilities.MaxCharges);
        }
        nextUseTime[0] = nextUseTime[1] = nextUseTime[2] = nextUseTime[3] = nextUseTime[4] = 0f;
    }

    // ---------------- HUD 文本 ----------------

    /// <summary>
    /// 已解锁的能力 + 剩余充能（没解锁就不显示，所以刚开局这一行是空的）。
    /// 冷却中用括号写还剩几秒。
    /// </summary>
    public string HudLine()
    {
        List<string> parts = null;
        for (int i = 0; i < Abilities.Playable.Length; i++)
        {
            AbilityId id = Abilities.Playable[i];
            if (!unlocked[(int)id]) continue;
            if (parts == null) parts = new List<string>();

            string part = "[" + Abilities.KeyName(id) + "]" + Abilities.Get(id).name + "×" + charges[(int)id];
            if (Time.time < nextUseTime[(int)id])
                part += "(" + CooldownLeft(id).ToString("F0") + "s)";
            parts.Add(part);
        }

        if (parts == null) return "";
        if (IsDisguised) return "能力：" + string.Join(" · ", parts) + " · 伪装中 " + DisguiseLeft.ToString("F0") + "s";
        return "能力：" + string.Join(" · ", parts);
    }
}
