"""Render the era pattern SVGs in Art/Patterns to the mod's PNG textures.

LFS Core wants each pattern white on clear at twice its on-screen size, so every SVG is rendered at
its own pixel size with headless Microsoft Edge on a transparent background, then every pixel's
colour is set to white, keeping only its alpha.

Usage: python -I Art/tools/export-patterns.py [path to msedge.exe]
"""
import pathlib
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCES = ROOT / "Art" / "Patterns"
TARGET = ROOT / "Textures" / "EraLooks" / "Patterns"
EDGE = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


def render(svg: pathlib.Path, png: pathlib.Path, work: pathlib.Path) -> None:
    shot = work / f"{svg.stem}.png"
    root = ET.parse(svg).getroot()
    width, height = int(root.get("width")), int(root.get("height"))
    subprocess.run([
        EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars",
        "--force-device-scale-factor=1", "--default-background-color=00000000",
        f"--window-size={width},{height}", f"--user-data-dir={work / 'profile'}",
        f"--screenshot={shot}", svg.as_uri(),
    ], check=True, capture_output=True)
    # msedge.exe can return before its browser process has written the screenshot.
    for _ in range(100):
        if shot.exists() and shot.stat().st_size:
            break
        time.sleep(0.2)
    time.sleep(0.5)
    image = Image.open(shot).convert("RGBA")
    if image.size != (width, height):
        raise SystemExit(f"{svg.name}: rendered {image.size}, expected {(width, height)}")
    white = Image.new("RGBA", image.size, (255, 255, 255, 0))
    white.putalpha(image.getchannel("A"))
    white.save(png, optimize=True)


def main() -> None:
    TARGET.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as work:
        for svg in sorted(SOURCES.glob("*.svg")):
            png = TARGET / f"{svg.stem}.png"
            render(svg, png, pathlib.Path(work))
            print(png.relative_to(ROOT))


if __name__ == "__main__":
    main()
