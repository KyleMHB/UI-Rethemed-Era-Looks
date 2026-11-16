"""Make the small-surface version of a flat era pattern by thickening its dark lines.

Core draws a pattern's texPath on small surfaces (icons, tabs, cards) and its largeTexPath on large backgrounds.
On a small surface a 1024 px tile shrinks to about 100 px, so a 4 px seam or trace becomes a fraction of a
screen pixel and averages away. This widens the low-alpha lines (a minimum filter on alpha, wrap-padded so the
tile still repeats) and then shrinks the tile, so each line stays at least about one screen pixel wide.
Patterns whose lines are lit rather than cut in pass `light` to widen the high-alpha lines; patterns with both
(archotech circuits on plates) pass `both`, which widens dark seams (alpha under 60) and lit lines together.

Usage: python -I make-small-variant.py <source.png> <out.png> [grow_px=4] [out_size=512] [dark|light|both]
"""
import sys
import numpy as np
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]
grow = int(sys.argv[3]) if len(sys.argv) > 3 else 4
out_size = int(sys.argv[4]) if len(sys.argv) > 4 else 512
mode = sys.argv[5] if len(sys.argv) > 5 else "dark"

image = Image.open(src).convert("RGBA")
w, h = image.size
alpha = np.array(image)[..., 3]
pad = grow + 1
padded = Image.fromarray(np.pad(alpha, pad, mode="wrap"))
dark = np.array(padded.filter(ImageFilter.MinFilter(2 * grow + 1)))[pad:pad + h, pad:pad + w]
light = np.array(padded.filter(ImageFilter.MaxFilter(2 * grow + 1)))[pad:pad + h, pad:pad + w]
thick = {"dark": dark, "light": light, "both": np.where(dark < 60, dark, light)}[mode]

result = np.zeros((h, w, 4), np.uint8)
result[..., :3] = 255
result[..., 3] = thick
small = Image.fromarray(result, "RGBA").resize((out_size, out_size * h // w), Image.LANCZOS)
small.save(out, optimize=True)
a = np.array(small)[..., 3]
print(f"{out}: {small.size}, alpha p5 {np.percentile(a, 5):.0f}, p50 {np.percentile(a, 50):.0f}, std {a.std():.1f}")
