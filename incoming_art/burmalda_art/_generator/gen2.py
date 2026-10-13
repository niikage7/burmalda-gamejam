"""Тайлы окружения в едином стиле с танками (cel-shading, чёрный контур,
верхняя + лицевая грань как в Brawl Stars). 47-тайловый blob-набор, 64x64.

Использует бесшовную геометрию из gen.py: каждая четверть тайла зависит
только от трёх соседей угла, все узоры периодичны с шагом 64.
Запуск: python3 gen2.py <папка> [tiles brick_damaged Steel Water Bush Ground]
"""
import os, sys
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt as edt

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen as g  # noqa: E402
from gen import (T, S, TS, C, U, V, X, Y, TAU, CONFIGS, hexc, shape, downsample, lerp)  # noqa: E402

PI = np.pi


# ------------------------------------------------------------ helpers
def bleed(img):
    """Цвет полностью прозрачных пикселей = цвет ближайшего видимого.
    Прозрачность не меняется; убирает тёмную кайму при фильтрации и мипмапах в движке."""
    a = img[..., 3]
    if a.min() > 0 or a.max() == 0:
        return img
    hole = a == 0
    _, (iy, ix) = edt(hole, return_indices=True)
    out = img.copy()
    out[..., :3][hole] = img[iy[hole], ix[hole], :3]
    return out


def below(M, px):
    """A[y] = M[y + px]: пиксель на px ниже тоже в маске."""
    k = int(round(px * S)); A = np.zeros_like(M)
    A[:-k] = M[k:]
    return A


def above(M, px):
    """A[y] = M[y - px]."""
    k = int(round(px * S)); A = np.zeros_like(M)
    A[k:] = M[:-k]
    return A


def leftof(M, px):
    k = int(round(px * S)); A = np.zeros_like(M)
    A[:, k:] = M[:, :-k]
    return A


def faces(M, h):
    """Верхняя грань и лицевая (южная) грань высотой h."""
    top = M & below(M, h)
    front = M & ~top
    return top, front


def dist_in(M):
    return edt(M) / S


def dist_out(M):
    return edt(~M) / S


def paint(col, where, c):
    col[where] = c


def wrap(d):
    return (d + T / 2) % T - T / 2


def seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = np.clip(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy), 0, 1)
    dx, dy = px - (ax + t * vx), py - (ay + t * vy)
    return np.sqrt(dx * dx + dy * dy)


def finish(col, M, outline_rgb, outline_w=2.2, alpha_body=1.0, alpha_line=1.0):
    d = dist_in(M)
    line = M & (d < outline_w)
    col[line] = outline_rgb
    col[..., 3] = np.where(line, alpha_line, alpha_body)
    out = np.zeros((C, C, 4), np.float32)
    out[M] = col[M]
    return out


def separation(col, top, front, rgb, w=1.5):
    sep = front & (dist_out(top) < w)
    col[sep] = rgb


def new_canvas(rgb):
    return np.broadcast_to(rgb, (C, C, 4)).copy()


# ------------------------------------------------------------ BRICK
BR = dict(
    top=hexc('#e2774a'), hi=hexc('#f6a173'), sh=hexc('#c45d37'),
    front=hexc('#a8442b'), front_hi=hexc('#bb5534'),
    mortar=hexc('#3a1813'), outline=hexc('#1c0d0d'),
    chip=hexc('#a8452a'), chip_sh=hexc('#74301d'),
)
BRICK_H = 7.0


def brick_pattern(col, top):
    """Кирпичная кладка на верхней грани: ряды по 32 px, перевязка со сдвигом."""
    row = np.floor(V / 32) % 2
    bx = np.mod(U + 16 * row, 32)
    by = np.mod(V, 32)
    dvx = np.minimum(bx, 32 - bx)
    dvy = np.minimum(by, 32 - by)
    hi = top & (by >= 1.2) & (by < 5.0) & (dvx >= 1.2)
    sh = top & (by > 26.5) & (dvx >= 1.2)
    col[hi] = BR['hi']; col[sh] = BR['sh']
    # маленький блик в левом верхнем углу кирпича
    spot = top & (by >= 5) & (by < 8) & (bx >= 4) & (bx < 9)
    col[spot] = BR['hi']
    mortar = top & ((dvx < 1.2) | (dvy < 1.2))
    col[mortar] = BR['mortar']


