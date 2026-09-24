# 技术与设计规范（Spec）

> **这份文档是长期权威约定**：写代码、改素材、跑验证、以及 AI/harness 每次开工前，都先读这一份。
> 计划、临时结论、一次性排错过程写在 [`2026-09-23.md`](2026-09-23.md) 这类**日志**里，不进本文档。
> 本文档与代码冲突时：**以代码为准，然后回来修正本文档**。

---

## 1. 项目速览

| 项目 | 值 |
| --- | --- |
| 游戏 | 《What a Bug？》（好大一个虫）——2D 俯视小游戏，小虫在**无限流式的世界**里吃、长大、躲村民；世界由「草原/森林/沙漠 × 荒野/农村/城市」两套体系构成（见 §4.12） |
| 引擎 | Tuanjie **1.10.3**（内核 2022.3.62t15），Built-in 渲染管线 |
| 工程性质 | **2D 工程**：`ProjectSettings/EditorSettings.asset` → `m_DefaultBehaviorMode = 1`，已装 `com.unity.feature.2d` |
| 场景扩展名 | **`.scene`**（Tuanjie），不是 Unity 的 `.unity` |
| 入口 | `Assets/Scenes/MainMenu.scene` →（开始游戏/继续游戏）→ `Assets/Scenes/BugScene.scene` |
| 脚本语言 | C#，**无命名空间**，注释与 UI 文案用中文 |
| 编辑器安装根 | `D:\TuanJie\Hub\Editor\2022.3.62t15`（可用 `tuanjie.exe install-path --get` 复核） |

**设计基调（用户明确要求，别自作主张改）**

- 2D 而非 3D；美术**程序化占位**（图元 Sprite + LineRenderer 曲线）即可，不追求精细美术。
- 操作是**点击式**：左键点地图移动、空格进食、`F` 统一交互、`Shift` 冲刺、`Esc` 返回菜单。**不要加 WASD 持续移动**。
- 小虫尺寸刻意很小（原尺寸 0.4×），相机视野刻意很大（`orthographicSize 8.4`）——「一只小虫在很大的村子里」是核心观感。

---

## 2. 目录与文件约定

| 路径 | 内容 | 能不能动 |
| --- | --- | --- |
| `Assets/Scripts/` | 全部游戏脚本（**扁平一层**，不建子目录；只有 `Editor/` 一个子目录） | 可以 |
| `Assets/Scripts/Editor/` | 编辑器脚本（`AssetPostprocessor` 等），只进编辑器程序集 | 可以 |
| `Assets/Sprites/` | 程序化美术用的**原始图元**（`S_*` / `T_*`），游戏默认外观 | 可以（改外观先看第 5 节） |
| `Assets/Resources/ArtOverride/` | 人工提供的图片（按素材名替换） | 可以（格式见根 `README.md`） |
| `Assets/Resources/AudioOverride/` | 人工提供的音频（按 key 播放） | 可以（格式见根 `README.md`） |
| `Assets/Codely/Fonts/` | 中文字体资产 `NotoSansSC-Regular SDF.asset`，已进 TMP 全局 fallback | 谨慎 |
| `Assets/Scenes/` | `MainMenu.scene` / `BugScene.scene`（`SampleScene.scene` 未使用） | **尽量用脚本改，别手改 YAML**（见第 10 节） |
| `Docs/` | 文档（本目录）：日志与规范。**放在 `Assets/` 外**，Unity 不导入 | 可以 |
| `README.md`（根） | **资源交付说明**（图片/音频格式），对外给美术/音效 | 可以 |
| `Library/`、`Temp/`、`obj/` | 编辑器生成物 | **不要动、不要提交** |
| `*.meta` | Unity 导入元数据 | **绝不手写/手改**（让编辑器生成） |

---

## 3. 代码规范

1. **风格**：沿用现有文件的写法 —— `public` 字段 + `[Header("…")]` / `[Tooltip("…")]` 分组，
   `SerializeField` 用得少（现有代码基本用 public + Tooltip）。
2. **注释**：类头用中文 `<summary>` 说明「这个脚本干什么、关键规则、坑」；方法/字段按需用 `<summary>`。
   **不要**用注释复述代码；注释写「为什么」。
3. **命名**：类 `PascalCase`（`VillageGenerator`），字段/方法 `camelCase`（`halfExtent`、`TryEat()`），
   UI 文案与 `Debug.Log` 前缀用中文 + 方括号标签，如 `[Bug] 长大到 2 级`、`[ArtOverride] …`、`[AudioOverride] …`。
4. **静态注册表**：需要「全局查找同类对象」时用静态表 + `OnEnable`/`OnDestroy` 登记，
   例如 `Edible.All`、`Burrow` 的静态查找、`Villager.All`。新加同类系统照这个模式写（别每帧 `FindObjectsOfType`）。
5. **禁止每帧分配**：`Update` 里避免 `new List<>` / LINQ / 字符串拼接（HUD 除外，它已经按需刷新）。
6. **`Mesh`/`LineRenderer`**：小虫尾巴是 `WormBody` 用 `LineRenderer` 画的等宽曲线，宽度/节距/摆幅都在它上面。
7. **数值集中**：可调数值放 `[Header]` 字段并给中文 Tooltip；**不要**散落魔法数字。
8. 新增脚本后**不需要**手工创建 `.meta`；编辑器打开时会自动生成（编辑器没开时暂时没有 `.meta` 是正常的）。

---

## 4. 架构地图

### 4.1 关键脚本职责

| 领域 | 脚本 | 说明 |
| --- | --- | --- |
| 玩家 | `BugController` | 点击移动、朝向、冲刺、`F` 键统一分发（出洞 → 放下 → 钻洞/传送 → 拾取）、`useWorldLimit` 默认关闭 |
| | `WormBody` | `LineRenderer` 尾巴：链式跟随、速度驱动摆动、**每帧写头部缩放**（见红线 6）、`SetVisible` 供钻洞隐藏 |
| | `BugEat` | 空格进食，头部为中心整圈判定，按 `Edible.requiredLevel` 过滤，吃到村民通知目击者 |
| | `BugVitality` / `BugGrowth` / `HiddenValue` | 体力（饿死回菜单）/ 成长 3 级 / 不显示给玩家的数值 |
| | `DragController` + `Draggable` | `F` 拾取放下，物品停在头前方 |
| 世界 | `VillageWorld` | **区块流式调度**：加载/回收、冻结远处村民、地道配对；村民可见性 / 冻结距离按当前区块的聚落取值 |
| | `WorldBiome` | **世界地貌与聚落体系**（§4.12）：草原/森林/沙漠 × 荒野/农村/城市、低频噪声分布、密度、生成参数、地面与路面 key —— **生成参数的唯一权威** |
| | `VillageGenerator` | `BuildChunk(coord, seed)` 确定性生成一个区块的全部内容；食物 / 可交互物品来自 `FoodCatalog` / `ItemCatalog` / `ContentPack`（见 4.5 / 4.12） |
| | `VillageMap` | 设施锚点表（农田/摊位/长椅/房屋/巡逻点/按种类登记的 `anchors`…），支持按距离剪枝 |
| | `InfiniteGround` / `YSort` / `FollowCamera` | 无限草地对齐、俯视深度排序、相机跟随 |
| | `Spinner` / `BellTower` | 风车叶片这类「会转的小景」/ 钟楼整点敲钟（发声 + 广播噪音） |
| | `ContentPack` | **按「聚落 × 自然」分家的内容**（蘑菇只在森林、垃圾桶只在城市…）：写定义 + `Register` 一行，门控靠 `SpawnRule.InNatures/InSettlements` |
| 村民 | `Villager` | 状态机 `Idle/Commute/Work/Socialize/Play/Chase/Flee/Alert/Investigate/Search/Recover` + 作息 + 扇形视野 + **听觉（噪音）** + `fearsBug` + 冻结 |
| | `VillagerJobs` | 职业表：中文名、衣服颜色、工作描述、看到小虫的反应、**好奇倍率 `Curiosity`（决定听得多远 / 会不会离开岗位去看）** |
| | `VillageClock` / `NightGlow` | 游戏内时间（240 秒一天、5 时段）/ 夜里亮起来的东西 |
| 事件 | `GameEvent` | **全局事件总线**（噪音 `Noise` / 吃掉东西 `Eaten` / 看到小虫 `Spotted` / 村民状态变化 `StateChanged` / 用能力 `AbilityUsed`），见 §4.6 |
| 能力 | `Ability` / `AbilitySet` / `Decoy` | 能力表与键位 / 充能与冷却 + 五个能力的实际效果 / 分裂出来的假小虫，见 §4.7 |
| 连锁 | `Breakable` / `Power` / `Water` / `Hazard` | 可破坏物+碎片 / 电线与通电机器 / 漏水与水洼 / 油桶与警报器，见 §4.8 |
| 度量 | `ChaosMeter` / `Alertness` / `RunStats` / `SettlementPanel` | 混乱值 / 警觉值 / 本局统计 / 结算面板，见 §4.9（**任务模块 2026-09-25 已删**） |
| 故障特效 | `AbilityFx` / `GlitchOverlay` | 世界级故障（缺图占位 / 撕裂 / 残影 / 挖像素）与屏幕级故障（闪帧 / 撕裂条 / 扫描线 / 假报错），见 §4.10 |
| 玩法 | `Burrow` / `EncounterWindow` / `EntityInfo` / `Highlighter` / `ProximityHighlight` | 地洞地道 / 右下悬浮窗 / 介绍文本 / 高亮 |
| 存档 | `SaveSystem` / `AutoSave` | JSON（**当前 v5**，先 `.tmp` 再替换）/ 每 5 秒自动存 + 失焦退出补存 + 读档 |
| 界面 | `SimpleHUD` / `MainMenu` / `ReturnToMenu` / `GameInput` / `GameSettings` | HUD（uGUI+TMP）：左上角「操作说明 + 状态行」、**屏幕正下方三条条（体力 / 混乱 / 警觉）**、右下角悬浮窗；面板高度 = 文字高度 + 边距、位置在运行时算（`SimpleHUD.LayoutPanels`），所以改文案不会顶出面板也不会互相压住 / 开始界面 / Esc / 按键统一表 / 全局音量 |
| 素材 | `ArtOverride` + `ArtSlot` + `MenuArt` + `Applier` + `Editor/ArtOverridePostprocessor` | 图片按 key「一物一图」替换（key 表见 `ArtKeys` / `Slots`） |
| | `AudioOverride` + `AudioOverridePlayer` + `BugFootsteps` + `Editor/AudioOverridePostprocessor` | 音频按 key 播放 |

