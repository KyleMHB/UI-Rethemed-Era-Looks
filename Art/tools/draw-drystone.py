"""Draw candidate medieval stone-wall textures as flat, white-on-clear UI Rethemed tiles.

Usage: python -I draw-drystone.py <out-dir>
Flat faces and joints only, like the Brick tile: no bevels or shading. Each stone gets a small
flat tone shift so the wall reads as separate stones. All tiles wrap on both axes.
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

out = sys.argv[1]
SS = 2  # supersample


def periodic_noise(rng, w, h, cells, blur=1.0):
    # Low-res random grid tiled 3x3 and upsampled, centre crop, so it wraps.
    g = rng.random((cells, cells)).astype(np.float32)
    im = Image.fromarray((np.tile(g, (3, 3)) * 255).astype(np.uint8), "L").resize((w * 3, h * 3), Image.BICUBIC)
    a = np.asarray(im, dtype=np.float32)[h:2 * h, w:2 * w] / 255
    return ndimage.gaussian_filter(a, blur * w / cells / 4, mode="wrap") * 2 - 1


def warp(labels_fn, w, h, rng, amp, cells):
    # Sample the label layout through a smooth wrapped displacement so joints wander like split stone.
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    dx = periodic_noise(rng, w, h, cells) * amp
    dy = periodic_noise(rng, w, h, cells) * amp * 0.6
    return labels_fn((x + dx) % w, (y + dy) % h)


def render(name, labels, joint_px, round_px, tone=10, face=110.0, joint=12.0, seed=1, size=None):
    h, w = labels.shape
    # Joint wherever a different stone is within joint_px.
    r = max(1, int(round(joint_px * SS / 2)))
    fp = np.ones((2 * r + 1, 2 * r + 1), bool)
    hi = ndimage.maximum_filter(labels, footprint=fp, mode="wrap")
    lo = ndimage.minimum_filter(labels, footprint=fp, mode="wrap")
    facem = (hi == lo).astype(np.float32)
    # Opening with a disc rounds off each stone's corners without closing up the joints.
    if round_px:
        R = int(round(round_px * SS))
        yy, xx = np.mgrid[-R:R + 1, -R:R + 1]
        disc = xx * xx + yy * yy <= R * R
        pad = np.pad(facem > 0, R + 1, mode="wrap")
        facem = ndimage.binary_opening(pad, structure=disc)[R + 1:-R - 1, R + 1:-R - 1].astype(np.float32)
    rng = np.random.default_rng(seed)
    shift = rng.uniform(-tone, tone, labels.max() + 1).astype(np.float32)
    a = joint + (face + shift[labels] - joint) * facem
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "L").resize(size or (w // SS, h // SS), Image.LANCZOS)
    t = Image.new("RGBA", im.size, (255, 255, 255, 0))
    t.putalpha(im)
    t.save(f"{out}/{name}.png", optimize=True)


def coursed(w, h, rng, course_h, stone_w, stagger=True):
    # Courses of uneven height; stones of uneven length; joints never line up between courses.
    rows, y0 = [], 0
    while y0 < h:
        rows.append(y0)
        y0 += int(rng.uniform(*course_h) * SS)
    rows = np.array(rows) * h / y0  # stretch so courses fill the tile exactly
    edges = []
    for _ in rows:
        widths = []
        while sum(widths) < w:
            widths.append(rng.uniform(*stone_w) * SS)
        cum = np.cumsum(widths) * w / sum(widths)
        edges.append(np.sort((cum + rng.uniform(0, w)) % w))
    rows_all = np.append(rows, h)

    def fn(x, y):
        ri = np.clip(np.searchsorted(rows_all, y, side="right") - 1, 0, len(rows) - 1)
        lab = np.zeros(x.shape, np.int32)
        base = 0
        for i, xs in enumerate(edges):
            m = ri == i
            si = np.searchsorted(xs, x[m], side="right") % len(xs)  # wrap last stone into first
            lab[m] = base + si
            base += len(xs)
        return lab
    return fn


def rubble(w, h, rng, n, aspect):
    # Periodic Voronoi in a stretched metric: flattish stones packed like a random-rubble wall.
    px = rng.uniform(0, w, n)
    py = rng.uniform(0, h, n)

    def fn(x, y):
        best = np.full(x.shape, np.inf, np.float32)
        lab = np.zeros(x.shape, np.int32)
        for i in range(n):
            ddx = np.abs(x - px[i]); ddx = np.minimum(ddx, w - ddx) / aspect
            ddy = np.abs(y - py[i]); ddy = np.minimum(ddy, h - ddy)
            d = ddx * ddx + ddy * ddy
            m = d < best
            best[m] = d[m]; lab[m] = i
        return lab
    return fn


W = H = 512 * SS

# 1. Coursed drystone: rough courses, irregular stone lengths and wandering joints.
rng = np.random.default_rng(3)
render("DryStoneCoursed", warp(coursed(W, H, rng, (26, 40), (40, 96)), W, H, rng, 9 * SS, 8), 3.0, 3.0, seed=3)

# 2. Random rubble: flattish stones of mixed size, no courses.
rng = np.random.default_rng(5)
render("DryStoneRubble", warp(rubble(W, H, rng, 70, 1.7), W, H, rng, 6 * SS, 10), 3.0, 4.0, seed=5)

# 3. Castle ashlar: big dressed blocks in straight courses, tall and short courses alternating.
rng = np.random.default_rng(11)
render("CastleAshlar", warp(coursed(W, H, rng, (44, 64), (90, 170)), W, H, rng, 1.5 * SS, 6), 3.0, 1.0, tone=8, seed=11)

# 4. Rubble with levelling courses: bands of rough rubble between long thin bonding courses.
rng = np.random.default_rng(17)
band_h = H // 2
lev_h = 16 * SS
rub = rubble(W, band_h - lev_h, rng, 22, 1.5)
lev = coursed(W, lev_h, rng, (16, 16), (110, 200))


def banded(x, y):
    yy = y % band_h
    lab = np.where(yy < lev_h, lev(x, np.minimum(yy, lev_h - 1)), 1000 + rub((x + np.where(y >= band_h, W / 3, 0)) % W, np.clip(yy - lev_h, 0, band_h - lev_h - 1)))
    return lab + np.where(y >= band_h, 2000, 0).astype(np.int32)


render("RubbleLevelled", warp(banded, W, H, rng, 5 * SS, 10), 3.0, 3.0, seed=17)
