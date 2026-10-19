#!/usr/bin/env python3
"""Write docs/village-blocks.md: every Minecraft block in the village pieces and its Survivalcraft mapping.

Usage: write_block_doc.py <client.jar> <out.md>
"""
import collections
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [HERE, os.path.dirname(HERE)]
import build_villages as bv     # noqa: E402
import village_blocks as vb     # noqa: E402

NOTES = {
    'grass_path': '生存战争没有草径；碎石最像路，且不会像泥土那样被草长满',
    'spruce_planks': '生存战争木板本身就和云杉木板一样深（贴图平均色距离 18）',
    'oak_planks': '调色板没有中间暖色，涂任何颜色都比不涂更远；比原版深',
    'acacia_planks': '调色板调不出金合欢的橙色，只能用木板（比原版暗、偏棕）',
    'smooth_sandstone': '涂浅黄（4）：原版平滑砂岩比生存战争砂岩亮', 'cut_sandstone': '同上',
    'white_terracotta': '涂橙粉（6）的大理石：平滑、偏粉米色',
    'yellow_terracotta': '调色板没有饱和黄，按色相取浅黄砂岩（4）',
    'orange_terracotta': '砖块（红褐）最接近', 'red_terracotta': '砖块', 'terracotta': '砖块',
    'snow_block': '白色黏土：最接近的白色，且不会融化', 'snow': '1~7 层用薄雪，8 层用白色黏土',
    'packed_ice': '浅蓝黏土：冰块会被天气融化', 'blue_ice': '浅蓝大理石',
    'diorite': '大理石（白色石头）', 'smooth_stone': '涂浅灰（7）的花岗岩，色差为 0',
    'smooth_stone_slab': '涂浅灰的石台阶', 'hay_block': '没有干草块；涂浅黄的木板',
    'bookshelf': '没有书架；木板（颜色几乎一样）', 'white_wool': '白色大理石（色差 14）',
    'yellow_wool': '浅黄黏土', 'light_gray_wool': '黏土本色',
    'glass_pane': '窗户方块（整格，不是薄板）', 'farmland': '湿润施肥的耕地，黑麦不会退化',
    'wheat': '黑麦，保留生长阶段', 'bell': '没有钟', 'cauldron': '没有炼药锅', 'brewing_stand': '没有酿造台',
    'potted_cactus': '没有花盆；放植物在砂岩上会掉落', 'sea_pickle': '没有对应',
    'lectern': '没有讲台；立牌', 'composter': '没有堆肥桶；木板', 'grindstone': '没有砂轮；石台阶',
    'stonecutter': '没有切石机；石台阶', 'loom': '工作台', 'cartography_table': '工作台',
    'fletching_table': '工作台', 'smithing_table': '工作台',
    'brown_wall_banner': '没有旗帜；涂棕色的墙上木牌', 'melon_stem': '没有西瓜；带藤的小南瓜',
    'pumpkin_stem': '同上', 'melon': '南瓜', 'lantern': '柳条灯', 'grass': '高草', 'tall_grass': '高草',
    'large_fern': '高草', 'fern': '高草', 'mossy_cobblestone': '没有苔石；圆石', 'iron_bars': '铁栅栏',
    'barrel': '箱子（填战利品）', 'chest': '箱子（填战利品）',
}


def note(n):
    if n in NOTES:
        return NOTES[n]
    if n.startswith('potted_'):
        return '没有花盆'
    if n.endswith('_bed'):
        return '没有床；同色地毯（两格）'
    if n.endswith('_trapdoor'):
        return '木活板门，朝向/上下/开关照搬'
    if n.endswith('_door'):
        return '木门，朝向/开关/门轴照搬'
    if 'glazed_terracotta' in n:
        return '涂色砖块/黏土，按色相取'
    return ''


def main():
    jar, out = sys.argv[1], sys.argv[2]
    source = bv.TemplateSource(jar)
    pools = json.load(open(os.path.join(HERE, 'pools.json'), encoding='utf-8'))
    locations = sorted({e['location'] for p in pools.values() for e in p['elements'] if e.get('location')})
    counts, sample, styles = collections.Counter(), {}, collections.defaultdict(set)
    for loc in locations:
        for pos, (name, props, nbt) in bv.final_blocks(source(loc)).items():
            if name in bv.EMPTY:
                continue
            n = name.removeprefix('minecraft:')
            counts[n] += 1
            styles[n].add(loc.split('/')[1])
            if n not in sample or props.get('type') == 'bottom':
                sample[n] = props
    lines = ['# 村庄方块对照表', '',
             f'五种风格全部 {len(locations)} 个村庄件里出现的每种 Minecraft 方块，按出现次数排序。',
             '颜色依据：MC 原版贴图平均色 vs 生存战争贴图平均色（可涂色方块按默认调色板 16 色逐一相乘），',
             '`tools/village/compare_colors.py --table` 可重算；逐栋对比图用 `--sheets` 生成。',
             '形状：除表中"没有"的方块变空气、床变地毯外，每格都有对应方块（对比图每栋丢失 0~2 格）。',
             '本文件由 `tools/village/write_block_doc.py` 生成。', '',
             '| MC 方块 | 次数 | 风格 | 生存战争方块 | data | 说明 |', '|---|---:|---|---|---:|---|']
    for n, c in sorted(counts.items(), key=lambda kv: (-kv[1], kv[0])):
        block, data = vb.map_block('minecraft:' + n, sample.get(n, {}), {})
        lines.append(f"| {n} | {c} | {','.join(sorted(styles[n]))} | {block} | {data} | {note(n)} |")
    lines += ['', '朝向类方块（楼梯、梯子、火把、门、活板门、按钮、栅栏门、箱子）的 data 随朝向变化，表里只列一个样例；',
              '规则见 `tools/convert_nbt.py` 顶部 FACE/LOOK 注释。', '', '## 已知差距', '',
              '- **木材颜色**：生存战争木板只有一种颜色（接近云杉），调色板也调不出橡木的浅黄、金合欢的橙色。'
              '平原村比原版深，热带草原村的橙色屋顶变成棕色。',
              '- **陶瓦**：黄色、浅绿色陶瓦在调色板里没有接近色，按色相取了浅黄、深绿。',
              '- **街道**：草径变碎石（灰色）。',
              '- **薄方块**：玻璃板变成整格窗户；床变成地毯；花盆、钟、炼药锅、酿造台没有。',
              '- **宝箱**：MC 只有少数职业房有箱子；这里每栋封闭的房子都补了一个（农田、畜栏、广场除外），战利品按职业选。']
    with open(out, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    print(f'{len(counts)} blocks -> {out}')


if __name__ == '__main__':
    main()