### 4.2 世界生成模型

- 世界**无边界**：区块边长 `chunkSize 32`，以玩家所在区块为中心加载 `viewRadius 1`（9 块），
  超出 `keepRadius 2` 才回收；地面是跟随玩家的 `InfiniteGround`。
- 区块内容 = **坐标 + 世界种子**的确定性函数（`BuildChunk`），所以走远再回头、读档后回来，世界一模一样。
- 每个区块自己属于一种**自然体系**（草原 / 森林 / 沙漠）与一种**聚落体系**（荒野 / 农村 / 城市），
  两者都由「坐标 + 种子」的低频噪声决定 —— 见 §4.12。**没有「开局选地图」了**。
- 「有没有路 / 有没有广场 / 有没有井」都是按几率来的，不再是每个区块都有十字路与中央水井。
- 村民离玩家超过 `WorldBiome.FreezeRadius(聚落)`（城市 22、其余 26）被冻结（停状态机 + 停物理 + 关渲染 + 移出 `Villager.All`），
  走回来自动解冻；`VillageWorld` 每 0.5s 检查可见框内村民数，不足 `WorldBiome.MinVillagersInView(聚落)` 就近补人
  （**荒野不补人**：人少才像荒野）。
- **`chunkSize` 在 `VillageWorld` 与 `VillageGenerator` 上都存在，必须一致**（`VillageWorld.Awake` 会自动对齐到 generator 的值）。

### 4.3 渲染与排序（2D 俯视的核心）

- 新建了 **`Ground` 排序层**，整体画在 `Default` 层之下，内部用固定次序：
  草地 −900 → 道路 −885 → 农田 −870/−860/−850 → 牧场 −845 → 花坛 −840/−835/−830。
- 「一整块铺在地上」的东西（农田/花坛/牧场草地/地洞洞口）**属于 Ground 层，按固定次序排**；
  只有 `YSort` 对象（小虫/村民/木箱）用**世界 Y** 排序。
- 角色、房屋、树、羊都在 Default 层，所以永远踩在这些地面贴图之上。

### 4.4 数据与存档