def brick_front(col, front):
    col[front] = BR['front']
    # вертикальные швы лицевой грани продолжают швы нижнего ряда
    row = np.floor(V / 32) % 2
    bx = np.mod(U + 16 * row, 32)
    dvx = np.minimum(bx, 32 - bx)
    col[front & (dvx < 1.1)] = BR['mortar']
    # светлая полоска сверху лицевой грани
    col[front & ~above(front, 1.8) & (dvx >= 1.1)] = BR['front_hi']


def render_brick(m, rng, damaged=False):
    noise = 0.45 * (np.sin(TAU * 2 * U / T + 1.1) + 0.8 * np.sin(TAU * 3 * V / T + 0.3))
    if damaged:
        # выщербины по краю — только в середине сторон (далеко от границ клетки)
        bites = np.zeros_like(U)
        for u0, v0, a, w in [(22, 0, 3.0, 3.0), (43, 0, 2.6, 2.8), (0, 25, 2.8, 3.0), (0, 44, 2.4, 2.8)]:
            if v0 == 0:
                bites -= a * np.exp(-(wrap(U - u0) ** 2) / (2 * w * w))
            else:
                bites -= a * np.exp(-(wrap(V - v0) ** 2) / (2 * w * w))
        # доп. «рваность» плавно гаснет к границам клетки (sin^2), поэтому на стыке
        # с целым кирпичом край совпадает и нет изломов
        extra = 0.9 * (np.sin(TAU * 5 * U / T + 0.4) * np.sin(PI * U / T) ** 2
                       + np.sin(TAU * 5 * V / T + 1.7) * np.sin(PI * V / T) ** 2)
        noise = noise + extra + bites
    M, _ = shape(m, inset=3.5, R=10, rf=3, noise=noise)
    top, front = faces(M, BRICK_H)
    col = new_canvas(BR['top'])
    brick_pattern(col, top)
    brick_front(col, front)

    if damaged:
        # отбитые углы кирпичей: треугольная выбоина у угла одного кирпича
        chips = [  # (u, v, r, sx, sy): угол в точке стыка, (sx, sy) — в какой кирпич
            (32, 0, 13.0, 1, 1), (16, 32, 12.0, -1, 1), (0, 32, 11.0, 1, -1),
            (48, 64, 12.5, -1, -1), (48, 32, 9.0, 1, 1)]
        for u0, v0, r, sx, sy in chips:
            du = wrap(U - u0) * sx; dv = wrap(V - v0) * sy
            along = du - dv
            rr = r + 1.4 * np.sin(along * 0.7 + u0) + 0.5 * np.sin(along * 1.3 + v0)
            ch = top & (du > 1.2) & (dv > 1.2) & ((du + dv) < rr)
            col[ch] = BR['chip']
            # тень вдоль швов раствора внутри выбоины (сторона, обращённая к углу)
            col[ch & ((du < 2.0) | (dv < 2.0))] = BR['chip_sh']
            col[top & ~ch & (dist_out(ch) < 1.2)] = BR['mortar']
        # трещины
        cracks = [[(8, 12), (12, 17), (10, 22), (14, 27)],
                  [(44, 40), (40, 45), (43, 50), (39, 56)],
                  [(54, 14), (58, 19), (55, 24)],
                  [(24, 38), (28, 43), (26, 47)]]
        for pts in cracks:
            for (ax, ay), (bx, by) in zip(pts, pts[1:]):
                dd = seg_dist(U, V, ax, ay, bx, by)
                col[top & (dd < 1.0)] = BR['mortar']
        # сколы на лицевой грани
        for u0 in (9, 41):
            dd = np.abs(wrap(U - u0))
            col[front & (dd < 3) & ~above(front, 3.5)] = BR['chip_sh']

    separation(col, top, front, BR['outline'])
    return finish(col, M, BR['outline'])


# ------------------------------------------------------------ STEEL
ST = dict(
    top=hexc('#9fb2c8'), hi=hexc('#e4eef8'), sheen=hexc('#c3d3e4'), sh=hexc('#8297b0'),
    front=hexc('#5e6f89'), front_hi=hexc('#73849e'),
    seam=hexc('#2c3446'), seam_hi=hexc('#d6e2ee'),
    rivet=hexc('#d9e3ee'), rivet_sh=hexc('#7d8ea6'), outline=hexc('#0f131c'),
)
STEEL_H = 7.0


