# 技术与设计规范（Spec）

> **这份文档是长期权威约定**：写代码、改素材、跑验证、以及 AI/harness 每次开工前，都先读这一份。
> 计划、临时结论、一次性排错过程写在 [`2026-09-23.md`](2026-09-23.md) 这类**日志**里，不进本文档。
> 本文档与代码冲突时：**以代码为准，然后回来修正本文档**。

---

## 1. 项目速览

| 项目 | 值 |
| --- | --- |
| 游戏 | 《What a Bug？》（好大一个虫）——2D 俯视小游戏，小虫在无限流式村庄里吃、长大、躲村民 |
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
| 世界 | `VillageWorld` | **区块流式调度**：加载/回收、冻结远处村民、地道配对、`ApplyMapKind()` |
| | `VillageGenerator` | `BuildChunk(coord, seed)` 确定性生成一个区块的全部内容 |
| | `VillageMap` | 设施锚点表（农田/摊位/长椅/房屋/巡逻点…），支持按距离剪枝 |
| | `InfiniteGround` / `YSort` / `FollowCamera` | 无限草地对齐、俯视深度排序、相机跟随 |
| 村民 | `Villager` | 状态机 `Idle/Commute/Work/Socialize/Play/Chase/Flee` + 作息 + 扇形视野 + `fearsBug` + 冻结 |
| | `VillagerJobs` | 职业表：中文名、衣服颜色、工作描述、看到小虫的反应 |
| | `VillageClock` / `NightGlow` | 游戏内时间（240 秒一天、5 时段）/ 夜里亮起来的东西 |
| 玩法 | `Burrow` / `MapProfiles` / `EncounterWindow` / `EntityInfo` / `Highlighter` / `ProximityHighlight` | 地洞地道 / 三张地图密度 / 右下悬浮窗 / 介绍文本 / 高亮 |
| 存档 | `SaveSystem` / `AutoSave` | JSON v2 落盘（先 `.tmp` 再替换）/ 每 5 秒自动存 + 失焦退出补存 + 读档 |
| 界面 | `SimpleHUD` / `MainMenu` / `ReturnToMenu` / `GameInput` / `GameSettings` | HUD（uGUI+TMP）/ 开始界面 / Esc / 按键统一表 / 全局音量 |
| 素材 | `ArtOverride` + `ArtOverrideApplier` + `Editor/ArtOverridePostprocessor` | 图片按素材名替换 |
| | `AudioOverride` + `AudioOverridePlayer` + `BugFootsteps` + `Editor/AudioOverridePostprocessor` | 音频按 key 播放 |

### 4.2 世界生成模型

- 世界**无边界**：区块边长 `chunkSize 32`，以玩家所在区块为中心加载 `viewRadius 1`（9 块），
  超出 `keepRadius 2` 才回收；地面是跟随玩家的 `InfiniteGround`。
- 区块内容 = **坐标 + 世界种子**的确定性函数（`BuildChunk`），所以走远再回头、读档后回来，村庄一模一样。
- 密度由「地图类型」覆盖（`MapProfiles`）：荒野 / 农村 / 城市，**只改参数不改生成逻辑**。
- 村民离玩家超过 `freezeRadius`（26）被冻结（停状态机 + 停物理 + 关渲染 + 移出 `Villager.All`），
  走回来自动解冻；`VillageWorld` 每 0.5s 检查可见框内村民数，不足 `minVillagersInView` 就在附近补人。
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
- `GameSave` 字段：地图类型、世界种子、小虫位置、已吃数量、体力、成长等级、游戏内小时、游玩秒数、存档时间、`version`。
- **存档只存「玩家自身进度」**；村民、地洞属于区块，读档后按种子重建；`Villager.fearsBug` 这类个人状态不存档。
- 存档结构变更必须：**提高 `version`** 并在读取时按 `version` 分支处理（红线 8）。

---

## 5. 资源规范（图片 / 音频）

**完整规范在根 [`README.md`](../../README.md)**（对外交接文档，含每个 key 的像素/时长/响度要求）。
这里只记「工程侧流程」：

- 图片 key（9 个）：`bug_head` / `crosshair` / `ground` / `road` / `rect` / `roundrect` / `disc` / `berry` / `leaf`
  → 映射表在 `Assets/Scripts/ArtOverride.cs` 的 `AliasSource`。
- 音频 key（8 个）：`eat` / `grow` / `pickup` / `drop` / `step` / `ui_click` / `bgm` / `bgm_menu`
  → 别名表在 `Assets/Scripts/AudioOverride.cs` 的 `AliasSource`，常量在 `AudioKeys`。
- **新增/改名/删除 key 时，必须同时更新三处**：脚本里的别名表 + 根 `README.md` 的表格 + 对应目录里的 `README.md`。
- 图片替换是**按素材名全场景替换**，而 `rect` / `disc` 被大量复用（房屋/木箱/村民身子、树冠/村民头/羊/路灯/地洞…）
  → 画这两个素材要**中性**，别带具体细节。
- 小虫的尾巴是 `LineRenderer`，**没有图片 key**，换不掉（要改只能改 `WormBody` 参数或材质）。
- 资源目录为空时：图片→程序化美术；音频→静音。两种情况都**不能报错**。

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
| 玩家设置 | `PlayerPrefs`：`game.volume`、`game.mapKind`、`menu.resWidth/resHeight/fullscreen` |
| 日志 | 编辑器日志 `%LOCALAPPDATA%\Tuanjie\Editor\Editor.log`；工程内 `Logs/` |
| 运行时日志前缀 | `[Bug]` / `[ArtOverride]` / `[AudioOverride]` |

> 注意：`D:\Tuanjie Cowork\hub\tuanjie.exe` 是 **Hub/Cowork 本体**，不是编辑器进程；
> 判断「编辑器是否在运行」要看 `Temp/UnityLockfile` 或编辑器窗口，别看这个进程名。