- 存档文件：`Application.persistentDataPath/whatabug_save.json`（`%USERPROFILE%\AppData\LocalLow\<公司>\<产品>\`）。
- 写入时**先写 `.tmp` 再替换**，防止正好被杀掉留下坏档。
- `GameSave` 字段：世界种子、**存档时所在地貌与聚落**（v6 起，只给界面显示用）、小虫位置、已吃数量、
  体力、成长等级、游戏内小时、游玩秒数、存档时间、`version`；另有能力 / 混乱 / 警觉 / 统计等（v3~v4）。
  **v6 起不再有 `mapKind`**（开局不选地图了）—— 老存档里多出来的字段会被 `JsonUtility` 直接忽略。
- **存档只存「玩家自身进度」**；村民、地洞属于区块，读档后按种子重建；`Villager.fearsBug` 这类个人状态不存档。
- 存档结构变更必须：**提高 `version`** 并在读取时按 `version` 分支处理（红线 8）。

### 4.5 新增内容：食物与可交互物品（内容目录）

**加一种食物 / 可交互物品 = 写一个定义 + Register 一行，不要改生成器。**

| 文件 | 作用 |
| --- | --- |
| `Assets/Scripts/SpawnKit.cs` | `SpawnRule`（怎么刷：权重、每区块固定数量、几率、村庄/野外限定、占位）；`IContentDefinition` 接口（生成器只认它）；`SpawnKit.YOrder`（俯视排序公式的唯一定义） |
| `Assets/Scripts/ArtShapes.cs` | `ArtShape` 形状枚举 + 精灵登记表（由生成器 Awake 登记）+ `AddSprite`（拼组合外观用） |
| `Assets/Scripts/FoodCatalog.cs` | `FoodDefinition` / `FoodCatalog`（`Register` / `Find` / `Unregister` / `SetChance`）+ 内置三种食物（`berry` / `leaf` / `special_food`） |
| `Assets/Scripts/ItemCatalog.cs` | `ItemDefinition` / `ItemCatalog` + 内置木箱（`crate`） |

- 生成模型（`VillageGenerator.BuildFoods` / `BuildItems`，两者共用泛型 `BuildSlots` / `BuildFixed` / `Place`）：
  1. **位**：每区块抽 `foodSlotsMin~Max` 个食物位、`itemSlotsMin~Max` 个物品位，每个位按 `SpawnRule.weight` 抽种类；
  2. **固定生成**：`SpawnRule.minPerChunk/maxPerChunk` > 0 的定义按 `chance` 额外生成（神奇果实就是 `Fixed(1,1,0.45)`）。
- 物体结构统一为「**逻辑在根上、外观在 Visual 子物体上**」：根上挂 `Edible` / `HiddenValue` / `EntityInfo` /
  `Highlighter` / `Draggable` / `Rigidbody2D` / `Collider2D` / `YSort`，外观精灵在 `Visual` 上（缩放 = 物体大小）。
  这样组合外观（光晕 / 星星）用世界单位写就行，`decorate` 回调也不会踩到父级缩放。
- 吃的逻辑（`BugEat`）、拾取（`DragController`）、高亮（`ProximityHighlight`）、悬浮窗（`EntityInfo`）
  **全是按组件找对象的**，所以注册完就自动生效，不需要改这些脚本。
- 地貌 / 聚落的密度调节：`WorldBiome.Apply` 会按区块覆盖 `foodSlots*` / `itemSlots*`（见 §4.12），
  稀有物品的几率用 `FoodCatalog.SetChance(id, x)` / `ItemCatalog.SetChance(id, x)`。
- 加新内容时**同时要做**：给它一个美术 key（`ArtKeys` + `ArtOverride.Slots` + 根 `README.md` 的表），
  否则玩家没法单独给它换图（见红线 15）。

---

## 4.6 事件总线与噪音（`GameEvent`）

「世界里发生了什么」统一从 `Assets/Scripts/GameEvent.cs` 广播，**谁关心谁订阅**，
不要互相 `FindObjectOfType`、也不要把反应逻辑塞进触发方。加新系统（能力 / 连锁 / 任务 / 混乱值）时优先订阅这里。

| 事件 | 载荷 | 现在谁在用 |
| --- | --- | --- |
| `Noise` | `NoiseEvent{ position, loudness, kind }` | 村民听觉（`Villager.OnNoise`） |
| `Eaten` | `EatenEvent{ food, position, wasVillager }` | 暂无（留给混乱值 / 任务） |
| `Spotted` | `Villager` | 暂无（留给警觉值 / 「被发现次数」） |
| `StateChanged` | `Villager` | 暂无（HUD / 调试 / 连锁条件） |

**噪音从哪来**：走路（`BugFootsteps`，0.35）、冲刺（`BugController`，0.6，冲刺期间每 0.25s 一次）、
啃食（`BugEat`，0.5；吃村民用 1.2）、拿起物品（`DragController`，0.5）、放下物品（`DragController`，1.2）。
默认响度集中在 `GameEvent.StepLoudness` / `DropLoudness` / `LoudnessOf(kind)`，**不要在调用点写魔法数字**。

**听到之后会发生什么**（`Villager`）：
- 听觉半径 = `hearingBase`(6) × 响度 × `VillagerJobs.Curiosity(job)`；**全向**，不受朝向限制（红线 18）；
  距离 ≤ 半径的一半 → 知道准确位置，否则只知道大概位置（加随机抖动，免得像雷达一样精准）。
- 同一个人 `hearingCooldown`（1s）内只响应一次；正在追 / 躲的人、被冻结的人、怕了小虫的人不响应。
- 状态链：`Alert`（站住转身起疑 0.6~1.2s）→ 好奇倍率 ≥ `leavePostThreshold`（1.0）才 `Investigate`
  （走过去张望）→ 没发现 → `Recover`（回过神）→ 交回 `Decide()` 回日常作息；
  追人追丢了走 `Chase → Search`（在最后看见小虫的地方附近翻找 `searchPoints` 处）→ `Recover`。
- 头顶「!」提示：`Villager.Awake` 里用 `ArtShapes.Get(ArtShape.Rect)` 拼的两块方块，
  **故意不占美术 key**（它是状态提示，不是场景物件），只在这三个状态（含「正走过去」的 Commute）显示。
- 玩家反馈：`SimpleHUD.NoticeHint()` 在状态行加一句「附近有人听到了动静 / 正朝这边过来」（`noticeRadius` 12 之内才算）。

**关键行为约定（2026-09-24 修）**：视野检查现在对**所有状态**生效（正在追 / 躲的除外），
也就是「正在赶路的村民也会看见小虫」。以前只在 `Decide()`（到点重新做决定，最长要等十几秒）里检查，
村民会径直从小虫身上走过去当作没看见。

**听到动静要当场打断闲事（2026-09-24 修）**：`OnNoise` 记下位置后，如果这个人正在
`Idle` / `Work` / `Socialize` / `Play` / `Commute`，会立刻调一次 `Decide()` 去过问。
不打断的话，动静要等它手上这件事做完（干活最长十几秒）才处理 —— 「扔个箱子把人引开」这类玩法会失效。

---

## 4.7 能力系统（吃物品 → 获得能力）

**获得方式统一是「吃掉东西」**：带 <see cref="AbilityPickup"/> 的物体被吃掉时，
`BugEat` 调 `AbilitySet.GrantFromPickup(...)`。第一次吃**永久解锁**，之后每次吃补充满能。

| | 值 |
| --- | --- |
| 四种能力 | 电击（旧电池 `battery`）/ 伪装（破布团 `trash`）/ 分裂（孢子囊 `spore`）/ 抖动（锈齿轮 `gear`） |
| 键位 | **数字键 1~4**，集中在 `GameInput.AbilityKeys` + `Abilities.Playable`；**不要各自写按键** |
| 充能 | 上限 `Abilities.MaxCharges`(3)，每次使用消耗 1；用完得再吃同类东西 |
| 未实现 | 腐蚀 `AbilityId.Corrode`（`playable = false`）：它要有「可破坏物体」才有作用对象，留给那一步 |
| 内容定义 | `FoodDefinition.hasAbility / ability / abilityCharges`（**枚举 0 值是真能力，不能用它表示「没有」**） |
| 存档 | `GameSave.version = 3`：`abilityMask`（位掩码）+ `abilityCharges`（长度 5 的数组） |

**效果都复用已有系统，不新造轮子**：
- 电击 → `Villager.Stun()`（新状态 `Stunned`：动不了、看不见、听不见）+ `NightGlow.Flicker()` + `NoiseKind.Shock`(2.5)；
- 伪装 → `BugController.IsDisguised`，`Villager.CanSeeBug()` 在**贴脸判定之前**把它挡掉
  （顺序错了会失效：贴脸分支先 return true 的话，伪装就只在尾巴上生效）；
- 分裂 → 生成 `Decoy`：乱窜 + 每 0.7s 发一次 0.6 响度的噪音，让村民去调查它（**不改视野逻辑**）；
- 抖动 → 把附近的 `Draggable` 震飞 + 附近村民 `Panic()`（走 `Flee`）+ `NoiseKind.Break`(2.0) + 灯闪。

**抖动震飞的关键坑**：可搬物品（木箱）平时是 **`RigidbodyType2D.Kinematic`**（只能被搬，不会自己动），
**运动学刚体完全不吃 `AddForce` / `velocity`**。所以要先临时切成 `Dynamic` 并 `WakeUp()`（红线 1），
再用 `AddForce(..., Impulse)` 推出去，最后用 `SettleAfter` 协程把速度归零、切回 `Kinematic`
—— 不收回的话村子会被越推越乱，而且会改变木箱原本的手感（实测收回后仍可正常 F 搬运）。

**能力的静态引用要惰性拿**：`AbilitySet` 是场景加载完成后（`RuntimeInitializeOnLoadMethod`）才挂到小虫身上的，
任何在 `Awake` 里 `FindObjectOfType<AbilitySet>()` 的组件（`AutoSave`、`SimpleHUD`）都**必须惰性重试**，
否则永远拿到 null —— 2026-09-24 就是这么踩到的（存档写不出能力，`abilityCharges` 变成空数组）。

---

## 4.8 连锁与可互动物体（`Breakable` / `Power` / `Water` / `Hazard`）

设计文档 §9 要的是「**不下发唯一答案，靠场景规则自己制造故事**」。
落地就是四类组件 + 一个约定：**谁影响谁按半径就近绑定，不做 id / 连线编辑器**。

| 组件 | 作用 | 触发方式 |
| --- | --- | --- |
| `Breakable` | 可破坏物：碎掉时掉碎片 + 响度 2.0 + 广播 `Broken`，然后**把控制权交给身上的其它组件** | 被吃 / 被腐蚀 / 被爆炸 / 被撞（可选）/ `TakeHit()` |
| `ElectricWire` | 电线：剪断 → **`linkRadius`(6) 内的 `PoweredProp` 全部断电** | 被吃（`Edible.onConsumed`）/ 被腐蚀 / 被打碎 |
| `PoweredProp` | 通电才工作的机器：断电 → 变灰缩一下 + 广播 `PowerChanged` + 停机噪音 0.9 | 被附近的电线剪断 |
| `WaterSource` | 漏水：按间隔生成 `Puddle` | `leakOnStart`（破水缸）/ **断电才漏**（泵，`leakWhenUnpowered`） |
| `Puddle` | 地面水洼：村民踩上去**滑倒**（`slipChance`） | 漏水积出来 |
| `Explosive` | 油桶：爆炸 → 震飞半径内的东西 + 吓跑村民 + **波及警报器** + 响度 3.0 | 被电击 / 被打碎 / 被腐蚀 |
| `Alarm` | 警报器：半径内村民全部 `Panic(10s)` 撤离 + 灯乱闪 | 被电击 / 被爆炸波及 / 被打碎 |

**两条样板链**（Spec 里记下来，改这些组件时按它回归）：
```
链 A  吃掉电线 → 附近抽水泵断电 → 泵开始漏水 → 地上积水 → 村民走过滑倒（响 1.8）
      → 附近村民来查看 → 滑倒的人撞翻旁边的箱子
