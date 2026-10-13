"""Строгая проверка: рисуем случайную карту целиком (мир) тем же кодом
и сравниваем каждый тайл из набора с соответствующим куском мира.
Если тайлы бесшовны, совпадение должно быть почти точным."""
import os, sys
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt as edt
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen, gen2

T, S, TS = gen.T, gen.S, gen.TS
N, NE, E, SE, S_, SW, W, NW = 1, 2, 4, 8, 16, 32, 64, 128
DIRS = {N: (0, -1), NE: (1, -1), E: (1, 0), SE: (1, 1), S_: (0, 1), SW: (-1, 1), W: (-1, 0), NW: (-1, -1)}
MATS = {
    'tiles': ('brick_blob_{:03d}.png', lambda: gen2.render_brick(0, None)),
    'brick_damaged': ('brick_damaged_blob_{:03d}.png', lambda: gen2.render_brick(0, None, damaged=True)),
    'Steel': ('steel_{:03d}.png', lambda: gen2.render_steel(0, None)),
    'Water': ('water_{:03d}.png', lambda: gen2.render_water(0, None)),
    'Bush': ('bush_{:03d}.png', lambda: gen2.render_bush(0, None)),
}


def set_world(n):
    cw = n * TS
    ys, xs = np.mgrid[0:cw, 0:cw].astype(np.float32)
    X = (xs + 0.5) / S; Y = (ys + 0.5) / S
    for mod in (gen, gen2):
        mod.C = cw; mod.X = X; mod.Y = Y; mod.U = np.mod(X, T); mod.V = np.mod(Y, T)
    for name in ('BIGLEAVES', 'LEAVES', 'CLUMPS', 'FRONDS', 'CLUMP'):
        if hasattr(gen2, name):
            setattr(gen2, name, None)


def configs(occ):
    n = occ.shape[0]; cf = {}
    for y in range(n):
        for x in range(n):
            if not occ[y, x]:
                continue
            m = 0
            for bit, (dx, dy) in DIRS.items():
                xx, yy = x + dx, y + dy
                if 0 <= xx < n and 0 <= yy < n and occ[yy, xx]:
                    m |= bit
            cf[(x, y)] = gen.canonical(m)
    return cf


def world_shape_factory(cf, n):
    def world_shape(m, inset, R, rf, noise):
        geom = np.zeros((n * TS, n * TS), bool)
        for (x, y), c in cf.items():
            geom[y * TS:(y + 1) * TS, x * TS:(x + 1) * TS] = gen.tile_mask(c, inset, R, rf)
        sd = (edt(geom) - edt(~geom)) / S
        mask = (sd + noise) > 0
        return mask, edt(mask) / S
    return world_shape


def main(pkg, mats, maps=3, n=6, seed=1):
    rng = np.random.default_rng(seed)
    set_world(n)
    for mat in mats:
        pat, render = MATS[mat]
        worst = 0; cells = 0
        for _ in range(maps):
            occ = rng.random((n, n)) < 0.62
            cf = configs(occ)
            gen2.shape = world_shape_factory(cf, n)
            world = render()
            for (x, y), c in cf.items():
                tile = np.asarray(Image.open(os.path.join(pkg, mat, pat.format(c)))).astype(int)
                ref = gen2.bleed(gen.downsample(world[y * TS:(y + 1) * TS, x * TS:(x + 1) * TS])).astype(int)
                worst = max(worst, int(np.abs(tile - ref).max())); cells += 1
        print(f'{mat}: {cells} клеток на {maps} картах, макс. отличие тайла от эталона = {worst}', flush=True)


if __name__ == '__main__':
    # python3 qa_world.py <папка incoming_art> [наборы...]
    main(sys.argv[1], sys.argv[2:] or list(MATS))
