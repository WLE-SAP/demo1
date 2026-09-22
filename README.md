# 小虫村庄（2D 俯视小游戏 · 无限地图）

一个 Unity/Tuanjie **2D** 小游戏：小虫在一片**没有边界、无限延伸**的村庄世界里散步、吃果子、搬箱子。
建筑、设施、树木、食物和村民都随小虫移动**流式随机刷新**，走远再回头还是同一个村子。
村民有职业、有作息：干活、赶集、闲聊、天黑回家；离得远的村民会被自动「冻结」省资源。
美术全部是程序化的（图元 Sprite + LineRenderer 曲线），也可以用自己提供的图片整体替换（格式规范见下）。

- 引擎：Tuanjie 1.10.3（2022.3 内核），渲染管线：Built-in，2D 模式
- 入口场景：`Assets/Scenes/MainMenu.scene`（开始界面）→ 点「开始游戏」进入 `BugScene.scene`

---

## 1. 操作

| 操作 | 效果 |
| --- | --- |
| 鼠标左键点击地图 | 小虫走到该处（头顶会出现十字标记），**可以点到任意远的地方** |
| 空格 | 吃掉**头附近**的食物（整圈判定，不要求朝向；有啃食动作） |
| **F** | **统一交互键**：拾取 / 放下物品、钻地洞 / 出洞、走地道传送 |
| **Shift** | 朝当前目标（没目标就朝朝向）**冲一小段**：0.22 秒冲刺 + 0.9 秒冷却 |
| Esc | 返回开始界面 |

**往任意方向走**，附近会不断刷出新村庄；走远的区块会被回收。游戏**每 5 秒自动存一次**，随时可以关掉。

左上角 HUD **只显示小虫自己的信息和操作指引**，三块内容自上而下自动排布（不会互相遮挡）：

| 区域 | 内容 |
| --- | --- |
| 操作说明 | 左键 / 空格 / F / Shift / Esc，以及「每 5 秒自动保存一次」 |
| 小虫状态 | `已吃 N 个 · 体力 82/100 · 速度 3.0 · 2 级 · 搬运中 / 躲在地洞里 / 冲刺 · 已保存` |
| 操作提示 | 随场合变化：`[F] 钻地洞躲起来` / `[F] 拾取箱子` / `[空格] 进食` 等 |
| 体力条 | 一条彩色血条（绿 → 黄 → 红），**不吃东西会一直掉** |

> 三块面板的位置由 `SimpleHUD.LayoutPanels()` 在运行时按上一块的实际高度往下排，改文案 / 加行都不会再叠在一起。

右下角偏下还有一个**悬浮窗**：小虫**靠近物品或村民**时自动弹出它的介绍（走开约 1.2 秒后淡出）：

- 物品 / 景物：读 `EntityInfo`——果子、叶子、木箱、树、地洞 / 地道、神奇果实都有自己的介绍；
- 村民：显示 `名字 · 职业` + 这个职业的一段介绍 + **他现在在做什么**；
- 隐藏数值（`HiddenValue`）**不会**出现在这里。

## 2. 体力、成长与隐藏数值

### 体力（`BugVitality`）

- 满体力 100，**每秒掉 0.8**（站着不动也掉）：`drainPerSecond`
- 吃东西恢复体力，**不同食物恢复的量不同**（见下面的隐藏数值）
- 掉到 0 → **饿死**：小虫停下、把当前进度存档（体力回 60%，免得一读档又立刻饿死），
  停留 1.8 秒（`deathDelay`）后自动回到开始界面

### 成长（特殊食物 → `BugGrowth`）

区块里随机刷出**神奇果实**（金色发光果实，`specialFoodChance = 45%`）。吃掉它小虫长大一级（最多 3 级），**每级**都会：

| 项目 | 每级加成 | 字段 |
| --- | --- | --- |
| 体型（头 / 尾巴粗细 / 碰撞体） | ×1.35 | `scalePerLevel` |
| 移动速度 | +12% | `speedPerLevel` |
| 捕食范围 | +28%（**能吃到更多、更远的东西**） | `eatRangePerLevel` |
| 体力上限 | +25 | `staminaPerLevel` |

实测：头 0.112 → 0.151 → 0.190 → 0.230，尾宽 0.064 → 0.131，碰撞 0.056 → 0.115，
移速 2.6 → 3.54，捕食半径 0.7 → 1.29，体力上限 100 → 175；满级后不再长。

### 隐藏数值（`HiddenValue`）

