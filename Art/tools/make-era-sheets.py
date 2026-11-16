"""Make one contact sheet per era: every era pattern tiled at its on-screen scale over the era's panel colour.

The colours are a preview blend (white pattern over the panel colour, scaled by the pattern's weight), not an
in-game capture. Spacer uses Core's slate, which takes the theme's own colours, so its panel colour is approximate.
Keep ERAS in step with Defs/EraLooks.xml; each era's default pattern goes first.

Usage: python -I Art/tools/make-era-sheets.py <out-dir>   (run from the mod root)
"""
import pathlib
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

out = pathlib.Path(sys.argv[1])
patterns_dir = pathlib.Path("Textures/EraLooks/Patterns")
LARGE = 288 / 1024  # largeTileSize over the large texture's size

# era: (palette name, panel colour, accent from the palette's green, [(label, texture, scale, weight)])
ERAS = {
    "Animal": ("Bone", "34302A", "CDBE98", [("Split wood", "SplitWood", .35, 2.0), ("Flagstone", "Flagstone", .5, 2.0)]),
    "Neolithic": ("Rock art", "3A2F28", "C99A45",
                  [("Fieldstone", "NeolithicFieldstone", .5, 2.0), ("Rock art (figure)", "RockArt", 1, 1.0)]),
    "Medieval": ("Tapestry", "2C3350", "D4A63A",
                 [("Cut stone", "CutStone", .5, 1.6), ("Brick", "Brick", .5, 1.6),
                  ("Castle ashlar", "CastleAshlar", .5, 1.6), ("Coursed drystone", "DryStoneCoursed", .5, 1.6),
                  ("Random rubble", "DryStoneRubble", .5, 1.6), ("Levelled rubble", "RubbleLevelled", .5, 1.6),
                  ("Tapestry (figure)", "Tapestry", 1, 1.0)]),
    "Industrial": ("Engraving", "2A3830", "C4A350",
                   [("Tread plate", "TreadPlate", .5, 1.6), ("Engraving (figure)", "Engraving", 1, 1.0)]),
    "Spacer": ("Slate (approximate)", "2B3138", "2CAC41",
               [("Hull panels", "HullPanels", LARGE, 2.2), ("Orbital platform", "OrbitalPlatform", .5, 2.2)]),
    "Ultra": ("Ultratech", "22384A", "4FC3D9", [("Hex tiles", "HexTile", .5, 1.6)]),
    "Archotech": ("Archotech", "3E4032", "E2EA3E",
                  [("Etched circuits", "EtchedCircuits", LARGE, 2.0), ("Archotech plates", "ArchotechPlates", .5, 2.0)]),
}
SW, SH, GAP, PAD = 520, 320, 28, 48


def rgb(hex_colour):
    return tuple(int(hex_colour[i:i + 2], 16) for i in (0, 2, 4))


def font(size, bold=False):
    return ImageFont.truetype(f"C:/Windows/Fonts/{'segoeuib' if bold else 'segoeui'}.ttf", size)


def swatch(texture, scale, weight, panel):
    image = Image.open(patterns_dir / f"{texture}.png").getchannel("A")
    image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.LANCZOS)
    alpha = np.asarray(image, np.float32) / 255
    alpha = np.tile(alpha, (SH // alpha.shape[0] + 1, SW // alpha.shape[1] + 1))[:SH, :SW]
    ground = np.array(panel, np.float32)
    return Image.fromarray((ground + (255 - ground) * np.clip(alpha * 0.3 * weight / 1.6, 0, 1)[..., None]).astype(np.uint8))


out.mkdir(parents=True, exist_ok=True)
for era, (palette, panel, accent, patterns) in ERAS.items():
    cols = 3 if len(patterns) >= 5 else min(2, len(patterns))
    rows = -(-len(patterns) // cols)
    sheet = Image.new("RGB", (PAD * 2 + cols * SW + (cols - 1) * GAP, 150 + rows * (SH + 52 + GAP) + PAD - GAP), (17, 18, 20))
    draw = ImageDraw.Draw(sheet)
    ink = rgb(accent)
    draw.rectangle((PAD, 46, PAD + 6, 112), fill=ink)
    draw.text((PAD + 22, 40), f"{era} era patterns", font=font(44, True), fill=(238, 234, 224))
    draw.text((PAD + 22, 96), f"{palette} palette, on-screen scale. {len(patterns)} pattern{'s' * (len(patterns) > 1)}.",
              font=font(22), fill=(160, 158, 150))
    for i, (label, texture, scale, weight) in enumerate(patterns):
        x, y = PAD + (i % cols) * (SW + GAP), 150 + (i // cols) * (SH + 52 + GAP)
        draw.text((x, y + 6), label, font=font(26, True), fill=(230, 226, 214))
        if i == 0:
            end = x + draw.textlength(label, font=font(26, True))
            draw.rounded_rectangle((end + 14, y + 10, end + 104, y + 40), 6, fill=ink)
            draw.text((end + 59, y + 25), "default", font=font(18, True), fill=(17, 18, 20), anchor="mm")
        sheet.paste(swatch(texture, scale, weight, rgb(panel)), (x, y + 52))
    sheet.save(out / f"era-{era.lower()}.png", optimize=True)
    print(out / f"era-{era.lower()}.png", sheet.size)
