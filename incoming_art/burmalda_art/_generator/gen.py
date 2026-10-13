"""Ядро генератора 47-тайловых blob-автотайлов 64x64: геометрия, бесшовность, даунсэмплинг.

Каждый тайл рисуется в "мировых" координатах на композите 3x3 клетки
(центр + 8 соседей), затем вырезается центр. Все поля и узоры зависят
только от окрестности < 64 px и от локальных координат по модулю 64,
поэтому края соседних тайлов совпадают пиксель в пиксель.
"""
import os, sys
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, distance_transform_edt

T = 64          # размер тайла
S = 4           # суперсэмплинг
TS = T * S
C = 3 * TS      # композит 3x3

# Биты соседей: N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128 (y вниз в картинке)
DIRS = {1: (0, -1), 2: (1, -1), 4: (1, 0), 8: (1, 1),
        16: (0, 1), 32: (-1, 1), 64: (-1, 0), 128: (-1, -1)}
N, NE, E, SE, S_, SW, W, NW = 1, 2, 4, 8, 16, 32, 64, 128


def canonical(m):
    """Диагональ важна только если оба соседних кардинальных соседа есть."""
    if not (m & N and m & E): m &= ~NE
    if not (m & S_ and m & E): m &= ~SE
    if not (m & S_ and m & W): m &= ~SW
    if not (m & N and m & W): m &= ~NW
    return m


CONFIGS = sorted({canonical(m) for m in range(256)})
assert len(CONFIGS) == 47, len(CONFIGS)

# координаты композита в базовых пикселях и локальные (mod 64)
ys, xs = np.mgrid[0:C, 0:C].astype(np.float32)
X = (xs + 0.5) / S
Y = (ys + 0.5) / S
U = np.mod(X, T)
V = np.mod(Y, T)
TAU = 2 * np.pi


def occupancy(m):
    occ = np.zeros((3, 3), np.float32)
    occ[1, 1] = 1
    for bit, (dx, dy) in DIRS.items():
        if m & bit:
            occ[1 + dy, 1 + dx] = 1
    return np.kron(occ, np.ones((TS, TS), np.float32))


def hexc(h, a=1.0):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)] + [a], np.float32)


def lerp(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + b * t


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def tile_mask(cfg, inset, R, rf):
    """Геометрическая маска тайла по четвертям: каждая четверть зависит только
    от трёх соседей своего угла (как в 47-тайловом blob-наборе)."""
    yy, xx = np.mgrid[0:TS, 0:TS].astype(np.float32)
    x = (xx + 0.5) / S; y = (yy + 0.5) / S
    full = np.zeros((TS, TS), bool)
    quads = [  # (a, b, h, v, d, область)
        (T - x, y, E, N, NE, (x >= T / 2) & (y < T / 2)),
        (x, y, W, N, NW, (x < T / 2) & (y < T / 2)),
        (T - x, T - y, E, S_, SE, (x >= T / 2) & (y >= T / 2)),
        (x, T - y, W, S_, SW, (x < T / 2) & (y >= T / 2)),
    ]
    m = inset
    for a, b, hb, vb, db, reg in quads:
        h, v, d = bool(cfg & hb), bool(cfg & vb), bool(cfg & db)
        if not h and not v:
            f = (a >= m) & (b >= m)
            corner = (a < m + R) & (b < m + R)
            f &= ~corner | (((a - (m + R)) ** 2 + (b - (m + R)) ** 2) <= R * R)
        elif h and not v:
            f = b >= m
        elif v and not h:
            f = a >= m
        elif not d:
            notch = (a < m) & (b < m)
            fillet = (a > m - rf) & (b > m - rf) & (((a - (m - rf)) ** 2 + (b - (m - rf)) ** 2) > rf * rf)
            f = ~notch | fillet
        else:
            f = np.ones_like(a, bool)
        full |= f & reg
    return full


def shape(m, inset, R, rf, noise):
    """Маска в композите 3x3 + «живой» край: шум (в пикселях) добавляется
    к знаковому расстоянию до геометрической границы."""
    occ = np.zeros((5, 5), bool)
    occ[2, 2] = True
    for bit, (dx, dy) in DIRS.items():
        if m & bit:
            occ[2 + dy, 2 + dx] = True
    geom = np.zeros((C, C), bool)
    for cy in range(3):
        for cx in range(3):
            if not occ[cy + 1, cx + 1]:
                continue
            cfg = 0
            for bit, (dx, dy) in DIRS.items():
                if occ[cy + 1 + dy, cx + 1 + dx]:
                    cfg |= bit
            geom[cy * TS:(cy + 1) * TS, cx * TS:(cx + 1) * TS] = tile_mask(canonical(cfg), inset, R, rf)
    sd = (distance_transform_edt(geom) - distance_transform_edt(~geom)) / S
    mask = (sd + noise) > 0
    d_in = distance_transform_edt(mask) / S
    return mask, d_in


def downsample(rgba):
    a = rgba[..., 3:4]
    pm = rgba[..., :3] * a
    pm = pm.reshape(T, S, T, S, 3).mean(axis=(1, 3))
    aa = a.reshape(T, S, T, S, 1).mean(axis=(1, 3))
    rgb = np.where(aa > 1e-6, pm / np.maximum(aa, 1e-6), 0)
    res = np.concatenate([rgb, aa], -1)
    return (np.clip(res, 0, 1) * 255 + 0.5).astype(np.uint8)