def render_steel(m, rng):
    noise = 0.35 * (np.sin(TAU * 2 * U / T + 1.3) + 0.8 * np.sin(TAU * 3 * V / T + 0.4))
    M, _ = shape(m, inset=3.5, R=9, rf=3, noise=noise)
    top, front = faces(M, STEEL_H)
    col = new_canvas(ST['top'])

    # диагональные блики (жёсткие полосы), период 64
    diag = np.mod(U + V, T)
    col[top & (np.abs(diag - 20) < 4.5)] = ST['sheen']
    col[top & (np.abs(diag - 29) < 1.6)] = ST['sheen']
    # тень у нижнего края каждой плиты
    pv = np.mod(V, T)
    col[top & (pv > 56) & (pv < 62.5)] = ST['sh']

    # швы плит по сетке
    gx = np.minimum(np.mod(X, T), T - np.mod(X, T))
    gy = np.minimum(np.mod(Y, T), T - np.mod(Y, T))
    seam = top & ((gx < 1.1) | (gy < 1.1))
    col[top & ~seam & ((np.mod(X, T) < 2.6) | (np.mod(Y, T) < 2.6))] = ST['seam_hi']
    col[seam] = ST['seam']

    # заклёпки
    d_top = dist_in(top)
    for cx, cy in [(13, 12), (51, 12), (13, 48), (51, 48)]:
        dx = U - cx; dy = V - cy
        r = np.sqrt(dx * dx + dy * dy)
        ok = top & (d_top > 4.0)
        rv = ok & (r < 3.6)
        col[rv] = ST['rivet']
        col[rv & ((dx + dy) > 1.0)] = ST['rivet_sh']
        col[ok & (r >= 3.6) & (r < 4.8)] = ST['seam']

    # блик-кромка по верху верхней грани
    col[top & ~above(top, 2.6) & ~seam] = ST['hi']

    # лицевая грань
    col[front] = ST['front']
    col[front & ~above(front, 1.8)] = ST['front_hi']
    col[front & (gx < 1.1)] = ST['seam']
    for cx in (13, 51):
        r = np.abs(U - cx)
        col[front & (r < 1.6) & below(front, 2.5) & above(front, 2.5)] = ST['rivet_sh']

    separation(col, top, front, ST['outline'])
    return finish(col, M, ST['outline'])


# ------------------------------------------------------------ WATER
WA = dict(
    rim=hexc('#74d7fb'), base=hexc('#36b4f2'), deep=hexc('#2a9ce8'),
    wall=hexc('#1b6fb8'), wall_hi=hexc('#2483cc'), foam=hexc('#f2fcff'), outline=hexc('#0b2238'),
)
WATER_WALL = 6.0


def render_water(m, rng):
    noise = 0.6 * (np.sin(TAU * 2 * U / T + 0.7) + 0.7 * np.sin(TAU * 3 * V / T + 2.0)
                   + 0.5 * np.sin(TAU * (U - V) / T * 2 + 1.0))
    M, _ = shape(m, inset=4, R=15, rf=4, noise=noise)
    # водоём утоплен: у верхнего берега видна стенка берега
    # стенка берега — только там, где ближайший край воды сверху (а не сбоку)
    up = np.full(M.shape, np.inf, np.float32)
    for k in range(1, int(WATER_WALL * S) + 1):
        hole = np.zeros_like(M); hole[k:] = ~M[:-k]; hole[:k] = False
        up[(up == np.inf) & hole & M] = k / S
    d_m = dist_in(M)
    wall = M & (up <= WATER_WALL) & (up <= d_m + 1.0)
    surf = M & ~wall
    col = new_canvas(WA['base'])
    d_s = dist_in(surf)
    wav = 0.5 * np.sin(TAU * 4 * U / T) * np.sin(TAU * 4 * V / T)
    col[surf & (d_s < 7.5 + wav)] = WA['rim']
    col[surf & (d_s >= 16 + wav)] = WA['deep']
    col[surf & (d_s < 2.6 + 0.6 * wav)] = WA['foam']
    col[wall] = WA['wall']
    col[wall & (np.mod(np.floor((U + 3) / 8), 2) == 0) & ~above(wall, 2.0)] = WA['wall_hi']
    separation(col, ~wall & M, wall, WA['outline'], w=0.0)
    return finish(col, M, WA['outline'])