**每个物品和村民都挂着一个不显示给玩家的数字**：

| 对象 | 数值含义 | 实际作用 |
| --- | --- | --- |
| 果子 / 叶子 | 分量 6~14 | 决定吃下去恢复**多少体力**（`Edible.SatietyAmount`） |
| 神奇果实 | 分量 22~30 | 恢复得多，而且让虫子长大 |
| 木箱 / 树 / 地洞 | 分量 5~30 | 目前只作记录（`note` 写明用途），给后续玩法留口子 |
| 村民 | 体魄 2~10 | 影响**追逐的耐心**（`reactMin/Max` ×0.75~1.35）和一点点移速（×0.94~1.10） |

数值只在代码 / 编辑器里可见（`note` 字段是给人看的备注），HUD 和悬浮窗都不会显示它。

## 3. 存档与「继续游戏」

- 进入游戏后**每 5 秒自动存档一次**（`AutoSave.interval`），另外在
  **窗口失焦 / 切后台 / 退出游戏 / 返回主菜单**时都会再补存一次，
  所以直接点右上角关掉游戏也不会丢进度。
- 主菜单新增 **「继续游戏」**：有存档时可点、没存档时置灰写着「还没有存档」。
  「开始游戏」= 新开一局（**换一个新的世界种子**）。
- 存档位置：`%USERPROFILE%\AppData\LocalLow\<公司名>\<产品名>\whatabug_save.json`
  （即 `Application.persistentDataPath`）。写入时先写 `.tmp` 再替换，避免正好被杀掉留下坏档。
- 存的内容（`GameSave`）：**世界种子、小虫位置、已吃数量、当前体力、成长等级、游戏内累计小时数、本局游玩秒数、存档时间**。
  因为区块内容是「坐标 + 种子」确定性生成的，所以只要种子对得上，走远再回来、读档后回来，村庄都一模一样。
- 村民 / 地洞不存：它们属于区块，读档后由区块重新生成（位置和职业由种子决定，和上次一致）。

## 4. 地洞与地道

每个区块会随机出现 **0~2 个地洞**（`VillageGenerator.burrowMin~burrowMax`），其中约 45% 是**地道**：

| 类型 | 外观 | 按 E 的结果 |
| --- | --- | --- |
| 地洞 | 深色洞口 + 土黄边 | 钻进去躲起来：小虫隐入地下（头和尾巴都收起来、碰撞关闭、点击不动），**村民看不见你**；再按一次 E 出来 |
| 地道 | 洞口 + 旁边堆着两块小石头 | 直接传送到配对的另一头钻出来（镜头会立刻跟上，不会拉一路） |

- 配对由 `VillageWorld.PairTunnels()` 完成：在**已加载的区块**里挑两头距离 24~110 之间的地道连起来，
  所以传送一定是「走一段路」的距离；另一头被回收时配对自动解开、剩下的重新找搭档。
  想只放地洞不放地道：把 `VillageGenerator.tunnelChance` 设为 0。
- 地洞本身是「地面上的洞」——和草地/道路/农田一样放在 **Ground 排序层**，站在洞口的角色绝不会被洞口盖住。
- 躲起来的时候不能吃东西、不能搬箱子（按 E 出来即可）。村民的视野看不到躲起来的小虫，
  被追的时候钻进地洞是最有效的脱身办法。

## 5. 无限地图与流式加载

地图**没有边界**：原来的栅栏环和四面碰撞墙已经删除，`BugController.useWorldLimit` 关闭，
草地是一块跟着小虫走、并对齐 4 格贴图网格的 `InfiniteGround`（看起来无限延伸）。

世界被切成 **32×32 世界单位的区块（chunk）**，由 `VillageWorld` 调度：

| 参数（`VillageWorld`） | 当前值 | 含义 |
| --- | --- | --- |
| `chunkSize` | 32 | 区块边长 |
| `viewRadius` | 1 | 以小虫所在区块为中心，加载 (2×1+1)² = **9 个区块** |
| `keepRadius` | 2 | 超出 2 格（切比雪夫距离）的区块才回收，避免来回乱刷 |
| `worldSeed` | 20260922 | 世界种子，决定整个世界长什么样 |
| `streamInterval` | 0.25 s | 检查间隔 |
| `freezeRadius` | 26 | 超过这个距离的村民被冻结 |
| `freezeFarVillagers` | true | 是否启用远距离冻结 |

