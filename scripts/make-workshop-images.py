"""Build About/Preview.png and the Workshop gallery images from a screenshot test run.

Usage: python -I scripts/make-workshop-images.py <run evidence folder>
The run is `python tools/eralooks_test.py run eralooks-screens`; its folder holds <level>-<screen>.png.
"""
import pathlib
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parents[1]
RUN = pathlib.Path(sys.argv[1])
GALLERY = ROOT / "docs" / "images" / "workshop"
FONT = "C:/Windows/Fonts/segoeuib.ttf"
# (screenshot name, tech level, texture, label colour from the palette's green).
LOOKS = [("animal", "Animal", "Grass", (205, 190, 152)),
         ("neolithic", "Neolithic", "Scree", (201, 154, 69)),
         ("medieval", "Medieval", "Quatrefoil", (212, 166, 58)),
         ("industrial", "Industrial", "Tread plate", (196, 163, 80)),
         ("spacer", "Spacer", "Panel lines", (77, 187, 224)),
         ("ultra", "Ultra", "Honeycomb", (226, 92, 210)),
         ("archotech", "Archotech", "Circuit", (226, 234, 62))]


def font(size):
    return ImageFont.truetype(FONT, size)


# Pattern texture and palette ground per tech level, for the preview's columns; None is Core's honeycomb.
COLUMNS = [("EraLooks/Patterns/Grass.png", (23, 21, 19)),
           ("EraLooks/Patterns/Scree.png", (28, 23, 20)),
           ("EraLooks/Patterns/Quatrefoil.png", (20, 24, 38)),
           ("EraLooks/Patterns/TreadPlate.png", (18, 26, 22)),
           ("EraLooks/Patterns/PanelLines.png", (21, 25, 29)),
           (None, (13, 23, 29)),
           ("EraLooks/Patterns/ArchoCircuit.png", (26, 27, 20))]
HONEYCOMB = pathlib.Path("E:/Steam/steamapps/workshop/content/294100/3832843406/Common/Textures/UIRethemedCore/HexTiled.png")


def column(tex, ground, ink, size, scale):
    # One era's palette ground covered in its own pattern, figures drawn larger than in game so they read as a thumbnail.
    src = Image.open(HONEYCOMB if tex is None else ROOT / "Textures" / tex).getchannel("A")
    src = src.resize((int(src.width * scale), int(src.height * scale)), Image.LANCZOS)
    tiled = Image.new("L", size, 0)
    for y in range(0, size[1], src.height):
        for x in range(0, size[0], src.width):
            tiled.paste(src, (x, y))
    tiled = tiled.point(lambda v: int(v * 0.55))
    return Image.composite(Image.new("RGB", size, ink), Image.new("RGB", size, ground), tiled)


def preview():
    # Built for the Workshop browse grid, where the image shows a few hundred pixels wide: a large title, then one column per
    # era in its palette and pattern, named in large type. Small text would vanish at that size, so the art style sits
    # second and the columns carry the rest.
    w, h = 1280, 720
    out = Image.new("RGB", (w, h), (11, 12, 13))
    d = ImageDraw.Draw(out)
    d.text((w // 2, 92), "Era Looks", font=font(124), fill=(240, 234, 218), anchor="mm")
    d.text((w // 2, 178), "for UI Rethemed", font=font(40), fill=(170, 164, 150), anchor="mm")
    top, gap = 232, 12
    cw = (w - (len(LOOKS) + 1) * gap) // len(LOOKS)
    ch = h - top - gap
    for i, ((key, era, art, ink), (tex, ground)) in enumerate(zip(LOOKS, COLUMNS)):
        x = gap + i * (cw + gap)
        block = column(tex, ground, ink, (cw, ch), 1.0 if tex is None else 1.6)
        out.paste(block, (x, top))
        band = Image.new("RGBA", (cw, 132), (8, 8, 9, 215))
        out.paste(band, (x, top + ch - 132), band)
        d.text((x + cw // 2, top + ch - 84), era, font=font(30), fill=ink, anchor="mm")
        d.text((x + cw // 2, top + ch - 36), art, font=font(22), fill=(200, 194, 180), anchor="mm")
        d.rectangle((x, top, x + cw - 1, top + ch - 1), outline=ink, width=3)
    out.save(ROOT / "About" / "Preview.png", optimize=True)


def gallery():
    # Crops keep the game's own HUD but leave out the test runner's progress panel (bottom right) and the main menu's
    # version and Steam account text, which only the menu screen shows.
    GALLERY.mkdir(parents=True, exist_ok=True)
    for old in GALLERY.glob("*.jpg"):
        old.unlink()
    for key, *_ in LOOKS:
        for screen in ("factions", "ideology"):
            shot = Image.open(RUN / f"{key}-{screen}.png").convert("RGB").crop((0, 220, 2134, 1420))
            shot.resize((1920, 1080), Image.LANCZOS).save(GALLERY / f"{key}-{screen}.jpg", quality=90)
    # This mod's own settings page and the faction window with Show each faction's era on, both in the medieval look.
    settings = Image.open(RUN / "eralooks-settings.png").convert("RGB").crop((30, 30, 1500, 857))
    settings.resize((1920, 1080), Image.LANCZOS).save(GALLERY / "settings.jpg", quality=90)
    shot = Image.open(RUN / "faction-eras.png").convert("RGB").crop((0, 220, 2134, 1420))
    shot.resize((1920, 1080), Image.LANCZOS).save(GALLERY / "faction-eras.jpg", quality=90)

# About/Preview.png is hand-made outside this script, so only the gallery is rebuilt.
# preview() stays for reference.
gallery()
print(len(list(GALLERY.glob("*.jpg"))), "gallery images in", GALLERY.relative_to(ROOT))
