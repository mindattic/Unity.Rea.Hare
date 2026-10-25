# Unity.Hare

A robotic rabbit flies, perches and fights in low orbit over a drifting asteroid field. A Unity 6 rebuild of
*HARE*, a GameMaker 8.1 arcade shoot-'em-up (in-game title *H.O.P., High Orbit Patrol*) that was functionally
complete but had hit the limits of GML.

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-222222)](ProjectSettings/ProjectVersion.txt)
[![C#](https://img.shields.io/badge/language-C%23-512BD4)](Assets/Unity.Hare)
[![Status](https://img.shields.io/badge/status-in%20development-orange)](docs/BIBLE.md)

There is no public build yet. Build it from source with the steps below.

## The game

The ship flies on its own heading: A and D turn it, boost and brake burn a shared energy bar, and mines drop
behind it. Land on a rock and the rabbit perches: from there it raises a shield wall and fires its ground weapons
(vulcan, shotgun, flamer and more) at the swarms that pour out of spinning vortices, then leaps off to fly again.
Rocks drift and spin, darken under fire, heal, and break into flotsam. Ten levels across an open arena, a vertical
shaft and a side-scroll stage lead to the Scrap Mental boss, with a story, a map and a garage between them.

## Status

In development, rebuilding the original to full parity. On main and covered by tests, as components (nothing is
placed in a playable room yet; the generated scene holds a camera and a light):

- the ship's flight, boost, brake, leap and shield (`Player/`), health and hurt (`PlayerHealth`)
- the shared four-pool ammo economy (`PlayerAmmo`) and every weapon: vulcan, laser, railgun, rocket, shotgun,
  flamethrower, mine (`Weapons/`)
- the ambient insects, the dagger, the eight set-piece enemies and the enemy mine: their rules are Core's
  (`Core/Enemies/`, an `EnemyWorld` changed by commands and driven by a seeded random), the `Enemies/` scripts are
  views
- the vortex emitters and generator mode's wormholes with `generate_enemy_list` (`Core/Enemies/Vortexes.cs`,
  `Core/EnemyList`)
- death effects with the original sprites and sounds: tiny and grenade explosions, GameMaker's `ef_smokeup` and
  `ef_firework`, gibs and death particles (`Effects/`)
- the rock field: drift, spin, darkening, healing, breaking, flotsam, replacement (`Rocks/`)
- player profiles: the shared MindAttic Unity.Shared package for display name, colour, music/SFX volume, mutes
  and reduce motion, plus Hare's own `difficulty`/`useMetric`/`fullscreen` block of settings.json
  (`Scripts/Settings/ActiveProfile.cs`, `HareSettings`); a campaign save belongs under the profile's `Saves/`
  folder (U.HARE-T65)
- the presentation seam: sorting layers (`Backdrop`/`Default`/`Overlay`), a 2D camera rig letterboxed to the
  original's 1920x1080 reference resolution, and the screen-fade/flash layer later tickets will drive
  (`Rendering/`); URP itself stays behind `Unity.Hare.Editor.Rendering`'s seam
- the UI foundation every HUD, menu, results, upgrade and story screen will build on: a PanelSettings and root
  VisualElement at the 1920x1080 reference resolution built entirely in code (`HarePanel`, `HareUiFit`), a shared
  theme (`Resources/Hare/UI/Theme.uss`), the named-slot seam for the original's eight `draw_set_font` faces
  (`HareFonts`, filled in by U.HARE-T48), and a widget kit (`HareUi`) whose `OutlineText` family reproduces the
  original's `draw_text_outline` scripts exactly, plus `Button`, `Bar`, `Window` and `Dialog` (`UI/`)

- the art pipeline (U.HARE-T46): all 213 of the original's sprites (398 frames) and 18 backgrounds, imported from
  `HARE.gmx` by `tools/import-gml-art.ps1` into `Assets/Unity.Hare/Art` with their GML origins, collision masks,
  bounding boxes and tiling, a looping animation clip per animated sprite timed from its `image_speed`, a sprite
  atlas per group, and `HareArtManifest` mapping every GML name to its Unity assets (`Art/`; the views don't draw
  from it yet)

- the audio pipeline (U.HARE-T47): all 75 of the original's sounds (69 `sfx_*`, 6 `music_*`), imported from
  `HARE.gmx` by `tools/import-gml-audio.ps1` into `Assets/Unity.Hare/Audio` with the load type each sound.gmx asks
  for (the music streamed Vorbis, the sfx PCM), an AudioMixer (`Audio/HareMixer.mixer`: Master, with Music and Sfx
  under it) whose volumes follow the profile's music and sfx volume and mutes, `HareAudioManifest` mapping every
  GML sound name to its clip and channel, `HareAudio` to play them by name, and the original's sound credits
  (`Audio/credits.txt`) beside the clips

Still to come, in build order: the font pipeline, then the rooms,
landing, levels, HUD, menus, story, map, garage, boss and music. The full list, one ticket per step, is
[docs/RECONSTRUCTION.md, build order](docs/RECONSTRUCTION.md#build-order); open tickets are in the MindAttic ticket
database (`tickets ticket list --repo Unity.Hare`).

## Build, run and test

Prerequisites: Unity 6000.4.3f1 and the .NET SDK (for the Core tests). Close the Editor before any batchmode run;
`tools/unity.ps1` refuses to run while it is open.

In the Editor, open the project and use the **Unity.Hare** menu:

- **Set Up URP 2D** writes the 2D renderer and pipeline assets under `Assets/Unity.Hare/Settings/`.
- **Set Up Project Settings** sets the Input System as the active input handler, the 1920x1080 reference
  resolution, and the `Backdrop`/`Default`/`Overlay` sorting layers.
- **Rebuild Main Scene** regenerates `Assets/Unity.Hare/Scenes/Main.unity` (the 2D camera rig, the global
  light and the player ship) and the build settings from code.

Run the component tests from **Window > General > Test Runner** (EditMode).

From the command line (PowerShell, from the repo root):

```powershell
# pipeline, project settings and the main scene (each only if missing); prints compile errors
powershell -File tools/unity.ps1 -Method Unity.Hare.EditorTools.ProjectSetup.SetUpAll -Log setup

# the original's sprites and backgrounds, from HARE.gmx (default: the R:\ source path; -Source to point elsewhere),
# then the clips, atlases and HareArtManifest in Unity; rerunning it changes nothing (-NoUnity skips the Unity step)
powershell -File tools/import-gml-art.ps1

# the original's sounds and credits, from HARE.gmx, then the AudioMixer and HareAudioManifest in Unity; the same
# switches and the same rerun-changes-nothing guarantee as the art
powershell -File tools/import-gml-audio.ps1

# Core's engine-free rules, outside Unity (extra arguments pass through to dotnet test)
powershell -File tools/run-core-tests.ps1

# the EditMode component tests
powershell -File tools/unity.ps1 -Log editmode -Extra "-runTests -testPlatform EditMode -testResults TestResults-EditMode.xml"
```

## Controls

What the components read today, through the generated `HareControls` wrapper over
`Assets/Unity.Hare/Scripts/Input/HareControls.inputactions` (U.HARE-T42: an Input Actions asset with
a keyboard&mouse scheme and a gamepad scheme, replacing the raw `Keyboard.current`/`Mouse.current`
polling the five views used before):

| Input | Flying | Perched |
|---|---|---|
| A / D, left/right arrow | turn left / right | |
| W or Space | boost | raise the shield (held) |
| S or Left Shift | brake | raise the shield (held) |
| Mouse | aim | aim |
| Left mouse | fire the air weapon (rocket or mine) | fire the ground primary (vulcan, laser, railgun, shotgun, flamer) |
| Ctrl + left mouse | drop a mine | fire the ground secondary (shotgun) |
| Q | | next ground primary (vulcan, laser, railgun, shotgun, flamer) |
| E | next air weapon (rocket, mine) | next air weapon (rocket, mine) |

The weapon controls match gmlHARE (docs/BIBLE.md §8 V6). `HareControls` also carries a `Meta` action
map (Leap, Shield, Flashbomb, Helper, Pause, Restart, Map, Debug, Menu) with keyboard and, where it
makes sense, gamepad bindings already defined as data; nothing reads them yet; they land with
landing (U.HARE-T53), the hotkeys ticket (U.HARE-T60), flashbombs (U.HARE-T67), the HUD/debug
overlay (U.HARE-T59) and the map/menu rooms (U.HARE-T19/U.HARE-T26).

## Project layout

```
Assets/Unity.Hare/
  Core/                Unity.Hare.Core asmdef (noEngineReferences): player, weapon and enemy rules (commands, events, tuning), EnemyKind, EnemyList
  Scripts/             Unity.Hare.Runtime asmdef: the components
    Player/  Weapons/  Enemies/  Rocks/  Effects/  Settings/ (ActiveProfile, HareSettings)
    Art/               HareArtManifest: every GML sprite and background name to its imported assets
    Audio/             HareAudioManifest (every GML sound name to its clip and mixer channel), HareAudio (play by name, mixer volumes)
    Rendering/         sorting layers, the 2D camera rig and letterbox, the fade/flash overlay layer (no render pipeline reference)
    UI/                HarePanel/HareUiFit (PanelSettings at the 1920x1080 reference), HareFonts (the draw_set_font seam), HareUi (the widget kit: OutlineText, Button, Bar, Window, Dialog)
  Art/                 generated by tools/import-gml-art.ps1 and GmlArtBuilder; do not edit by hand:
    gml-art.json       every GML sprite and background, its source data, its Unity assets, and anything skipped
    Sprites/  Backgrounds/  Animations/  Atlases/
  Audio/               generated by tools/import-gml-audio.ps1 and GmlAudioBuilder; do not edit by hand:
    gml-audio.json     every GML sound, its sound.gmx fields, its source file, its clip, and anything skipped
    Sounds/            the clips, in the original's sound groups (music/, player/, vulcan/)
    HareMixer.mixer    Master > Music, Sfx; MasterVolume, MusicVolume, SfxVolume exposed
    credits.txt        datafiles/credits.txt, the original's sound credits
  Editor/              Unity.Hare.Editor asmdef: ProjectSetup, HareArtImporter; Art/ (GmlArtData, GmlArtImporter, GmlArtBuilder); Audio/ (GmlAudioData, GmlAudioImporter, GmlAudioBuilder)
    Rendering/         Unity.Hare.Editor.Rendering asmdef: UrpSeam, SortingLayerSetup (the only code that references UnityEngine.Rendering.Universal)
  Tests/EditMode/      Unity.Hare.Tests.EditMode: NUnit component tests (Unity Test Framework)
  Tests/PlayMode/      Unity.Hare.Tests.PlayMode: scripted play (UH-T70)
  Resources/Hare/      the original's sprites (with sprites.txt origins), particle shapes; HareArtManifest, HareAudioManifest; UI/Theme.uss (U.HARE-T45)
  Scenes/Main.unity    generated by ProjectSetup; do not edit by hand
  Settings/            generated URP 2D renderer and pipeline assets
Tests/Core.Tests/      dotnet test project that compiles Core/ directly (NUnit, C# 9)
tools/
  unity.ps1            Unity batchmode runner that surfaces compile and build errors
  run-core-tests.ps1   dotnet test for Core
  import-gml-art.ps1   imports the original's sprites and backgrounds into Assets/Unity.Hare/Art
  import-gml-audio.ps1 imports the original's sounds and credits into Assets/Unity.Hare/Audio
  gml-json.ps1         the stable JSON writer both import scripts share
docs/
  BIBLE.md             design rules, architecture, data, fixed defects and deviations
  RECONSTRUCTION.md    the original's inventory, parity matrix, standards audit, build order
  REVERSE_ENGINEERING.md  per-object notes read from the GML source
  USER_STORIES.md      generated from the ticket database
  legacy-poc/          the old Unity 5 proof of concept, as non-compiling reference
```

References only point one way: Runtime → Core, Editor → Runtime and Core, tests → what they test. See
[docs/BIBLE.md §5](docs/BIBLE.md#UH-§5).

## Documentation

- [docs/BIBLE.md](docs/BIBLE.md): what the game is, the design rules, the architecture, the data files, every
  defect of the original that the rebuild fixes and every deliberate deviation, with the reason.
- [docs/RECONSTRUCTION.md](docs/RECONSTRUCTION.md): the original's inventory, what the rebuild still lacks and the
  build order to parity.
- [docs/REVERSE_ENGINEERING.md](docs/REVERSE_ENGINEERING.md): what each object and script of the original does.
- [AGENTS.md](AGENTS.md): the rebuild standard and where the original source lives.
