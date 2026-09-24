# What a Bug？· 资源交付说明（图片 / 音频）

> 这份文档只讲一件事：**图片和音频要按什么格式、什么名字、放到哪里**。
> 游戏玩法与代码结构不在这里 —— 需要时直接看 `Assets/Scripts`（每个脚本头部都有中文说明）。

---

## 0. 背景与两个投放目录

- 项目：Unity / Tuanjie（2022.3 内核）**2D** 俯视小游戏，入口场景 `Assets/Scenes/MainMenu.scene` → `BugScene.scene`。
- **现状：美术全部是代码画的图元（方块 / 圆 / LineRenderer 曲线）占位，音频全程静音。**
  你提供的文件会**自动顶替**这些占位内容，不需要改一行代码、不需要动场景或 Prefab。
- 只做了「读文件」这一种接入方式：**把文件拖进下面的目录就生效**，删掉就回到默认。

| 类型 | 投放目录 | 目录为空时 |
| --- | --- | --- |
| 图片（Sprite） | `Assets/Resources/ArtOverride/` | 用代码画的图元（含开始界面的渐变背景 / 圆角按钮） |
| 音频（AudioClip） | `Assets/Resources/AudioOverride/` | 全程静音（不报错、不影响玩法） |

- 图片是**一物一图**：**一张图只管一个物件**（第 1.2 节一张 key 表），想换哪样就放哪样；
- 覆盖两个场景：游戏 `BugScene` 与开始界面 `MainMenu`（背景 / 面板 / 按钮都能换）。

> 文件名就是「用在哪里」的唯一凭据，所以**命名比什么都重要**：对照第 1.2 / 2.2 节的表来起名。
> 名字写错不会崩，但会完全不生效 —— 运行时 Console 会点名告诉你是哪个文件没对上（见 1.5 / 2.6）。

---

## 1. 图片资源

### 1.1 硬性格式要求

| 项目 | 要求 | 原因 |
| --- | --- | --- |
| 文件格式 | **.png**（24 位或 32 位） | 只有 png 能带透明通道；`.jpg` 没有 alpha、`.psd/.ai` 是源文件 |
| 透明通道 | **必须有，且背景真透明**（不要白底 / 绿幕底） | 不透明背景会实心盖住它下面的草地、角色 |
| alpha 类型 | **直通 alpha（Straight）**，不要预乘 alpha | 预乘会让边缘发暗发脏 |
| 色彩空间 | sRGB，8 位/通道 | 不要做 HDR / 线性空间转换 |
| 单张尺寸 | 任意分辨率，**最大 2048×2048** | 超过会爆显存、拖慢加载 |
| 单文件体积 | 建议 < 2 MB | 贴图都进内存 |
| 命名 | 只用**英文小写 + 数字 + 下划线**，如 `house_roof.png`；**一个 key 只放一张** | 中文/空格文件名的匹配容易出岔子 |
| 建议像素密度 | **64 像素 = 1 世界单位**（直接按这个画最省事） | 和项目自带素材一致 |

### 1.2 要哪些图（一物一图，按 key 对照）

**一张图只管一个物件**：`house_roof.png` 只换屋顶，**不会**连带换掉木箱、村民身子、农田。
想换哪样就放哪样，其余保持程序化美术、互不影响
（这是与早期「一份 `disc.png` 换遍树冠 + 村民头 + 羊 + 路灯」最大的区别）。

「九宫格」= 会按物体大小拉伸、四角不跟着变形（导入时自动把边框按原素材圆角比例放大到你的图上）。

**游戏里（`BugScene`）**

