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
BODY_FONT = "C:/Windows/Fonts/segoeui.ttf"
# (screenshot name, tech level, default pattern, label colour from the palette's green).
LOOKS = [("animal", "Animal", "Fieldstone", (205, 190, 152)),
         ("neolithic", "Neolithic", "Split wood", (201, 154, 69)),
         ("medieval", "Medieval", "Cut stone", (212, 166, 58)),
         ("industrial", "Industrial", "Tread plate", (185, 191, 196)),
         ("spacer", "Spacer", "Hull panels", (77, 187, 224)),
         ("ultra", "Ultra", "Hex tiles", (226, 92, 210)),
         ("archotech", "Archotech", "Etched circuits", (226, 234, 62))]


def font(size, bold=True):
    return ImageFont.truetype(FONT if bold else BODY_FONT, size)


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


def label(img, era, art, ink):
    # The era's name in its own accent, centred in the empty middle of the Factions title bar.
    d = ImageDraw.Draw(img, "RGBA")
    text = f"{era}  ·  {art}"
    f = font(34)
    w = d.textlength(text, font=f)
    x0, y0 = (1920 - w) // 2 - 28, 8
    d.rounded_rectangle((x0, y0, x0 + w + 56, y0 + 60), 10, fill=(8, 8, 9, 225), outline=ink, width=2)
    d.text((960, y0 + 30), text, font=f, fill=ink, anchor="mm")


# How the settings work, beside the settings page. Kept to what a player needs before subscribing.
STEPS = [("Follow the era", "The interface takes your colony's tech level look, and changes when research raises it."),
         ("Your own look", "Pick any palette and pattern for each era, or keep your own UI Rethemed pick."),
         ("Turn it off", "Your own UI Rethemed look shows at the main menu and whenever Follow the era is off."),
         ("Faction eras", "Optional: in the Factions window, each faction shows the look of its own era.")]


def wrap(d, text, f, width):
    lines, line = [], ""
    for word in text.split():
        trial = (line + " " + word).strip()
        if d.textlength(trial, font=f) <= width:
            line = trial
        else:
            lines.append(line)
            line = word
    return lines + [line]


def settings_image():
    # The Era Looks window alone, cropped from the game, on the right; the steps on the left.
    out = Image.new("RGB", (1920, 1080), (14, 15, 17))
    window = Image.open(RUN / "eralooks-settings.png").convert("RGB").crop((605, 165, 1955, 1211))
    window = window.resize((int(window.width * 1010 / window.height), 1010), Image.LANCZOS)
    out.paste(window, (1920 - window.width - 35, 35))
    d = ImageDraw.Draw(out)
    left, width, y = 60, 1920 - window.width - 35 - 60 - 50, 70
    d.text((left, y), "How it works", font=font(56), fill=(240, 234, 218))
    y += 110
    for title, body in STEPS:
        d.text((left, y), title, font=font(36), fill=(212, 166, 58))
        y += 52
        for line in wrap(d, body, font(30, False), width):
            d.text((left, y), line, font=font(30, False), fill=(200, 194, 180))
            y += 40
        y += 44
    out.save(GALLERY / "8-settings.jpg", quality=90)


def gallery():
    # One image per era (its Factions window, which shows the pattern large) and the settings page: eight in all.
    # Crops keep the game's own HUD but leave out the test runner's progress panel (bottom right).
    GALLERY.mkdir(parents=True, exist_ok=True)
    for old in GALLERY.glob("*.jpg"):
        old.unlink()
    for i, (key, era, art, ink) in enumerate(LOOKS, 1):
        shot = Image.open(RUN / f"{key}-factions.png").convert("RGB").crop((0, 220, 2134, 1420))
        shot = shot.resize((1920, 1080), Image.LANCZOS)
        label(shot, era, art, ink)
        shot.save(GALLERY / f"{i}-{key}.jpg", quality=90)
    settings_image()

# About/Preview.png is hand-made outside this script, so only the gallery is rebuilt.
# preview() stays for reference.
gallery()
print(len(list(GALLERY.glob("*.jpg"))), "gallery images in", GALLERY.relative_to(ROOT))
