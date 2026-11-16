"""Adapt vanilla and DLC floor/building textures into flat, white-on-clear UI Rethemed pattern tiles.

Usage: python -I adapt-game-textures.py <decoded-png-dir> <out-dir>
Input PNGs are the game textures (Flagstone, TileStone, OrbitalPlatform,
HexTile, GrandArchotechStructure) decoded from RimWorld/Data/*/Assets.zip; DLC ones are BC7 .split DDS files
joined with the game's DdsFile.TryJoin first. No bevels or shading: joints and grain only.
"""
import sys
import numpy as np
from PIL import Image, ImageOps
src, out = sys.argv[1], sys.argv[2]
def lum(n): return np.asarray(Image.open(f"{src}/{n}.png").convert("L"), dtype=np.float32)
def to_alpha(a, lo=8, hi=143, p=(2, 98)):
    l, h = np.percentile(a, p); a = np.clip((a - l) / (h - l), 0, 1)
    return lo + (hi - lo) * a
from PIL import ImageFilter
def flat(a, r=20):
    # Drop the floor's broad light and dark patches so only its joints and grain are left.
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "L")
    return a - np.asarray(im.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32)
def two_level(a, pct, face=110.0, joint=12.0):
    # Flat face and joint, like vanilla UI Rethemed: blur first so grain doesn't speckle the faces.
    im = Image.fromarray(np.clip(a + 128, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(1.5))
    b = np.asarray(im, dtype=np.float32)
    m = np.where(b > np.percentile(b, pct), 255, 0).astype(np.uint8)
    # A median pass removes the stray grain dots left inside faces.
    m = np.asarray(Image.fromarray(m, "L").filter(ImageFilter.MedianFilter(5)), dtype=np.float32) / 255
    return joint + (face - joint) * m
def save(name, a, size):
    im = Image.fromarray(a.astype(np.uint8), "L").resize(size, Image.LANCZOS)
    w = Image.new("RGBA", size, (255, 255, 255, 0)); w.putalpha(im); w.save(f"{out}/{name}.png", optimize=True)
# Floors are seamless at full size, so a whole-texture resize keeps them tiling.
save("Flagstone", to_alpha(flat(lum("Flagstone"))), (1024, 1024))
# TileStone and HexTile repeat every 256 px, so one repeat drawn larger shows the game scale.
br = flat(lum("TileStone"), 6)[:256, :256]
# Upscale before thresholding so the thin mortar lines survive the median pass.
br = np.asarray(Image.fromarray(np.clip(br + 128, 0, 255).astype(np.uint8), "L").resize((768, 768), Image.BICUBIC), dtype=np.float32) - 128
save("Brick", two_level(br, 15), (768, 768))
save("OrbitalPlatform", to_alpha(flat(lum("OrbitalPlatform"), 30)), (1024, 1024))
# HexTile is now drawn as clean geometry on the same grid by draw-hex-tile.py.
# Archotech structures aren't tiles: take a panel area of the grand structure and mirror it 2x2 so it tiles.
g = Image.open(f"{src}/GrandArchotechStructure.png").convert("L")
c = g.crop((300, 360, 620, 760))
m = Image.new("L", (c.width * 2, c.height * 2))
m.paste(c, (0, 0)); m.paste(ImageOps.mirror(c), (c.width, 0)); m.paste(ImageOps.flip(c), (0, c.height)); m.paste(ImageOps.flip(ImageOps.mirror(c)), (c.width, c.height))
save("ArchotechPlates", to_alpha(np.asarray(m, dtype=np.float32)), m.size)