- 每个区块的十字路口都在区块中心 → 相邻区块自然接成**连续的路网**，走起来像一整片村落。
- 区块中心有广场 + 水井的是「村庄区块」（默认 65%），其余是「野外区块」（树多、几乎没房子）。
- 生成是**确定性**的：区块坐标 + 世界种子决定一切，所以走远再回来，房屋树木位置一模一样
  （只有村民会走动）。
- 区块回收时会 `Destroy` 整个区块物体，并把 `VillageMap` 里对应的锚点剪掉，内存不会无限涨。

### 村民冻结（省资源）

`VillageWorld` 每 0.5 秒检查一次：离小虫超过 `freezeRadius`（26）的村民会调用
`Villager.SetFrozen(true)`：

- 停状态机（`enabled = false`，不再跑 Update / FixedUpdate）
- 停物理（`rb.simulated = false`）
- 关渲染（`freezeVisuals`，精灵不再提交绘制）
- 从 `Villager.All` 里移出（其他村民不会再找它闲聊）

走回 26 格以内自动解冻，位置、状态、作息原样恢复。

**视野里保证有村民**：`VillageWorld` 每 0.5 秒数一次「可见范围（`viewHalfSize` = 13 × 7.5）里有几个村民」，
少于 `minVillagersInView`（默认 2）就在小虫附近补人，补出来的村民挂在当前区块下（区块回收时一起销毁），
每个区块最多补 `maxExtraPerChunk`（4）个，不会越补越多。

## 6. 场景与对象

| 对象 | 组件 | 说明 |
| --- | --- | --- |
| `Main Camera` | Camera + FollowCamera | 正交俯视，`orthographicSize = 8.4`（可见 16.8 × 29.9 世界单位） |
| `Environment/Ground` | SpriteRenderer(T_Grass) + **InfiniteGround** | 无限草地，跟着小虫走并对齐 4 格；在 **Ground 排序层**（最底层），保证永远在所有内容之下 |
| `Bug` | Rigidbody2D + CircleCollider2D + BugController + BugEat + YSort | 玩家小虫 |
| `Bug/Head` | SpriteRenderer(S_BugHead) → `Mouth` | 头；`Mouth` 是嘴部锚点 |
| `Bug/Body` | LineRenderer + WormBody | 等宽的黑色曲线尾巴 |
| `MoveMarker` | SpriteRenderer(S_Crosshair) | 点击目标点标记 |
| `GameDirector` | DragController + SimpleHUD + ReturnToMenu + ProximityHighlight + **AutoSave** + ArtOverrideApplier | 全局逻辑 + 自动存档 |
| `Village` | **VillageWorld** + VillageGenerator + VillageClock + VillageMap | 流式调度、区块生成、游戏内时间、设施锚点表 |
| `HUD` | Canvas + CanvasScaler | 左上角操作说明 + 小虫状态（2 行） |
| MainMenu `Canvas/Panel` | **ContinueButton** + StartButton | 继续游戏 / 开始游戏 |

> `Environment` 下原来还有 `Fence_0..67` 和 `Boundaries`（四面墙），为了做无限地图已经删除。

## 7. 区块里有什么

每个「村庄区块」（32×32）大致包含：

| 内容 | 数量（每区块） | 说明 |
| --- | --- | --- |
| 十字路 + 广场 | 1 | 区块中心，相邻区块接成路网 |
| 水井 | 1 | 广场中心，带碰撞 |
| 房屋 | 3~5（野外 0~1） | 每个村民的家（夜里会回去），窗户夜里透出暖黄灯光 |
| 农田 | 70% 概率 | 翻好的地 + 垄 + 幼苗，农夫在田里干活 |
| 畜栏 | 35% | 三面围栏 + 4 只羊，牧羊人在里面走动 |
| 面包房 / 铁匠铺 | 各 22% | 带招牌的房子；铁匠铺有铁砧 + 夜里更亮的炉火 |
| 集市摊位 | 80% | 柜台 + 遮阳篷 + 货物，摆在广场边 |
| 花园 / 长椅 / 公告板 | 70% / 1~2 / 40% | 闲逛、坐着聊天、看留言的地方 |
| 路灯 | 4~12 | 沿区块内的十字路排布，**夜里亮起来** |
| 树 / 食物 / 木箱 | 10~16（野外 20~48） / 5~8 / 0~2 | 树木用碰撞体挡住去路；食物可吃；木箱可搬 |
| 地洞 | 0~2 | 可以钻进去躲起来；约 45% 是能相互传送的地道（见第 3 节） |
| 村民 | 2~4（野外 0~1） | 见下一节 |