# ------------------------------------------------------------ BUSH
BU = dict(
    base=hexc('#3ea83a'), leaf=hexc('#4fba42'), leaf_tip=hexc('#67c950'), leaf_sh=hexc('#349a37'),
    rim=hexc('#5ec64a'), front=hexc('#21692a'), front_hi=hexc('#2c7f31'), outline=hexc('#0f2a12'),
)
BUSH_H = 5.0
BIGLEAVES = None


def shifted(M, dx, dy):
    """A[y, x] = M[y - dy, x - dx] (сдвиг маски на dx, dy пикселей)."""
    kx, ky = int(round(dx * S)), int(round(dy * S))
    A = np.zeros_like(M)
    ys = slice(max(ky, 0), M.shape[0] + min(ky, 0)); yd = slice(max(-ky, 0), M.shape[0] + min(-ky, 0))
    xs = slice(max(kx, 0), M.shape[1] + min(kx, 0)); xd = slice(max(-kx, 0), M.shape[1] + min(-kx, 0))
    A[ys, xs] = M[yd, xd]
    return A


def big_leaves():
    """Несколько крупных листьев на тайл в близких к фону тонах (спокойная текстура).
    Периодично с шагом 64."""
    rng = np.random.default_rng(41)
    col = np.broadcast_to(BU['base'], (C, C, 4)).copy()
    spots = [(14, 20, -0.35), (46, 12, 0.4), (30, 44, 0.05), (60, 50, -0.5)]
    for cx, cy, ang in spots:
        ln = rng.uniform(24, 28); wd = rng.uniform(12, 14)
        ax, ay = np.sin(ang), -np.cos(ang)
        bx, by = cx - ax * ln / 2, cy - ay * ln / 2            # основание листа
        du = wrap(U - bx); dv = wrap(V - by)
        sp = (du * ax + dv * ay) / ln
        dp = du * ay - dv * ax
        half = 1.3 * wd * np.sqrt(np.clip(sp, 0, 1)) * (1 - sp)
        leaf = (sp > 0) & (sp < 1) & (np.abs(dp) < half)
        col[leaf] = BU['leaf']
        col[leaf & (sp > 0.6)] = BU['leaf_tip']
        col[leaf & (dp > half - 1.6)] = BU['leaf_sh']            # теневая сторона листа
        col[leaf & (np.abs(dp) < 0.55) & (sp > 0.12) & (sp < 0.8)] = BU['leaf_sh']   # жилка
    return col


def render_bush(m, rng):
    global BIGLEAVES
    if BIGLEAVES is None:
        BIGLEAVES = big_leaves()
    # мягкие крупные выступы (2 на сторону) + короткие острые кончики листьев
    puffs = 3.8 * (np.abs(np.sin(2 * PI * U / T + 0.5)) + np.abs(np.sin(2 * PI * V / T + 1.8)))
    tips = 1.6 * ((1 - np.abs(np.sin(3 * PI * U / T + 1.2))) ** 5 + (1 - np.abs(np.sin(3 * PI * V / T + 0.3))) ** 5)
    M, _ = shape(m, inset=6, R=15, rf=4, noise=puffs + tips - 5.4)
    top, front = faces(M, BUSH_H)

    col = BIGLEAVES.copy()
    # матовая светлая кромка по верхним краям (свет сверху-слева)
    rim = top & ~shifted(M, 2.5, 4.0)
    col[rim] = BU['rim']
    col[front] = BU['front']
    col[front & ~above(front, 1.6)] = BU['front_hi']
    separation(col, top, front, BU['outline'], w=1.2)
    return finish(col, M, BU['outline'], outline_w=2.3, alpha_body=0.88, alpha_line=0.97)


