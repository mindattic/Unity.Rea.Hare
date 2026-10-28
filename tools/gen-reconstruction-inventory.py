#!/usr/bin/env python3
"""Generate the per-asset inventory appendix of docs/RECONSTRUCTION.md (U.HARE-T94) from HARE.gmx.

Usage: python tools/gen-reconstruction-inventory.py [--source <HARE.gmx>] [--out <file>] [--write]

Without --write the appendix is printed (or written to --out); with --write the section from the
"## Appendix: per-asset inventory" heading to the end of docs/RECONSTRUCTION.md is replaced.

Every resource named in HARE.project.gmx (objects, scripts, rooms, sprites, backgrounds, sounds, fonts,
data files) gets one row: its resource-tree group, where the original uses it (by name, found by scanning
every object event, script and room file for the identifier) and its port status. The status is derived:

  ported         the name appears in Unity.Hare's runtime code, content or tuning (Core, Scripts, Resources),
                 not counting the importer tables (gml-*.json, Gml*.cs)
  in progress    the OVERRIDES table below (work not landed yet)
  imported       a sprite, background, sound or font that is imported as an asset but no code names it
  not reached    nothing in the original uses it (so parity does not need it)
  not ported     the original uses it and no Unity code names it; the reason is in the Notes column

Names built at run time from a prefix (spr_boss_hull + index, say) do not show as references.
"""
import argparse, os, re, sys, json
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_SRC = r"R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlHARE\HARE.gmx"
HEADING = "## Appendix: per-asset inventory"
TOKEN = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")

# Status the derived rule cannot know. name -> (status, note)
OVERRIDES = {
    "obj_scrapmental": ("in progress, U.HARE-T68", "Scrap Mental boss; level 8's defeat target"),
    "obj_ring": ("not reached", "named only in commented-out code in obj_game (the ring gate); see 'Content the original never reaches'"),
    "obj_ring_back": ("not reached", "only the never-placed obj_ring uses it; see 'Content the original never reaches'"),
    "sfx_crash": ("not reached", "only in commented-out audio_play_sound lines of obj_title and obj_drop_title; see 'Content the original never reaches' (U.HARE-T30)"),
    "obj_spawnpoint": ("not ported, by design", "only sets angle = 270, draws nothing and nothing reads it (BIBLE V29, U.HARE-T21)"),
}
# Extra note appended to a derived row. name -> note
NOTES = {
    "obj_btn_upgrade": "garage button, U.HARE-T24 (eb2d6e8)",
    "obj_btn_upgrade_vulcan": "garage upgrade button, U.HARE-T24 (eb2d6e8)",
    "obj_btn_upgrade_shotgun": "garage upgrade button, U.HARE-T24 (eb2d6e8)",
}
# Reasons for items the derived rule leaves as 'not ported'. name -> note
REASONS = {}