一个村庄区块大约 100 个精灵，加载 9~12 个区块约 1000~1700 个渲染器。

> **排序坑（已修）**：农田、花坛、牧场的草地这类「一整块铺在地上」的贴图，如果按自己的中心 Y 排序，
> 站在它上半部分的角色会被整块地**盖住看不见**。现在它们全部放在 **Ground 排序层**用固定次序排
> （草地 -900 → 道路 -885 → 农田 -870/-860/-850 → 牧场 -845 → 花坛 -840/-835/-830），
> Ground 层整体画在 Default 层之下，所以角色、房屋、树、羊永远在它们上面。

## 8. 村民：职业、作息、视野与反应

`VillagerJobs.All` 是一张职业花名册，生成时随机取用（9 种职业）。
**衣服颜色就是职业标识**（同职业的人有深浅差异）。

| 职业 | 衣服颜色 | 上班的地方 | 工作时的描述 | 看到小虫的反应 |
| --- | --- | --- | --- | --- |
| 农夫 | 麦黄 | 自己区块的农田 | 在田里劳作 | 无视（干活要紧） |
| 面包师 | 面粉白 | 面包房门口 | 在面包房烤面包 | **躲避** |
| 摊贩 | 橙红 | 集市摊位（柜台前） | 守着自己的摊位 | **躲避**（怕吓跑客人） |
| 守卫 | 制服蓝 | 沿区块内主路一段段巡逻 | 沿街巡逻 | **追逐**（要把虫子赶走） |
| 铁匠 | 铁灰 | 铁匠铺 | 在铁匠铺打铁 | 无视（胆子大） |
| 樵夫 | 林绿 | 找最近的树 | 在林子里砍柴 | **追逐**（拿家伙追） |
| 牧羊人 | 草青 | 畜栏里 | 在畜栏放牧 | **躲避** |
| 孩子 | 亮紫 | 自己那片村庄中心（到处跑） | 在广场上玩 | **追逐**（好奇） |
| 长者 | 长者灰紫 | 附近的长椅 / 水井 | 在井边晒太阳 | **躲避**（怕虫） |

职业与据点都是**在自己这块区块里**分配的（家 = 离工作点最近的房子，闲逛点 = 区块中心），
所以每个区块都是一个自给自足的小村子。

### 视角与反应

村民美术只会左右翻转（头永远朝上），所以**视野是朝向前方的左右扇形**：

- `viewRadius`(5.5)：视野半径；`viewHalfAngle`(55°)：以朝向为中轴的扇形半角
- `awareRadius`(1.4)：贴脸距离，这么近不管朝哪都会被发现
- 小虫**躲进地洞**时村民完全看不见它，追到一半会跟丢
- 在 Unity 里选中村民可以在 Scene 视图看到视野扇形（`drawViewGizmo`，仅编辑器）

看到一个村民被摆到小虫面前就会按职业进入 `Chase`（追，保持 `chaseDistance`=1.2 不会贴脸）或
`Flee`（往反方向跑 `fleeDistance`=7），反应持续 `reactMin~Max` 秒，之后回到原本的作息安排。

**反应时会降低移速**：`Villager.reactSpeed = 0.72`。村民基础速度也从 1.1~2.1 降到
**0.95~1.7**（孩子 1.5~2.2），所以：

| | 速度 |
| --- | --- |
| 小虫 `walkSpeed` | 2.6 |
| 普通村民 | 0.95~1.7 |
| 村民追逐时 | 基础 × 0.72 ≈ 0.7~1.2 |

也就是说小虫永远跑得掉，被追上只是「被围观」，不会真的被抓住。

### 状态机

```
Idle（原地待着）  Commute（走向目标点）  Work（干活）  Socialize（闲聊）  Play（玩耍）
Chase（看到小虫追过来）  Flee（看到小虫躲开）
```

- 每个状态都有计时；计时到点 → `Decide()` 按 **当前作息 + 职业 + 自己区块的设施** 决定下一件事
- `Decide()` 会**先检查视野**：看到小虫且职业不是「无视」就立刻转入 Chase / Flee
- `Commute` 以「到达目的地」结束；**被挡住超过 `stuckTimeout`** 就当作到了，就地做事
- `Socialize` 时每 0.3 秒找一次 `chatRadius` 内的邻居，**转过身面对面**（只左右翻转）
- 换班时（比如中午到了）正在干活的人最多再撑 2 秒就转入新安排；走到半路换班也会提前收尾
- 朝向由**目标方向**决定（不是物理速度），被挤到时不会左右乱翻