# ------------------------------------------------------------ GROUND (трава)
GR = dict(light=hexc('#9bd35d'), dark=hexc('#8cc753'), tuft=hexc('#6fae45'), speck=hexc('#b6e67a'))
# пучки (x, y — основание) и искорки для вариантов 1 и 2; вариант 0 — ровный тон
GRASS_DECOR = {
    ('light', 1): ([(37, 34), (32, 42)], []),
    ('light', 2): ([(40, 24), (31, 43), (37, 41)], [(49, 18), (29, 36)]),
    ('dark', 1): ([(14, 21), (45, 47)], [(52, 15)]),
    ('dark', 2): ([(15, 21), (40, 45), (37, 37)], [(32, 33), (49, 30)]),
}


def tuft_mask(u, v, bx, by):
    """Пучок травы: 4 заострённые травинки на общем основании (~11x9 px)."""
    m = np.zeros(u.shape, bool)
    for dx, tip_dx, h, w in [(-3.5, -5.0, 6.5, 2.4), (-1.2, -1.8, 8.5, 2.6), (1.3, 1.6, 8.0, 2.6), (3.6, 5.0, 6.0, 2.4)]:
        x0 = bx + dx
        t = np.clip((by - v) / h, 0, 1)            # 0 у основания, 1 у кончика
        cx = x0 + (tip_dx - dx) * 0.6 * t * t
        half = (w / 2) * (1 - t)
        m |= (v <= by) & (v >= by - h) & (np.abs(u - cx) < half)
    m |= ((u - bx) ** 2 / 30 + (v - by + 0.6) ** 2 / 2.2) < 1
    return m


def render_grass(tone, variant):
    yy, xx = np.mgrid[0:TS, 0:TS].astype(np.float32)
    u = (xx + 0.5) / S; v = (yy + 0.5) / S
    col = np.broadcast_to(GR[tone], (TS, TS, 4)).copy()
    tufts, specks = GRASS_DECOR.get((tone, variant), ([], []))
    for bx, by in tufts:
        col[tuft_mask(u, v, bx, by)] = GR['tuft']
    for sx, sy in specks:
        col[((u - sx) ** 2 + (v - sy) ** 2) < 1.4 ** 2] = GR['speck']
    col[..., 3] = 1.0
    return col


def write_grass(out_root):
    """Ground/: grass_{light,dark}_{0,1,2}.png (64x64) и grass_background_128.png (шахматка 2x2)."""
    d = os.path.join(out_root, 'Ground'); os.makedirs(d, exist_ok=True)
    tiles = {}
    for tone in ('light', 'dark'):
        for vnt in (0, 1, 2):
            img = downsample(render_grass(tone, vnt))
            tiles[(tone, vnt)] = img
            Image.fromarray(img, 'RGBA').save(os.path.join(d, f'grass_{tone}_{vnt}.png'))
    big = np.zeros((2 * T, 2 * T, 4), np.uint8)
    big[:T, :T] = tiles[('light', 1)]; big[:T, T:] = tiles[('dark', 0)]
    big[T:, :T] = tiles[('dark', 2)]; big[T:, T:] = tiles[('light', 0)]
    Image.fromarray(big, 'RGBA').save(os.path.join(d, 'grass_background_128.png'))
    print('Ground done', flush=True)


# ------------------------------------------------------------ main
SETS = [
    ('tiles', 'brick_blob_{:03d}.png', lambda m, r: render_brick(m, r)),
    ('brick_damaged', 'brick_damaged_blob_{:03d}.png', lambda m, r: render_brick(m, r, damaged=True)),
    ('Steel', 'steel_{:03d}.png', render_steel),
    ('Water', 'water_{:03d}.png', render_water),
    ('Bush', 'bush_{:03d}.png', render_bush),
]


def main(out_root, only=None):
    if not only or 'Ground' in only:
        write_grass(out_root)
    for folder, pattern, fn in SETS:
        if only and folder not in only:
            continue
        d = os.path.join(out_root, folder)
        os.makedirs(d, exist_ok=True)
        for m in CONFIGS:
            rng = np.random.default_rng(1000 + m)
            comp = fn(m, rng)
            img = bleed(downsample(comp[TS:2 * TS, TS:2 * TS]))
            Image.fromarray(img, 'RGBA').save(os.path.join(d, pattern.format(m)))
        print(folder, 'done', flush=True)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2:] or None)
