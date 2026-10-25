# Unity.Hare reconstruction: parity and standards audit

Audit of UH-T34 (owner 2026-10-23). It compares the original prototype with the rebuild as it stands on
main, and the project with Unity best practice and the shared MindAttic Unity standards. Every gap has
its own story and ticket, and the tickets are in build order: building them in order reaches full parity
and a finished proof of concept. The per-object behaviour notes stay in
[REVERSE_ENGINEERING.md](REVERSE_ENGINEERING.md); this file is the inventory, the gap list and the plan.

Stories and tickets live in the ticket database (`tickets ticket queue --repo Unity.Hare`); the queue
priority number is the build order (higher first), and every ticket in the list below requires the one
before it.

## The original

The authoritative original is GameMaker 8.1 `HARE.gmx` at
`R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlHARE\HARE.gmx` (the in-game title string is
H.O.P., High Orbit Patrol; H.A.R.E. is commented out in `globals.gml`). `gmlRabbit` is an earlier
build kept as reference only; nothing in it is needed for parity.

Counted again from the source for this audit (the counts in REVERSE_ENGINEERING.md were low):

| Kind | Count | Notes |
|---|---|---|
| Objects | 150 | includes 7 `par_*` parents |
| Scripts | 96 | levels, weapons, helpers, drawing helpers |
| Rooms | 10 | `startup`, `room_title`, `room_intro`, `room_story`, `room_map`, `room_gamescreen`, `room_sidescroll`, `room_shaft`, `room_upgrade`, `room_boss1` |
| Sprites | 214 | |
| Backgrounds | 18 | |
| Sounds | 75 | 69 `sfx_*`, 6 `music_*` |
| Fonts | 8 | `fnt_basic`, `fnt_medium`, `fnt_point`, `fnt_score`, `fnt_small`, `font4`, `font7`, `font8` |
| Data files | 1 | `datafiles/credits.txt` (sound credits) |
| Paths, timelines, shaders | 0 | |

Room sizes and speeds: gamescreen 3840x2160 at 120 steps/s, boss1 3000x2000, shaft 1920x12000,
sidescroll 12000x1080 (all 120 steps/s with views); title, intro, map, story, upgrade and startup are
1920x1080 at 60 steps/s.

### Game flow in the original

`startup` (circuit-board backdrop, controller note; any key) goes to `room_title`. The title's
`obj_stand_title` brings in the menu: quick start, campaign, generator (difficulty buttons), quit.
Campaign runs `room_story` (typewriter text over a scrolling earth), then `room_map` (the earth turns
with A/D between four missions, the USS Warren sends the white dot to the destination), then a level
in `room_gamescreen`, `room_shaft` or `room_sidescroll` chosen by `load_level` (scenario 1 is levels
1-5, the others 6-10; level 4 is the shaft, level 5 the side-scroll, level 8 the Scrap Mental boss in
the arena). Completing a level shows `obj_level_results`; Enter sends the rabbit away and returns to
the map. `room_upgrade` (garage) spends SKILLPOINTS on vulcan, shotgun and missile upgrades.

### Content the original never reaches

These exist in the project but nothing runs them, so parity does not require them (recorded so no one
rebuilds them by mistake):

- `room_boss1` is never entered by any script or object. The boss is level 8 (`obj_scrapmental`) in
  `room_gamescreen`. `obj_structure1`, `obj_structure1_cover` named in the earlier notes do not exist
  in HARE.gmx; only `par_structure` and `par_structure_cover` do.
- `obj_ring` / `obj_ring_back` (a ring gate) are only referenced from commented-out code in `obj_game`.
- `obj_butterfly_emitter`, `obj_firefly_emitter`, `obj_fly_emitter` and the weather emitters other than
  the cloud emitter are not placed by any room or level script (the weather ones are gated by
  commented-out lines); UH-T18 still ports them because they are real objects.
- There is no grenade weapon (already noted), and `obj_fly`'s second frame never shows (already noted).

## Parity matrix

State of the rebuild on main against every part of the original. "Built" means present as game
components with EditMode tests; nothing yet runs in a room, with art or sound (the only scene is the
generated `Main.unity`, the only art is none, the only audio is six weapon wavs).

