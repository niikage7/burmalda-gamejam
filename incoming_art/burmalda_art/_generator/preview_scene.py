"""Превью: демо-карта из готовых тайлов, травы и танков.
Целый и повреждённый кирпич считаются одним материалом (как в игре с SiblingRuleTile).

    python3 preview_scene.py <папка incoming_art> <out.png>
"""
import os, sys, random
from PIL import Image

ART, OUT = sys.argv[1], sys.argv[2]
N, NE, E, SE, S_, SW, W, NW = 1, 2, 4, 8, 16, 32, 64, 128
DIRS = {N: (0, -1), NE: (1, -1), E: (1, 0), SE: (1, 1), S_: (0, 1), SW: (-1, 1), W: (-1, 0), NW: (-1, -1)}
MAP = [
    "...............",
    ".WWWW...KKKK.B.",
    ".WWWWW..KD.K.BB",
    "..WWW...KKDK...",
    "..W.....K......",
    "....BBB...DKD..",
    ".S..BBBB..K.K.S",
    ".S...BB...KDK..",
    ".SSS.....KKDKK.",
]
MAT = {'W': ('Water', 'water_{:03d}.png'), 'B': ('Bush', 'bush_{:03d}.png'),
       'S': ('Steel', 'steel_{:03d}.png'), 'K': ('tiles', 'brick_blob_{:03d}.png'),
       'D': ('brick_damaged', 'brick_damaged_blob_{:03d}.png')}
TANKS = [('blue', 6.0, 1.5, 0), ('red', 11.5, 3.3, 180), ('green', 6.3, 6.7, 90), ('yellow', 3.0, 3.6, -45)]


def canonical(m):
    if not (m & N and m & E): m &= ~NE
    if not (m & S_ and m & E): m &= ~SE
    if not (m & S_ and m & W): m &= ~SW
    if not (m & N and m & W): m &= ~NW
    return m


def family(c):
    return 'brick' if c in 'KD' else c


h, w = len(MAP), len(MAP[0])
img = Image.new('RGBA', (w * 64, h * 64))
rnd = random.Random(5)
for y in range(h):
    for x in range(w):
        tone = 'light' if (x + y) % 2 == 0 else 'dark'
        img.alpha_composite(Image.open(os.path.join(ART, 'Ground', f'grass_{tone}_{rnd.choice([0, 0, 0, 1, 2])}.png')), (x * 64, y * 64))
for y in range(h):
    for x in range(w):
        c = MAP[y][x]
        if c not in MAT:
            continue
        m = 0
        for bit, (dx, dy) in DIRS.items():
            xx, yy = x + dx, y + dy
            if 0 <= xx < w and 0 <= yy < h and family(MAP[yy][xx]) == family(c):
                m |= bit
        folder, pat = MAT[c]
        img.alpha_composite(Image.open(os.path.join(ART, folder, pat.format(canonical(m)))), (x * 64, y * 64))
for color, tx, ty, ang in TANKS:
    d = os.path.join(ART, 'tank_sprites', f'tank_{color}')
    t = Image.new('RGBA', (96, 96), (0, 0, 0, 0))
    for n in ['shadow', 'track_left_0', 'track_right_0', 'body', 'barrel', 'turret']:
        t.alpha_composite(Image.open(os.path.join(d, n + '.png')))
    img.alpha_composite(t.rotate(ang, resample=Image.BICUBIC), (int(tx * 64 - 16), int(ty * 64 - 16)))
img.save(OUT)
print('сохранено', OUT)
