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
- the weather (U.HARE-T18): the play rooms' drifting clouds, and the rain, snow, sand and fog emitters a level can
  name in its `Weather` data, ruled by Core's `WeatherField` and drawn by `WeatherView` (`Weather/`)
- level setup and objectives (U.HARE-T57): each play room runs setup_level when it comes up and on the fade-in's level
  advance: the level's rocks, structures, vortices and mimics (or generator mode's wormhole emitter), the ship flown in,
  the obj_levelstart banner, and the objective (points, time limit with its sfx_beep countdown, defeat, survive) ticked
  on the room clock until set_level_complete pauses play (`Arena/ArenaRoom`, `Arena/LevelStartView`, Core's
  `LevelSetup`, `LevelStartBanner` and `GameSession`)
- scoring and the popups (U.HARE-T27): every scoring kill and rock break floats its points up from where it died
  (obj_float_text) and adds them to the score; obj_tip's sliding tip box and obj_alert's pinging mission marker are
  ready for the map and intro rooms (Core's `Popups/`, `Popups/FloatTextView`, `TipView`, `AlertMarkerView`)
- the title menu (U.HARE-T26): a key or click on room_title brings in obj_logo's buttons (Campaign, the eight
  quick-start levels, Enemy Generator, Empty Level, Quit Game), each turning cornflower blue with its message under
  the mouse; Campaign fades to the story, a level or difficulty to the arena in that game mode, and Quit asks
  "Are you sure?" (Yes or Enter quits the player, No or Esc closes it) (Core's `Rooms/TitleScreen`, `Rooms/TitleRoom`)
- player profiles: the shared MindAttic Unity.Shared package for display name, colour, music/SFX volume, mutes
  and reduce motion, plus Hare's own `difficulty`/`useMetric`/`fullscreen` block of settings.json
  (`Scripts/Settings/ActiveProfile.cs`, `HareSettings`); a campaign save belongs under the profile's `Saves/`
  folder (U.HARE-T65)
- the presentation seam: sorting layers (`Backdrop`/`Default`/`Overlay`), a 2D camera rig letterboxed to the
  original's 1920x1080 reference resolution, and the screen-fade/flash layer later tickets will drive
  (`Rendering/`); URP itself stays behind `Unity.Hare.Editor.Rendering`'s seam
- the UI foundation every HUD, menu, results, upgrade and story screen will build on: a PanelSettings and root
  VisualElement at the 1920x1080 reference resolution built entirely in code (`HarePanel`, `HareUiFit`), a shared
  theme (`Resources/Hare/UI/Theme.uss`), the original's eight `draw_set_font` faces (`HareFonts`) and the named
  text styles screens draw with (`HareTextStyles`), and a widget kit (`HareUi`) whose `OutlineText` family reproduces the
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

- the font pipeline (U.HARE-T48): all eight of the original's fonts (Berlin Sans FB baked at 11, 12, 16 and 36, and
  Arial 12 four times over), imported from `HARE.gmx` by `tools/import-gml-fonts.ps1` as GameMaker's own baked glyph
  atlases and glyph tables, built into one static bitmap FontAsset each (`Resources/Hare/Fonts`) that draws exactly
  the original's glyphs at its native size, and mapped to named text styles (`HareTextStyles`: `HudScore`,
  `HudText`, `HudAmmo`, `MenuTitle`, `MenuText`, `Typewriter`, `StoryTypewriter`, `Conversation`,
  `ConversationName`, `FloatText`, `Default`). Both typefaces are proprietary, so no font file ships; see
  [BIBLE §6](docs/BIBLE.md#UH-§6)

Still to come, in build order: the rooms,
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
- **Rebuild Main Scene (Bootstrap)** regenerates `Assets/Unity.Hare/Scenes/Main.unity`, the bootstrap: only the
  room flow's director, which sends the game to the first room. **Rebuild Room Scenes** regenerates every room's
  scene under `Assets/Unity.Hare/Scenes/Rooms/` (named after the GML rooms: `startup`, `room_title`, ...,
  `room_gamescreen` with the player ship) and the bootstrap, and the build settings (bootstrap first, then the
  rooms). Press Play on `Main.unity` to run the room flow, or on a room scene to start in that room.
- **Rebuild Online Scene** regenerates `Assets/Unity.Hare/Scenes/Online.unity`: the online game on the shared
  Unity.Multiplayer kit (U.HARE-T35). Open it and press Play: the kit's menu offers **Host**, **Join by IP**,
  **Join by code** and **Play solo** (the local game). For a second player, host in the Editor and join `127.0.0.1`
  from a build (or Multiplayer Play Mode). The local game, `Main.unity`, stays first in Build Settings and has none
  of the kit. `mp-host game local` / `mp-host game smoke` (Unity.Multiplayer's tool) serve this scene and run two
  `HareBot`s against it.

The multiplayer kit's packages are referenced by `file:` path, as sibling repos of this one: clone
`mindattic/Unity.Multiplayer`, `mindattic/MindAttic.Components` and `mindattic/Unity.Shared` beside it.

Run the component tests from **Window > General > Test Runner** (EditMode).

From the command line (PowerShell, from the repo root):

```powershell
# pipeline, project settings and the main scene (each only if missing); prints compile errors
powershell -File tools/unity.ps1 -Method Unity.Hare.EditorTools.ProjectSetup.SetUpAll -Log setup

# the online scene, its player prefab and prefab list, and mp-host.json (each only if missing)
powershell -File tools/unity.ps1 -Method Unity.Hare.EditorTools.Online.OnlineSceneBuilder.BuildAll -Log online

# the original's sprites and backgrounds, from HARE.gmx (default: the R:\ source path; -Source to point elsewhere),
# then the clips, atlases and HareArtManifest in Unity; rerunning it changes nothing (-NoUnity skips the Unity step)
powershell -File tools/import-gml-art.ps1

# the original's sounds and credits, from HARE.gmx, then the AudioMixer and HareAudioManifest in Unity; the same
# switches and the same rerun-changes-nothing guarantee as the art
powershell -File tools/import-gml-audio.ps1

# the original's fonts (GameMaker's baked glyph atlases and glyph tables), from HARE.gmx, then one bitmap FontAsset
# each in Unity; the same switches and guarantee
powershell -File tools/import-gml-fonts.ps1

# Core's engine-free rules, outside Unity (extra arguments pass through to dotnet test)
powershell -File tools/run-core-tests.ps1

# the EditMode component tests
powershell -File tools/unity.ps1 -Log editmode -Extra "-runTests -testPlatform EditMode -testResults TestResults-EditMode.xml"

# the PlayMode tests; -Graphics gives them a graphics device, which the text rendering test needs (it skips without)
powershell -File tools/unity.ps1 -Graphics -Log playmode -Extra "-runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml"

# only the online tests (a real host or dedicated server and real clients on a free loopback port)
powershell -File tools/unity.ps1 -Log online-tests -Extra "-runTests -testPlatform PlayMode -assemblyNames Unity.Hare.Tests.Online -testResults TestResults-Online.xml"

# a bare dev build (no icon/product-settings polish -- U.HARE-T71) to Build/Hare/Hare.exe
powershell -File tools/deploy.ps1

# builds (if Build/Hare/Hare.exe is missing or -Build is given) and launches it, muted by default
powershell -File tools/play.ps1
```

### Muted launches

`tools/play.ps1` launches muted: it sets `MINDATTIC_CLI_MUTED=1` and passes `-muted`, so a CLI or agent
starting the game never blasts audio. Pass `-Sound` to launch with audio. Either switch works on its own when
you start `Build/Hare/Hare.exe` (or the editor) yourself:

```
$env:MINDATTIC_CLI_MUTED = '1'; .\Build\Hare\Hare.exe
.\Build\Hare\Hare.exe -muted
```

The mute is the shared `com.mindattic.unity.shared` session mute: read before the first scene loads, it makes
every profile's `CommonSettings` report music and SFX muted at volume 0 for that run only. `HareAudio.ApplyProfile`
(already wired in by U.HARE-T47 to read the profile's music/SFX volume and mutes onto `HareMixer.mixer`) reads
those settings the same way whether the mute came from the CLI or from the profile's own saved mute, so no
extra glue is needed to reach the mixer -- Master, Music and Sfx are all silenced, and every `AudioSource` the
game makes routes through one of those groups (`HareAudio.Route`). The player's saved `settings.json` is never
written, so the next normal launch has sound. The `-batchmode` runs in `tools/unity.ps1` play nothing audible
and don't need it.

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

The system cursor is hidden while the rooms run: the mouse is drawn as obj_mouse, a reticle on the aim point in
play (green flying, blue when a leap can go that way, red when it cannot) and spr_pointer on the title, map and
upgrade rooms (U.HARE-T61, docs/BIBLE.md V38).

The weapon controls match gmlHARE (docs/BIBLE.md §8 V6). `HareControls` also carries a `Meta` action
map with keyboard and, where it makes sense, gamepad bindings defined as data. obj_game's keys are all
there (U.HARE-T60, docs/BIBLE.md V37):

| Key | In a play room |
|---|---|
| P | pause / resume (not once the level is complete) |
| R | reset the ship and set the level up again |
| M | show / hide the minimap |
| U | speedometer in mph / kph (saved) |
| Keypad + / - (held) | master volume up / down in 0.01 steps (saved on release) |
| F1 | fullscreen (any room that answers it) |
| F11 | +100 score (debug) |
| F12 | debug overlay |
| Esc | back to the title |

Leap, Shield, Flashbomb and Helper land with flashbombs (U.HARE-T67) and the helper.

## Project layout

```
Assets/Unity.Hare/
  Core/                Unity.Hare.Core asmdef (noEngineReferences): player, weapon and enemy rules (commands, events, tuning), EnemyKind, EnemyList
  Scripts/             Unity.Hare.Runtime asmdef: the components
    Player/  Weapons/  Enemies/  Rocks/  Effects/  Settings/ (ActiveProfile, HareSettings, SessionSettings)
    Art/               HareArtManifest: every GML sprite and background name to its imported assets
    Audio/             HareAudioManifest (every GML sound name to its clip and mixer channel), HareAudio (play by name, mixer volumes)
    Rendering/         sorting layers, the 2D camera rig and letterbox, the fade/flash overlay layer (no render pipeline reference)
    UI/                HarePanel/HareUiFit (PanelSettings at the 1920x1080 reference), HareFonts (the eight draw_set_font faces), HareTextStyles (the named text styles), HareUi (the widget kit: OutlineText, Button, Bar, Window, Dialog)
  Art/                 generated by tools/import-gml-art.ps1 and GmlArtBuilder; do not edit by hand:
    gml-art.json       every GML sprite and background, its source data, its Unity assets, and anything skipped
    Sprites/  Backgrounds/  Animations/  Atlases/
  Audio/               generated by tools/import-gml-audio.ps1 and GmlAudioBuilder; do not edit by hand:
    gml-audio.json     every GML sound, its sound.gmx fields, its source file, its clip, and anything skipped
    Sounds/            the clips, in the original's sound groups (music/, player/, vulcan/)
    HareMixer.mixer    Master > Music, Sfx; MasterVolume, MusicVolume, SfxVolume exposed
    credits.txt        datafiles/credits.txt, the original's sound credits
  Fonts/               generated by tools/import-gml-fonts.ps1; do not edit by hand:
    gml-fonts.json     every GML font, its font.gmx fields and glyph table, who draws with it, the typeface licences
    Atlases/           GameMaker's baked glyph atlases (GmlFontBuilder's source)
  Online/              Unity.Hare.Online asmdef (U.HARE-T35, on Unity.Multiplayer): HareNetPlayer (the NetEntity player), HareAuthority (the server's one world), HareOnlineGame (the front end's items), HareBot
  Editor/              Unity.Hare.Editor asmdef: ProjectSetup, HareArtImporter, HareBuild (the dev build tools/deploy.ps1 runs); Art/ (GmlArtData, GmlArtImporter, GmlArtBuilder); Audio/ (GmlAudioData, GmlAudioImporter, GmlAudioBuilder)
    Rendering/         Unity.Hare.Editor.Rendering asmdef: UrpSeam, SortingLayerSetup (the only code that references UnityEngine.Rendering.Universal)
    Online/            Unity.Hare.Editor.Online asmdef: OnlineSceneBuilder (Online.unity, the player prefab, the prefab list, mp-host.json)
  Tests/EditMode/      Unity.Hare.Tests.EditMode: NUnit component tests (Unity Test Framework)
  Tests/PlayMode/      Unity.Hare.Tests.PlayMode: scripted play (UH-T70)
  Tests/Online/        Unity.Hare.Tests.Online (PlayMode on the kit's NetSessionTest/NetBotTest) and .Online.EditMode (the scene's wiring)
  Resources/Hare/      the original's sprites (with sprites.txt origins), particle shapes; HareArtManifest, HareAudioManifest; UI/Theme.uss (U.HARE-T45)
  Scenes/Main.unity    generated by ProjectSetup; do not edit by hand
  Scenes/Online.unity  generated by OnlineSceneBuilder; do not edit by hand
  Settings/            generated URP 2D renderer and pipeline assets
Tests/Core.Tests/      dotnet test project that compiles Core/ directly (NUnit, C# 9)
tools/
  unity.ps1            Unity batchmode runner that surfaces compile and build errors
  run-core-tests.ps1   dotnet test for Core
  import-gml-art.ps1   imports the original's sprites and backgrounds into Assets/Unity.Hare/Art
  import-gml-audio.ps1 imports the original's sounds and credits into Assets/Unity.Hare/Audio
  import-gml-fonts.ps1 imports the original's fonts (baked glyph atlases and tables) into Assets/Unity.Hare/Fonts
  gml-json.ps1         the stable JSON writer the import scripts share
  deploy.ps1           a bare dev build (HareBuild.BuildWindows) to Build/Hare/Hare.exe (U.HARE-T80; release polish is U.HARE-T71)
  play.ps1             builds if needed, then launches Build/Hare/Hare.exe muted by default (-Sound to opt in; U.HARE-T80)
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