| key | 是什么 | 占地 | 推荐像素 | 特殊要求 |
| --- | --- | --- | --- | --- |
| `bug_head` | 小虫的头 | 1 × 1 | 128 × 128 | 朝向**默认朝右**（+X 是正前方）；长大时等比放大，别贴边 |
| `crosshair` | 点击移动的标记 | 1 × 1 | 64 × 64 | 居中、线条细一点更清楚 |
| `highlight` | 高亮底衬（食物 / 果实） | 1 × 1 | 64 × 64 | 正圆、铺满画布；会被黄 / 橙两档染色 |
| `highlight_rect` | 高亮底衬（木箱） | 1 × 1 | 64 × 64 | 九宫格圆角矩形；同样会被染色 |
| `ground` | 地面草地 | 4 × 4 | 256 × 256 | **必须无缝平铺**（左右、上下都接得上）；用 2 的幂尺寸 |
| `road` | 道路 / 广场 | 4 × 4 | 256 × 256 | **必须无缝平铺**；走向按图内纹理 |
| `house_roof` | 房屋屋顶 | 1 × 1 | 128 × 128 | 九宫格（屋顶每栋大小不同，四角要留够不变形区） |
| `house_wall` | 房屋墙体 | 1 × 1 | 64 × 64 | 九宫格 |
| `house_door` | 房屋门 | 1 × 1 | 64 × 64 | 九宫格 |
| `house_window` | 房屋窗 | 1 × 1 | 64 × 64 | 九宫格；白天偏蓝、夜里变暖黄（代码染色） |
| `house_chimney` | 烟囱（房屋 / 作坊共用一张） | 1 × 1 | 64 × 64 | 九宫格 |
| `shop_sign` | 作坊招牌（面包房 / 铁匠铺共用一张） | 1 × 1 | 64 × 64 | 九宫格 |
| `anvil` | 铁砧 | 1 × 1 | 64 × 64 | |
| `forge` | 铁匠炉火 | 1 × 1 | 64 × 64 | 夜里更亮更大（代码染色） |
| `villager_body` | 村民的身子 | 1 × 1 | 64 × 64 | 会被**职业颜色**染色（白图 = 直接用职业色） |
| `villager_head` | 村民的头 | 1 × 1 | 64 × 64 | 会被肤色染色 |
| `tree_canopy` | 树冠 | 1 × 1 | 128 × 128 | 给了图就不再叠程序化的「内部高光」 |
| `berry` | 地上的果子 | 1 × 1 | 64 × 64 | 内容居中、留 1–2px 透明边 |
| `leaf` | 地上的叶子 | 1 × 1 | 64 × 64 | 同上 |
| `special_food` | 神奇果实 | 1 × 1 | 128 × 128 | 给了图就不再叠内部亮斑与小星星（外圈光晕保留） |
| `farm_soil` | 农田的土地 | 1 × 1 | 128 × 128 | 九宫格 |
| `farm_row` | 农田的垄 | 1 × 1 | 64 × 64 | |
| `farm_sprout` | 农田的幼苗 | 1 × 1 | 64 × 64 | |
| `pen_grass` | 牧场草地 | 1 × 1 | 128 × 128 | 九宫格 |
| `fence` | 畜栏围栏 | 1 × 1 | 64 × 64 | |
| `sheep` | 羊的身子 | 1 × 1 | 64 × 64 | |
| `sheep_head` | 羊的头 | 1 × 1 | 64 × 64 | |
| `garden_bed` | 花坛的土 | 1 × 1 | 128 × 128 | 九宫格 |
| `garden_edge` | 花坛的边 | 1 × 1 | 64 × 64 | |
| `garden_flower` | 花坛里的花 | 1 × 1 | 64 × 64 | 会被 5 种花色随机染色 |
| `well_rim` | 水井的井圈 | 1 × 1 | 128 × 128 | |
| `well_water` | 水井的水面 | 1 × 1 | 128 × 128 | |
| `well_post` | 水井的立柱 | 1 × 1 | 64 × 64 | |
| `stall_counter` | 摊位的柜台 | 1 × 1 | 64 × 64 | |
| `stall_awning` | 摊位的遮阳篷 | 1 × 1 | 64 × 64 | 会被 5 种篷布色随机染色 |
| `stall_post` | 摊位的立柱 | 1 × 1 | 64 × 64 | |
| `stall_goods` | 摊位上的货物 | 1 × 1 | 64 × 64 | |
| `bench_seat` | 长椅的坐板 | 1 × 1 | 64 × 64 | |
| `bench_back` | 长椅的靠背 | 1 × 1 | 64 × 64 | |
| `board_post` | 公告板的柱子 | 1 × 1 | 64 × 64 | |
| `board` | 公告板的板面 | 1 × 1 | 64 × 64 | |
| `board_paper` | 公告板上的纸 | 1 × 1 | 64 × 64 | |
| `lamp_post` | 路灯的灯柱 | 1 × 1 | 64 × 64 | |
| `lamp_head` | 路灯的灯头 | 1 × 1 | 64 × 64 | 夜里会发亮（代码染色） |
| `burrow_rim` | 地洞的洞口边 | 1 × 1 | 128 × 128 | |
| `burrow_hole` | 地洞的洞 | 1 × 1 | 128 × 128 | |
| `burrow_inner` | 地洞的深处 | 1 × 1 | 128 × 128 | |
| `burrow_stone` | 地道旁的小石头 | 1 × 1 | 64 × 64 | |
| `crate` | 木箱 | 1 × 1 | 64 × 64 | 九宫格；木箱大小随机 |

