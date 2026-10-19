# 村庄方块对照表

五种风格全部 267 个村庄件里出现的每种 Minecraft 方块，按出现次数排序。
颜色依据：MC 原版贴图平均色 vs 生存战争贴图平均色（可涂色方块按默认调色板 16 色逐一相乘），
`tools/village/compare_colors.py --table` 可重算；逐栋对比图用 `--sheets` 生成。
形状：除表中"没有"的方块变空气、床变地毯外，每格都有对应方块（对比图每栋丢失 0~2 格）。
本文件由 `tools/village/write_block_doc.py` 生成。

| MC 方块 | 次数 | 风格 | 生存战争方块 | data | 说明 |
|---|---:|---|---|---:|---|
| grass_path | 3755 | plains,savanna,snowy,taiga | GravelBlock | 0 | 生存战争没有草径；碎石最像路，且不会像泥土那样被草长满 |
| cobblestone | 3692 | common,desert,plains,savanna,snowy,taiga | CobblestoneBlock | 0 |  |
| grass_block | 2989 | desert,plains,savanna,snowy,taiga | GrassBlock | 0 |  |
| smooth_sandstone | 2722 | desert | SandstoneBlock | 9 | 涂浅黄（4）：原版平滑砂岩比生存战争砂岩亮 |
| dirt | 2177 | desert,plains,savanna,snowy,taiga | DirtBlock | 0 |  |
| spruce_planks | 2083 | snowy,taiga | PlanksBlock | 0 | 生存战争木板本身就和云杉木板一样深（贴图平均色距离 18） |
| oak_stairs | 1884 | plains | WoodenStairsBlock | 2 |  |
| spruce_log | 1638 | taiga | SpruceWoodBlock | 1 |  |
| oak_planks | 1496 | plains | PlanksBlock | 0 | 调色板没有中间暖色，涂任何颜色都比不涂更远；比原版深 |
| acacia_stairs | 1205 | savanna | WoodenStairsBlock | 2 |  |
| sand | 1164 | desert,taiga | SandBlock | 0 |  |
| snow | 1088 | snowy | SnowBlock | 0 | 1~7 层用薄雪，8 层用白色黏土 |
| acacia_planks | 1022 | savanna | PlanksBlock | 0 | 调色板调不出金合欢的橙色，只能用木板（比原版暗、偏棕） |
| oak_log | 975 | plains | OakWoodBlock | 0 |  |
| cut_sandstone | 823 | desert | SandstoneBlock | 9 | 同上 |
| stripped_spruce_log | 783 | snowy | SpruceWoodBlock | 0 |  |
| acacia_log | 717 | savanna | MimosaWoodBlock | 0 |  |
| stripped_spruce_wood | 691 | snowy | SpruceWoodBlock | 0 |  |
| smooth_sandstone_slab | 569 | desert | SandstoneSlabBlock | 18 |  |
| farmland | 539 | desert,plains,savanna,snowy,taiga | SoilBlock | 7 | 湿润施肥的耕地，黑麦不会退化 |
| grass | 522 | plains,savanna,snowy | TallGrassBlock | 0 | 高草 |
| wheat | 489 | desert,plains,savanna,snowy,taiga | RyeBlock | 7 | 黑麦，保留生长阶段 |
| wall_torch | 417 | desert,plains,savanna,snowy,taiga | TorchBlock | 2 |  |
| acacia_fence | 410 | savanna | WoodenFenceBlock | 0 |  |
| spruce_slab | 390 | snowy,taiga | WoodenSlabBlock | 0 |  |
| acacia_wood | 377 | savanna | MimosaWoodBlock | 2 |  |
| snow_block | 352 | snowy | ClayBlock | 1 | 白色黏土：最接近的白色，且不会融化 |
| oak_fence | 349 | plains | WoodenFenceBlock | 0 |  |
| glass_pane | 325 | plains,savanna,snowy,taiga | WindowBlock | 0 | 窗户方块（整格，不是薄板） |
| spruce_trapdoor | 308 | taiga | WoodenTrapdoorBlock | 7 | 木活板门，朝向/上下/开关照搬 |
| water | 292 | common,desert,plains,savanna,snowy,taiga | WaterBlock | 0 |  |
| spruce_fence | 266 | snowy,taiga | WoodenFenceBlock | 0 |  |
| acacia_slab | 240 | savanna | WoodenSlabBlock | 0 |  |
| yellow_terracotta | 211 | savanna | SandstoneBlock | 9 | 调色板没有饱和黄，按色相取浅黄砂岩（4） |
| sandstone_slab | 208 | desert | SandstoneSlabBlock | 0 |  |
| spruce_stairs | 190 | snowy,taiga | WoodenStairsBlock | 2 |  |
| white_terracotta | 188 | plains | MarbleBlock | 13 | 涂橙粉（6）的大理石：平滑、偏粉米色 |
| sandstone_wall | 168 | desert | StoneFenceBlock | 144 |  |
| smooth_sandstone_stairs | 164 | desert | SandstoneStairsBlock | 290 |  |
| cobblestone_stairs | 144 | plains,taiga | CobblestoneStairsBlock | 3 |  |
| torch | 142 | desert,plains,savanna,snowy,taiga | TorchBlock | 4 |  |
| spruce_door | 126 | snowy,taiga | WoodenDoorBlock | 3 | 木门，朝向/开关/门轴照搬 |
| diorite | 104 | snowy | MarbleBlock | 0 | 大理石（白色石头） |
| cobblestone_wall | 100 | plains,savanna,snowy,taiga | StoneFenceBlock | 0 |  |
| orange_terracotta | 100 | savanna | BricksBlock | 0 | 砖块（红褐）最接近 |
| packed_ice | 100 | snowy | ClayBlock | 7 | 浅蓝黏土：冰块会被天气融化 |
| oak_slab | 99 | plains | WoodenSlabBlock | 0 |  |
| oak_door | 86 | plains | WoodenDoorBlock | 11 | 木门，朝向/开关/门轴照搬 |
| terracotta | 81 | desert,plains | BricksBlock | 0 | 砖块 |
| acacia_door | 76 | savanna | WoodenDoorBlock | 1 | 木门，朝向/开关/门轴照搬 |
| tall_grass | 76 | plains,savanna | TallGrassBlock | 0 | 高草 |
| smooth_stone_slab | 72 | desert,plains,savanna,snowy,taiga | StoneSlabBlock | 30 | 涂浅灰的石台阶 |
| large_fern | 68 | taiga | TallGrassBlock | 0 | 高草 |
| jungle_door | 64 | desert | WoodenDoorBlock | 1 | 木门，朝向/开关/门轴照搬 |
| smooth_stone | 59 | plains,savanna,snowy | GraniteBlock | 15 | 涂浅灰（7）的花岗岩，色差为 0 |
| brown_wall_banner | 56 | savanna | WoodenAttachedSignBlock | 102 | 没有旗帜；涂棕色的墙上木牌 |
| jungle_button | 56 | desert | ButtonBlock | 3 |  |
| stripped_oak_log | 56 | plains | OakWoodBlock | 0 |  |
| lantern | 53 | snowy | WickerLampBlock | 0 | 柳条灯 |
| bookshelf | 50 | desert,plains,savanna,snowy,taiga | PlanksBlock | 0 | 没有书架；木板（颜色几乎一样） |
| oak_leaves | 49 | plains | OakLeavesBlock | 0 |  |
| hay_block | 42 | desert,plains,taiga | PlanksBlock | 9 | 没有干草块；涂浅黄的木板 |
| chest | 41 | desert,plains,savanna,snowy,taiga | ChestBlock | 0 | 箱子（填战利品） |
| potted_cactus | 41 | desert | AirBlock | 0 | 没有花盆；放植物在砂岩上会掉落 |
| red_terracotta | 40 | savanna | BricksBlock | 0 | 砖块 |
| blue_ice | 38 | snowy | MarbleBlock | 7 | 浅蓝大理石 |
| white_bed | 32 | plains,snowy | CarpetBlock | 0 | 没有床；同色地毯（两格） |
| melon_stem | 31 | savanna | PumpkinBlock | 7 | 没有西瓜；带藤的小南瓜 |
| mossy_cobblestone | 30 | plains,taiga | CobblestoneBlock | 0 | 没有苔石；圆石 |
| yellow_wool | 28 | plains | ClayBlock | 9 | 浅黄黏土 |
| poppy | 27 | plains,savanna,taiga | RedFlowerBlock | 0 |  |
| white_wool | 27 | plains | MarbleBlock | 1 | 白色大理石（色差 14） |
| blue_bed | 26 | snowy,taiga | CarpetBlock | 11 | 没有床；同色地毯（两格） |
| ladder | 26 | desert,plains,taiga | WoodenLadderBlock | 2 |  |
| iron_bars | 25 | desert,plains,savanna,snowy | IronFenceBlock | 0 | 铁栅栏 |
| oak_trapdoor | 23 | plains | WoodenTrapdoorBlock | 7 | 木活板门，朝向/上下/开关照搬 |
| spruce_wood | 23 | taiga | SpruceWoodBlock | 1 |  |
| gravel | 21 | taiga | GravelBlock | 0 |  |
| bell | 20 | desert,plains,savanna,snowy,taiga | AirBlock | 0 | 没有钟 |
| orange_bed | 20 | savanna | CarpetBlock | 6 | 没有床；同色地毯（两格） |
| fern | 19 | taiga | TallGrassBlock | 0 | 高草 |
| yellow_stained_glass_pane | 19 | plains,savanna | WindowBlock | 0 |  |
| furnace | 17 | desert,plains,snowy,taiga | FurnaceBlock | 2 |  |
| pumpkin_stem | 17 | taiga | PumpkinBlock | 1 | 同上 |
| barrel | 16 | desert,plains,savanna,snowy,taiga | ChestBlock | 0 | 箱子（填战利品） |
| composter | 16 | desert,plains,savanna,snowy,taiga | PlanksBlock | 0 | 没有堆肥桶；木板 |
| pumpkin | 16 | taiga | PumpkinBlock | 0 |  |
| purple_carpet | 16 | taiga | CarpetBlock | 10 |  |
| clay | 15 | desert,plains,savanna,taiga | ClayBlock | 0 |  |
| diorite_wall | 15 | snowy | StoneFenceBlock | 16 |  |
| purple_bed | 14 | taiga | CarpetBlock | 10 | 没有床；同色地毯（两格） |
| white_stained_glass_pane | 14 | plains | WindowBlock | 0 |  |
| white_glazed_terracotta | 13 | desert | BricksBlock | 3 | 涂色砖块/黏土，按色相取 |
| diorite_stairs | 12 | snowy | MarbleStairsBlock | 2 |  |
| light_blue_glazed_terracotta | 12 | desert | BricksBlock | 7 | 涂色砖块/黏土，按色相取 |
| lime_bed | 12 | desert | CarpetBlock | 13 | 没有床；同色地毯（两格） |
| white_carpet | 12 | desert,plains,savanna,taiga | CarpetBlock | 0 |  |
| cauldron | 11 | desert,plains,savanna,snowy,taiga | AirBlock | 0 | 没有炼药锅 |
| stripped_acacia_log | 11 | savanna | MimosaWoodBlock | 0 |  |
| grindstone | 10 | desert,plains,savanna,snowy,taiga | StoneSlabBlock | 0 | 没有砂轮；石台阶 |
| potted_dead_bush | 10 | desert | AirBlock | 0 | 没有花盆 |
| yellow_bed | 10 | plains | CarpetBlock | 4 | 没有床；同色地毯（两格） |
| crafting_table | 9 | desert,savanna,taiga | CraftingTableBlock | 0 |  |
| light_gray_wool | 9 | snowy | ClayBlock | 0 | 黏土本色 |
| loom | 9 | desert,plains,savanna,snowy,taiga | CraftingTableBlock | 0 | 工作台 |
| oak_pressure_plate | 9 | plains | PressurePlateBlock | 8 |  |
| red_carpet | 9 | savanna,snowy,taiga | CarpetBlock | 14 |  |
| blast_furnace | 8 | desert,plains,savanna,snowy,taiga | FurnaceBlock | 3 |  |
| brewing_stand | 8 | desert,plains,savanna,snowy,taiga | AirBlock | 0 | 没有酿造台 |
| dandelion | 8 | plains | WhiteFlowerBlock | 0 |  |
| green_bed | 8 | desert | CarpetBlock | 13 | 没有床；同色地毯（两格） |
| red_bed | 8 | savanna,snowy | CarpetBlock | 14 | 没有床；同色地毯（两格） |
| smoker | 8 | desert,plains,savanna,snowy,taiga | FurnaceBlock | 0 |  |
| acacia_pressure_plate | 7 | savanna | PressurePlateBlock | 8 |  |
| granite | 7 | desert | BricksBlock | 0 |  |
| lava | 7 | desert,plains,savanna,snowy | MagmaBlock | 0 |  |
| lectern | 7 | desert,plains,savanna,snowy,taiga | WoodenPostedSignBlock | 2 | 没有讲台；立牌 |
| yellow_carpet | 7 | plains | CarpetBlock | 4 |  |
| stonecutter | 6 | desert,plains,savanna,snowy,taiga | StoneSlabBlock | 0 | 没有切石机；石台阶 |
| yellow_glazed_terracotta | 6 | savanna | BricksBlock | 9 | 涂色砖块/黏土，按色相取 |
| campfire | 5 | taiga | CampfireBlock | 0 |  |
| cartography_table | 5 | desert,plains,savanna,snowy,taiga | CraftingTableBlock | 0 | 工作台 |
| fletching_table | 5 | desert,plains,savanna,snowy,taiga | CraftingTableBlock | 0 | 工作台 |
| oak_fence_gate | 5 | plains | WoodenFenceGateBlock | 0 |  |
| sandstone | 5 | desert | SandstoneBlock | 0 |  |
| smithing_table | 5 | desert,plains,savanna,snowy,taiga | CraftingTableBlock | 0 | 工作台 |
| blue_carpet | 4 | snowy | CarpetBlock | 11 |  |
| cyan_bed | 4 | desert | CarpetBlock | 9 | 没有床；同色地毯（两格） |
| diorite_slab | 4 | snowy | MarbleBlock | 0 |  |
| green_carpet | 4 | desert,plains | CarpetBlock | 13 |  |
| jungle_fence_gate | 4 | desert | WoodenFenceGateBlock | 2 |  |
| jungle_trapdoor | 4 | desert | WoodenTrapdoorBlock | 7 | 木活板门，朝向/上下/开关照搬 |
| lime_glazed_terracotta | 4 | desert | ClayBlock | 11 | 涂色砖块/黏土，按色相取 |
| orange_stained_glass_pane | 4 | savanna | WindowBlock | 0 |  |
| acacia_fence_gate | 3 | savanna | WoodenFenceGateBlock | 2 |  |
| cactus | 3 | desert | CactusBlock | 0 |  |
| lime_terracotta | 3 | desert | ClayBlock | 27 |  |
| potted_dandelion | 3 | plains,savanna | AirBlock | 0 | 没有花盆 |
| spruce_fence_gate | 3 | snowy,taiga | WoodenFenceGateBlock | 3 |  |
| spruce_pressure_plate | 3 | taiga | PressurePlateBlock | 8 |  |
| stone_button | 3 | desert | ButtonBlock | 3 |  |
| acacia_sapling | 2 | savanna | SaplingBlock | 4 |  |
| bricks | 2 | plains | BricksBlock | 0 |  |
| cobblestone_slab | 2 | plains | CobblestoneSlabBlock | 0 |  |
| granite_stairs | 2 | desert | BrickStairsBlock | 2 |  |
| granite_wall | 2 | desert | StoneFenceBlock | 0 |  |
| jungle_fence | 2 | desert | WoodenFenceBlock | 0 |  |
| orange_carpet | 2 | savanna | CarpetBlock | 6 |  |
| sandstone_stairs | 2 | desert | SandstoneStairsBlock | 0 |  |
| spruce_wall_sign | 2 | taiga | WoodenAttachedSignBlock | 2 |  |
| chiseled_sandstone | 1 | desert | SandstoneBlock | 9 |  |
| dead_bush | 1 | desert | DryBushBlock | 0 |  |
| lime_carpet | 1 | desert | CarpetBlock | 13 |  |
| melon | 1 | savanna | PumpkinBlock | 0 | 南瓜 |
| orange_glazed_terracotta | 1 | savanna | ClayBlock | 0 | 涂色砖块/黏土，按色相取 |
| oxeye_daisy | 1 | plains | WhiteFlowerBlock | 0 |  |
| potted_poppy | 1 | taiga | AirBlock | 0 | 没有花盆 |
| potted_spruce_sapling | 1 | taiga | AirBlock | 0 | 没有花盆 |
| sea_pickle | 1 | desert | AirBlock | 0 | 没有对应 |
| stone_bricks | 1 | snowy | StoneBrickBlock | 0 |  |
| stripped_oak_wood | 1 | plains | OakWoodBlock | 0 |  |

朝向类方块（楼梯、梯子、火把、门、活板门、按钮、栅栏门、箱子）的 data 随朝向变化，表里只列一个样例；
规则见 `tools/convert_nbt.py` 顶部 FACE/LOOK 注释。

## 已知差距

- **木材颜色**：生存战争木板只有一种颜色（接近云杉），调色板也调不出橡木的浅黄、金合欢的橙色。平原村比原版深，热带草原村的橙色屋顶变成棕色。
- **陶瓦**：黄色、浅绿色陶瓦在调色板里没有接近色，按色相取了浅黄、深绿。
- **街道**：草径变碎石（灰色）。
- **薄方块**：玻璃板变成整格窗户；床变成地毯；花盆、钟、炼药锅、酿造台没有。
- **宝箱**：MC 只有少数职业房有箱子；这里每栋封闭的房子都补了一个（农田、畜栏、广场除外），战利品按职业选。
