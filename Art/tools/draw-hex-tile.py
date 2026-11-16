"""Draw the ultratech hex tile pattern as clean geometry, white-on-clear.

Usage: python -I draw-hex-tile.py <out-png>
Same grid as the traced Ideology hex floor it replaces: pointy-top cells, 5 across and 6 rows per 256 px tile,
so it keeps the game's slightly squashed hex and the old pattern's scale and phase. Drawn at 8x and averaged
down so the joints have even width and antialiased edges.
"""
import sys
import numpy as np
from PIL import Image

SIZE, SS = 256, 8
COLS, ROWS = 5, 6
DX, DY = SIZE / COLS, SIZE / ROWS
ORIGIN = (39.4, 57.5)  # a cell centre in the old texture, so the pattern doesn't shift in game
HALF_JOINT = 2.2  # a little wider than the traced joints, whose broken bits made them read heavier
FACE, JOINT = 110.0, 12.0  # alpha levels of the old texture

n = SIZE * SS
y, x = (np.mgrid[0:n, 0:n] + 0.5) / SS
# Distances to every cell centre near enough to matter; the tile repeats, so wrap around the edges.
centres = [(ORIGIN[0] + c * DX + (r % 2) * DX / 2, ORIGIN[1] + r * DY)
           for r in range(-2, ROWS + 2) for c in range(-2, COLS + 2)]
# Keep the two nearest centres per pixel without holding every distance at once.
d1 = np.full((n, n), np.inf); d2 = d1.copy()
i1 = np.zeros((n, n), np.int32); i2 = i1.copy()
for i, (cx, cy) in enumerate(centres):
    d = (x - cx) ** 2 + (y - cy) ** 2
    first, second = d < d1, (d >= d1) & (d < d2)
    d2 = np.where(first, d1, np.where(second, d, d2)); i2 = np.where(first, i1, np.where(second, i, i2))
    d1 = np.where(first, d, d1); i1 = np.where(first, i, i1)
c = np.array(centres)
gap = np.hypot(*(c[i2] - c[i1]).transpose(2, 0, 1))
# Distance from the point to the boundary between its two nearest cells.
edge = (d2 - d1) / (2 * gap)
face = (edge > HALF_JOINT).astype(np.float32)
face = face.reshape(SIZE, SS, SIZE, SS).mean(axis=(1, 3))
alpha = JOINT + (FACE - JOINT) * face

out = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
out.putalpha(Image.fromarray(np.round(alpha).astype(np.uint8), "L"))
out.save(sys.argv[1], optimize=True)