**开始界面（`MainMenu`）**

| key | 是什么 | 推荐像素 | 特殊要求 |
| --- | --- | --- | --- |
| `menu_background` | 整屏背景 | 1920 × 1080（16:9） | **整张铺满屏幕**（不重复平铺），画一张完整背景图即可 |
| `menu_panel` | 面板底板（主面板 / 设置 / 地图选择） | 128 × 128 | 九宫格圆角矩形；会被面板颜色染色（白图 = 米白面板） |
| `menu_button` | **所有按钮**共用的底图 | 128 × 128 | 九宫格圆角矩形；会被各按钮自己的颜色染色（开始=绿、继续=蓝…） |

> **这三个 key 不放也没问题**：开始界面会**自己用代码画** —— 背景是纵向渐变（深绿 → 稍亮的绿），
> 面板与按钮是白色圆角矩形九宫格，再乘上各自的颜色。删掉图片 = 回到程序绘制，行为一致。

**哪些 key 会被代码染色（其余都是图片原样显示）**

- `villager_body` / `villager_head`：衣服颜色 = 职业、肤色随机，颜色本身是「这个村民是谁」的信息；
- `house_window` / `lamp_head` / `forge`：白天 / 夜里颜色不同（夜里亮起来）；
- `highlight` / `highlight_rect` / `garden_flower` / `stall_awning`：底衬的黄橙、花的 5 色、篷布的 5 色；
- `menu_panel` / `menu_button`：颜色是「哪个按钮干什么」的标识。

想完全控制这些物件的外观，就把图**画成白色 / 浅灰的形状图**（染色后即你要的颜色），或干脆不换它。

### 1.3 尺寸、锚点与九宫格（导入时自动算好，不用你改设置）

放进目录后，编辑器脚本会自动把图片设成 **Sprite**，并按**该 key 对应的原素材在世界里的宽度**反推 `Pixels Per Unit`：

- **不管你是 64px 还是 1024px，替换后占的位置都和原素材一样大**，不会破坏布局；
- **九宫格 key 的边框会自动换算**：原素材的圆角占宽度多少比例，你的图就按同样比例设边框
  （例：`menu_button` 的原素材 64px、圆角 20px，你交 128px 的图 → 边框自动设成 40px），四角因此不会被拉伸；
- **锚点固定为图片中心** → 请把内容画在画布正中间，四周留出透明边距；
- 比例按**宽度**对齐，高度按图片自身长宽比等比缩放（非正方形会让物件变高/变矮）；
- 想手工改大小：改该图片 Import Settings 里的 `Pixels Per Unit`（数值越大画面里越小）。
  自动配置只在**首次导入**时执行，之后你手改的值会保留；