| Area | Original | Rebuild on main | Ticket |
|---|---|---|---|
| Player flight, boost, brake, leap, shield | `obj_player`, `player_*`, `obj_shieldwall` | Built (`PlayerShipController`, `ShieldWall`) | done: UH-T5, UH-T7 |
| Player health, hurt, invulnerability | `player_hurt`, healthbar | Built (`PlayerHealth`) but no lives, death wait, respawn or restart | UH-T6 done, rest in UH-T54 |
| Landing on rocks and structures, perch, crouch, `fling_player`, spin | `obj_player` collisions | `Land()` exists, nothing calls it | UH-T53 |
| Player visuals: poses, held weapon, trails, flash | `spr_rabbit_*`, `effect_create_*` | None | UH-T55 |
| Ammo economy | `set_ammo_cost`, regen alarms | Built (`PlayerAmmo`) | done: UH-T12 |
| Weapons: vulcan, laser, railgun, rocket, shotgun, flamer, mine | `shoot_*`, `fire_*` | Built | done: UH-T8 to UH-T11 |
| Insects and dagger | `ai_*`, `obj_dagger` | Built | done: UH-T13 |
| Set-piece enemies (8) | `obj_hydra` ... `obj_mimic` | Built (`SetPieceEnemy` family) | done: UH-T14 |
| Emitters, wormholes, `generate_enemy_list` | `par_enemy_emitter`, `obj_wormhole*` | Built (`EnemyEmitter`, `WormholeEmitter`, `EnemyList`) but not driven by a level | done: UH-T15; level hook in UH-T57 |
| Death effects, gibs, death particles | `spawn_death_particles`, `*_gib`, `obj_tinyexplosion`, `ef_smokeup`/`ef_firework` | Built (`DeathEffects`, `GmlEffects`, `TinyExplosion`, `DeathParticle`) with the original sprites and sounds; `spawn_death_particles` waits on its callers (UH-T54, UH-T68) | done: UH-T16 |
| Rocks | `par_rock`, `obj_rock1-5`, `create_new_rock`, `move_rocks`, `rock_destroyed`, `obj_flotsum` | Built (`Rock`, `RockField`, `Flotsum`) with the original sprites, breaking with the UH-T16 death effects; `fling_player` waits on landing (UH-T53), the level values on UH-T20/UH-T57 | done: UH-T17 |
| Structures and covers | `par_structure`, `par_structure_cover` | None | UH-T56 |
| Weather and ambience | clouds, rain, snow, sand, fog | None | UH-T18 |
| Backdrop props | `obj_moon_surface`, `obj_surface_reflection`, backgrounds (`obj_flotsum` is built with the rocks) | None | UH-T58 |
| Pickups | carrot, coin, chip, cartridge, `random_pickup`, `spawn_chips` | None | UH-T23 |
| VFX | muzzle flash, oval/tiny/rocket/grenade explosion, rail swirl | Rocket and grenade explosion logic only | UH-T31 |
| Level data and the ten levels | `level_01-10`, `level_default`, composition/objective/setting | None (`EnemyComposition` is a start) | UH-T20 |
| Level setup, objectives, time limit, level start banner | `setup_level`, `obj_game`, `obj_levelstart`, `set_level_complete` | None | UH-T57 |
| Arena room, camera, loops, boundaries, minimap | `obj_game`, room views | None | UH-T52 |
| Side-scroll and shaft stages | `room_sidescroll`, `room_shaft` | None | UH-T21 |
| Level results | `obj_level_results` | None | UH-T22 |
| HUD: health, energy, weapon icons, speedometer | `obj_healthbar`, `obj_energybar`, `obj_weapon_gui`, `obj_speedometer` | None | UH-T25 |
| HUD: score, objective, clock, lives, debug overlay | `obj_game` Draw | None | UH-T59 |
| Floating text, tips, alerts | `obj_float_text`, `obj_tip`, `obj_alert` | None | UH-T27 |
| Hotkeys and pause | `obj_game` key events | None | UH-T60 |
| Reticle and pointer | `obj_mouse` | None | UH-T61 |
| Title, menus, quit dialog | `obj_title`, `obj_btn_*`, `obj_yesno_dialog` | None | UH-T26 |
| Title and intro presentation | `obj_drop_*`, `obj_stand_title`, `obj_logo`, `obj_intro` | None | UH-T62 |
| Story and typewriter | `obj_story`, `obj_conversation`, `obj_typewriter*`, `load_scenario` | None | UH-T28 |
| Map room | `obj_map`, `obj_destination`, `obj_btn_level*` | None | UH-T19 |
| Map scenery | `obj_earthball`, `obj_scanner`, `obj_uss_warren`, `obj_white_dot`, `obj_scenario_details` | None | UH-T63 |
| Game modes and difficulty | `gm_*`, `obj_btn_diff*`, `level_enemygenerator` | None | UH-T64 |
| Garage and upgrades | `obj_upgrade*`, schematics, `SKILLPOINTS` | None | UH-T24 |
| Helper hawk | `obj_helper`, `create_helper` | None | UH-T66 |
| Flashbomb | `obj_flashbomb`, `obj_rabbit_schematic` | None | UH-T67 |
| Boss: Scrap Mental | `obj_scrapmental`, `obj_shrapnel_*` | None | UH-T68 (supersedes UH-T32) |
| Music and jukebox | `obj_jukebox`, `play_random_music` | None | UH-T29 |
| Sound effects | 69 `sfx_*` | Six wavs | UH-T47 (import), UH-T30 (wiring) |
| Credits | `datafiles/credits.txt` | None | UH-T69 |
| Startup room | `obj_startup` | None | UH-T51 |
| Room flow and fades | `obj_fade_*`, `next_room` | None | UH-T50 |
| Sprites, backgrounds | 213 (398 frames) / 18 | All imported, none skipped: `tools/import-gml-art.ps1` + `GmlArtBuilder` into `Assets/Unity.Hare/Art`, mapped by `HareArtManifest` (not yet drawn by the views, which still use `Resources/Hare/Sprites`) | done: U.HARE-T46 |
| Fonts | 8 | None | UH-T48 |
| Session state | `globals.gml` | None | UH-T49 |
| Campaign persistence | (not in the original) | None | UH-T65 (deliberate addition) |

