"""Draw the Spacer era's hull panels as a flat, white-on-clear UI Rethemed tile.

The layout is generated, in the style of Art/Mockups/spacer-hull-panels.png: large interlocking steel plates
with stepped edges, T-shaped seam joints and a few corners cut at 45 degrees. Plates are grown on a grid that
wraps at the tile edges, so the seams always repeat.

Usage: python -I draw-hull-panels.py <out-dir> [seed]
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]
seed = int(sys.argv[2]) if len(sys.argv) > 2 else 34
size, cells, supersample, seam_width = 1024, 24, 4, 8.0
plate, joint = 118.0, 0.0
cell = size / cells


def grow_plates(rng, count=24, spacing=5):
    # count and spacing let draw-archotech-circuits.py grow fewer, larger plates on a coarser grid.
    labels = np.full((cells, cells), -1)
    seeds = []
    while len(seeds) < count:
        p = rng.integers(0, cells, 2)
        if all(min(abs(p[0] - q[0]), cells - abs(p[0] - q[0])) + min(abs(p[1] - q[1]), cells - abs(p[1] - q[1])) >= spacing for q in seeds):
            seeds.append(p)
    targets = rng.integers(14, 34, len(seeds))
    for r, (y, x) in enumerate(seeds):
        labels[y, x] = r
    # Plates grow by whole rectangles, which gives the stepped outlines of the reference.
    active = set(range(len(seeds)))
    while active:
        for r in rng.permutation(sorted(active)):
            own = np.argwhere(labels == r)
            placed = False
            for _ in range(30):
                y, x = own[rng.integers(len(own))]
                h, w = rng.integers(2, 6, 2)
                dy, dx = [(-1, 0), (1, 0), (0, -1), (0, 1)][rng.integers(4)]
                y0 = y - h if dy < 0 else y + 1 if dy > 0 else y - rng.integers(h)
                x0 = x - w if dx < 0 else x + 1 if dx > 0 else x - rng.integers(w)
                ys, xs = np.arange(y0, y0 + h) % cells, np.arange(x0, x0 + w) % cells
                block = labels[np.ix_(ys, xs)]
                if (block == -1).all():
                    labels[np.ix_(ys, xs)] = r
                    placed = True
                    break
            if not placed or (labels == r).sum() >= targets[r]:
                active.discard(r)
    # Leftover gaps become plates when large enough; small ones join a neighbour.
    next_label = len(seeds)
    for y, x in np.argwhere(labels == -1):
        if labels[y, x] != -1:
            continue
        stack, comp = [(y, x)], []
        labels[y, x] = next_label
        while stack:
            cy, cx = stack.pop(); comp.append((cy, cx))
            for ny, nx in ((cy + 1) % cells, cx), ((cy - 1) % cells, cx), (cy, (cx + 1) % cells), (cy, (cx - 1) % cells):
                if labels[ny, nx] == -1:
                    labels[ny, nx] = next_label; stack.append((ny, nx))
        next_label += 1
    neighbours = lambda cy, cx: (((cy + 1) % cells, cx), ((cy - 1) % cells, cx), (cy, (cx + 1) % cells), (cy, (cx - 1) % cells))
    # Smooth away one-cell spurs and notches; the reference's plates have clean, broad steps.
    for _ in range(12):
        changed = False
        for cy in range(cells):
            for cx in range(cells):
                around = [labels[n] for n in neighbours(cy, cx)]
                if around.count(labels[cy, cx]) <= 1:
                    others = [a for a in around if a != labels[cy, cx]]
                    labels[cy, cx] = max(set(others), key=others.count)
                    changed = True
        if not changed:
            break
    # Smoothing can split a plate, so give each connected piece its own label.
    pieces = np.full((cells, cells), -1)
    count = 0
    for y, x in np.argwhere(pieces == -1):
        if pieces[y, x] != -1:
            continue
        stack = [(y, x)]; pieces[y, x] = count
        while stack:
            cy, cx = stack.pop()
            for n in neighbours(cy, cx):
                if pieces[n] == -1 and labels[n] == labels[cy, cx]:
                    pieces[n] = count; stack.append(n)
        count += 1
    labels = pieces
    while True:
        ids, counts = np.unique(labels, return_counts=True)
        small = [i for i, c in zip(ids, counts) if c < 6]
        if not small:
            break
        r = small[0]
        shared = {}
        for cy, cx in np.argwhere(labels == r):
            for ny, nx in ((cy + 1) % cells, cx), ((cy - 1) % cells, cx), (cy, (cx + 1) % cells), (cy, (cx - 1) % cells):
                if labels[ny, nx] != r:
                    shared[labels[ny, nx]] = shared.get(labels[ny, nx], 0) + 1
        labels[labels == r] = max(shared, key=shared.get)
    return labels


def seam_edges(labels):
    # Lattice edges between cells of different plates, keyed by their two lattice points.
    edges = set()
    for y in range(cells):
        for x in range(cells):
            if labels[y, x] != labels[y, (x - 1) % cells]:
                edges.add(((x, y), (x, y + 1)))
            if labels[y, x] != labels[(y - 1) % cells, x]:
                edges.add(((x, y), (x + 1, y)))
    return edges


def wrap(p):
    return (p[0] % cells, p[1] % cells)


def degrees(edges):
    d = {}
    for a, b in edges:
        for p in (wrap(a), wrap(b)):
            d[p] = d.get(p, 0) + 1
    return d


def has(edges, a, b):
    a, b = wrap(a), wrap(b)
    for e in edges:
        ea, eb = wrap(e[0]), wrap(e[1])
        if {ea, eb} == {a, b}:
            return e
    return None


def cut_corners(edges, labels, rng):
    # Replace some L-shaped corners with a 45 degree cut, like the reference's chamfered plates.
    cuts, used = [], set()
    deg = degrees(edges)
    for p in rng.permutation(sorted(deg)):
        p = tuple(p)
        if deg.get(p, 0) != 2 or rng.random() > 0.6:
            continue
        k = int(rng.integers(2, 4))
        arms = []
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            run = []
            for i in range(k):
                a = (p[0] + d[0] * i, p[1] + d[1] * i)
                b = (p[0] + d[0] * (i + 1), p[1] + d[1] * (i + 1))
                e = has(edges, a, b)
                if e is None or e in used or (i > 0 and deg.get(wrap(a), 0) != 2):
                    break
                run.append(e)
            if len(run) == k:
                arms.append((d, run))
        # Only a corner can be cut: the two arms must be at right angles.
        if len(arms) != 2 or arms[0][0][0] * arms[1][0][0] + arms[0][0][1] * arms[1][0][1] != 0:
            continue
        (d1, r1), (d2, r2) = arms
        # Only cut a plate that is solid well past the cut, so no thin wedge is left behind.
        inside = {labels[int(np.floor(p[1] + (j + .5) * d1[1] + (i + .5) * d2[1])) % cells,
                         int(np.floor(p[0] + (j + .5) * d1[0] + (i + .5) * d2[0])) % cells]
                  for i in range(k + 1) for j in range(k + 1)}
        if len(inside) != 1:
            continue
        for e in r1 + r2:
            edges.discard(e); used.add(e)
        cuts.append(((p[0] + d1[0] * k, p[1] + d1[1] * k), (p[0] + d2[0] * k, p[1] + d2[1] * k)))
        deg = degrees(edges)
        for a, b in cuts:
            for q in (wrap(a), wrap(b)):
                deg[q] = deg.get(q, 0) + 1
    return edges, cuts


def score(labels, edges):
    deg = degrees(edges)
    crosses = sum(1 for v in deg.values() if v == 4)
    ids, counts = np.unique(labels, return_counts=True)
    return crosses, len(ids), counts.max()


if __name__ == "__main__":
    rng = np.random.default_rng(seed)
    labels = grow_plates(rng)
    edges = seam_edges(labels)
    crosses, plates, largest = score(labels, edges)
    edges, cuts = cut_corners(edges, labels, rng)
    print(f"seed {seed}: {plates} plates, largest {largest} cells, {crosses} four-way joints, {len(cuts)} cut corners")

    lines = [[a, b] for a, b in edges] + [[a, b] for a, b in cuts]
    s = cell * supersample
    canvas = Image.new("L", (3 * size * supersample,) * 2, 0)
    draw = ImageDraw.Draw(canvas)
    width = round(seam_width * supersample)
    for ox in range(-1, 4):
        for oy in range(-1, 4):
            for line in lines:
                pts = [((x + ox * cells) * s, (y + oy * cells) * s) for x, y in line]
                draw.line(pts, fill=255, width=width)
                for px, py in pts:
                    r = width / 2
                    draw.rectangle((px - r, py - r, px + r, py + r), fill=255)
    m = size * supersample
    inner = canvas.crop((m, m, 2 * m, 2 * m))
    coverage = np.asarray(inner.resize((size, size), Image.LANCZOS), dtype=np.float32) / 255

    # Soft mottling in the steel uses wrapped noise so it repeats with the seams.
    noise = np.random.default_rng(11).random((size // 32, size // 32)).astype(np.float32)
    tiled = Image.fromarray(np.tile((noise * 255).astype(np.uint8), (3, 3)), "L")
    tiled = tiled.resize((size * 3, size * 3), Image.BICUBIC).filter(ImageFilter.GaussianBlur(10))
    noise = np.asarray(tiled, dtype=np.float32)[size:2 * size, size:2 * size] / 255
    face = plate + 14 * (noise - 0.5)
    alpha = face + (joint - face) * coverage

    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    image.putalpha(Image.fromarray(np.clip(alpha, 0, 255).astype(np.uint8), "L"))
    image.save(f"{out}/HullPanels.png", optimize=True)
