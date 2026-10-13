"""Checks map layouts (Assets/_Project/Editor/MapLayouts/*.txt) without Unity: legend characters, 180-degree symmetry
(both bases equal), that every open cell is reachable for a 1-cell tank without breaking bricks (same moves as the bots'
grid), marker counts, how free the zone is, base-to-zone path lengths, spacing of spawns and power-up points.

    python3 Tools/maps/validate_map.py Assets/_Project/Editor/MapLayouts/*.txt"""
import sys, math
from collections import deque

TERRAIN = set('.#x~%')
MARKERS = set('12fpZn')
PASSABLE = set('.%12fpZn')          # where a tank can stand (bricks excluded: they come back)
MIRROR = {'1': '2', '2': '1'}

def load(path):
    meta, rows = {}, []
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\n')
        if line == '#' or line.startswith('# '):
            if ':' in line:
                k, v = line[1:].split(':', 1)
                meta[k.strip()] = v.strip()
            continue
        if line.strip():
            rows.append(line)
    return meta, rows

def bfs(rows, start, passable):
    H, W = len(rows), len(rows[0])
    starts = start if isinstance(start, list) else [start]
    dist = {s: 0 for s in starts}
    q = deque(starts)
    while q:
        c, r = q.popleft()
        for dc, dr in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
            nc, nr = c+dc, r+dr
            if not (0 <= nc < W and 0 <= nr < H) or rows[nr][nc] not in passable or (nc, nr) in dist:
                continue
            if dc and dr and (rows[r][nc] not in passable or rows[nr][c] not in passable):
                continue
            dist[(nc, nr)] = dist[(c, r)] + 1
            q.append((nc, nr))
    return dist

def main(path):
    meta, rows = load(path)
    errors, notes = [], []
    H, W = len(rows), len(rows[0])
    if any(len(r) != W for r in rows):
        errors.append('rows differ in width: ' + str(sorted(set(len(r) for r in rows))))
        return errors, notes
    bad = {ch for r in rows for ch in r if ch not in TERRAIN | MARKERS}
    if bad: errors.append(f'unknown chars {bad}')
    for r in range(H):
        for c in range(W):
            a = rows[r][c]; b = rows[H-1-r][W-1-c]
            if MIRROR.get(a, a) != b:
                errors.append(f'asymmetric at col {c} row {r}: {a!r} vs {b!r}')
                break
    cells = {ch: [(c, r) for r in range(H) for c in range(W) if rows[r][c] == ch] for ch in '12fpZn'}
    want = {'1': (3, 3), '2': (3, 3), 'f': (8, 12), 'p': (6, 12), 'Z': (1, 4)}
    for ch, (lo, hi) in want.items():
        n = len(cells[ch])
        if not lo <= n <= hi: errors.append(f'marker {ch}: {n}, want {lo}..{hi}')
    passable = [(c, r) for r in range(H) for c in range(W) if rows[r][c] in PASSABLE]
    dist = bfs(rows, cells['1'][0], PASSABLE) if cells['1'] else {}
    unreachable = [p for p in passable if p not in dist]
    if unreachable: errors.append(f'{len(unreachable)} passable cells unreachable without breaking bricks, e.g. {unreachable[:5]}')
    if cells['Z']:
        zc = (sum(c for c, _ in cells['Z']) / len(cells['Z']) + 0.5, sum(r for _, r in cells['Z']) / len(cells['Z']) + 0.5)
        radius = float(meta.get('zone', 3))
        inside = [(c, r) for r in range(H) for c in range(W) if math.hypot(c + 0.5 - zc[0], r + 0.5 - zc[1]) <= radius]
        free = [p for p in inside if rows[p[1]][p[0]] in PASSABLE]
        notes.append(f'zone r={radius}: {len(free)}/{len(inside)} cells free')
        if len(free) < 0.55 * len(inside): errors.append('zone is too blocked')
        for t in '12':
            dt = bfs(rows, cells[t], PASSABLE)
            d = [dt.get(p) for p in cells['Z']]
            notes.append(f'path base{t}->zone: {min(x for x in d if x is not None)}')
    if cells['1'] and cells['2']:
        d12 = min(bfs(rows, cells['1'], PASSABLE).get(x, 10**6) for x in cells['2'])
        notes.append(f'path base1->base2: {d12} steps, straight {math.dist(cells["1"][0], cells["2"][0]):.1f}')
    notes.append(f"nitro points {len(cells['n'])}")
    for p in cells['p']:
        near = min(math.dist(p, b) for b in cells['1'] + cells['2'])
        if near < 5: errors.append(f'power-up point {p} only {near:.1f} from a base')
    f = cells['f']
    if f:
        mind = min(math.dist(a, b) for i, a in enumerate(f) for b in f[i+1:])
        notes.append(f'free spawns min spacing {mind:.1f}')
        if mind < 5: errors.append('free spawn points too close to each other')
    counts = {ch: sum(r.count(ch) for r in rows) for ch in '#x~%'}
    notes.append(f'size {W}x{H}, steel {counts["#"]}, brick {counts["x"]}, water {counts["~"]}, bush {counts["%"]}, open {len(passable)}')
    return errors, notes

if __name__ == '__main__':
    ok = True
    for p in sys.argv[1:]:
        errors, notes = main(p)
        print(('OK   ' if not errors else 'FAIL ') + p)
        for n in notes: print('     ' + n)
        for e in errors[:12]: print('  !  ' + e)
        ok &= not errors
    sys.exit(0 if ok else 1)