链 B  电击油桶 → 爆炸（震飞东西 / 吓跑人 / 响 3.0）→ 波及警报器 → 警报响 → 半径内村民全部撤离
```
**腐蚀能力**（步骤② 留的位）在这套里是「第二条路」：蚀穿木箱（`Breakable`）或剪断电线（`ElectricWire`）都能走通链 A。
目标选取规则：**面前最近的那个**，不按类型分优先级（不然站在电线旁会去蚀远处的泵，玩家会觉得不听使唤）。

**表现层约定**：碎片（`Debris`，在 `Breakable.cs` 里）不挂碰撞体 —— 免得一地碎屑把村民卡住；
水洼放在 `Ground` 排序层固定 `-820`（永远在角色下面）；`Power`/`Hazard` 都不做伤害，只做**惊吓与物理**（村民没有血量系统）。

---

## 4.9 度量层（混乱 / 警觉 / 统计 / 结算）

这一步是**事件总线的最大回报**：所有数字都只是**订阅** `GameEvent`，
新增玩法只要发事件就自动被统计，**不需要改任何度量脚本**。改动这一层前先读本节。

| 系统 | 一句话 | 关键数 |
| --- | --- | --- |
| `ChaosMeter` | 混乱值 0~100 → 6 级（§8），**等级越高衰减越慢** | 吃 +1 / 碎 +6 / 断电 +5 / 漏水 +3 / 滑倒 +7 / 能力 +2 / 警报 +15 / 吃村民 +12；阈值 0/12/28/48/70/88；衰减 0.6→0.2 每秒 |
| `Alertness` | 警觉值 0~100（§6），**没人看得见你时才下降** | 被看到 +18、噪音 ×2；-1.2/s；≥40 村民视野 ×1.15 / 听觉 ×1.35；≥70 每 6 秒就近派 2 个人 `Search` |
| `RunStats` | 本局统计（§17） | 破坏 / 吞噬 / 被发现 / 滑倒 / 能力 / 警报 + **最长连锁**（3 秒滑动窗口） |
| `SettlementPanel` | 结算面板：**H 键开关** + **饿死时自动弹**（`BugVitality.settlementSeconds` 秒后回菜单） | Esc 语义不变 |

> **任务模块（`TaskCatalog` / `TaskSystem`）已于 2026-09-25 按用户要求删掉**：
> 存档里原来的 `taskIndex` / `taskProgress` 也不再读写（存档升到 v5；老档里多出来的字段会被
> `JsonUtility` 直接忽略，不会报错）。`GameEvent.TaskCompleted` 事件保留着，
> 以后要做「目标 / 成就」可以直接用。
> **混乱 / 警觉怎么给玩家看**：屏幕正下方三条条（`SimpleHUD` 运行时建的 `ChaosBar` / `AlertBar`
> + 场景里的体力条），文本不再是主要载体 —— 见 §4.10 末尾的 HUD 约定。

**上下游接线**（改这些地方要一起看）：
- `Villager.EffectiveViewRadius` / `HearingRadius` 会乘上 `Alertness` 与 `ChaosMeter` 的加成 —— **只能在这两处读**，
  别在别处再乘一遍；
- 混乱升级的阶段事件在 `ChaosMeter.OnLevelUp`：L1 灯闪 / L2 围观噪音 / L3 视野听觉 / L4 `minVillagersInView +1` / L5 全场警报；
- **一次事故只算一次混乱**：电线剪断时统一广播一次 `PowerChanged(false)`，
  它身上的机器走 `SetPower(false, announce: false)` 静默断电（否则一次停电算两次，见 `Power.cs`）；
- 为了让「啃断一根孤零零的电线」也有反馈，`ElectricWire.CutPower()` **不管射程内有没有机器都会广播断电事件**。

---

## 4.10 故障特效：小虫把游戏搞出 bug 了（`AbilityFx` / `GlitchOverlay`）

**风格锚点（2026-09-25 用户要求，写能力表现时照这个来）**：
五种能力的视觉不是「魔法光效」，而是**游戏本身出错的样子** —— 因为小虫就是这只游戏里的 bug。
统一的「故障语言」：

| 故障语言 | 具体做法 | 用在哪 |
| --- | --- | --- |
| 缺图占位 | 灰白 / 深紫相间的**棋盘格**（`AbilityFx.MissingTextureBox`） | 伪装（小虫自己变成一张没加载出来的图） |
| 错误材质 | 纯洋红 `AbilityFx.ErrorMagenta` 的闪光 | 电击 / 抖动 / 腐蚀 / **解锁能力**的瞬间 |
| 贴图撕裂 | 逐帧把精灵本地坐标推来推去（`GlitchJitter`，可顺带打乱 sortingOrder） | 抖动（半径内所有东西）、被电麻的村民 |
| 图块复制 | 半透明残影按固定偏移重复（`AbilityFx.AddGhosts`） | 分裂出来的假小虫 |
| 像素被挖掉 | 随机小方格「挖洞」（`AbilityFx.PixelCarve`） | 腐蚀的目标 |
| 局部黑屏 | 几块黑色横条不规则盖住画面一部分（`GlitchOverlay.Blackout`） | **解锁新能力**（0.55 秒） |
| 剧烈抖动 | HUD / 整屏 OVERLAY 抖到 `violentShakePixels`（默认 9px，普通撕裂只抖 3px） | **解锁新能力**、大事故 |
| 屏幕级故障 | 全屏闪帧 / 横向撕裂条 / 滚动扫描线（`GlitchOverlay`） | 用能力 / 警报 / 混乱 ≥4 级 |
| **左下角红色报错** | `GlitchOverlay.LogError(text)`：屏幕**左下角**一个控制台，最多 5 行、新行把旧行往上顶（= 滚动），每行从左边滑进来、2.2 秒后淡出 | **所有故障场景都走这里**，不要自己到处飘字 |

**解锁新能力的那一下（用户点名要的仪式感）**：`AbilitySet.Grant` 第一次解锁时调
`GlitchOverlay.AbilityUnlocked(能力名)` = **局部黑屏 + 撕裂拉满 + 剧烈抖动 + 洋红闪 + 两行左下角报错**
（`AbilityUnlockedException: 电击` / `MemoryPatch applied: 电击`），并留了音效接口
`AudioKeys.AbilityUnlock`（`ability_unlock`；没放文件就是静音）。
世界一侧还会在小虫头上 `AbilityFx.Jitter` 一下 + 冒一团洋红。

**约定（红线 24）**：
- 这些特效**一律程序化、不占美术 key**（同头顶「!」/ 碎片 / 水洼 / 分身）：它们是运行时表现，不是场景物件；
- 每个特效都**自带寿命**（`GlitchLifetime` / `GlitchJitter` 到点自己收工并把位置、排序还回去），
  **绝不会糊在屏幕上**（验证方式：等 1.5 秒后 `GlitchOverlay.IsGlitching == false`）；
- `GlitchOverlay` 挂在 **HUD 画布**下（不是 `GameDirector`），并 `SetAsLastSibling()` 盖在其它 HUD 之上；
- 屏幕级故障由 `GameEvent` 驱动（`AbilityUsed` / `AlarmRaised` / `ChaosLevelChanged`），
  **加新能力时不用改 `GlitchOverlay`**；`enabledFx = false` 可以整体关掉。

**HUD 与底部的三条条（同一批做的）**：
- 屏幕正下方从下往上 = **体力 → 混乱 → 警觉**，都在 `SimpleHUD` 里；
- 体力条：场景里那一条 + 运行时加的 **3 条分段刻度 + 数字**，低体力（≤30%）时填充会**闪**；
- 混乱 / 警觉条：`SimpleHUD.MakeMeter` 运行时建（底衬颜色照抄体力条），条上有小字（混乱等级名 / 警觉百分比
  以及「村民更警觉了」「他们在找你」这类状态字），**混乱 5 级时条是洋红色**（呼应故障主题）；
- 三条条的长度都跟着成长等级走（和体力条一样），位置由 `LayoutPanels` 统一排（红线 23）。

---

## 4.11 密度与「有图优先」

**全局密度系数**：`WorldBiome.Density`（当前 **1.8**，2026-09-25 从 `MapProfiles` 搬家过来）。
`WorldBiome.ApplyDensity` 在套完聚落 / 自然参数之后统一乘它，作用对象是
**房子 / 树 / 灌木 / 仙人掌 / 枯树 / 食物位 / 物品位 / 各类设施几率**；
**村民数量（`villagerMin/Max`）故意不乘** —— 人口直接影响性能与手感，要单独调。
另外村庄 / 城市会保证 `itemSlotsMin >= 1`（每区块至少有一个能搬能玩的东西）。

**「有图优先」规则**：内容目录里的每一条定义**都必须有 `artKey`**（不然美术没法单独换它，红线 15）；
并且当这个 key **真的有图片**时，**程序化的装饰件要让位** —— 用 `if (ArtOverride.Has(key)) return;`
跳过光晕 / 星星 / 内部高光这类「凑数」的子精灵（神奇果实、孢子囊、树冠都是这么写的）。
**判断依据是 `ArtOverride.Has`，不是「有没有 decoreate」**。

**「就近生成」规则（2026-09-25 加）**：`SpawnRule.nearTrees` / `SpawnRule.nearAnchor` 让「什么长在哪」有生活感：

| 定义 | 规则 | 效果 |
| --- | --- | --- |
| `berry`（野果子） | `nearTrees = true`，`nearRadius 2.2` | **只掉在树底下**（实测 41/43 在树 3 米内） |
| `leaf`（嫩叶） | `nearTrees = true`，`nearRadius 2.4` | 同上（实测 19/19） |
| `battery`（旧电池） | `nearAnchor = "power"`，`nearRadius 3.6` | **聚在发电站旁边**（实测 14/14 在站 4.5 米内） |

- 参照物从 `VillageMap.trees` 与 `VillageMap.anchors[kind]` 里取（`AddAnchor("power", 位置)` 由
  `VillageGenerator.BuildPowerPlant` 登记，`AddAnchor("cactus", 位置)` 由 `CreateCactus` 登记，
  `PruneFarFrom` 会一起剪）；
- **找不到参照物就自动退回普通随机落点**（这片没树 / 没发电站也照样刷，不会断供）——
  写新内容时就近规则只是「偏好」，别让它变成「唯一出路」；
- `BuildChunk` 里的顺序是 **先 `BuildTrees()` 再 `BuildFoods()`**（2026-09-25 调过）：
  这样「果子长在树旁」在同一个区块里就能成立（原来是反过来的，第一个区块常常没树可依附）。

---

## 4.12 世界地貌与聚落体系（`WorldBiome` / `ContentPack`）

**2026-09-25 起，「开局选三张地图」被彻底删掉**（`MapProfiles.cs` 已删除）。世界是一整片连续的无限地图，
每个区块自己属于两条正交的体系，两者都由「区块坐标 + 世界种子」的低频值噪声决定（**纯函数**）：

| 体系 | 取值 | 管什么 |
| --- | --- | --- |
| **自然体系** `NatureKind` | 草原 `Grassland` / 森林 `Forest` / 沙漠 `Desert` | 地面、树与植被、野外的食物与物品、农事难不难做 |
| **聚落体系** `SettlementKind` | 荒野 `Wilderness` / 农村 `Village` / 城市 `City` | 房子多少与房型、设施、广场、路宽与路型、人口 |

### 为什么是「成片」而不是「一个区块一个样」

噪声是**低频值噪声**（`WorldBiome.Noise`：以 `NatureRegion`(3) / `SettlementRegion`(4) 个区块为一个格子，
四角取随机值 + `Mathf.SmoothStep` 插值），所以**相邻区块的取值是连续的** ——
走到一片就是一片森林、一片沙漠，而不是满地噪点。实测（种子 123456，61×61 区块）：

- 占比：草原 38% / 森林 46% / 沙漠 16%；荒野 44% / 农村 47% / 城市 9%；
- **相邻区块同类率**：自然 0.71、聚落 0.80；而隔得很远的两个区块只有 0.40 / 0.41（≈ 随机相撞）——
  差距就是「成片」的证据。

### 出生地是固定的

`WorldBiome.IsStartArea`：`|x| ≤ 1 且 |y| ≤ 1` 的 **3×3 个区块固定是「草原 + 农村」**。
理由：开局必须有村子（有井 / 广场 / 村民 / 设施）和一定量的食物，不能让玩家一睁眼掉在沙漠城市里。
`StartRadius` 是常量，想改成「开局也全随机」就把它设成 0（但要有心理准备：可能开局没吃的）。

### 生成参数怎么套

`WorldBiome.Apply(settlement, nature, generator)` 在 `BuildChunk` 开头调一次，**是整体覆盖而不是累乘**：

```
ApplySettlement(...)   // 聚落决定基数（绝对值）：房子 / 人口 / 设施 / 广场 / 路宽 / 地洞
ApplyNature(...)       // 自然体系在基数上乘系数：树 ×1.7（森林）/ ×0.12（沙漠）、仙人掌、灌木、食物位、农事几率
ApplyDensity(...)      // 最后统一乘 Density(1.8)；村民数量不乘
```

所以 `VillageGenerator` 上那些 `houseMin / treeMin / foodSlots…` 字段**只是 Inspector 的默认值**，
运行时会被整体覆盖 —— **不要在那里调数值，改 `WorldBiome`**。

### 每个聚落 / 自然各有什么（实测数据，种子 20260925，每格 4 个区块）

| 聚落 × 自然 | 树 | 灌木 | 仙人掌 | 枯树 | 房子 | 村民 |
| --- | --- | --- | --- | --- | --- | --- |
| 草原·农村 | 18~29（每区块） | 6 | 0 | 1 | 5~9 | 2~4 |
| 草原·荒野 | 18~29 | 7 | 0 | 1 | 0~2 | 0~1 |
| 森林·农村 | 31~49 | 9 | 0 | 3 | 5~9 | 2~4 |
| 森林·荒野 | 31~49 | 9 | 0 | 3 | 0~2 | 0~1 |
| 沙漠·农村 | 2~4 | 0 | 8 | 6 | 5~9 | 2~4 |
| 沙漠·荒野 | 2~4 | 0 | 12 | 6 | 0~2 | 0~1 |
| 草原·城市 | 5~13 | 1 | 0 | 1 | 11~16（实际约 8） | 6~9 |
| 森林·城市 | 9~22 | 5 | 0 | 3 | 11~16（实际约 8） | 6~9 |
| 沙漠·城市 | 0~2 | 0 | 8 | 6 | 11~16（实际约 8） | 6~9 |

> **城市的房子数为什么达不到 11~16**：宽路面 + 广场 + 设施把 32×32 的区块切碎了，
> 大公寓（需要约 5.8×6.8 的空地）塞不进碎地块。`VillageGenerator.BuildHouses` 的对策是
> **「连着失败 60 次就把房型降小一号」**（公寓 → 排屋 → 两层小楼 → 农舍），实测每区块约 8 栋。
> 想更密就调小 `BuildHouses` 里 `IsFree(area, 1.0f)` 的余量。

### 路型与广场不再是必然

`VillageGenerator.PickRoadShape` 按聚落加权抽 5 种路型 + `None`：
十字 `Cross` / 直路 `StraightH·V` / T 型 `TJunction` / L 型 `LJunction` / 没有路。
**按「2×2 个区块一片」的粒度抽签**（`roadRunLength`），同一片常常是同一种路型，路才连得起来；
另外再按 `roadChance` 掷一次（荒野只有 45% 的区块有路）。实测 36 个区块里 7 个完全没有路、8 个只有一条直路、21 个有路口。

- 广场：只有农村 / 城市可能有（`plazaChance` 0.55 / 0.7）；
- 水井：农村 0.55、荒野 0.12；**城市没有井，中心换成喷泉**（`fountainChance` 0.6）；
- 井 / 喷泉放在广场中心，没有广场时就在区块中心一带找一块空地。

### 房型

`HouseType`：农舍 `Cottage` / 两层小楼 `TwoStory` / 谷仓 `Barn` / 排屋 `RowHouse` / 木屋 `Cabin` / 公寓 `Apartment`。

- 城市：排屋 40% / 两层小楼 32% / 公寓 28%；农村：农舍 45% / 两层 25% / 谷仓 18% / 木屋 12%；荒野：只有木屋；
- **沙漠里木屋与谷仓换成农舍**（木结构站不住），且房子**不砌烟囱**、屋顶只用土黄沙色（`DesertRoofColors`）；
- 墙 / 门 / 窗是所有房型共用的 key（`house_wall` / `house_door` / `house_window`），
  只有**上层墙、公寓的顶与窗、谷仓的顶与门、木屋的墙与顶**有各自的 key。

### 地面

- **草原：不铺额外地面**，直接用场景里那张跟随小虫的无限草地 —— 所以 `ground` 这个 key 依然是「草地地面」，
  玩家交了图立刻看得见；
- **森林 / 沙漠：按区块铺一整块** `chunkSize × chunkSize` 的平铺 sprite（`ground_forest` / `ground_desert`），
  放在 `Ground` 排序层 `-895`（**高于**无限草地 `-900`、**低于**道路 `-885`）；
- 没有图片时的程序化配色：森林 = 用无限草地那张图乘 `(0.70,0.78,0.52)`（深绿），
  沙漠 = 用路面那张土黄图乘 `(1.00,0.97,0.74)`（沙黄）。

### 按地貌分家的内容（`ContentPack`）

`ContentPack.RegisterAll()` 在 `VillageGenerator.Awake` 里跟着 `EnsureDefaults` 一起调，
内容本身仍然是「写一个定义 + Register 一行」，只是多了门控：

| 定义 | 门控 | 效果 |
| --- | --- | --- |
| `berry` / `leaf` | 草原 + 森林 | 沙漠里不长果子与嫩叶 |
| `mushroom` / `pinecone` | 森林 + `nearTrees` | 只长在树底下 |
| `cactus_fruit` | 沙漠 + `nearAnchor "cactus"` | 只长在仙人掌旁边 |
| `wheat` | 草原 | 草原的口粮 |
| `rock` | 沙漠 + 草原 | 能搬（`carryWeight 2.2`，很沉）能砸 |
| `hay_bale` | 草原 | 能搬能吃 |
| `log` | 森林 | 能搬能吃 |
| `trash_can` | 城市 | 能搬能吃 |
| `bucket` | 农村 / 城市 + 草原 / 森林 | 能搬能吃 |
| `lantern` | 农村 / 城市 | 能搬、夜里发亮 |

**通用的东西不门控**：神奇果实（唯一的长大途径，任何地貌都刷，避免断档）、五种能力食物、木箱、故障链四件套。
`pick` 抽签时 `SpawnKit.PickWeighted` 会用 `SpawnRule.Includes(settlement, nature)` 过滤，
抽不到任何一条就提前收工（不白占位）。

---

## 5. 资源规范（图片 / 音频）

**完整规范在根 [`README.md`](../../README.md)**（对外交接文档，含每个 key 的像素/时长/响度要求）。
这里只记「工程侧流程」：

- 图片是**一物一图**：**一个 key 只管一个物件**，key 表（key → 原素材 / 是否保留代码染色 / 是否九宫格 / 是否平铺）
  在 `Assets/Scripts/ArtOverride.cs` 的 `Slots`，常量在 `ArtKeys`。
  已废弃的旧写法：按原素材名匹配（`rect` / `disc` / `roundrect` / `marker` / `grass`）——一张图换遍全场景，已删除。
- 图片 key（**100 个**）：其中 2026-09-25 新增 36 个（地貌地面 3 + 路面 2 + 自然景物 7 + 房型 7 +
  聚落地标 8 + 按地貌分家的食物 4 + 按聚落分家的物品 6；地面/路面共 6 个 key 标了 `tiling`）。
- **平铺 key 必须在 `Slots` 里标 `tiling: true`**：`ArtOverridePostprocessor` 会据此把导入的
  Wrap Mode 设成 `Repeat`（否则平铺时接缝处会被拉伸出一道糊边）。运行时的地块还会再兜一次
  （`VillageGenerator.ForceTiling`）。
- 音频 key（20 个）：`eat` / `grow` / `pickup` / `drop` / `step` / `ui_click` / `bgm` / `bgm_menu`
  + 6 个能力 + `break` / `alarm` / `slip` / `fire` / `bell` / `swarm`
  → 别名表在 `Assets/Scripts/AudioOverride.cs` 的 `AliasSource`，常量在 `AudioKeys`。
  **`bell` 现在真的有人在用了**（钟楼整点敲钟，见 §4.12 / `BellTower.cs`）；`fire` / `swarm` 仍是预留。
- **新增/改名/删除 key 时，必须同时更新三处**：脚本里的表 + 根 `README.md` 的表格 + 对应目录里的 `README.md`。
- 套用规则：`ArtOverride.Apply(SpriteRenderer, key)` 会把渲染器颜色**刷成白色**（图片原样显示），
  除非该 key 的 `Slot.tint = true`（村民身/头 = 职业色与肤色、窗户/路灯/炉火 = 昼夜变色、
  高亮底衬、花/篷布的随机配色、界面面板与按钮 = 颜色即功能标识）。
- 落地方式：村庄物件在 `VillageGenerator` 创建时**立刻**套用（`AddRect`/`AddDisc`/`AddSlice` 的 `key:` 参数），
  所以后生成的区块同样生效；场景里手工摆的三个精灵用 `ArtSlot` 组件标 key；
  开始界面用 `MenuArt`（挂 `Canvas`），**没有图片时代码绘制**渐变背景与九宫格圆角矩形。
- 小虫的尾巴是 `LineRenderer`，**没有图片 key**，换不掉（要改只能改 `WormBody` 参数或材质）。
- 资源目录为空时：图片→程序化美术（含开始界面的程序绘制）；音频→静音。两种情况都**不能报错**。

---

## 6. 红线清单（已验证过、违反必出 bug）

> 这些都是实际踩过并修好的坑，改相关代码前先看这里。

1. **`Rigidbody2D` 会睡眠**：睡眠后写 `velocity` / `SetRotation` 一律无效 →
   小虫与村民都设 `rb.sleepMode = RigidbodySleepMode2D.NeverSleep`。
2. **`freezeRotation = true` 时 `MoveRotation()` 不生效** → 小虫用 `rb.SetRotation()`；
   **村民完全不旋转**，只用 `Visual` 的 X 缩放做左右翻转。
3. **朝向只认「想去的方向」**：村民翻转取 `FixedUpdate` 里算出的目标方向，**不要**取 `rb.velocity`
   （被挤/撞时物理会改 velocity，朝向会乱翻）。
4. **整块铺地的贴图不能按自身中心 Y 排序**（会盖住站在上面的角色）→ 见 4.3 的 Ground 层规则。
5. **区块调度必须先回收再生成**：否则锚点表里残留上个位置的农田/摊位，新村民会被派到几十格外上班。
6. **区块反复生成/回收 → 「把小虫放到出生点」只能执行一次**：`VillageGenerator.playerPlaced` 一次性标记 +
   `placePlayerOnFirstChunk` 开关（读档时由 `AutoSave` 关掉），否则小虫在原点附近会被瞬移回出生点。
7. **`WormBody` 每帧写头部缩放**：成长后的头部缩放必须由 `WormBody.ApplyScale(baseHeadScale × SizeMultiplier)` 计算，
   在别处直接写 `Head.localScale` 会被覆盖。
8. **`JsonUtility.FromJson` 会执行字段初始化器**：判断老存档**必须看显式 `version`**，
   不能用「字段是不是缺省值」当哨兵（否则老档会莫名变成默认地图）。
9. **`BugVitality` 的基础体力上限不能放 `Awake` 缓存**（组件 Awake 顺序不保证，`BugGrowth` 可能先跑）
   → 改为首次读取时惰性捕获。
10. **对象可能已被区块销毁**：`Villager` / `Burrow` 这类「区块里的对象」被销毁后外部仍可能持有引用，
    访问前要用 `this == null` 之类的防护（`Villager.CanSeeBug()` 就是例子）。
11. **中文 UI 必须用 uGUI + TextMeshPro**，**禁止 IMGUI**（`OnGUI` / `GUI.Label` 不走 TMP 全局 fallback，中文会变方块）。
12. **改小虫尺寸要成套改**（同乘同一系数）：`Bug/Head` 缩放、`WormBody.width`、`WormBody.spacing`、
    `CircleCollider2D.radius`、`MoveMarker` 缩放，外加 `WormBody.idleAmplitude/walkAmplitude/waveLength`。
13. **吞食是两段延迟**（`BugEat.consumeDelay 0.22s` + `Edible` 缩小后再 `Destroy`）：
    测试脚本判定「已销毁」要等 > 0.44s，否则会误判。
14. **Tuanjie AI 资产生成（材质/天空盒等）因账号积分不足不可用**（返回 `NotEnoughBalance`）
    → 不要提议用 AI 生成素材；走程序化或人工提供。
15. **美术替换是「一物一图」，新增可见物件必须给它自己的 key**：生成器里的物件走
    `AddRect`/`AddDisc`/`AddSlice` 的 `key:`（`ArtKeys` 常量），场景里的手摆精灵挂 `ArtSlot`，
    界面走 `MenuArt`。**不要把同一个 key 用在两个不同物件上** —— 那就是已经废弃的
    「一张 `disc` 换遍树冠 + 村民头 + 羊 + 路灯」老问题（玩家没法单独换其中一个）。
16. **组件 `Awake` 顺序不保证，公开属性别直接吃 `Awake` 里赋的值**：2026-09-24 实测
    `SimpleHUD.Awake` → `BugController.NearbyBurrow` 里 `rb.position` 空引用崩掉（HUD 比小虫先跑，
    `rb` 还没赋值；同一个 `Awake` 里的统计/文案被中断，HUD 版式整块没生效）。
    写这种「给别人读」的属性时给个兜底：`Vector2 self = rb != null ? rb.position : (Vector2)transform.position;`（同类：红线 9）。
17. **静态事件（`GameEvent`）的订阅必须在 `OnDisable` 里退订**：村民被区块回收 / 被冻结都会走 `OnDisable`，
    不退订就会留下指向已销毁对象的委托；另外 `GameEvent.Reset()`（`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`）
    负责在每次进 Play 前清空订阅表（编辑器关掉「域重载」时静态字段会带着上一局的订阅者）。
    自检方式：`GameEvent.NoiseSubscriberCount` 应当**恒等于**「未被冻结的村民数 + 常驻订阅者数」
    （常驻的目前有 `Alertness` 一个；所以更好的写法是**比重建前后有没有变多**，而不是硬比村民数）。
18. **声音是全向的，不能按视野扇形算**：视野是「以朝向为中轴的扇形」（`viewHalfAngle`），
    听觉必须是「以自己为圆心的圆」（半径 = `hearingBase` × 响度 × `VillagerJobs.Curiosity`）。
    照抄视野逻辑会让玩家绕到村民背后搞事完全没反应。
19. **`NotoSansSC-Regular SDF` 的图集索引会卡在空槽上，导致新中文字形加不进去（2026-09-24 已修，改字体前先看这条）**：
    该字体是动态（Dynamic）多图集，图集写满会走 `SetupNewAtlasTexture()` —— 它在**运行时**新建一张图集贴图并
    `AddObjectToAsset`，**如果这次扩容没被存进资产**，磁盘上就会留下
    `m_AtlasTextures.Length = 2`（槽 1 为 null）+ `m_AtlasTextureIndex = 1`（`atlasTextureCount` 就是索引 +1）。
    之后**任何新字形**都会在 `TryAddCharacterInternal` 里抛
    `UnassignedReferenceException: The variable m_AtlasTextures of TMP_FontAsset has not been assigned`，
    那一帧 `ForceMeshUpdate` 中断 → 新加的中文文案不显示 / 漏字。
    - 自查：`atlasTextureCount != atlasTextures.Length` 或 `atlasTextures[i] == null` 就是坏了。
    - 修法（就地、保留资产 GUID、不破坏引用）：`fontAsset.ClearFontAssetData(false)` → `SetDirty` → `SaveAssets`，
      再把游戏要用的中文用 `fontAsset.TryAddCharacters(字符串, out missing, true)` 预热一遍。
      **skill「TMPChineseFont」只会检查「有没有中文 fallback」，它对这个缺陷会报 READY、不会修**。
    - 不要以为「TMP 一次性小毛病」就算了：不修的话所有新增中文文案都会踩。
20. **可搬物品是 `Kinematic` 刚体，完全不吃力**（2026-09-24 踩到）：`ItemCatalog` 造出的木箱是
    `RigidbodyType2D.Kinematic`（平时只由 `DragController` 直接写位置），**`AddForce` / `velocity` 对它无效**。
    要「震飞 / 推走 / 撞开」这类效果时：先临时切 `Dynamic` + `WakeUp()`（红线 1），推完再用协程
    把速度归零并切回 `Kinematic`（见 `AbilitySet.SettleAfter`），否则村子会被越推越乱、木箱手感也变了。
21. **场景加载后才自挂的组件，别的脚本必须在用时惰性查找**：`AbilitySet` / `BugFootsteps` 走
    `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 挂到小虫身上，**在别人的 `Awake` 里一定还不存在**
    （`AutoSave.Awake` 早于它跑 → 拿到 null → 能力永远存不进存档、`abilityCharges` 写成空数组）。
    写法：`if (cache == null) cache = FindObjectOfType<AbilitySet>();` 放在属性里每次兜一下（同类：红线 16）。