- 文件名没对上 key 时，按通用值 `64 像素 = 1 世界单位` 导入。

**平铺素材**（`ground` / `road`）和**九宫格素材**（`house_roof` / `crate` / `farm_soil` / `menu_button`…）是两种特殊约定，
务必按 1.2 节的要求画。

### 1.4 生效范围与限制

- 替换是**按 key 精确替换**：`tree_canopy.png` 只会换树冠，木箱、村民、农田照旧。
- 影响两个场景：`BugScene`（游戏内）与 `MainMenu`（开始界面的背景 / 面板 / 按钮）。
- 无限地图的区块会边走边生成/回收，但同类物件用的是同一个 key 的图，所以**替换是全局一致的**
  （而且区块**生成的那一刻**就套好图片了，不是只在开局套一次）。
- **换不了的东西**：小虫的尾巴是 `LineRenderer` 画的等宽黑色曲线，没有对应图片 key。
  想调它得改场景里 `Bug/Body` 上 `WormBody` 的 `width`（粗细）、`spacing`（节距）和摆动幅度。

### 1.5 自检 / 恢复默认

运行游戏后看 Console：

| 日志 | 含义 |
| --- | --- |
| `[ArtOverride] 从 Resources/ArtOverride 载入 N 张图片。` | 图片被读进来了 |
| `[ArtOverride] 图片 N 张，场景精灵替换了 M 个（村庄里的物件在生成时已各自替换）。` | 生效 |
| `没对上 key（名字写错了？）：xxx` | 有文件名没对上，照 1.2 节的表改名 |
| `key xxx 放了多张图片，只用先载入的 …` | 同一个 key 放多了，删到只剩一张 |
| `[ArtOverride] 开始界面：背景 用图片 / 程序绘制渐变，面板 x/y 用图片，按钮 x/y 用图片。` | 开始界面的生效情况 |
| `[ArtOverride] Resources/ArtOverride 里没有图片，使用程序化美术。` | 目录是空的，走的默认美术 |

**恢复默认：把目录里的图片删掉即可**，不需要改代码。

---

## 2. 音频资源

### 2.1 硬性格式要求

| 项目 | 要求 | 原因 |
| --- | --- | --- |
| 文件格式 | 音效：**.wav**（首选）或 .ogg；BGM：**.ogg**（首选）或 .wav | mp3 也能读，但解码开销大、循环点往往对不齐，**不建议** |
| 采样率 | **44100 Hz 或 48000 Hz**，同一个 key 只用一种 | 混采样率会让响度和音色不一致 |
| 位深 | 16 位（24 位也行，导入会转） | |
| 声道 | 音效：**单声道（mono）**；BGM：单声道或立体声都行 | 音效是 2D 播放，立体声只是白占一倍内存 |
| 响度 | 峰值 **不超过 −1 dBFS**（绝不削波），同一批音效之间电平差控制在 ±2 dB 内 | 全局只有一个音量滑块（见 2.5），**素材之间音量是自己对齐的** |
| 静音头尾 | 音效**不要留静音头**（触发即响）；BGM **首尾都不要留静音** | 静音头会显得"按了没反应"；BGM 的静音会在循环时变成空档 |
| BGM 循环 | **必须无缝循环**：首尾都是零交叉、正好切在小节线上，**不要自带淡入淡出** | 播放器是整段首尾相接循环（loop = 整段），带淡入淡出会一循环就"喘一口气" |
| 时长 | 音效 0.05–2 秒；BGM 30–120 秒（长了浪费内存） | |
| 文件体积 | 音效 < 300 KB；BGM < 8 MB | |
| 命名 | 英文小写 + 数字 + 下划线，如 `eat.wav`；**一个 key 只放一个文件** | 同名多文件时只会用其中一个 |