## Standards audit

Findings against AGENTS.md "Rebuild standard", Unity best practice and the shared Unity standards.
Reference architectures: Unity.SpaceGod and Unity.GridGame.

| Finding | Evidence | Ticket |
|---|---|---|
| No assembly split; Core is not engine-free and not tested outside Unity | one `Unity.Hare` asmdef with `noEngineReferences: false`; one EditMode test asmdef; no dotnet test project | UH-T37 |
| No README, no BIBLE | neither file in the repo | done: UH-T38 (README.md, docs/BIBLE.md) |
| Rules live in MonoBehaviours (player, weapons) | `PlayerShipController`, `PlayerHealth`, `PlayerAmmo`, loadouts | UH-T39 |
| Rules live in MonoBehaviours (enemies), engine random | `SetPieceEnemy`, enemy scripts, `UnityEngine.Random` | done: UH-T40 (Core enemy world, seeded random) |
| Tuning and content hard-coded | `SerializeField` constants and literals throughout | UH-T41 |
| Input System used as raw polling, no actions asset, no gamepad | `Keyboard.current` / `Mouse.current` in five scripts | UH-T42 |
| Unity.Shared profiles and settings not adopted | not in `Packages/manifest.json` | done: UH-T43 (`com.mindattic.unity.shared`, `ActiveProfile`, `HareSettings`) |
| URP present but no pipeline-agnostic seam, no reference resolution handling | `ProjectSetup` generates the 2D renderer only | done: UH-T44 (`Unity.Hare.Editor.Rendering`'s `UrpSeam`/`SortingLayerSetup`, the `Backdrop`/`Default`/`Overlay` sorting layers, `CameraRig`/`LetterboxCamera`, 1920x1080 reference resolution) |
| No UI stack (UI Toolkit or MindAttic components) | none | done: UH-T45 (UI Toolkit: `Scripts/UI/HarePanel`/`HareUiFit` for the PanelSettings and 1920x1080 reference resolution, `Resources/Hare/UI/Theme.uss`, `HareFonts` the `draw_set_font` face seam for U.HARE-T48, `HareUi` the widget kit — `OutlineText` family reproducing `draw_text_outline` exactly, `Button`, `Bar`, `Window`, `Dialog`) |
| Multiplayer-ready seams and the shared Unity.Multiplayer kit | already ticketed | UH-T35 |
| Tests are EditMode only; no PlayMode, no scripted play with screenshots | no PlayMode asmdef | UH-T70 |
| No release polish (name, icon, build settings) | not started | UH-T71 |
| Scenes composed in code | `ProjectSetup` generates the one scene; the flow scenes are not generated yet | UH-T50 |

Already meeting the standard: Unity 6 (6000.4.3f1), URP 17.4 with a generated 2D renderer behind a
pipeline-agnostic seam, a 2D camera rig letterboxed to the original's 1920x1080 reference resolution, the Input
System package, an EditMode test assembly, scene, pipeline and sorting-layer settings generated by an editor
script.

## Build order

The queue priority of each ticket is its place in this list (1000 for the first, 10 less each step).
In-flight tickets (UH-T16, UH-T17) come first. "Existing" marks tickets that already existed; the rest
were filed by this audit. UH-T32 (boss room, structures) was rejected as superseded by UH-T56 and UH-T68.

| # | Ticket | What |
|---|---|---|
| 1 | UH-T16 | Death effects, gibs, particles (existing, in flight) |
| 2 | UH-T17 | Rock field (existing, in flight) |
| 3 | UH-T37 | Assembly definitions, Core, dotnet test project |
| 4 | UH-T38 | README and BIBLE |
| 5 | UH-T39 | Core: player and weapon rules as commands and events |
| 6 | UH-T40 | Core: enemy rules and spawn pacing |
| 7 | UH-T41 | Tuning and content as validated data |
| 8 | UH-T42 | Input System actions asset |
| 9 | UH-T43 | Unity.Shared profiles and settings |
| 10 | UH-T44 | Pipeline-agnostic URP 2D seam, reference resolution |
| 11 | UH-T45 | UI foundation |
| 12 | UH-T46 | Sprite and background import pipeline |
| 13 | UH-T47 | Audio import and mixer |
| 14 | UH-T48 | Font import |
| 15 | UH-T49 | GameSession state (the globals) |
| 16 | UH-T35 | Unity.Multiplayer scaffolding (existing) |
| 17 | UH-T50 | Room flow, fades, scenes composed in code |
| 18 | UH-T51 | Startup room |
| 19 | UH-T52 | Arena room, camera, loops, boundaries, minimap |
| 20 | UH-T53 | Landing, perching, leaping |
| 21 | UH-T54 | Lives, death, respawn, game over |
| 22 | UH-T55 | Rabbit presentation |
| 23 | UH-T31 | Weapon and explosion VFX (existing) |
| 24 | UH-T23 | Pickups (existing) |
| 25 | UH-T56 | Structures and covers |
| 26 | UH-T18 | Weather (existing) |
| 27 | UH-T20 | Level data and the ten levels (existing) |
| 28 | UH-T57 | Level setup, objectives, banner |
| 29 | UH-T21 | Side-scroll and shaft stages (existing) |
| 30 | UH-T58 | Backdrop props and level backgrounds |
| 31 | UH-T22 | Level results (existing) |
| 32 | UH-T25 | HUD: health, energy, weapons, speedometer (existing) |
| 33 | UH-T59 | HUD: score, objective, clock, lives, debug |
| 34 | UH-T27 | Floating text, tips, alerts (existing) |
| 35 | UH-T60 | Hotkeys and pause |
| 36 | UH-T61 | Reticle and pointer |
| 37 | UH-T26 | Title and menus (existing) |
| 38 | UH-T62 | Title and intro presentation |
| 39 | UH-T28 | Story and typewriter (existing) |
| 40 | UH-T19 | Map room (existing) |
| 41 | UH-T63 | Map scenery |
| 42 | UH-T64 | Game modes and difficulty |
| 43 | UH-T24 | Garage and upgrades (existing) |
| 44 | UH-T65 | Campaign save and load |
| 45 | UH-T66 | Helper hawk |
| 46 | UH-T67 | Flashbomb |
| 47 | UH-T68 | Scrap Mental boss |
| 48 | UH-T29 | Music (existing) |
| 49 | UH-T30 | Sound effects wiring (existing) |
| 50 | UH-T69 | Credits |
| 51 | UH-T70 | PlayMode tests and scripted play verification |
| 52 | UH-T71 | Release polish |
| 53 | UH-T33 | End-to-end port verification (existing) |

## How this audit was made

The source was read directly from the `.gmx` project (room files for size, view and instance counts,
every script under `scripts/` that sets level data or flow, and the event code of the objects that the
existing notes did not cover: `obj_game`, `obj_startup`, `obj_title`, `obj_map`, `obj_helper`,
`obj_flashbomb`, `obj_scrapmental` and the shrapnel family, the HUD objects, pickups, results and
upgrade). Behaviour of the objects the earlier tickets already ported stays as recorded in
REVERSE_ENGINEERING.md. Behaviour that a later ticket ports is read from the source again by that
ticket and added to REVERSE_ENGINEERING.md before it is built, as AGENTS.md rule 1 requires.

Not read in full, so the owning ticket must do it first: the sound data (UH-T47; the sprite and
background data were read in full by U.HARE-T46, see REVERSE_ENGINEERING.md, "Sprites and backgrounds"), `obj_conversation` and the story text (UH-T28), `obj_upgrade_*` costs and effects (UH-T24),
the weather emitters (UH-T18), and `obj_btn_*` handlers
(UH-T26, UH-T64).