22. **可互动物件只能「被玩家弄坏」时才改世界状态，不能在 `OnDestroy` 里改**（2026-09-25 定）：
    区块会随玩家走远而回收，被回收时物件也会走 `OnDestroy` —— 如果在 `OnDestroy` 里断电 / 漏水，
    玩家走一圈回来会发现整个村子都瘫痪了。所以「被吃掉」用 `Edible.onConsumed`、
    「被打碎」用 `Breakable.Break()` 这些**只由玩家的行为触发**的入口，别用 `OnDestroy`。
    同理：这些状态**不进存档**（Spec §4.4），区块按种子重建时恢复原样就是对的。
23. **HUD 新增面板必须走 `SimpleHUD.LayoutPanels`**（2026-09-25 定）：面板的位置由它按
    「前一块的实际文字高度 + 间距」算出来，自己写 `anchoredPosition` 迟早会和别的面板压在一起。
    新面板要**运行时按现有面板的样子建**（底板 `Image` 的 `color` 照抄 `statusPanel`、文字字体 / 字号照抄
    `instructions`，锚点 `(0,1)` / pivot `(0,1)`，文字子物体锚点拉伸 + `sizeDelta(-40,-20)`），
    这样不改场景也能长得一样。**注意挂在 HUD 画布下，不是挂在 `GameDirector` 上**
    （`SettlementPanel.Panel` 在 HUD 画布下 —— 想遍历它的子物体别从组件所在物体找）。
