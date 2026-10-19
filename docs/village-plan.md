# 村庄与村民 — 规划

玩家诉求：把《我的世界》原版的村庄和村民搬进生存战争。素材来源
`/home/dev/projects/1.16.5-Forge_36.2.34/1.16.5-Forge_36.2.34.jar`（1.16.5 + Forge）。

**进度（2026-10-20）：A 期（村庄本体，不含村民）已完成**，未经游戏内实测。实现与原规划的差异：
- 布局离线生成：`tools/village/`（`extract_pools.py` 从反编译的 `*VillagePools` 抽池子 →
  `jigsaw.py` 移植 `JigsawPlacement` → `build_villages.py` 每风格挑 8 套 9~20 栋、≤150 格的布局，
  按"件×旋转"转成 SC 方块写进 `Assets/Villages/`）。重新生成：
  `python3 tools/village/build_villages.py <1.16.5 client.jar> src/SurvivalcraftRuins/Assets/Villages --layouts 8`
- 高度：`Core/TerrainSurface.cs` 逐列复算游戏第一遍地形（测试里与游戏自身填充 18432/18432 列一致），
  房屋按 MC 规则取连接点地表；`Core/Village/VillageBuilder.cs` 清房屋体积、垫地基、街道贴地、过水搭桥。
- 气候分风格见 `VillageClimate`；中心外 56 格 5 点至少 4 点同风格。密度实测每 6.3 km² 陆地一座（5 个种子）。
- 战利品：MC 16 张表合成 12 个池（绿宝石→铁锭）。
- 工位方块/床/门等映射见 `village_blocks.py`（顺带查实旧 `convert_nbt.py` 朝向表有误）。

以下为原规划。所有"结论"都是查过 jar 和反编译的 SC 源码之后写的，
带出处；没查证的地方写明"待定"。

## 一、可行性核查（有出处）

| 问题 | 结论 | 出处 |
|---|---|---|
| 村庄件在哪 | jar 内 `data/minecraft/structures/village/` 下 **482 个 .nbt**，五套风格 plains / desert / savanna / snowy / taiga，各含 houses、streets、town_centers、villagers、zombie | 直接列表 |
| 现有转换器能读吗 | 能。`tools/convert_nbt.py` 实测 `plains_small_house_1` = 7×7×7、341 格，只有 2 种方块没映射（`oak_door`、`white_bed`） | 实跑 |
| 拼装规则在哪 | 池子权重与 jigsaw 连接写在**代码**里（`PlainVillagePools` 等），jar 内没有 `template_pool` JSON，需要反编译取 | 查过 jar |
| 战利品表 | jar 内 `loot_tables/chests/village_*.json` **16 张**（armorer / butcher / cartographer / fisher / fletcher / mason / shepherd / tannery / temple / toolsmith / weaponsmith + 五种民居） | 直接列表 |
| 村民能不能做 | 能。SC 有 `ComponentHumanModel`（人类模型 + 可换贴图 + 外衣模型），且它对非玩家实体**空安全**（`m_componentPlayer` 由 `FindComponent<ComponentPlayer>()` 取，可为 null，用到的两处都判了空） | `Game/ComponentHumanModel.cs:30,408,437` |
| 新实体模板怎么加 | `.xdb` 文件，**插件版和联机版都支持**：`ModEntity.LoadXdb` → `ModsManager.CombineDataBase` → `DatabaseManager.LoadDataBaseFromXml` | `Game/ModEntity.cs:195`、`Game/DatabaseManager.cs:42` |
| 村民贴图 | 用游戏自带的：`Assets/Textures/Creatures/HumanMale1..4`、`HumanFemale1..4`，共 8 张，**不需要新美术资源** | `Content.zip` |
| 换装 | `ComponentClothing.SetClothesInitial(slot, clothes)` 可在生成时直接给 NPC 穿衣，职业靠现成衣物区分 | `Game/ComponentClothing.cs:154` |
| 交易界面 | SC 有完整 Dialog 体系可仿（`ListSelectionDialog`、`EditSignDialog` 等 30+ 个）；触发点用 `ModLoader.UpdateInput` 钩子 + 自己射线拾取实体 | `Game/*Dialog.cs`、`Game/ModLoader.cs:109` |
| 床 | **SC 没有床方块**（`BlocksData.txt` 里只有 `BedrockBlock`），用 `CarpetBlock` 当铺盖 | 已确认 |

## 二、已定事项

- **素材**：可以直接提取、反编译、照搬（作者已确认）。反编译产物照旧放在仓库外
  （`~/mc-src-1.16.5/`），不进 git。
- **货币**：铁锭 `IronIngotBlock`（铁矿比孔雀石稀有，作者定）。
- **贴图**：只用游戏现有的人类贴图 + 现有衣物，不新增美术资源。
- **规模**：一个村庄 8~14 栋，密度约每 6~8 km² 一个（比神殿还稀有——村庄是地标）。
- **床**：地毯当铺盖。
- **节奏**：不急，分期做，A 期先发。

## 三、A 期：村庄本体（不含村民）

1. **五套风格按温湿度落位**：沙漠村复用现成沙漠判据（温≥9 湿≤6）；雪村按低温；
   针叶林按冷湿；平原/热带草原按中间带。判据仍取游戏自己的
   `CalculateTemperature`/`CalculateHumidity`，用现成的离线复现工具实测覆盖率再定阈值。
