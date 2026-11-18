"""Copy the mod's runtime files to the RimWorld Mods folder, which is also the folder published to the Workshop.

The destination gets exactly the files below and nothing else from this repository (no Art, DevTests, Source, tools or
docs). Files already there that the mod no longer ships are removed, except About/PublishedFileId.txt, which Steam writes
on first publish and which ties the folder to its Workshop item.

Usage: python -I tools/deploy.py [destination]
"""
import filecmp
import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEST = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "E:/Steam/steamapps/common/RimWorld/Mods/UI-Rethemed-Era-Looks")
FOLDERS = ["About", "1.7", "Defs", "Languages", "Patches", "Textures"]
FILES = ["LoadFolders.xml", "LICENSE"]
KEEP = {pathlib.Path("About/PublishedFileId.txt")}


def wanted():
    files = {pathlib.Path(f) for f in FILES}
    for folder in FOLDERS:
        files |= {p.relative_to(ROOT) for p in (ROOT / folder).rglob("*") if p.is_file()}
    return files


def main():
    files = wanted()
    if not (ROOT / "1.7" / "Assemblies" / "EraLooks.dll").is_file():
        raise SystemExit("build first: dotnet build Source/EraLooks.csproj -c Release")
    DEST.mkdir(parents=True, exist_ok=True)
    for old in sorted(p for p in DEST.rglob("*") if p.is_file()):
        rel = old.relative_to(DEST)
        if rel not in files and rel not in KEEP:
            old.unlink()
            print("removed", rel)
    for rel in sorted(files):
        target = DEST / rel
        if target.is_file() and filecmp.cmp(ROOT / rel, target, shallow=False):
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / rel, target)
        print("copied", rel)
    for folder in sorted((p for p in DEST.rglob("*") if p.is_dir()), reverse=True):
        if not any(folder.iterdir()):
            folder.rmdir()
    print("Deployed", len(files), "files to", DEST)


if __name__ == "__main__":
    main()