### 作息（`VillageClock`）

一天 = `dayLengthSeconds` 秒现实时间（默认 **240 秒**），开局 `startHour = 7`。

| 时段 | 游戏时间 | 村民在做什么 |
| --- | --- | --- |
| 夜里 | 22:00 – 05:00 | 回家待着（Idle）；路灯和窗户亮着 |
| 早晨 | 05:00 – 11:00 | 去上班地点干活 |
| 正午 | 11:00 – 14:00 | 社交时间：就近找人聊 / 去广场、水井、摊位扎堆 |
| 下午 | 14:00 – 18:00 | 继续干活 |
| 傍晚 | 18:00 – 22:00 | 再聊一会儿 / 歇脚 |

`VillageClock.Night01`（0=白天，1=深夜）驱动 `NightGlow`：路灯、房子窗户、铁匠铺炉火。
想快进：调 `VillageClock.speed`；调试可用 `SkipHours()` / `SetHour()`。

## 9. 代码结构（Assets/Scripts）

| 脚本 | 职责 |
| --- | --- |
| `BugController.cs` | 点击移动、朝向、加减速、卡住超时放弃目标；**F 键统一分发交互**（拾取 / 地洞）；**Shift 冲刺**；`useWorldLimit` 默认关闭（无限地图） |
| `GameInput.cs` | **按键统一表**：所有交互键默认 F（`GameInput.Interact`），冲刺默认 Shift；HUD 文案也从这里取 |
| `BugVitality.cs` | **体力**：持续下降、吃东西回复、掉光饿死（存档 + 回主菜单） |
| `BugGrowth.cs` | **成长**：吃特殊食物升级，放大体型 / 提速 / 扩大捕食范围 / 提高体力上限 |
| `HiddenValue.cs` | **不显示给玩家的隐藏数值**：物品的分量、村民的体魄 |
| `EntityInfo.cs` | 物品 / 景物的介绍文本（给右下角悬浮窗用） |
| `EncounterWindow.cs` | 右下角**悬浮窗**：靠近物品 / 村民时弹出介绍，走开淡出 |
| `WormBody.cs` | LineRenderer 尾巴：链式跟随、等宽、速度驱动摆动、捕食时收紧；`SetVisible` 供钻洞时隐藏 |
| `BugEat.cs` | 空格进食：**以头部为中心整圈判定**（不要求朝向），不同食物恢复不同体力，特殊食物触发成长 |
| `Edible.cs` | 可吃食物标记：`satiety`（恢复体力，-1 = 按隐藏数值）、`growth`（长大级数） |
| `Burrow.cs` | **地洞 / 地道**：静态表供就近查找，地道靠 `partner` 配对 |
| `DragController.cs` / `Draggable.cs` | F 拾取/放下，物品停在头前方 |
| `Edible.cs` | 可吃食物标记（静态表随区块回收自动清理） |
| `Highlighter.cs` / `ProximityHighlight.cs` | 物品高亮底衬与高亮等级 |
| `VillageWorld.cs` | **无限世界流式调度**：生成/回收区块、冻结远处村民、**给地道配对** |
| `VillageGenerator.cs` | **区块生成器**：一个区块里的道路、设施、房屋、树、食物、地洞、村民 |
| `VillageMap.cs` | 设施锚点表（农田/摊位/长椅/树/房屋/巡逻点…），支持按距离剪枝 |
| `VillageClock.cs` | 游戏内时间、时段、夜晚程度、时间快进 |
| `VillagerJobs.cs` | 职业表：中文名、衣服颜色、工作描述、**看到小虫的反应** |
| `Villager.cs` | 村民状态机 + 作息 + **视野与追/躲反应** + 左右翻转朝向 + 远距离冻结 |
| `NightGlow.cs` | 夜里亮起来的东西（路灯、窗户、炉火） |
| `InfiniteGround.cs` | 无限草地：跟随小虫并按贴图尺寸对齐 |
| `YSort.cs` | 俯视 2D 深度排序（按世界 Y，绝对坐标也安全） |
| `FollowCamera.cs` | 相机跟随 + 朝向前移 + `Snap()`（走地道后立刻贴过去） |
| `SimpleHUD.cs` | 左上角 HUD（uGUI + TextMeshPro）：**只放小虫状态 + 操作指引** |
| `MainMenu.cs` / `ReturnToMenu.cs` | 开始界面（标题 What a Bug？，继续游戏 / 开始游戏）/ Esc 返回 |
| `SaveSystem.cs` | **存档读写**：`GameSave` 数据结构 + JSON 落盘（先写 .tmp 再替换） |
| `AutoSave.cs` | **自动存档 + 读档**：每 5 秒存一次，失焦 / 切后台 / 退出 / 返回菜单补存，读档恢复局面 |
| `ArtOverride.cs` / `ArtOverrideApplier.cs` | 美术覆盖表与启动套用 |
| `Editor/ArtOverridePostprocessor.cs` | 编辑器：把上传的图片配成 Sprite 并按原素材换算 PPU |