2. **转换器补课**（`tools/convert_nbt.py`）：
   - 门：`oak_door` 等 → `WoodenDoorBlock`，要还原上下半扇与朝向 data 位（现在一律转空气）
   - 床 → 地毯
   - 工位方块：堆肥桶/木桶/讲台/烟熏炉/织布机/磨石… → SC 近似物（箱子/工作台/熔炉）或省略
   - 农田+作物 → `DirtBlock` + `RyeBlock`；路径 → `GravelBlock`
3. **新的"聚落"放置模式**（工作量大头）。现在一个结构 = 一个包围盒 + 一次坡度检查；
   村庄要改成：一个大网格掷一个村庄，村庄内**每栋房子各自找地基高度、各自平整填基座**，
   再用碎石路连通。与现有 `SeparationRadius` 打通，让危险遗迹避开村庄范围。
4. **战利品**：16 张 `village_*.json` 转成 `pools.json` 的权重格式。
5. **测试**：门可通行、地基不悬空、不落水不落陡坡、密度用真实种子离线复现实测。

## 四、B 期：村民（实体）

`.xdb` 新增实体模板，继承 SC 自己的 `AICreature`：

```
Creature ── AICreature ── LandAnimal ── Wolf ── Wolf_Gray
   │            │
   │            └── Villager（我们要加的）
   └── 组件：Spawn / CreatureSounds / Creature / Body / Locomotion / Health / OnFire / Factors
```

- `AICreature` 是**游戏自己的模板名**（`Assets/Database.xml`），不是我们发明的层：
  它给出 `Loot`、`Pilot`、`BehaviorSelector`、`EatPickableBehavior`、`Pathfinding`，
  是所有非玩家生物的共同祖先。狼、狼人、鸟全都挂在它下面。
- 村民 = `AICreature` + `ComponentHumanModel`（人类模型）+ `ComponentClothing`（换装）
  + 挑几个现成行为组件：`WalkAroundBehavior`（村里游荡）、`RunAwayBehavior`（见掠食者跑）、
  `LookAroundBehavior`、`StareBehavior`（看向玩家）。SC 现成的行为组件有 29 个，够用。
- **生成与持久化**沿用守卫那套：随村庄下发，`AutoDespawn` 在 52 格外回收、靠
  `SpawnEntityData` 重建。

## 五、C 期：职业 · 交易 · 经验

MC 的三条机制怎么落到 SC：

### 1. 职业从哪来（MC：附近工位方块决定）

SC 只有三个天然"工位"方块（工作台、熔炉、箱子），照搬 MC 的 13 种职业不现实。
采用**双轨**：

- **生成时按房子指定**（主）：我们知道自己放的是哪一栋（`plains_butcher_shop_1`
  就是屠夫），直接把职业写进村民的岗位数据。确定、零开销、绝不出错。
- **运行时找工位**（次，保留 MC 玩法）：无业村民定期扫描周围 5 格找工位方块转职，
  这样玩家在自家门口摆个熔炉也能招到铁匠。

工位映射（**提议，待细化**）：

| 职业 | SC 工位判据 | 收（给铁锭） | 卖（收铁锭） |
|---|---|---|---|
| 农民 | 3 格内有农田 + 黑麦 | 黑麦、南瓜 | 种子、面包 |
| 铁匠 | `FurnaceBlock` | 铁矿、煤 | 铁工具、铁甲 |
| 工具匠 | `CraftingTableBlock` | 木材、石料 | 镐、斧、铲 |
| 商人 | `ChestBlock` | 杂货 | 绳、箭、蜡烛 |
| 牧羊人 | `WoodenFenceBlock` 且 5 格内有牲畜 | 毛皮、皮革 | 衣物、地毯 |
| 渔夫 | 5 格内有水（SC 没有钓竿方块） | 生鱼 | 船、鱼叉 |

约 6 种职业，覆盖 SC 的实际物品面。

### 2. 交易怎么做

- 触发：`ModLoader.UpdateInput` 钩子里检测交互键 + 自己射线拾取实体，命中村民则开面板。
- 面板：仿 SC 现有 Dialog 风格自绘，列出该村民当前可用的交易条目。
- 货币：铁锭。买入价/卖出价写死在交易表里，按职业和等级取。
- **联机**：交易结算只在主机端执行，和我们现有"结构生成/箱子填充/守卫触发只在主机"
  的设计一致。

### 3. 经验与升级

- 每笔交易给该村民加经验，攒够进下一档（新手 → 学徒 → … → 大师），
  每档解锁更好的交易条目——和 MC 一个思路。
- 等级用**衣服**和名字后缀体现（换 `ComponentClothing` 的槽位），玩家一眼能看出来。
- **关键实现约束**：SC 的生物会在 52 格外被回收再重建（守卫那次已经踩过），
  所以职业、等级、经验、库存**绝不能存在实体身上**，必须存进我们自己的世界 JSON
  （`SurvivalcraftRuins.json`），key 用村民的固定岗位 id（`village:x,y,z`）——
  和现在的埋伏状态同一套机制。
- 库存按游戏天数刷新（`SubsystemGameInfo.TotalElapsedGameTime`）。

## 六、风险与未决

- **聚落放置**是本项目最大的新东西：多件独立锚定 + 道路连通 + 地形平整，
  现有 `PlacementEngine` 只支持单包围盒。需要新写并配测试。
- **僵尸村庄**（`*/zombie/` 下 176 个件）暂不做。
- **铁傀儡**：MC 用刷怪蛋方块生成，SC 没有对应生物；可选用现有生物顶替，或跳过。
- 村民的**寻路质量**（SC 的 `Pathfinding` 在建筑内部表现如何）只能进游戏看。
- 交易面板在联机客户端的表现需要实测。
