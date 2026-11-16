"""Draw the Archotech etched-circuits pattern: hull plates with no square corners, each holding lit circuit traces
in the style of the Ideology archotech structures.

Plates: the hull-panel layout from draw-hull-panels.py, grown on a coarser grid so the plates are about twice as
large. Their seams are drawn as lines with every bend cut at 45 degrees, and where one seam meets another the stem
bends into it at 45 degrees (a Y), so no plate corner is square.

Circuits: every trace starts at a plate edge, leaving a straight stretch of seam at a right angle, either as a plain
line end or from a half-circle pad on the seam. It then runs in straight legs with 45-degree cuts or right-angle
corners, and ends at a dot or ring inside the plate or by running square into another edge. Away from the edges it
meets, a trace keeps 30 px from every seam and never leaves its plate. Some leave an edge as nested groups of two or
three. Each trace is picked from many candidates by how empty its surroundings are.

Every shape is also drawn at the eight neighbouring offsets, so the tile repeats with no seam. White on clear:
plate face alpha 118, seams 0, traces 245. Make the small version with make-small-variant.py in `both` mode.

Usage: python -I draw-archotech-circuits.py <out.png>
"""
import importlib.util
import pathlib
import sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

out = sys.argv[1]
PLATE_SEED, CIRCUIT_SEED, CUT = 5, 1, 30
N, SS = 1024, 2
FACE, GLOW = 115, 245
W = 6        # trace width
KEEP = 30    # trace centreline clearance from seams, away from the edges it meets
SPACE = 22   # centreline clearance between traces
OG = 4       # occupancy grid pitch in px
ON = N // OG
DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]


class Tile:
    def __init__(self):
        self.im = Image.new("L", (N * SS, N * SS), FACE)
        self.d = ImageDraw.Draw(self.im)

    def _each(self, fn):
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                fn(ox, oy)

    def line(self, pts, w, v):
        def f(ox, oy):
            p = [((x + ox) * SS, (y + oy) * SS) for x, y in pts]
            r = w * SS / 2
            # Each segment is its own quad so 45-degree joints aren't distorted by Pillow's joint handling.
            for (x0, y0), (x1, y1) in zip(p, p[1:]):
                L = ((x1 - x0) ** 2 + (y1 - y0) ** 2) ** 0.5
                if L == 0:
                    continue
                nx, ny = -(y1 - y0) / L * r, (x1 - x0) / L * r
                self.d.polygon([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], fill=v)
            # Round joins at bends (a square there pokes out on 45-degree turns); square caps on the straight ends.
            for x, y in p[1:-1]:
                self.d.ellipse([x - r, y - r, x + r, y + r], fill=v)
            for x, y in (p[0], p[-1]):
                self.d.rectangle([x - r, y - r, x + r, y + r], fill=v)
        self._each(f)

    def dot(self, x, y, r, v):
        self._each(lambda ox, oy: self.d.ellipse(
            [(x + ox - r) * SS, (y + oy - r) * SS, (x + ox + r) * SS, (y + oy + r) * SS], fill=v))

    def save(self, path):
        a = self.im.resize((N, N), Image.LANCZOS)
        rgba = Image.new("RGBA", (N, N), (255, 255, 255, 0))
        rgba.putalpha(a)
        rgba.save(path, optimize=True)


def chamfer(pts, r):
    """Cut each right-angle corner with a 45-degree segment of size r."""
    res = [pts[0]]
    for i in range(1, len(pts) - 1):
        p0, p1, p2 = (np.array(p, float) for p in pts[i - 1:i + 2])
        a, b = p0 - p1, p2 - p1
        rr = min(r, np.linalg.norm(a) / 2, np.linalg.norm(b) / 2)
        res += [tuple(p1 + a / np.linalg.norm(a) * rr), tuple(p1 + b / np.linalg.norm(b) * rr)]
    res.append(pts[-1])
    return res


# --- plates ---------------------------------------------------------------------------------------------------