### 几个必踩的坑（已处理，别改回去）

1. `Rigidbody2D` 静止一会儿会进入睡眠，睡眠后写 `velocity` / `SetRotation` 都无效 →
   `rb.sleepMode = RigidbodySleepMode2D.NeverSleep`（小虫和村民都设了）。
2. `freezeRotation = true` 时 `MoveRotation()` 完全不生效 →
   小虫的旋转必须用 `rb.SetRotation()`；**村民干脆不旋转**，只用 `Visual` 的 X 缩放做左右翻转。
3. **整块铺在地上的贴图不能按自己的中心 Y 排序**：农田/花坛/牧场草地这样排会把站在它上半部分的角色
   整块盖住。现在这些「地面物件」全部放进 **Ground 排序层**（草地 → 道路 → 农田 → 牧场 → 花坛 → 地洞），
   Ground 层整体在 Default 层之下，角色/房屋/树/羊永远在上面。
4. 区块调度必须**先回收再生成**：否则锚点表里还留着上一个位置的农田/摊位，
   新村民会被派到几十格外「上班」，人会刷在区块外面。
5. 村民/地洞这类「区块里的对象」被区块销毁后，外部若还持有引用会报
   `Rigidbody2D has been destroyed`——`Villager.CanSeeBug()` 里做了 `this == null` 防护。
6. **「把小虫放到出生点」只能做一次**：区块会反复生成/回收，如果每次建原点区块都调用 `PlacePlayer()`，
   小虫在原点附近被重新加载时会被**瞬移回出生点**。现在用 `playerPlaced` 一次性标记 +
   `placePlayerOnFirstChunk` 开关（读档时由 `AutoSave` 关掉，位置以存档为准）。

## 10. 参数速查

### 小虫尺寸（当前 = 原尺寸的 0.4x）

| 位置 | 字段 | 当前值 | 含义 |
| --- | --- | --- | --- |
| `Bug/Head` | Transform Scale | 0.112 | 头的大小 |
| `Bug/Body` | `WormBody.width` | 0.064 | 尾巴粗细 |
| `Bug/Body` | `WormBody.spacing` | 0.0268 | 每节间距；尾长 = spacing × 26 ≈ 0.70 |
| `Bug/Body` | `WormBody.idleAmplitude` / `walkAmplitude` | 0.018 / 0.064 | 摆动幅度 |
| `Bug/Body` | `WormBody.waveLength` | 0.76 | 摆动波长 |
| `Bug` | `CircleCollider2D.radius` | 0.056 | 碰撞体积 |
| `MoveMarker` | Transform Scale | 0.2 | 点击标记（视野放大后偏小，可调到 0.4~0.6） |

### 手感与视角

| 位置 | 字段 | 当前值 | 作用 |
| --- | --- | --- | --- |
| `BugController` | `walkSpeed` / `acceleration` / `arrivalRadius` | 2.6 / — / — | 移动速度 / 加速度 / 到达判定（成长会改 walkSpeed） |
| `BugController` | `useWorldLimit` | false | **是否限制点击范围**（无限地图关掉） |
| `BugController` | `dashSpeed` / `dashDuration` / `dashCooldown` | 11 / 0.22 / 0.9 | 冲刺速度 / 持续 / 冷却 |
| `BugController` | `burrowRange` / `burrowCooldown` | 0.9 / 0.5 | 钻洞交互距离 / 冷却（出洞不受冷却限制） |
| `BugEat` | `eatRadius` / `eatAngle` | 0.7 / 360 | 捕食半径 / 夹角（360 = 整圈，只要靠近头部；成长会放大半径） |
| `BugVitality` | `maxStamina` / `drainPerSecond` / `deathDelay` / `respawnRatio` | 100 / 0.8 / 1.8 / 0.6 | 体力上限 / 每秒掉多少 / 饿死停留 / 死后存档保留比例 |
| `BugGrowth` | `maxLevel` / `scalePerLevel` / `speedPerLevel` / `eatRangePerLevel` / `staminaPerLevel` | 3 / 0.35 / 0.12 / 0.28 / 25 | 最高等级 / 每级体型、速度、捕食半径、体力上限加成 |
| `EncounterWindow` | `radius` / `scanInterval` / `hideDelay` | 1.8 / 0.15 / 1.2 | 「遇到」判定距离 / 扫描间隔 / 走开后淡出延时 |
| `GameInput` | `Interact` / `Dash` | F / LeftShift | **所有交互键统一 F**，冲刺 Shift（改这里就全改） |
| `DragController` | `interactRange` / `holdDistance` | 1.2 / 0.7 | 交互范围 / 物品停在头前多远 |
| `FollowCamera` | `smooth` / `aimLead` | — / 0.28 | 跟随平滑度 / 朝向前移 |
| `Main Camera` | `Camera.orthographicSize` | 8.4 | **视野缩放**：越大看得越广、小虫越小 |