### 2.2 要哪些音频（key 对照表）

| 文件名（key） | 什么时候响 | 循环 | 建议时长 | 备注 |
| --- | --- | --- | --- | --- |
| `eat.wav` | 小虫吃掉东西的那一刻（果子 / 树叶 / 木箱 / 树 / 村民） | 否 | 0.2–0.4 s | 啃食感；吃到村民也用这一声 |
| `grow.wav` | 吃到神奇果实长大一级，**紧跟在 eat 后面**再响一次 | 否 | 0.6–1.5 s | 要有"升级了"的正反馈，允许比 eat 响一点 |
| `pickup.wav` | 按 F 拾取物品 | 否 | 0.1–0.3 s | |
| `drop.wav` | 按 F 放下物品（含搬运中途自动脱手） | 否 | 0.1–0.3 s | |
| `step.wav` | 小虫走路时的脚步，**按走过的距离触发**（默认每 0.6 世界单位一次，走得越密越快） | 否 | 0.05–0.15 s | 必须**很短、无尾音**，否则连成一片糊掉；播放器会给每次脚步 ±8% 的音高浮动 |
| `ui_click.wav` | 开始界面点任何按钮（开始游戏 / 继续游戏 / 设置 / 分辨率 / 音量 / 返回 / 选地图…） | 否 | ≤ 0.15 s | 干脆的"嗒"；会被连点，别太长 |
| `bgm.ogg` | 进入游戏场景 `BugScene` 时自动播放 | **是** | 30–120 s | 村里闲逛的轻松氛围曲 |
| `bgm_menu.ogg` | 开始界面 `MainMenu` 时自动播放 | **是** | 30–120 s | 音量别抢戏 |

> **没有的文件就是静音**：只交 `eat` 和 `bgm`，其余照旧不发声，不会有任何报错。

### 2.3 命名规则（写起来可以松一点）

文件名（不含扩展名）就是 key，比较时**忽略大小写、下划线、短横线和空格**，并自动忽略
`sfx_` / `se_` / `sound_` / `audio_` 前缀，所以下面这些写法都认：

| 会被当成 | 可接受的写法（举例） |
| --- | --- |
| `eat` | `eat.wav`、`Eat.wav`、`SFX_Eat.wav`、`se-eat.ogg`、`chew.wav` |
| `grow` | `grow.wav`、`level_up.wav`、`LevelUp.wav`、`powerup.wav` |
| `pickup` | `pickup.wav`、`pick_up.wav`、`grab.wav` |
| `drop` | `drop.wav`、`put_down.wav`、`place.wav` |
| `step` | `step.wav`、`footstep.wav`、`footsteps.wav` |
| `ui_click` | `ui_click.wav`、`UIClick.wav`、`click.wav`、`button.wav` |
| `bgm` | `bgm.ogg`、`bgm_game.ogg`、`game_music.ogg`、`music.ogg` |
| `bgm_menu` | `bgm_menu.ogg`、`menu_music.ogg`、`menubgm.ogg` |

**同一个 key 放了多个文件 → 只会用其中一个（谁先载入用谁）**，所以一个 key 只交一个文件。
名字没对上 key 的文件（比如 `explosion.wav`）不会生效，但会在 Console 里被点名。

### 2.4 响度与循环（最容易翻车的两点）

1. **响度统一**：游戏里只有**一个**总音量滑块，所有音效共用它。
   所以"SFX 之间的相对音量"全靠素材自己对齐 —— 建议所有音效都压到**峰值 −6 ~ −3 dBFS**，
   `grow` 这种"奖励音"可以到 −3 dBFS，`step` / `ui_click` 这种频繁播放的压到 **−12 ~ −9 dBFS** 以免吵。
2. **BGM 无缝循环**：导出时**不要**加淡入淡出、不要留头尾静音、首尾切成零交叉。
   播放器是"整段循环"，只要素材本身接得上，循环处就听不出接缝。
   （想验证：把文件连续叠三遍自己听接缝处。）