def load_hull():
    path = pathlib.Path(__file__).with_name("draw-hull-panels.py")
    spec = importlib.util.spec_from_file_location("hull_panels", path)
    hull = importlib.util.module_from_spec(spec)
    argv, sys.argv = sys.argv, [str(path), "unused"]
    spec.loader.exec_module(hull)
    sys.argv = argv
    hull.cells = 12  # half the Spacer grid, so plates come out about twice as large
    hull.cell = hull.size / hull.cells
    return hull


def seam_chains(edges, C):
    """Walk the lattice seams as chains from one junction to the next, in unwrapped coordinates."""
    adj = {}
    for a, b in edges:
        d = (b[0] - a[0], b[1] - a[1])
        adj.setdefault((a[0] % C, a[1] % C), set()).add(d)
        adj.setdefault((b[0] % C, b[1] % C), set()).add((-d[0], -d[1]))
    chains, seen = [], set()
    for v, ds in adj.items():
        if len(ds) == 2:
            continue
        for d in ds:
            if (v, d) in seen:
                continue
            pts, p, dd = [v], v, d
            while True:
                seen.add(((p[0] % C, p[1] % C), dd))
                q = (p[0] + dd[0], p[1] + dd[1])
                seen.add(((q[0] % C, q[1] % C), (-dd[0], -dd[1])))
                pts.append(q)
                nd = adj[(q[0] % C, q[1] % C)]
                if len(nd) != 2:
                    break
                dd = next(x for x in nd if x != (-dd[0], -dd[1]))
                p = q
            chains.append(pts)
    return adj, chains


def bends(pts):
    keep = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        if (b[0] - a[0], b[1] - a[1]) != (c[0] - b[0], c[1] - b[1]):
            keep.append(b)
    keep.append(pts[-1])
    return keep


def draw_plates():
    global FACE
    hull = load_hull()
    C = hull.cells
    labels = hull.grow_plates(np.random.default_rng(PLATE_SEED), count=7, spacing=3)
    adj, chains = seam_chains(hull.seam_edges(labels), C)
    cell = N / C
    side_rng = np.random.default_rng(PLATE_SEED)
    stems = []
    for pts in chains:
        for end in (0, -1):
            v, inner = pts[end], (pts[1] if end == 0 else pts[-2])
            into = (v[0] - inner[0], v[1] - inner[1])  # direction of travel arriving at the junction
            ds = adj[(v[0] % C, v[1] % C)]
            if len(ds) == 3 and into not in ds:  # straight on is closed, so this chain is the stem of a T
                stems.append(True)
    # The approved plates were drawn second in a run that drew two cut sizes; skip the first size's side picks.
    for _ in stems:
        side_rng.integers(2)
    FACE = 118
    tile = Tile()
    for pts in chains:
        P = chamfer([(x * cell, y * cell) for x, y in bends(pts)], CUT)
        for end in (0, -1):
            v, inner = pts[end], (pts[1] if end == 0 else pts[-2])
            into = (v[0] - inner[0], v[1] - inner[1])
            ds = adj[(v[0] % C, v[1] % C)]
            if len(ds) != 3 or into in ds:
                continue
            # A T-joint stem bends 45 degrees into the through seam, so neither plate at the joint has a square corner.
            side = [d for d in ds if d[0] * into[0] + d[1] * into[1] == 0]
            s = side[int(side_rng.integers(len(side)))]
            x, y = v[0] * cell, v[1] * cell
            back = (x - into[0] * CUT, y - into[1] * CUT)
            join = (x + s[0] * CUT, y + s[1] * CUT)
            P = [join, back] + P[1:] if end == 0 else P[:-1] + [back, join]
        tile.line(P, 8, 0)
    return np.asarray(tile.im.resize((N, N), Image.LANCZOS), float)


# --- circuits -------------------------------------------------------------------------------------------------

plates = draw_plates()
FACE = int(np.median(plates))  # ring holes match the plate face
# Distance to the nearest seam and plate identity, on a 3x3 tiling so lookups work across the tile edges.
face = np.tile(plates >= 70, (3, 3))
DIST = ndimage.distance_transform_edt(face)
LAB, _ = ndimage.label(face)