### 世界与村民

| 位置 | 字段 | 当前值 | 含义 |
| --- | --- | --- | --- |
| `VillageWorld` | `chunkSize` / `viewRadius` / `keepRadius` | 32 / 1 / 2 | 区块大小 / 加载半径 / 回收半径 |
| `VillageWorld` | `worldSeed` | 随机（读档时用存档里的） | 世界长相；`randomSeedEachRun` 打开时「开始游戏」会换新种子 |
| `VillageWorld` | `freezeRadius` / `freezeCheckInterval` | 26 / 0.5 | 村民冻结距离 / 检查间隔 |
| `VillageWorld` | `minVillagersInView` / `viewHalfSize` / `maxExtraPerChunk` | 2 / (13, 7.5) / 4 | **保证视野里至少有这么多村民** / 可见范围一半 / 每区块最多补几个 |
| `VillageWorld` | `tunnelMinGap` / `tunnelMaxGap` | 24 / 110 | 地道两头配对的距离范围（只在本侧已加载的区块里找） |
| `AutoSave` | `interval` / `applySaveOnStart` / `saveOnExit` | 5 / true / true | 自动存档间隔（秒）/ 进游戏读档 / 失焦退出时补存 |
| `VillageGenerator` | `placePlayerOnFirstChunk` | true | 新开一局时把小虫放到出生点（读档时由 AutoSave 关掉） |
| `VillageGenerator` | `hamletChance` | 0.65 | 村庄区块比例（其余是野外） |
| `VillageGenerator` | `houseMin~Max` / `treeMin~Max` / `villagerMin~Max` | 3~5 / 10~16 / 2~4 | 每区块内容量 |
| `VillageGenerator` | `burrowMin~Max` / `tunnelChance` | 0~2 / 0.45 | 每区块地洞数量 / 其中是地道的比例（设 0 = 只有普通地洞） |
| `VillageGenerator` | `specialFoodChance` | 0.45 | 每区块刷出「神奇果实」（吃了长大）的概率 |
| `VillageGenerator` | `farmChance` / `penChance` / `bakeryChance` / `smithyChance` / `stallChance` / `gardenChance` / `boardChance` | 0.7 / 0.35 / 0.22 / 0.22 / 0.8 / 0.7 / 0.4 | 设施出现概率 |
| `VillageClock` | `dayLengthSeconds` / `startHour` / `speed` | 240 / 7 / 1 | 一天多长 / 开局时刻 / 时间倍率 |
| `Villager` | `workMin~Max` / `chatMin~Max` / `idleMin~Max` | 5~10 / 5~11 / 1.5~4 | 干活 / 闲聊 / 发呆时长 |
| `Villager` | `viewRadius` / `viewHalfAngle` / `awareRadius` | 5.5 / 55° / 1.4 | 视野半径 / 扇形半角 / 贴脸必被发现的距离 |
| `Villager` | `reactMin~Max` / `reactSpeed` / `chaseDistance` / `fleeDistance` | 3~6 / 0.72 / 1.2 / 7 | 反应时长 / 反应时移速倍率 / 保持的追逐距离 / 躲开距离 |
| `Villager` | `moveSpeed` | 0.95~1.7（孩子 1.5~2.2） | 平时走路速度（比小虫慢） |
| `BugController` | `burrowRange` / `burrowCooldown` | 0.9 / 0.5 | 钻洞的交互距离 / 两次钻洞的冷却（出洞不受冷却限制） |