### 2.5 播放行为与音量

- **播放器**：游戏启动时自动创建一个常驻播放器（`AudioOverridePlayer`），**跨场景不中断**：
  进 `BugScene` 自动放 `bgm`，回 `MainMenu` 自动放 `bgm_menu`；同一首正在播时不会重头再来。
- **音量**：所有声音都受开始界面「**游戏设置 → 音量**」控制（写全局 `AudioListener.volume`，存 `PlayerPrefs`，启动自动套用）。
  音乐和音效的**相对**比例在 `AudioOverridePlayer` 的 `musicVolume`（默认 0.7）和 `sfxVolume`（默认 1）里调。
- **导入设置自动配置**：放进目录后不需要手改 Import Settings ——
  BGM 用**流式载入**（长音频不占内存），音效用**解压到内存 + 预载**（播放不卡顿）。
  已经配置过的文件不会再被改动，你想手工微调随时可以。
- 音频播放**不受画面影响**，也不需要往场景里拖任何东西。

### 2.6 自检 / 恢复默认

运行游戏后看 Console：

| 日志 | 含义 |
| --- | --- |
| `[AudioOverride] 音频 N 个，可用 key：eat、bgm…` | 生效，后面列的就是已经认出来的 key |
| `……没对上 key（名字写错了？）：xxx` | 有文件名没对上，照 2.2 / 2.3 节的表改名 |
| `[AudioOverride] Resources/AudioOverride 里没有音频，静音运行。` | 目录是空的（默认状态） |

**恢复默认：把目录里的音频删掉即可。**

---

## 3. 交付清单（可以直接照这个列表交）

**图片 —— 放进 `Assets/Resources/ArtOverride/`**（一物一图，随便挑着交）

```
—— 先做这几张就能看出整体风格 ——
ground.png        256×256   草地，无缝平铺
bug_head.png      128×128   小虫的头，朝右
house_roof.png    128×128   九宫格，屋顶
crate.png          64×64    九宫格，木箱
tree_canopy.png   128×128   树冠

—— 继续补（都是独立 key，互不影响）——
crosshair.png      64×64    点击标记
road.png          256×256   道路 / 广场，无缝平铺
house_wall.png     64×64    九宫格，墙体
house_door.png     64×64    九宫格，门
house_window.png   64×64    九宫格，窗（白天偏蓝、夜里暖黄）
house_chimney.png  64×64    九宫格，烟囱
shop_sign.png      64×64    九宫格，作坊招牌
anvil.png          64×64    铁砧
forge.png          64×64    炉火（夜里更亮）
villager_body.png  64×64    村民身子（被职业色染色）
villager_head.png  64×64    村民的头（被肤色染色）
berry.png          64×64    果子
leaf.png           64×64    叶子
special_food.png  128×128   神奇果实
farm_soil.png     128×128   九宫格，农田土地
farm_row.png       64×64    农田的垄
farm_sprout.png    64×64    幼苗
pen_grass.png     128×128   九宫格，牧场草地
fence.png          64×64    畜栏围栏
sheep.png          64×64    羊的身子
sheep_head.png     64×64    羊的头
garden_bed.png    128×128   九宫格，花坛土
garden_edge.png    64×64    花坛边
garden_flower.png  64×64    花（被 5 种花色染色）
well_rim.png      128×128   井圈
well_water.png    128×128   井水
well_post.png      64×64    井柱
stall_counter.png  64×64    摊位柜台
stall_awning.png   64×64    摊位遮阳篷（被 5 种篷布色染色）
stall_post.png     64×64    摊位立柱
stall_goods.png    64×64    摊位货物
bench_seat.png     64×64    长椅坐板
bench_back.png     64×64    长椅靠背
board_post.png     64×64    公告板柱子
board.png          64×64    公告板板面
board_paper.png    64×64    公告板上的纸
lamp_post.png      64×64    路灯灯柱
lamp_head.png      64×64    路灯灯头（夜里发亮）
burrow_rim.png    128×128   地洞洞口边
burrow_hole.png   128×128   地洞的洞
burrow_inner.png  128×128   地洞深处
burrow_stone.png   64×64    地道旁的小石头
highlight.png      64×64    高亮底衬（食物）
highlight_rect.png 64×64    九宫格，高亮底衬（木箱）

—— 开始界面（不放就程序绘制）——
menu_background.png  1920×1080  整屏背景
menu_panel.png        128×128   九宫格，面板底板
menu_button.png       128×128   九宫格，所有按钮共用的底图
```