def read(p):
    with open(p, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def tokens(text):
    return set(TOKEN.findall(text))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=DEFAULT_SRC)
    ap.add_argument("--out")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    src = a.source

    # ---- resource tree -------------------------------------------------------------------------
    root = ET.parse(os.path.join(src, "HARE.project.gmx")).getroot()
    kinds = ("sound", "sprite", "background", "script", "font", "object", "room")
    plural = {"sounds": "sound", "sprites": "sprite", "backgrounds": "background", "scripts": "script",
              "fonts": "font", "objects": "object", "rooms": "room"}
    res = {k: [] for k in kinds}
    folders = {k: [] for k in kinds}

    def walk(el, path):
        nres = nfold = 0
        for c in el:
            if c.tag in kinds:
                nres += 1
                nm = (c.text or "").strip().replace("\\", "/").split("/")[-1]
                nm = re.sub(r"\.gml$", "", nm)
                res[c.tag].append((nm, "/".join(path)))
            elif c.tag in plural and c.get("name") is not None:
                nfold += 1
                walk(c, path + [c.get("name")])
        folders[plural[el.tag]].append(("/".join(path[1:]) or "(top level: " + path[0] + ")", nres, nfold))

    for top in root:
        if top.tag in plural:
            walk(top, [top.get("name")])
    datafiles = [d.findtext("name") for d in root.iter("datafile")]
    # resource files the project tree does not list (GameMaker keeps the file when a resource is
    # dropped from the tree): still part of the original, so still recorded
    ondisk = {"object": ("objects", ".object.gmx"), "script": ("scripts", ".gml"), "room": ("rooms", ".room.gmx"),
              "sprite": ("sprites", ".sprite.gmx"), "background": ("background", ".background.gmx"),
              "sound": ("sound", ".sound.gmx"), "font": ("fonts", ".font.gmx")}
    for k, (d_, ext) in ondisk.items():
        have = {n for n, _ in res[k]}
        for f in sorted(os.listdir(os.path.join(src, d_))):
            if f.endswith(ext) and f[:-len(ext)] not in have:
                res[k].append((f[:-len(ext)], "/(not in the resource tree)"))

    # ---- original references -------------------------------------------------------------------
    files = {}
    for nm, _ in res["object"]:
        files["obj:" + nm] = tokens(read(os.path.join(src, "objects", nm + ".object.gmx")))
    for nm, _ in res["script"]:
        p = os.path.join(src, "scripts", nm + ".gml")
        files["scr:" + nm] = tokens(read(p)) if os.path.exists(p) else set()
    for nm, _ in res["room"]:
        files["room:" + nm] = tokens(read(os.path.join(src, "rooms", nm + ".room.gmx")))

    def users(name, own):
        return [k for k, toks in files.items() if k != own and name in toks]

    def fmt_users(ks, limit=4):
        if not ks:
            return "none"
        order = {"room": 0, "obj": 1, "scr": 2}
        ks = sorted(ks, key=lambda k: (order[k.split(":")[0]], k))
        names = ["`%s`" % k.split(":", 1)[1] for k in ks[:limit]]
        more = len(ks) - limit
        return ", ".join(names) + (" +%d more" % more if more > 0 else "")

    # ---- Unity side ----------------------------------------------------------------------------
    unity = {}      # token -> files naming it in code or content
    unity_c = {}    # token -> files naming it only in comments
    stems = set()   # code tokens, for family (prefix + index) matches
    strip = re.compile(r"//[^" + chr(10) + r"]*|/\*.*?\*/", re.S)
    for base in ("Assets/Unity.Hare/Core", "Assets/Unity.Hare/Scripts", "Assets/Unity.Hare/Resources"):
        for dp, dn, fn in os.walk(os.path.join(REPO, base)):
            for f in fn:
                if not f.endswith((".cs", ".json")) or re.match(r"Gml.*\.cs$", f) or f.startswith("gml-"):
                    continue
                rel = os.path.relpath(os.path.join(dp, f), REPO).replace("\\", "/")
                rel = rel.replace("Assets/Unity.Hare/", "")
                text = read(os.path.join(dp, f))
                code = strip.sub(" ", text) if f.endswith(".cs") else text
                ct = tokens(code)
                for t in ct:
                    unity.setdefault(t, set()).add(rel)
                for t in tokens(text) - ct:
                    unity_c.setdefault(t, set()).add(rel)
    imported = {"sprite": set(), "background": set(), "sound": set(), "font": set()}
    try:
        d = json.load(open(os.path.join(REPO, "Assets/Unity.Hare/Art/gml-art.json"), encoding="utf-8-sig"))
        imported["sprite"] = {s["name"] for s in d["sprites"]}
        imported["background"] = {s["name"] for s in d["backgrounds"]}
        d = json.load(open(os.path.join(REPO, "Assets/Unity.Hare/Audio/gml-audio.json"), encoding="utf-8-sig"))
        imported["sound"] = {s["name"] for s in d["sounds"]}
        d = json.load(open(os.path.join(REPO, "Assets/Unity.Hare/Fonts/gml-fonts.json"), encoding="utf-8-sig"))
        imported["font"] = {s["name"] for s in d["fonts"]}
    except Exception as e:
        print("warning: importer tables not read:", e, file=sys.stderr)

    def fmt_files(fs, limit=2):
        fs = sorted(fs)
        return ", ".join("`%s`" % f for f in fs[:limit]) + (" +%d" % (len(fs) - limit) if len(fs) > limit else "")

    def unity_files(name):
        return fmt_files(unity.get(name, ()))

    def family(name):
        """Files naming the name's family stem (spr_boss_hull for spr_boss_hull3), built there with an index."""
        stem = name.rstrip("0123456789")
        if stem != name and len(stem) > 4 and unity.get(stem):
            return stem, unity[stem]
        return None, ()

    def status(kind, name, used):
        extra = NOTES.get(name)
        uf = unity_files(name)
        if name in OVERRIDES:
            st, note = OVERRIDES[name]
            return st, "; ".join(x for x in (uf, note) if x)
        if not used:
            empty = kind == "script" and os.path.exists(os.path.join(src, "scripts", name + ".gml"))                 and os.path.getsize(os.path.join(src, "scripts", name + ".gml")) == 0
            note = "nothing in the original uses it" + ("; the script file is empty" if empty else "")
            if uf:
                note += "; Unity mentions it in " + uf
            elif unity_c.get(name):
                note += "; Unity mentions it only in comments: " + fmt_files(unity_c[name])
            if kind in imported and name in imported[kind]:
                note = "asset imported; " + note
            return ("imported, not reached" if kind in imported and name in imported[kind] else "not reached"), note
        if uf:
            return "ported", "; ".join(x for x in (uf, extra) if x)
        stem, ff = family(name)
        if ff:
            return "ported", "family `%s` + index in %s" % (stem, fmt_files(ff))
        if unity_c.get(name):
            return "ported, by comment reference", fmt_files(unity_c[name])
        if kind == "sound":
            if name.startswith("music_"):
                return "not ported, music", "imported as a streamed clip; no jukebox track list names it"
            return "not ported, UH-T30", "sound-effect wiring: imported as a clip, no event plays it yet"
        owners = [k.split(":", 1)[1] for k in used if k.startswith("obj:")]
        if kind in imported and name in imported[kind]:
            ported_owner = [o for o in owners if unity.get(o) or unity_c.get(o)]
            if ported_owner:
                return "imported, owner ported", "drawn by `%s`, which Unity names; no code names this %s itself" % (ported_owner[0], kind)
            ov = [o for o in owners if o in OVERRIDES]
            if ov:
                return "%s (owner `%s`)" % (OVERRIDES[ov[0]][0], ov[0]), "asset imported; " + OVERRIDES[ov[0]][1]
            return "not ported, owner not ported", "asset imported; users in the original: " + fmt_users(used)
        return "not ported", REASONS.get(name, "no Unity code names it; check the owning story")

    L = []
    w = L.append
    w(HEADING)
    w("")
    w("U.HARE-T94 (rebuild standard point 1: record every original asset). One row per resource in `HARE.gmx`'s")
    w("`HARE.project.gmx`, generated by `tools/gen-reconstruction-inventory.py` (re-run it with `--write` after")
    w("a port lands; do not hand-edit the rows). **Used by** lists what in the original names the asset (room")
    w("instance, object event or parent, script call; first four). **Status**: `ported` (Unity code or content names")
    w("it, or a family stem plus an index; the files are shown); `ported, by comment reference` (only comments in")
    w("the Unity code name it: the original's name is kept in the doc comments of the C# port that replaces it, so")
    w("this is documentary evidence, not a symbol match); `in progress`; `imported, owner ported` (the sprite or")
    w("background is in the project and its owning object is ported, but no code names the asset itself);")
    w("`not ported, <ticket or why>`; `not reached` and `imported, not reached` (nothing in the original uses")
    w("it, so parity does not need it; any Unity mention is noted). The original is scanned by name, so an asset")
    w("the original builds from a prefix at run time shows as unused there; the family tables above cover those.")
    w("")

    total = {}

    def table(title, kind, used):
        rows = res[kind]
        w("### %s (%d)" % (title, len(rows)))
        w("")
        w("| Name | Group | Used by | Status | Unity / notes |")
        w("|---|---|---|---|---|")
        counts = {}
        for nm, grp in sorted(rows):
            ub = used(nm)
            st, note = status(kind, nm, ub)
            key = st
            counts[key] = counts.get(key, 0) + 1
            w("| `%s` | %s | %s | %s | %s |" % (nm, grp.split("/", 1)[1] if "/" in grp else "", fmt_users(ub), st, note))
        w("")
        total[title] = counts

    table("Objects", "object", lambda n: users(n, "obj:" + n))
    table("Scripts", "script", lambda n: users(n, "scr:" + n))
    table("Rooms", "room", lambda n: users(n, "room:" + n))
    table("Sprites", "sprite", lambda n: users(n, None))
    table("Backgrounds", "background", lambda n: users(n, None))
    table("Sounds", "sound", lambda n: users(n, None))
    table("Fonts", "font", lambda n: users(n, None))

    w("### Data files (%d)" % len(datafiles))
    w("")
    w("| Name | Used by | Status | Unity / notes |")
    w("|---|---|---|---|")
    for nm in datafiles:
        ub = users(nm, None)
        w("| `%s` | %s | imported | `Audio/credits.txt` (the sound credits; shown by the credits room, UH-T69) |"
          % (nm, fmt_users(ub) if ub else "no object or script reads it"))
    w("")

    w("### Resource-tree folders")
    w("")
    w("Every group in the resource tree below the top level, so an empty folder is recorded too.")
    w("")
    w("| Kind | Folder | Direct resources | Sub-folders |")
    w("|---|---|---|---|")
    for k in kinds:
        for g, n, nf in sorted(folders[k]):
            w("| %s | `%s` | %d%s | %d |" % (k, g, n, " (empty)" if n == 0 and nf == 0 else "", nf))
    w("")
    w("### Totals")
    w("")
    w("| Kind | Count | Status counts |")
    w("|---|---|---|")
    for t, c in total.items():
        w("| %s | %d | %s |" % (t, sum(c.values()), ", ".join("%s %d" % (k, v) for k, v in sorted(c.items()))))
    w("")
    text = "\n".join(L)

    if a.write:
        p = os.path.join(REPO, "docs", "RECONSTRUCTION.md")
        # byte-preserving: the file mixes CRLF and LF, so keep everything above the appendix as it is
        cur = open(p, "rb").read()
        i = cur.find(HEADING.encode("utf-8"))
        head = cur if i < 0 else cur[:i]
        with open(p, "wb") as f:
            f.write(head.rstrip(b"\r\n") + b"\n\n" + text.encode("utf-8"))
    elif a.out:
        open(a.out, "w", encoding="utf-8", newline="").write(text)
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(text)


if __name__ == "__main__":
    main()