24. **故障特效一律程序化、不占美术 key，而且必须自带寿命**（2026-09-25 定）：缺图占位框 / 撕裂 /
    残影 / 挖像素这些是**运行时表现**，不是场景物件（同头顶「!」/ 碎片 / 水洼 / 分身）。
    每个特效物体都要挂 `GlitchLifetime`（到点淡出并销毁）或 `GlitchJitter`（到点把位置与 sortingOrder
    **原样还回去**）—— 否则会永久糊在屏幕上 / 让物体停在错位状态。
    屏幕级故障必须挂在 **HUD 画布**下并 `SetAsLastSibling()`，由 `GameEvent` 驱动（别在能力里直接调）。
25. **新增音频 key 必须同时加进 `AudioOverride.AliasSource` 别名表**（2026-09-25 踩到）：
    `AudioKeys` 里的常量只是「逻辑 key」，**文件名是靠别名表解析成 key 的** ——
    只加常量不加别名，玩家把 `ability_shock.wav` 丢进目录也**匹配不上、永远是静音**，
    而且会被列进 `UnmatchedFiles`（看起来像玩家命名错了）。
    三处一起改：`AudioKeys` 常量 + `AliasSource` 别名 + 根 `README.md` 的音频表。
    **拍照检查**：`AudioOverride.ResolveKey("sfx_" + key) == key`。
