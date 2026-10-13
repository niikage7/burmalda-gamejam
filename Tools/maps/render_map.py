"""Renders a map layout (Assets/_Project/Editor/MapLayouts/*.txt) with the project's real tile art, using the same
autotile and checker rules as BlobTile / CheckerTile, so a map can be previewed without Unity. Needs ImageMagick (`magick`).

    python3 Tools/maps/render_map.py Assets/_Project/Editor/MapLayouts/River.txt river.png

Base spawns are drawn as tanks facing the zone, 'f' as white rings, 'p' as yellow dots, 'n' as violet pads, the zone as a ring."""
import math, os, subprocess, sys

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Art')
LAYOUT, OUT = sys.argv[1], sys.argv[2]
CELL = 32
B = 2                        # border thickness, as in MapBuilder

meta, rows = {}, []
for line in open(LAYOUT, encoding='utf-8'):
    line = line.rstrip('\n')
    if line == '#' or line.startswith('# '):
        if ':' in line:
            k, v = line[1:].split(':', 1); meta[k.strip()] = v.strip()
        continue
    if line.strip():
        rows.append(line)
H, W = len(rows), len(rows[0])
GW, GH = W + 2 * B, H + 2 * B

def at(c, r):
    if 0 <= c < W and 0 <= r < H:
        return rows[r][c]
    return 'B'                # border

def world(c, r):              # world cell of layout cell (col, row), as MapBuilder.MapLayout.Cell
    return c - W // 2, H - 1 - r - H // 2

def u32(v): return v & 0xFFFFFFFF
def checker_hash(x, y, seed=0):
    h = u32(u32(x * 73856093) ^ u32(y * 19349663) ^ u32(seed * 83492791))
    h ^= h >> 16; h = u32(h * 0x7feb352d); h ^= h >> 15; h = u32(h * 0x846ca68b); h ^= h >> 16
    return h

def ground_sprite(c, r):
    x, y = world(c, r)
    light = ((x + y) & 1) == 0
    roll = checker_hash(x, y) % 5
    look = 0 if roll < 3 else (1 if roll < 4 else 2)
    return f"{ART}/Tiles/Ground/grass_{'light' if light else 'dark'}_{look}.png"

# BlobTile: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128 (world y up = row - 1)
OFFS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]
def blob_mask(c, r, ch):
    m = 0
    for i, (dc, dr) in enumerate(OFFS):
        if at(c + dc, r + dr) == ch:
            m |= 1 << i
    if not (m & 1 and m & 4): m &= ~2
    if not (m & 4 and m & 16): m &= ~8
    if not (m & 16 and m & 64): m &= ~32
    if not (m & 64 and m & 1): m &= ~128
    return m

BLOB = {'x': 'Bricks/brick_blob_', '#': 'Steel/steel_', '~': 'Water/water_', '%': 'Bush/bush_'}
def blob_sprite(c, r, ch):
    path = f"{ART}/Tiles/{BLOB[ch]}{blob_mask(c, r, ch):03d}.png"
    return path if os.path.exists(path) else f"{ART}/Tiles/{BLOB[ch]}000.png"

def px(c, r):                 # top-left pixel of a layout cell
    return (c + B) * CELL, (r + B) * CELL

mvg = [f'viewbox 0 0 {GW * CELL} {GH * CELL}']
def image(path, x, y, w, h):
    mvg.append(f'image over {x},{y} {w},{h} "{path}"')

for r in range(-B, H + B):
    for c in range(-B, W + B):
        x, y = px(c, r)
        image(ground_sprite(c, r), x, y, CELL, CELL)
# wall shadows (simplified: shifted dark squares)
mvg.append('fill rgba(0,0,0,0.30)')
for r in range(H):
    for c in range(W):
        if at(c, r) in 'x#':
            x, y = px(c, r)
            mvg.append(f'rectangle {x + 3},{y + 3} {x + CELL + 2},{y + CELL + 2}')
for ch in '~#x':
    for r in range(H):
        for c in range(W):
            if at(c, r) == ch:
                x, y = px(c, r); image(blob_sprite(c, r, ch), x, y, CELL, CELL)