**音频 —— 放进 `Assets/Resources/AudioOverride/`**

```
eat.wav  grow.wav  pickup.wav  drop.wav  step.wav  ui_click.wav   （单声道，峰值 -6 ~ -3 dBFS）
bgm.ogg  bgm_menu.ogg                                            （无缝循环，首尾无静音/无淡入淡出）
```

> 可以**只交其中一部分**（几个 key 也行）：先交 `ground / bug_head / house_roof / crate / tree_canopy`
> 就能看出整体风格，其余的慢慢补 —— 没交的 key 继续用程序化美术，互不影响。
> 交付时建议附一句「跑起来后 Console 里那行 `[ArtOverride] … / [AudioOverride] …` 的截图」，能立刻确认全部对上了。

---

## 4. 常见问题 / 排错

| 现象 | 原因 / 处理 |
| --- | --- |
| 图片放进去了但没变化 | Console 看 `没对上 key（名字写错了？）：` 列表 → 按 1.2 节改名；确认放在 `Assets/Resources/ArtOverride/`（**不是** `Assets/Sprites/`） |
| 只换了我想换的那一个物件？ | 本来就是这样：一图只换一个 key。若发现别的物件也跟着变了，说明那个 key 的名字写成了它的名字 |
| 图片的角被拉变形了 | 那个 key 不是九宫格（或者你交的图圆角比例和原素材差太多）→ 按 1.3 节给足四角不变形区，或手工调该图的 Sprite Editor 边框 |
| 我交的彩色图变暗/变绿了 | 这个 key 会被代码染色（见 1.2 节末尾的清单，如村民身子、按钮）→ 改成白色 / 浅灰的形状图 |
| 替换后物件位置/大小不对 | 图片四周没留透明边距（锚点是中心）→ 把内容居中重画；或手工调该图片的 `Pixels Per Unit` |
| 草地/道路接缝明显 | `ground` / `road` 没有做到无缝平铺（左右上下都要接得上），用 2 的幂尺寸重画 |
| 角色被地面贴图盖住 | 这是程序里排序层的事，不是素材问题 —— 排查排序层，别改素材 |
| 有音频但完全没声音 | ① 开始界面的「音量」是不是 0；② Console 有没有 `没对上 key`；③ 确认 ogg/wav 文件本身能播放 |
| 脚步声糊成一片 | `step` 音频太长/有尾音 → 换成 0.05–0.15 s 的干脆脚步 |
| BGM 一到循环处就"喘一口气" | 素材带了淡入淡出或头尾静音 → 重新导出成无缝循环（见 2.4） |
| 音效之间音量不齐 | 素材电平没对齐 → 统一压到 −6 ~ −3 dBFS（频繁音效再低些） |
| 想整体关掉音频/美术替换 | 把对应目录里的文件删掉即可，不需要改代码（开始界面会回到程序绘制的渐变背景与圆角按钮） |

---

## 5. 附：实现位置与可调参数（给程序看）