26. **密度是全局系数，别去改各个聚落 / 自然体系的数字**（2026-09-25 定，原「别改各张地图」）：
    加减密度改 `WorldBiome.Density` 一处；
    它只乘**房子 / 树 / 灌木 / 仙人掌 / 枯树 / 食物位 / 物品位 / 设施几率**，
    **村民数量不乘**（人口影响性能与手感，单独调）。
27. **所有「故障 / 报错」文字一律走 `GlitchOverlay.LogError`**（2026-09-25 定）：
    报错固定出现在**屏幕左下角**、红色、最多 5 行、新行把旧行往上顶（滚动）、2.2 秒后自己淡出。
    不要在别处再写一套飘字 / 用 `Debug.Log` 假装报错；加新的故障场景（新能力 / 新事故）时也调它，
    这样「小虫把游戏搞坏了」这件事在画面上始终是同一个语言。
28. **「就近生成」只是偏好，不能变成唯一出路**（2026-09-25 定）：写 `SpawnRule.nearTrees` /
    `nearAnchor` 时，**找不到参照物必须能退回普通随机落点**（`TryFindNearPoint` 返回 false → 走
    `TryFindFreePoint`）。否则玩家走到「没有那种设施」的区块就会整片断供（比如城里树少 → 果子全没了）。
    参照物用 `VillageMap.trees` / `VillageMap.anchors`（后者由 `AddAnchor` 登记、`PruneFarFrom` 一起剪）。
29. **地貌与聚落必须是「坐标 + 种子」的纯函数，而且成片**（2026-09-25 定）：
    `WorldBiome.NatureAt` / `SettlementAt` 只准依赖区块坐标与 `worldSeed`（不要吃 `Time` / `Random.Range` /
    已有对象状态），否则「走远再回头 + 读档」就还原不出来了。而且必须是**低频**噪声：
    直接对区块坐标取哈希会让每个区块一个样，看起来是噪点而不是地貌。
    实测验收标准：相邻区块同类率 > 0.6，且明显高于「隔得很远」的同类率（≈ 0.4）。
30. **`WorldBiome.Apply` 是「整体覆盖」而不是「累乘」**（2026-09-25 定）：它每个区块开头都会跑一次，
    所以 `ApplySettlement` 里必须写**绝对值**（`g.treeMin = 10`），只有 `ApplyNature` / `ApplyDensity`
    才做乘法。写成乘法会在区块之间来回迭代，树的密度会一轮比一轮高。
    同理：`VillageGenerator` 上的这些字段**只是 Inspector 默认值，运行时会被覆盖**，别去那里调数值。
31. **平铺素材的 key 必须标 `tiling: true`**（2026-09-25 定）：`ArtOverride.Slots` 里漏标的话，
    导入 Wrap Mode 会是 `Clamp`，平铺出来的地面 / 路面会在每个 tile 接缝处被拉伸出一道糊边。
    新增地面 / 路面类 key 时照 `ground` 的写法写第四位参数；`ArtOverride.TilingOf(key)` 是自检入口。
