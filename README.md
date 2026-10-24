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
- the ambient insects, the dagger, the eight set-piece enemies and the enemy mine (`Enemies/`)
- the vortex emitters and generator mode's wormholes with `generate_enemy_list` (`EnemyEmitter`,
  `WormholeEmitter`, `Core/EnemyList`)
- death effects with the original sprites and sounds: tiny and grenade explosions, GameMaker's `ef_smokeup` and
  `ef_firework`, gibs and death particles (`Effects/`)
- the rock field: drift, spin, darkening, healing, breaking, flotsam, replacement (`Rocks/`)

Still to come, in build order: rules moved into Core as commands and events, tuning as data, the Input System
action asset, Unity.Shared settings, the URP seam, the UI, the art, sound and font pipelines, then the rooms,
landing, levels, HUD, menus, story, map, garage, boss and music. The full list, one ticket per step, is
[docs/RECONSTRUCTION.md, build order](docs/RECONSTRUCTION.md#build-order); open tickets are in the MindAttic ticket
database (`tickets ticket list --repo Unity.Hare`).

## Build, run and test

Prerequisites: Unity 6000.4.3f1 and the .NET SDK (for the Core tests). Close the Editor before any batchmode run;
`tools/unity.ps1` refuses to run while it is open.

In the Editor, open the project and use the **Unity.Hare** menu:

- **Set Up URP 2D** writes the 2D renderer and pipeline assets under `Assets/Unity.Hare/Settings/`.
- **Set Up Project Settings** sets the Input System as the active input handler.
- **Rebuild Main Scene** regenerates `Assets/Unity.Hare/Scenes/Main.unity` and the build settings from code.

Run the component tests from **Window > General > Test Runner** (EditMode).

From the command line (PowerShell, from the repo root):

```powershell
# pipeline, project settings and the main scene (each only if missing); prints compile errors
powershell -File tools/unity.ps1 -Method Unity.Hare.EditorTools.ProjectSetup.SetUpAll -Log setup

# Core's engine-free rules, outside Unity (extra arguments pass through to dotnet test)
powershell -File tools/run-core-tests.ps1

# the EditMode component tests
powershell -File tools/unity.ps1 -Log editmode -Extra "-runTests -testPlatform EditMode -testResults TestResults-EditMode.xml"
```

## Controls

What the components read today (raw Input System polling; the action asset with gamepad support is UH-T42):

| Input | Flying | Perched |
|---|---|---|
| A / D | turn left / right | |
| W or Space | boost | raise the shield (held) |
| S or Left Shift | brake | raise the shield (held) |
| Mouse | aim | aim |
| Left mouse | | fire the ground primary (vulcan, shotgun, flamer) |
| Ctrl + left mouse | drop a mine | fire the ground secondary (shotgun) |
| Right mouse | fire the air weapon (laser, railgun, rocket) | |
| Q | | next ground primary |
| E | next air weapon | next air weapon |

The right-mouse air loadout is not the original's layout; see [docs/BIBLE.md §8 V6](docs/BIBLE.md#UH-§8). The leap,
pause and hotkeys arrive with landing (UH-T53) and the hotkeys ticket (UH-T60).

## Project layout

```
Assets/Unity.Hare/
  Core/                Unity.Hare.Core asmdef (noEngineReferences): EnemyKind, EnemyList; the rules move here
  Scripts/             Unity.Hare.Runtime asmdef: the components
    Player/  Weapons/  Enemies/  Rocks/  Effects/
  Editor/              Unity.Hare.Editor asmdef: ProjectSetup, HareArtImporter
  Tests/EditMode/      Unity.Hare.Tests.EditMode: NUnit component tests (Unity Test Framework)
  Tests/PlayMode/      Unity.Hare.Tests.PlayMode: scripted play (UH-T70)
  Resources/Hare/      the original's sprites (with sprites.txt origins), particle shapes, sounds
  Audio/Weapons/       the vulcan, reload and empty-clip sounds
  Scenes/Main.unity    generated by ProjectSetup; do not edit by hand
  Settings/            generated URP 2D renderer and pipeline assets
Tests/Core.Tests/      dotnet test project that compiles Core/ directly (NUnit, C# 9)
tools/
  unity.ps1            Unity batchmode runner that surfaces compile and build errors
  run-core-tests.ps1   dotnet test for Core
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