> 想改区块大小要注意：`VillageWorld.chunkSize` 与 `VillageGenerator.chunkSize` 必须一致
> （`VillageWorld.Awake` 会自动对齐到 generator 的值）。

## 11. 用自己的图片替换美术（含图片格式规范）

把图片放进 **`Assets/Resources/ArtOverride/`** 即可，无文件时用程序化美术。

**图片格式规范（完整版见该文件夹里的 `README.md`）**：

| 项目 | 要求 |
| --- | --- |
| 格式 | **.png**，带透明通道，直通 alpha（不要预乘），sRGB 8 位/通道 |
| 背景 | **必须真透明**（不要白底/绿幕底） |
| 尺寸 | 任意分辨率，最大 2048×2048；**推荐 64 像素 = 1 世界单位** |
| 命名 | 英文小写 + 下划线 + `.png`，一个 key 一张图 |
| 锚点 | 内容居中，四周留 1–2px 透明边 |
| 平铺素材 | `ground.png` / `road.png` 必须**无缝可平铺**，建议 256 或 512 见方 |
| 九宫格素材 | `rect.png` / `roundrect.png` 会被九宫格拉伸，四角保留 20px 不变形区 |

替换对照表（友好名或原始素材名都能用）：`bug_head` / `crosshair` / `ground` / `road` /
`rect` / `roundrect` / `disc` / `berry` / `leaf`。

导入时脚本会自动设成 Sprite，并按**被替换素材的世界宽度**换算 `Pixels Per Unit`，
所以任意分辨率的图片都会占同样大小，不会破坏布局。运行日志：
`[ArtOverride] 图片 N 张，替换了 M 个精灵。`；没生效的文件会被点名。

## 12. 继续开发建议

- **改世界长相**：换 `VillageWorld.worldSeed` 就是另一个世界；调 `hamletChance` 控制村庄密度。
- **加职业**：`VillagerJob` 加一项 + `VillagerJobs` 三个映射 + `Villager.WorkTarget()` 一个分支 +
  `VillageGenerator.WorkplaceFor()` 一个据点。
- **加设施**：照 `BuildFarm()` / `BuildStall()` 写 `CreateXxx()`，用 `TryFindFreePoint` / `IsFree`
  占位并写进 `map`，然后在 `BuildFacilities()` 里按概率调用。
- **更省的优化**：区块回收目前是 `Destroy`，可以改成对象池；也可以给冻结的村民连 `YSort` 一起关掉。
- **存档扩展**：想多做几个存档槽，把 `SaveSystem.FileName` 改成带索引；想存村民个人状态，
  就在 `GameSave` 里加一个按区块记录的列表（但村民本来会随区块重建，通常不必）。
- **音效 / BGM**：现在没有任何音频，可在 `BugEat` 吃到时、`DragController` 拾放时插 `AudioSource.PlayOneShot`。

## 13. 已知限制 / 注意

- **存档只有一个槽位**：`开始游戏` 会换新种子并覆盖存档（没有二次确认，也没有手动存档按钮）。
- **饿死是「回主菜单」而不是删档**：死时会存下当前进度、体力回 60%，所以「继续游戏」还能接着玩，不会卡在「一读档就死」。
- **村民状态不存档**：村民属于区块，读档后按种子重新生成，位置和职业与上次一致，但「当前在追谁 / 闲聊」这类临时状态不会保留。
- **村民美术只会左右翻转**：视野是「朝向前方的左右扇形」，所以从村民正上/正下方靠近时，只有贴得很近（`awareRadius`）才会被发现。
- **AI 资产生成不可用**：Tuanjie AI（材质 / 天空盒等）因账号积分不足报 `NotEnoughBalance`，美术走程序化方案。
- **中文必须用 TextMeshPro**：字体资产 `Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset`
  已加入 TMP 全局 fallback。不要用 IMGUI（`OnGUI`）写中文，那不受 TMP fallback 保护。
- **视觉验证受额度限制**：本项目里 `analyze_multimedia` 可能报额度不足，
  验证改动请用运行时结构化数据（Renderer / 组件字段）或人工在 Game View 里确认。
- 场景文件用 `.scene` 扩展名（Tuanjie），不是 Unity 的 `.unity`。
- 村民美术刻意保持极简（圆头 + 长方身子）：圆和长方形都左右对称，所以**翻转肉眼看不出差别**，
  以后加上不对称的细节（眼睛、背包）才会体现。
