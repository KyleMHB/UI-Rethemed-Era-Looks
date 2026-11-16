"""Draw the Industrial era's diamond tread plate as a flat, white-on-clear UI Rethemed tile.

The tread is laid as rectangular deck plates in staggered rows, with a bolt in each corner. One continuous
field of tread read as a pattern rather than a floor, so each plate has its own tread phase and tone.

Usage: python -I draw-tread-plate.py <out-dir>
"""
import sys
import numpy as np
from PIL import Image, ImageFilter

out = sys.argv[1]
supersample = 4
pitch_x, pitch_y = 36, 17
half_length, half_width = 12.5, 3.35
plate, lug, joint = 96.0, 222.0, 10.0
seam, rim, bolt_inset, bolt_radius = 6.0, 3.0, 10.0, 3.2
# Plates are 6 x 4 tread pitches. The tile holds two plates across and two rows, the second row offset by half.
plate_width, plate_height = 6 * pitch_x, 4 * 2 * pitch_y
width, height = 2 * plate_width, 2 * plate_height

y, x = (np.mgrid[0:height * supersample, 0:width * supersample] + 0.5) / supersample
row = np.floor(y / plate_height).astype(int)
shifted = x + (row % 2) * plate_width / 2
col = np.floor(shifted / plate_width).astype(int)
lx, ly = shifted - col * plate_width, y - row * plate_height
plate_id = (row % 2) * 2 + col % 2

phase = np.random.default_rng(5).random((4, 2)) * [pitch_x, 2 * pitch_y]
px, py = lx + phase[plate_id, 0], ly + phase[plate_id, 1]
inside = np.zeros(x.shape, dtype=bool)
for offset, direction in ((0, 1), (1, -1)):
    dx = (px - offset * pitch_x / 2) % pitch_x - pitch_x / 2
    dy = (py + pitch_y / 2 - offset * pitch_y) % (2 * pitch_y) - pitch_y
    along = (dx + direction * dy) / np.sqrt(2)
    across = (dy - direction * dx) / np.sqrt(2)
    inside |= (np.abs(along) < half_length) & (
        np.abs(across) <= half_width * (1 - (along / half_length) ** 2)
    )

edge = np.minimum.reduce([lx, plate_width - lx, ly, plate_height - ly])
inside &= edge > seam / 2 + rim
seams = edge < seam / 2
# Distance to the nearest corner bolt; the tread keeps clear of each bolt.
bx = np.minimum(np.abs(lx - bolt_inset), np.abs(lx - plate_width + bolt_inset))
by = np.minimum(np.abs(ly - bolt_inset), np.abs(ly - plate_height + bolt_inset))
bolt_distance = np.hypot(bx, by)
inside &= bolt_distance > 8
bolts = bolt_distance < bolt_radius


def downsample(mask):
    image = Image.fromarray((mask * 255).astype(np.uint8), "L").resize((width, height), Image.LANCZOS)
    return np.asarray(image, dtype=np.float32) / 255


lugs, joints = downsample(inside), downsample(seams | bolts)
plate_id = plate_id[supersample // 2::supersample, supersample // 2::supersample]

# Soft wear in the steel uses wrapped noise so it repeats with the tile.
rng = np.random.default_rng(7)
noise = rng.random((height // 16, width // 16)).astype(np.float32)
tiled = Image.fromarray(np.tile((noise * 255).astype(np.uint8), (3, 3)), "L")
tiled = tiled.resize((width * 3, height * 3), Image.BICUBIC).filter(ImageFilter.GaussianBlur(4))
noise = np.asarray(tiled, dtype=np.float32)[height:2 * height, width:2 * width] / 255
tone = np.array([0, 9, -7, 5], dtype=np.float32)[plate_id]
base = plate + tone + 22 * (noise - 0.5)
alpha = base + (lug - base) * lugs
alpha += (joint - alpha) * joints

texture = Image.new("RGBA", (width, height), (255, 255, 255, 0))
texture.putalpha(Image.fromarray(np.clip(np.rint(alpha), 0, 255).astype(np.uint8), "L"))
texture.save(f"{out}/TreadPlate.png", optimize=True)