def at(arr, x, y):
    return arr[int(round(y)) + N, int(round(x)) + N]


def seam_hit(p, d, limit):
    """March from p along d until the seam; return the last face point before it, or None."""
    x, y = p
    for _ in range(int(limit)):
        nx, ny = x + d[0], y + d[1]
        if not (-N < nx < 2 * N - 1 and -N < ny < 2 * N - 1):
            return None
        if at(DIST, nx, ny) < 1:
            return (x, y)
        x, y = nx, ny
    return None


def straight_edge(h, d):
    """The seam at h must run straight across d for a while, so the trace meets it square and away from corners."""
    px, py = -d[1], d[0]
    for s in (-28, -14, 14, 28):
        q = (h[0] + px * s, h[1] + py * s)
        if at(DIST, q[0], q[1]) > 3 or at(DIST, q[0] + d[0] * 3, q[1] + d[1] * 3) > 0.5:
            return False
    return True


def sample(poly, step=4):
    pts = []
    for (x0, y0), (x1, y1) in zip(poly, poly[1:]):
        n = max(1, int(np.hypot(x1 - x0, y1 - y0) / step))
        for i in range(n):
            t = i / n
            pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
    pts.append(poly[-1])
    return pts


class Layout:
    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)
        self.occ = np.zeros((ON, ON), bool)

    def free(self, samples):
        r = int(np.ceil(SPACE / OG))
        for x, y in samples:
            gx, gy = int(x // OG), int(y // OG)
            ys = [(gy + k) % ON for k in range(-r, r + 1)]
            xs = [(gx + k) % ON for k in range(-r, r + 1)]
            if self.occ[np.ix_(ys, xs)].any():
                return False
        return True

    def stamp(self, samples, extra=0):
        r = int(extra // OG)
        for x, y in samples:
            gx, gy = int(x // OG), int(y // OG)
            for k in range(-r, r + 1):
                for j in range(-r, r + 1):
                    self.occ[(gy + j) % ON, (gx + k) % ON] = True

    def density(self, samples):
        return np.mean([self.blur[int(y // OG) % ON, int(x // OG) % ON] for x, y in samples])


def candidate(r):
    # Start inside a plate and look back for a straight seam: that is the edge the trace leaves from.
    p = (float(r.integers(N)), float(r.integers(N)))
    if at(DIST, *p) < KEEP + 4:
        return None
    d = DIRS[int(r.integers(4))]
    back = (-d[0], -d[1])
    h = seam_hit(p, back, 220)
    if h is None or not straight_edge(h, back):
        return None
    legs = int(r.integers(1, 4))
    e = DIRS[int(r.integers(4))]
    while e[0] * d[0] + e[1] * d[1] != 0:
        e = DIRS[int(r.integers(4))]
    pts, cur = [h], h
    for i in range(legs):
        dirn = d if i % 2 == 0 else e
        length = int(r.integers(4, 13)) * 16 if i else int(r.integers(4, 10)) * 16
        cur = (cur[0] + dirn[0] * length, cur[1] + dirn[1] * length)
        pts.append(cur)
    last = d if (legs - 1) % 2 == 0 else e
    to_edge = r.random() < 0.35
    if to_edge:
        hit = seam_hit(cur, last, 400)
        if hit is None or not straight_edge(hit, last):
            return None
        pts[-1] = hit
    return dict(pts=pts, d=d, e=e, legs=legs, lab=at(LAB, *h), to_edge=to_edge, last=last)


def valid(c, poly):
    """Samples along the trace, or None if it leaves its plate or comes near a seam away from the edges it meets."""
    head = 34
    keep = []
    for x, y in sample(poly):
        near_start = np.hypot(x - poly[0][0], y - poly[0][1]) < head
        near_end = c["to_edge"] and np.hypot(x - poly[-1][0], y - poly[-1][1]) < head
        if at(LAB, x, y) != c["lab"]:
            return None
        if not (near_start or near_end) and at(DIST, x, y) < KEEP:
            return None
        keep.append((x, y))
    if not c["to_edge"] and at(DIST, *poly[-1]) < KEEP + 10:
        return None
    return keep


def strands(c, n, s=24):
    """n nested parallel strands: shift the corners diagonally, then re-snap each end that meets an edge."""
    group = []
    T = (c["d"][0] - c["e"][0], c["d"][1] - c["e"][1]) if c["legs"] > 1 else (-c["d"][1], c["d"][0])
    for k in range(n):
        pts = [(x + T[0] * s * k, y + T[1] * s * k) for x, y in c["pts"]]
        h = seam_hit(pts[1], (-c["d"][0], -c["d"][1]), 400)
        if h is None:
            return None
        pts[0] = h
        if c["to_edge"]:
            h2 = seam_hit(pts[-2], c["last"], 600)
            if h2 is None:
                return None
            pts[-1] = h2
        group.append(pts)
    return group


def build(seed, groups=6, singles=40, tries=60):
    L = Layout(seed)
    r = L.rng
    drawn = []
    for kind, count in (("group", groups), ("single", singles)):
        for _ in range(count):
            L.blur = np.asarray(Image.fromarray((L.occ * 255).astype(np.uint8)).resize((ON // 8, ON // 8), Image.BOX)
                                .resize((ON, ON), Image.BILINEAR), float)
            best = None
            for _ in range(tries * 20):
                c = candidate(r)
                if c is None:
                    continue
                n = int(r.integers(2, 4)) if kind == "group" else 1
                ss = strands(c, n)
                if ss is None:
                    continue
                if any(np.hypot(b[0] - a[0], b[1] - a[1]) < 40 for p in ss for a, b in zip(p, p[1:])):
                    continue  # a leg this short reads as a kink
                style = "chamfer" if r.random() < 0.6 else "sharp"
                polys = [chamfer(p, 28) if style == "chamfer" else p for p in ss]
                samples = []
                for p in polys:
                    v = valid(c, p)
                    if v is None:
                        samples = None
                        break
                    samples += v
                if samples is None or not L.free(samples):
                    continue
                score = L.density(samples)
                if best is None or score < best[0]:
                    best = (score, c, polys, samples, n)
                if r.random() < 1 / tries:
                    break
            if best is None:
                continue
            _, c, polys, samples, n = best
            L.stamp(samples)
            pad = r.random() < 0.6
            end = ("dot", "ring", "ringdot")[int(r.integers(3))]
            if n > 1:
                end = ("dot", "ring")[int(r.integers(2))]
            for p in polys:
                if not c["to_edge"]:
                    L.stamp([p[-1]], extra=18)  # room for the end dot or ring
            drawn.append((polys, pad, end, c["to_edge"], n))
    return drawn


def render(drawn):
    base = Image.fromarray(plates.astype(np.uint8)).resize((N * SS, N * SS), Image.BICUBIC)
    t = Tile()
    t.im = base.copy()
    t.d = ImageDraw.Draw(t.im)
    for polys, pad, end, to_edge, n in drawn:
        for p in polys:
            t.line(p, W, GLOW)
            if pad:
                t.dot(p[0][0], p[0][1], 8, GLOW)  # cut to a half circle when the seams go back on top
            if to_edge:
                if pad:
                    t.dot(p[-1][0], p[-1][1], 8, GLOW)
                continue
            x, y = p[-1]
            if n > 1:
                t.dot(x, y, 9, GLOW)  # strands of a group end side by side, so they get small dots
            elif end == "dot":
                t.dot(x, y, 12, GLOW)
            elif end == "ring":
                t.dot(x, y, 15.6, GLOW)
                t.dot(x, y, 8.4, FACE)
            else:
                t.dot(x, y, 19, GLOW)
                t.dot(x, y, 12, FACE)
                t.dot(x, y, 5.4, GLOW)
    # Put the seams back on top, so pads become half circles and nothing crosses into the next plate.
    b = np.asarray(base, float)
    t.im = Image.fromarray(np.where(b < 70, b, np.asarray(t.im, float)).astype(np.uint8))
    return t


render(build(CIRCUIT_SEED)).save(out)
print(out)