# border: the template's placeholder block tinted (0.26, 0.28, 0.33)
mvg.append('fill rgb(66,71,84) stroke rgb(48,52,62) stroke-width 1')
for r in range(-B, H + B):
    for c in range(-B, W + B):
        if at(c, r) == 'B':
            x, y = px(c, r); mvg.append(f'rectangle {x},{y} {x + CELL - 1},{y + CELL - 1}')
mvg.append('stroke none')

cells = {m: [(c, r) for r in range(H) for c in range(W) if rows[r][c] == m] for m in '12fpZn'}
zc = (sum(c for c, _ in cells['Z']) / len(cells['Z']) + 0.5, sum(r for _, r in cells['Z']) / len(cells['Z']) + 0.5)
zx, zy = (zc[0] + B) * CELL, (zc[1] + B) * CELL
zr = float(meta.get('zone', 3)) * CELL
mvg.append(f'fill rgba(255,255,255,0.10) stroke rgba(255,255,255,0.75) stroke-width 4 circle {zx},{zy} {zx + zr},{zy}')
mvg.append('stroke none')
# base pads (3x3) on the cell nearest to the middle of each base
for marker, color in (('1', 'rgba(90,165,255,0.30)'), ('2', 'rgba(255,100,90,0.30)')):
    pts = cells[marker]
    mc = math.floor(sum(c for c, _ in pts) / len(pts) + 0.5); mr = math.floor(sum(r for _, r in pts) / len(pts) + 0.5)
    x, y = px(mc - 1, mr - 1)
    mvg.append(f'fill {color} rectangle {x},{y} {x + 3 * CELL},{y + 3 * CELL}')
for c, r in cells['p']:
    x, y = px(c, r); cx, cy = x + CELL / 2, y + CELL / 2
    mvg.append(f'fill rgb(255,215,60) stroke rgb(90,60,0) stroke-width 2 circle {cx},{cy} {cx + CELL * 0.28},{cy}')
for c, r in cells['n']:
    x, y = px(c, r); cx, cy = x + CELL / 2, y + CELL / 2
    mvg.append(f'fill rgba(30,30,40,0.75) circle {cx},{cy} {cx + CELL * 0.45},{cy}')
    mvg.append(f'fill rgb(230,115,255) circle {cx},{cy} {cx + CELL * 0.27},{cy}')
for c, r in cells['f']:
    x, y = px(c, r); cx, cy = x + CELL / 2, y + CELL / 2
    mvg.append(f'fill none stroke rgba(255,255,255,0.9) stroke-width 2 circle {cx},{cy} {cx + CELL * 0.36},{cy}')
mvg.append('stroke none')
# tanks on the base spawns, facing the zone like the generated spawn points
for marker, skin in (('1', 'blue'), ('2', 'red')):
    for c, r in cells[marker]:
        x, y = px(c, r); cx, cy = x + CELL / 2, y + CELL / 2
        dx, dy = zc[0] - (c + 0.5), -(zc[1] - (r + 0.5))          # world direction (y up)
        ang = math.degrees(math.atan2(-dx, dy))                     # Vector2.SignedAngle(up, d)
        ang = round(ang / 45) * 45
        s = CELL * 1.5
        mvg.append(f'push graphic-context translate {cx},{cy} rotate {-ang}')
        for part in ('shadow', 'track_left_0', 'track_right_0', 'body', 'turret', 'barrel'):
            mvg.append(f'image over {-s / 2},{-s / 2} {s},{s} "{ART}/Tanks/{skin}/{part}.png"')
        mvg.append('pop graphic-context')
for ch in '%':
    for r in range(H):
        for c in range(W):
            if at(c, r) == ch:
                x, y = px(c, r); image(blob_sprite(c, r, ch), x, y, CELL, CELL)

mvg_path = OUT + '.mvg'
open(mvg_path, 'w').write('\n'.join(mvg) + '\n')
subprocess.run(['magick', '-size', f'{GW * CELL}x{GH * CELL}', 'xc:black', '-draw', f'@{mvg_path}', OUT], check=True)
os.remove(mvg_path)
print(OUT, f'{GW * CELL}x{GH * CELL}')