| 内容 | 位置 |
| --- | --- |
| 图片 key 表（key → 原素材 / 是否染色 / 是否九宫格）+ 载入逻辑 | `Assets/Scripts/ArtOverride.cs`（key 常量见同文件里的 `ArtKeys`） |
| 场景里手工摆的精灵标 key | `Assets/Scripts/ArtSlot.cs`（挂在 `BugScene` 的 `Bug/Head`、`MoveMarker`、`Environment/Ground` 上） |
| 村庄物件的 key（生成时套用） | `Assets/Scripts/VillageGenerator.cs` 的 `AddRect` / `AddDisc` / `AddSlice` 调用（每个调用末尾的 `key:`） |
| 食物 / 可交互物品的内容目录（加新东西改这里） | `Assets/Scripts/FoodCatalog.cs` / `Assets/Scripts/ItemCatalog.cs`；生成规则 `Assets/Scripts/SpawnKit.cs` |
| 高亮底衬的 key | `Assets/Scripts/Highlighter.cs`（`Setup(key, 程序化精灵)`） |
| 开始界面背景 / 面板 / 按钮的图片与程序绘制兜底 | `Assets/Scripts/MenuArt.cs`（挂在 `MainMenu.scene` 的 `Canvas` 上） |
| 图片启动套用（场景精灵那部分） | `Assets/Scripts/ArtOverrideApplier.cs`（挂在 `BugScene/GameDirector` 上） |
| 图片导入设置自动配置（PPU + 九宫格边框） | `Assets/Scripts/Editor/ArtOverridePostprocessor.cs` |
| 图片目录内详细说明 | `Assets/Resources/ArtOverride/README.md` |
| 音频 key 别名表 / 载入逻辑 | `Assets/Scripts/AudioOverride.cs`（key 常量见同文件里的 `AudioKeys`） |
| 音频播放器（音效 + BGM 切场景） | `Assets/Scripts/AudioOverridePlayer.cs` |
| 脚步声触发 | `Assets/Scripts/BugFootsteps.cs`（脚步间隔 `strideLength` 0.6、最短间隔 0.09 s） |
| 音频导入设置自动配置 | `Assets/Scripts/Editor/AudioOverridePostprocessor.cs` |
| 音效触发点 | `BugEat.cs`（吃 / 长大）、`DragController.cs`（拾取 / 放下）、`MainMenu.cs`（UI 点击） |
| 全局音量 | `Assets/Scripts/GameSettings.cs`（`AudioListener.volume` + `PlayerPrefs: game.volume`） |

**改 / 加图片 key**：在 `ArtKeys` 里加一个常量、在 `ArtOverride.Slots` 里加一行（写清原素材、是否染色、是否九宫格），
然后在生成物件的地方把 key 传进 `AddRect` / `AddDisc` / `AddSlice`（或给场景对象挂 `ArtSlot`）即可；
最后同步本文档 1.2 节的表和 `Assets/Resources/ArtOverride/README.md`。

**加一种新的食物 / 可交互物品**（比如「蘑菇」「木桶」）：
代码那边是**写一个定义 + Register 一行**（`FoodCatalog` / `ItemCatalog`，细节见 `Docs/DevLog/Spec.md` 第 4.5 节，
含生成权重、每区块数量、几率、村庄/野外限定、能不能吃、能不能搬）；
美术这边给它起一个**新的 key**（例如 `mushroom.png`），照上面「改 / 加图片 key」的三步同步。
新物件的吃 / 捡 / 高亮 / 悬浮窗都是自动接上的，不需要改别的脚本。

改音频 key 名字、加新 key（比如以后的攻击音、天气环境音）：在 `AudioOverride.AliasSource` 里加一行别名、
在 `AudioKeys` 里加一个常量，然后在需要的地方调 `AudioOverridePlayer.Play(AudioKeys.xxx)` 即可。

> 备注：本项目账号的 Tuanjie AI 资产生成（材质 / 天空盒 / 3D 等）因积分不足不可用，
> 所以图片与音频都走「人工提供 + 自动替换」这套方案，这也是这份文档存在的原因。
