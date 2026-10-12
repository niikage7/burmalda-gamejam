"""Быстрая проверка набора: имена и комплектность, размеры и формат, прозрачные края,
стыки на случайных картах (в т.ч. целый + повреждённый кирпич), бесшовность травы.
Строгая проверка стыков — qa_world.py.

    python3 qa.py <папка incoming_art>
"""
import os, sys
import numpy as np
from PIL import Image

ART = sys.argv[1]
N, NE, E, SE, S_, SW, W, NW = 1, 2, 4, 8, 16, 32, 64, 128
DIRS = {N: (0, -1), NE: (1, -1), E: (1, 0), SE: (1, 1), S_: (0, 1), SW: (-1, 1), W: (-1, 0), NW: (-1, -1)}
SETS = {'tiles': 'brick_blob_{:03d}.png', 'brick_damaged': 'brick_damaged_blob_{:03d}.png',
        'Steel': 'steel_{:03d}.png', 'Water': 'water_{:03d}.png', 'Bush': 'bush_{:03d}.png'}
GRASS = [f'grass_{t}_{v}.png' for t in ('light', 'dark') for v in (0, 1, 2)] + ['grass_background_128.png']


def canonical(m):
    if not (m & N and m & E): m &= ~NE
    if not (m & S_ and m & E): m &= ~SE
    if not (m & S_ and m & W): m &= ~SW
    if not (m & N and m & W): m &= ~NW
    return m


CONFIGS = sorted({canonical(m) for m in range(256)})
errors = 0


def fail(msg):
    global errors
    errors += 1
    print('  ОШИБКА:', msg)


# 1. комплектность и формат
print('1. Комплектность и формат')
for folder, pat in SETS.items():
    expected = {pat.format(m) for m in CONFIGS}
    have = {f for f in os.listdir(os.path.join(ART, folder)) if f.endswith('.png')}
    if have != expected:
        fail(f'{folder}: нет {sorted(expected - have)[:5]}, лишние {sorted(have - expected)[:5]}')
    for f in sorted(have):
        im = Image.open(os.path.join(ART, folder, f))
        if im.size != (64, 64) or im.mode != 'RGBA':
            fail(f'{folder}/{f}: {im.size} {im.mode}')
have = set(os.listdir(os.path.join(ART, 'Ground')))
if have != set(GRASS):
    fail(f'Ground: нет {sorted(set(GRASS) - have)}, лишние {sorted(have - set(GRASS))}')
print('   наборы: 5 x 47 тайлов 64x64 RGBA; трава: 7 файлов')


def load(folder):
    pat = SETS[folder]
    return {m: np.asarray(Image.open(os.path.join(ART, folder, pat.format(m)))).astype(np.float32) for m in CONFIGS}


def premul(a):
    return np.concatenate([a[..., :3] * a[..., 3:4] / 255.0, a[..., 3:4]], -1)


# 2. открытые стороны прозрачны (ничего не режется по границе клетки)
print('2. Прозрачность краёв без соседа')
for folder in SETS:
    for m, a in load(folder).items():
        al = a[..., 3]
        for bit, edge in [(N, al[0, :]), (S_, al[-1, :]), (W, al[:, 0]), (E, al[:, -1])]:
            if not m & bit and edge.max() > 0:
                fail(f'{folder} {m}: край {bit} не прозрачен')
print('   ok' if errors == 0 else '   есть ошибки')

# 3. стыки на случайных картах: скачок на шве не больше перепада внутри тайлов у края
print('3. Стыки на случайных картах')
rng = np.random.default_rng(0)


def seams(tiles_a, tiles_b=None, mix=0.0, trials=300, size=10):
    worst, bad, total = 0.0, 0, 0
    for _ in range(trials):
        occ = rng.random((size, size)) < 0.55
        kind = rng.random((size, size)) < mix
        cache = {}
        for y in range(size):
            for x in range(size):
                if not occ[y, x]:
                    continue
                m = 0
                for bit, (dx, dy) in DIRS.items():
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < size and 0 <= yy < size and occ[yy, xx]:
                        m |= bit
                src = tiles_b if (tiles_b is not None and kind[y, x]) else tiles_a
                cache[(x, y)] = premul(src[canonical(m)])
        for (x, y), a in cache.items():
            for dx, dy in [(1, 0), (0, 1)]:
                b = cache.get((x + dx, y + dy))
                if b is None:
                    continue
                if dx:
                    seam = np.abs(a[:, -1] - b[:, 0]).max(-1)
                    inner = np.maximum(np.abs(a[:, -2] - a[:, -1]).max(-1), np.abs(b[:, 0] - b[:, 1]).max(-1))
                else:
                    seam = np.abs(a[-1, :] - b[0, :]).max(-1)
                    inner = np.maximum(np.abs(a[-2, :] - a[-1, :]).max(-1), np.abs(b[0, :] - b[1, :]).max(-1))
                ex = float((seam - inner).max())
                worst = max(worst, ex); total += 1; bad += ex > 80
    return worst, bad, total


for folder in SETS:
    w, b, n = seams(load(folder))
    print(f'   {folder}: подозрительных стыков {b}/{n} (макс. превышение {w:.0f})')
    if b: fail(f'{folder}: подозрительные стыки')
w, b, n = seams(load('tiles'), load('brick_damaged'), mix=0.35)
print(f'   кирпич + повреждённый вперемешку: подозрительных стыков {b}/{n} (макс. превышение {w:.0f})')
if b: fail('кирпич + повреждённый: подозрительные стыки')

# 4. трава
print('4. Трава')
for f in GRASS:
    a = np.asarray(Image.open(os.path.join(ART, 'Ground', f))).astype(np.float32)
    if a[..., 3].min() < 255:
        fail(f'{f}: есть прозрачность')
    lr = np.abs(a[:, 0] - a[:, -1]).max(); tb = np.abs(a[0] - a[-1]).max()
    inner = max(np.abs(a[:, 1:] - a[:, :-1]).max(), np.abs(a[1:] - a[:-1]).max())
    if max(lr, tb) > max(inner, 1):
        fail(f'{f}: шов при повторении ({lr:.0f}/{tb:.0f})')
print('   7 файлов: непрозрачны, повторяются без шва')

print('ИТОГ:', 'ошибок нет' if errors == 0 else f'ошибок: {errors}')
sys.exit(1 if errors else 0)
