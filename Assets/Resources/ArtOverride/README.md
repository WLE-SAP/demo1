# 图片素材上传文件夹

把 PNG 拖进这个文件夹（**子目录随便建**，会递归扫描），游戏会按 key 自动替换对应物件；
**不放文件 = 全部保持程序化美术（默认状态）**。

| 子目录 | 放什么 |
| --- | --- |
| `character/` | 整身村民（`man1`~`man7`、`woman1`~`woman4` → 都归到 `villager`） |
| `emoji/` | 村民头顶的表情（`angry1`、`bulb`、`dizzy`… → `emoji_*`） |
| `Environment/` | 自然景物（`tree1`~`tree 4`、`bush1/2`、`stone1`~`stone7`） |
| `Structure/` | 建筑（`house1`~`house6`、`cottage1`、`castle1/2`、`medievalStructure_*`、`windmill1`、`status1`） |
| `Tile/` | 地面与道路（`grass*`、`sand*`、`gravel*`、`path*`、`cross`、`corner-*`、`t-intersection-*`、`roadhead-*`） |

> 子目录只是给人看的分类，**不影响匹配**；匹配只看文件名。

**每个 key 要哪些图、多大、什么要求 → 看项目根目录 `README.md` 的 1.2 节**（那是唯一权威清单）。
这里只讲三件容易踩的事：

**① 文件名 = key**：比较时忽略大小写 / 下划线 / 短横线 / 空格，结尾的**数字是变体号**。
不是正式 key 的名字先去 `Assets/Scripts/ArtOverride.cs` 的 `Aliases` 别名表里找（`house1` → `house_apartment`、
`stone3` → `rock_large`、`path1` → `road_straight_v`…），都没命中才会被点名列进 Console。
**能改成正式 key 就改**。

**② 同一个 key 放多张图 = 变体**（`tree1`~`tree4`、`sand1`~`sand4`、`man1`~`man7`、`angry1/angry2`）：
程序按结尾数字从小到大排好，运行时按地貌权重或位置确定性随机挑一张。
只放一张 = 全场都用这一张。

**③ 有些 key 会被程序自动「裁边」**（`tree`、`bush`、`rock_*`、`villager`、`castle`、`house_extra`、
八个整栋建筑 key）：导入时按不透明内容裁掉透明边，运行时按内容比例装进占地、底边贴地 ——
所以**把物体画在 128×128 画布正中也没关系**。反过来说，**表情（`emoji_*`）和路口瓦片不裁边**，
请把它们画满画布。

格式要求（摘要，完整版见项目根目录 `README.md` 的 1.1 / 1.3 节）：

- PNG + **真透明背景**、直通 alpha（不预乘）、sRGB 8 位；任意分辨率 ≤ 2048，**基准 64 像素 = 1 世界单位**；
- 命名全小写英文 + 下划线；**锚点是图片中心**；
- 地面 / 路面类（`ground*` / `road*` / 直路瓦片）**必须无缝平铺**，建议 256/512；
- 九宫格 key（墙 / 屋顶 / 木箱 / 面板…）四角要留够不变形区。

放进来的文件**不需要手改 Import Settings**：导入时会自动设成 Sprite、按 key 反推 Pixels Per Unit、
平铺类设成 Repeat、整图类自动裁边。**已经导入过的图改了 `whole` 之类的标记后，要清掉它 `.meta` 里的
`userData`（= `artoverride`）再重导一次**，否则旧设置会留着。

运行后看 Console：

- `[ArtOverride] 从 Resources/ArtOverride 载入 N 张图片（其中 M 个 key 有多个变体…）` → 生效；
- `xxx 不是正式 key，按别名当成 yyy（想换映射就改 ArtOverride.Aliases…）` → 走的别名表，能用，但建议改名；
- `没对上 key（名字写错了？）：xxx` → 文件名没对上任何 key，照根 README 的表改名。

想恢复默认：把这个文件夹里的图片删掉（或移出 `Resources/`）即可。