32. **按聚落 / 自然门控内容时，`hamletOnly` 与 `InSettlements` 的语义要和 README 一致**
    （2026-09-25 定）：`hamletOnly = true` 意思是「农村 + 城市」（不是「农村」）；只写一处门控不够 ——
    `pump` / `alarm` 就是因为只写了 `Fixed(...)` 而漏了门控，结果野外也会刷（README 却承诺「只在村庄」）。

---

## 7. 验证与自检规范

**原则：不接受「看起来对」。每句话都要有可复核的证据。**

### 7.1 编译校验（每次改代码**必做**）

编辑器没开时（常见）用 dotnet Roslyn 直接编译 `Assets/Scripts`：

```powershell
$root = "D:\TuanJie\Hub\Editor\2022.3.62t15"        # tuanjie.exe install-path --get
$proj = "D:\GameDemo\My Project"
$tmp  = "$env:TEMP\codely-verify"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $tmp | Out-Null

$refs  = @("$root\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll")
$refs += (Get-ChildItem "$root\Editor\Data\Managed\UnityEngine" -Filter *.dll |
          Where-Object { $_.Name -ne 'UnityEngine.dll' -and $_.Name -ne 'UnityEditor.dll' } |
          ForEach-Object FullName)
$refs += "$proj\Library\ScriptAssemblies\Unity.TextMeshPro.dll"
$refs += "$proj\Library\ScriptAssemblies\UnityEngine.UI.dll"

$lines  = @("-nologo","-nostdlib+","-target:library","-langversion:9.0",
            "-nowarn:0169,0649,0414,0219","-define:UNITY_EDITOR;UNITY_2022_3_OR_NEWER;UNITY_STANDALONE_WIN")
$lines += ($refs | ForEach-Object { '-r:"' + $_ + '"' })
$lines += (Get-ChildItem "$proj\Assets\Scripts" -Recurse -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' })
$lines += ('-out:"' + $tmp + '\verify.dll"')
Set-Content -Path "$tmp\csc.rsp" -Value $lines -Encoding UTF8

& dotnet "C:\Program Files\dotnet\sdk\9.0.305\Roslyn\bincore\csc.dll" "@$tmp\csc.rsp"
```

- **通过标准**：无 `error CS`，且生成了 `verify.dll`。
- **`CS0433`（类型重复）只有一个原因**：忘了排除 `UnityEngine.dll` / `UnityEditor.dll` 这两个**门面程序集**。
- 顺手把 `warning CS` 也看一眼，尤其新加字段的未使用警告。
- 换机器 / 换了 dotnet SDK 时，把最后的 csc 路径换成动态查找：
  `$csc = (Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Select-Object -Last 1).FullName; & dotnet $csc "@$tmp\csc.rsp"`
- 本机已实测：这段脚本原样运行 **EXIT=0 且生成 `verify.dll`**（2026-09-23）。

### 7.2 运行时验证（能力允许时）

优先用**结构化数据**，而不是观感描述：

- 组件字段 / 世界坐标 / `Renderer.enabled`、`WorldToScreenPoint` 是否在视口内、
  材质 `shader.name` 是否正常（判断有没有粉色错误材质）、`LineRenderer.BakeMesh` 量线宽。
- 需要看画面时：录一帧/一段 Game View，再用 `analyze_multimedia`（**不要**用 `read_file` 读 PNG/MP4，
  非视觉模型会只返回 `[Media omitted: …]`）。该工具有额度限制，报 402/额度不足时**回退到结构化数据**，
  并在日志里写明「未做视觉验证」。

### 7.3 禁止的表述

- 不许写「我看了截图/听了音频，没问题」——**除非**真的通过 `analyze_multimedia` 或实机验证过，并且要在日志里写明手段。
- 不许用「应该可以 / 理论上没问题」代替验证；做不到就写「**未验证**」。

---

## 8. AI / harness 运作约定

### 8.1 每次开工的标准流程

1. **读记忆**：`CODELY.md`（自动注入的 `## Codely Structured Memories`）。
2. **读规范**：本文档（`Docs/DevLog/Spec.md`）+ 最近 1~2 天的日志（`Docs/DevLog/日期.md`）。
3. **读代码再改**：定位方式 —— Unity 相关（类/方法/引用/场景与预制体结构）先用 Unity Insight VFS
   （`vfs_index_status` 不就绪时回退 `glob` / `search_file_content` / explore）；纯文件搜索用 `glob` / `search_file_content`。
   **先读后写，不要凭记忆猜字段名**。
4. **改动**：按本项目风格与红线来；能改脚本就不要改场景。
5. **收尾（缺一不可）**：
   - 编译校验（7.1）；
   - 写当天日志（追加到 `Docs/DevLog/YYYY-MM-DD.md`，模板见 `TEMPLATE.md`）；
   - 若产生了长期约定/新红线 → 更新**本文档**；
   - 若动了资源 key → 更新根 `README.md` 与目录内 README；
   - 值得跨会话记住的偏好/坑 → 存进项目记忆（`append_memory`，注意去重）。

### 8.2 改场景 / 预制体 / meta 的规则

- **`.meta` 文件：Never 手写、手改、手删**（GUID 由编辑器生成，写错整条引用链断掉）。
- **场景 YAML：能用脚本就不要手改**。需要「启动时就有的东西」时，优先用**运行时引导**：
  `[RuntimeInitializeOnLoadMethod]`（`GameSettings` / `AudioOverridePlayer` / `BugFootsteps` 都是这么做的）、
  `[DefaultExecutionOrder]`（`ArtOverrideApplier` 用 1000 保证在所有生成之后执行）。
- 必须改场景时（加面板、加按钮、调相机等）：改完要说明改了哪个场景的哪个对象，并在日志里记下来；
  不要顺手重排没有需求的字段。

### 8.3 文档与记忆的分工

| | 位置 | 生命周期 | 谁来写 |
| --- | --- | --- | --- |
| **规范/约定** | `Docs/DevLog/Spec.md`（本文档） | 长期权威，可提交进 git | 人 + AI，改动要慎重 |
| **开发日志** | `Docs/DevLog/YYYY-MM-DD.md` | 按天追加，只增不改 | AI 每次开发结束 |
| **资源格式** | 根 `README.md` + 目录内 `README.md` | 长期，对外交接 | 动了 key 就必须同步 |
| **跨会话记忆** | `CODELY.md` 结构化记忆 | 会话间自动注入 | AI（去重优先，别写代码细节） |

**冲突处理**：代码 > 本文档 > 日志；发现不一致时以代码为准并**回头修正本文档**。

---

## 9. 常用命令与路径速查

| 用途 | 命令 / 路径 |
| --- | --- |
| 编辑器安装路径 | `tuanjie.exe install-path --get` → 本机 `D:\TuanJie\Hub\Editor`；编辑器 `D:\TuanJie\Hub\Editor\2022.3.62t15` |
| 已装编辑器版本 | `tuanjie.exe editors list-installed`（1.10.3 / 2022.3.62t15） |
| 打开工程 | `tuanjie.exe open 'D:\GameDemo\My Project'` |
| 项目注册信息 | `tuanjie.exe projects info 'D:\GameDemo\My Project'` |
| 编译校验 | 见 7.1 的 PowerShell 片段（dotnet Roslyn `csc`） |
| 编译产物参考 | `Library/ScriptAssemblies/`（`Assembly-CSharp.dll`、`Unity.TextMeshPro.dll`、`UnityEngine.UI.dll`…） |
| 存档 | `%USERPROFILE%\AppData\LocalLow\<公司>\<产品>\whatabug_save.json` |
| 玩家设置 | `PlayerPrefs`：`game.volume`、`menu.resWidth/resHeight/fullscreen` |
| 世界生成 | 地貌 / 聚落与所有生成参数都在 `Assets/Scripts/WorldBiome.cs`；按地貌分家的内容在 `Assets/Scripts/ContentPack.cs` |
| 存档版本 | **v6**（v6 = 取消地图选择、改记地貌与聚落；v5 = 删任务模块；v4 = 混乱/警觉/统计；v3 = 能力；v2 = 地图类型） |
| 日志 | 编辑器日志 `%LOCALAPPDATA%\Tuanjie\Editor\Editor.log`；工程内 `Logs/` |
| 运行时日志前缀 | `[Bug]` / `[ArtOverride]` / `[AudioOverride]` |

> 注意：`D:\Tuanjie Cowork\hub\tuanjie.exe` 是 **Hub/Cowork 本体**，不是编辑器进程；
> 判断「编辑器是否在运行」要看 `Temp/UnityLockfile` 或编辑器窗口，别看这个进程名。
