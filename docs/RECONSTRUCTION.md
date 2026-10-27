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
with A/D between four missions, the USS Warren sends the white dot to the destination; the rebuild has ten,
one per level, and keeps the chosen one, by the owner's direction, BIBLE V42), then a level
in `room_gamescreen`, `room_shaft` or `room_sidescroll` chosen by `load_level` (scenario 1 is levels
1-5, the others 6-10; level 4 is the shaft, level 5 the side-scroll, level 8 the Scrap Mental boss in
the arena). Completing a level shows `obj_level_results`; Enter sends the rabbit off the top of the room
and the next level is set up at once (the original never goes back to the map after a level; BIBLE V31). `room_upgrade` (garage) spends SKILLPOINTS on vulcan, shotgun and missile upgrades.

### Content the original never reaches

These exist in the project but nothing runs them, so parity does not require them (recorded so no one
rebuilds them by mistake):

- `room_boss1` is never entered by any script or object. The boss is level 8 (`obj_scrapmental`) in
  `room_gamescreen`. `obj_structure1`, `obj_structure1_cover` named in the earlier notes do not exist
  in HARE.gmx; only `par_structure` and `par_structure_cover` do.
- `obj_ring` / `obj_ring_back` (a ring gate) are only referenced from commented-out code in `obj_game`.
- `obj_butterfly_emitter`, `obj_firefly_emitter`, `obj_fly_emitter` and the weather emitters other than
  the cloud emitter are not placed by any room or level script (the weather ones are gated by
  commented-out lines); U.HARE-T18 still ports them because they are real objects (a level can name them in its
  `Weather` data; no shipped level does).
- `obj_moon_surface` is created by nothing and nothing sets its `phase` to "on" (the title's moon is
  `obj_stand_title`'s own `draw_sprite(spr_moon_surface, 0, 0, y + 250)`); U.HARE-T58 ports it as it is
  (Core `MoonSurface`, Runtime `MoonSurfaceView`), created by nothing, as `obj_oval_explosion` is.
- `load_level` (and so `level_setting_00`/`_01`, `level_objective_00`/`_01`, `level_composition_00`/`_01`) is
  called by nothing, and `level_dialog`, which the objective parts set, is read by nothing. U.HARE-T20 ports
  the six parts as data and `load_level` as `ArenaTuning.LoadLevel` because the ticket asks for the model;
  no shipped flow uses them (BIBLE V27).
- There is no grenade weapon (already noted), and `obj_fly`'s second frame never shows (already noted).
- `room_intro` is entered by nothing: no script or object names it and it is not the first room. U.HARE-T62 ports it
  (`IntroScreen`, `IntroRoom`) because it is a real room; the room flow can be sent there.
- `obj_oval_explosion` is created by nothing; U.HARE-T31 ports it (`OvalExplosion`) because it is a real object,
  but nothing spawns it.

## Parity matrix

State of the rebuild on main against every part of the original. "Built" means present as game
components with EditMode tests; nothing yet runs in a room, with art or sound (the only scene is the
generated `Main.unity`; the art and sound are imported, U.HARE-T46 and U.HARE-T47, but mostly not yet played).

| Area | Original | Rebuild on main | Ticket |
|---|---|---|---|
| Player flight, boost, brake, leap, shield | `obj_player`, `player_*`, `obj_shieldwall` | Built (`PlayerShipController`, `ShieldWall`) | done: UH-T5, UH-T7 |
| Player health, hurt, invulnerability, death and respawn | `player_hurt`, `reset_player`, `obj_losthealth`, `obj_player` Alarms 1/4/5/6/7 and its out-of-bounds rule, `game_restart` | Built: Core `ShipHealth` (HP 6, hurt pose and sfx_hurt1..8, Alarm 5's colour cycle, death with `LIVES -= 1`, the 320-step wait, `reset_player`'s whole-degree heading) counting `LIVES` through an `ILivesLedger`, the session's under the room flow (`SessionLives`, `SpendLife`); `LostHealthPip`; `PlayerHealth` plays death particles, the respawn drop-in at the arena's spawn point and sfx_flyover; `ArenaRoom` binds the session's lives, puts `LostHealthView` on its HUD and hands game over to `RoomFlowDirector.RestartGame` (a new `GameSession`, back to the first room) | done: UH-T6, U.HARE-T54, U.HARE-T55 (the ship's sprite and the justspawned smoke) |
| Landing on rocks and structures, perch, crouch, `fling_player`, spin | `obj_player` collisions, `player_leap`, `fling_player`, `normal_detect`, `in_view`, Alt/middle-mouse/Space/right-mouse events, Alarm 1 | Built: Core `ShipFlight.LandOn`/`RideRock`/`Fling`/`Crouch`/`StartSpin`/`StopSpin` (facing = image_angle, current_rock/current_structure, land_angle, distance_to_center, take-off bonus, was_spinning), `PlayerShipController` + `PerchProbe` landing on any `IPerchSurface` (rocks, and the structures of U.HARE-T56), rocket hits fling a perched ship, Space/right mouse = `Leap`, Alt/middle mouse = `Spin` | done: U.HARE-T53 |
| Player visuals: poses, held weapon, trails, flash | `spr_rabbit_*`, `spr_weapon_*`, obj_player's Draw, Step (justspawned jet) and Alarms 2/3/5, `effect_create_below` (`ef_smokeup`, `ef_smoke`, `ef_spark`), `scale`, `player_size` | Built: Core `ShipLook` (the held ground weapon from `next_ground_weapon` and every `shoot_*`, Alarm 2's smoke trail, Alarm 3's spark trail while `turning`, the justspawned jet, the draw flags; numbers in `look`) and `ShipFlight.IsTurning`; `PlayerLook` draws the spr_rabbit_* pose (`PlayerShip.Pose`) at `scale` in Alarm 5's colour, the vulcan/laser/railgun/shotgun/flamer under it at gunX/gunY turned to mouse_angle while perched, nothing while dead, and plays the trails; `ef_smoke` and `ef_spark` (with `pt_shape_spark`) join `GmlEffects`; `player_size` is the arena's `PlayerSize`, held to spr_rabbit_idle's width by test | done: U.HARE-T55 |
| Ammo economy | `set_ammo_cost`, regen alarms | Built (`PlayerAmmo`) | done: UH-T12 |
| Weapons: vulcan, laser, railgun, rocket, shotgun, flamer, mine | `shoot_*`, `fire_*` | Built | done: UH-T8 to UH-T11 |
| Insects and dagger | `ai_*`, `obj_dagger` | Built | done: UH-T13 |
| Set-piece enemies (8) | `obj_hydra` ... `obj_mimic` | Built (`SetPieceEnemy` family) | done: UH-T14 |
| Emitters, wormholes, `generate_enemy_list` | `par_enemy_emitter`, `obj_wormhole*` | Built (`EnemyEmitter`, `WormholeEmitter`, `EnemyList`) but not driven by a level | done: UH-T15; level hook in UH-T57 |
| Death effects, gibs, death particles | `spawn_death_particles`, `*_gib`, `obj_tinyexplosion`, `ef_smokeup`/`ef_firework` | Built (`DeathEffects`, `GmlEffects`, `TinyExplosion`, `DeathParticle`) with the original sprites and sounds; `spawn_death_particles` waits on its callers (UH-T54, UH-T68) | done: UH-T16 |
| Rocks | `par_rock`, `obj_rock1-5`, `create_new_rock`, `move_rocks`, `rock_destroyed`, `obj_flotsum` | Built (`Rock`, `RockField`, `Flotsum`) with the original sprites, breaking with the UH-T16 death effects; the ship rides them and is flung when one breaks or leaves the room (U.HARE-T53), the level values on UH-T20/UH-T57 | done: UH-T17 |
| Structures and covers | `par_structure`, `par_structure_cover`, `setup_level`'s structure pass and Clear Objects, `move_rocks`, `random_positions` | Built: `StructureField` (structure_min/max per level as `LevelSetting.StructureMin/Max`, placement, the spawn-point clear), `Structure` (image_angle +/-30, precise mask, bullets spent in a tiny explosion, rockets go off, `IBeamBlocker`, `IPerchSurface` landing), `StructureCover` (depth -1 over it, follows it: BIBLE D15), `LevelReset.ClearObjects` (rocks, structures, enemies, projectiles, pickups), wired in `ArenaRoom.Begin`; BIBLE V25 | done: U.HARE-T56 |
| Weather and ambience | `obj_cloud_emitter`/`obj_cloud` (placed by every play room), `obj_rain_emitter`/`obj_raindrop`, `obj_snow_emitter`/`obj_snow`, `obj_sand_emitter`/`obj_sand`, `obj_fog_emitter`/`obj_fog` | Built: Core's `Weather/WeatherField` (each emitter's Create, alarm and Step, each particle's Create, Step, motion and pause, the original's random draws in its order; numbers in `weather`), `LevelSetting.Weather` per level (every shipped level `Cloud`, as the rooms place it), Runtime's `WeatherView` drawing it with the original's sprites, alphas, angles and depths, started by `ArenaRoom.Begin`, stepped each room step and cleared by `LevelReset.ClearObjects`; snow that never fell, sand that rose for ever and invisible fog fixed (BIBLE D16-D18, V26) | done: U.HARE-T18 |
| Backdrop props | `obj_moon_surface`, `obj_surface_reflection`, backgrounds (`obj_flotsum` is built with the rocks) | Built: `obj_surface_reflection` as Core's `SurfaceReflection` on the arena (`StepReflection`: Alarm 0's cornflower `ef_firework` every 5 steps within 32 px of the water line, the reflection 250 px above `room_sidescroll`'s bottom, the water as a floor that turns the ship to 0 or 180; numbers in the side-scroll room's `Reflection` data) drawn by `SurfaceReflectionView` (the ship's pose at depth 2, alpha 0.3, yscale 0.5), the ship's half `SkimSurface`; `obj_moon_surface` as `MoonSurface`/`MoonSurfaceView`, created by nothing as in the original; `obj_flotsum` was U.HARE-T17's (`Flotsum`, D5); every level's backgrounds and scroll speeds were U.HARE-T52's (`ArenaWorld` layers, `ArenaRoom.BuildLayers`), checked against the source again; BIBLE V30, D20 | done: U.HARE-T58 |
| Pickups | carrot, coin, chip, cartridge, `random_pickup`, `spawn_chips` | Built: Core `Pickups/PickupField` (random_pickup's rolls, obj_carrot's hover/ping/heal, obj_coin's grades/fall/sparkle/COINS, the 48 px magnet, spawn_chips and obj_chip, obj_cartridge's cartridge and shell; numbers in `pickups`), `RestoreHealth` on the ship, the session's `CollectCoin`; Runtime `PickupDirector`, `PickupView`, `ScrapView`, wired to enemy kills, the vulcan, the shotgun, `player_leap` and the par_rock landing; `ef_ring` in `GmlEffects` (BIBLE V24) | done: U.HARE-T23 |
| VFX | `obj_muzzleflash`, `obj_oval_explosion`/`obj_tinyexplosion`/`obj_rocket_explosion`/`obj_grenade_explosion`, `generate_swirl`/`obj_rail_swirl`, the weapons' `effect_create_below` (`ef_smoke`, `ef_smokeup`, `ef_flare`) | Built: `MuzzleFlash` (vulcan, shotgun), `RocketExplosion` drawn and sounded, `OvalExplosion` (created by nothing, as in the original), `RailgunShot`'s swirls inside the room and its smoke, the shotgun/flamer smoke and the laser's flare through `GmlEffects` (BIBLE V23) | done: U.HARE-T31 |
| Level data and the ten levels | `level_01-10`, `level_default`, `level_enemygenerator`, composition/objective/setting, `load_level` | Built: every level script as one `LevelSetting` in the `arena` section (its setting; `RockMin/Max`, `StationaryPercent` (-1 where the script leaves the global alone), `StructureMin/Max`; `Enemies`, each kind's min/max, `ToComposition()` for setup_level's Generate blocks; `Objective`, kind, goal, time limit, target name, level_08's boss object, and which of score and clock it resets, `ToObjective()` for the session's `BeginLevel`), `StartingStationaryPercent`, and load_level's parts (`SettingParts`, `ObjectiveParts`, `CompositionParts`, `LoadLevel`); BIBLE V27. Placing them at level start is U.HARE-T57's | done: U.HARE-T20 |
| Level setup, objectives, time limit, level start banner | `setup_level`, `obj_game`, `obj_levelstart`, `set_level_complete` | Built: `ArenaRoom.Begin` as setup_level (Clear Objects, banner, the ship via `PlayerHealth.SetUp`/`SetUpShip`, the session's `BeginLevel` with level_name and stationary_percent, rocks, structures, vortices and mimics, generator mode's wormhole emitter), `LevelSetup` (script and room_goto per game mode), `LevelStartBanner`/`LevelStartView` (obj_levelstart), the session clock and objective check every room step (sfx_beep countdown, o_points, o_timelimit, o_defeat through `LevelTargets`, o_survive), set_level_complete pausing play, and the fade-in's level advance; BIBLE V28 | done: U.HARE-T57 |
| Arena room, camera, loops, boundaries, minimap | `obj_game`, room views | Core's `Arena/ArenaWorld` (the play rooms' sizes from `rooms`, their views and background slots and every level script's setting half as data in `arena`: backgrounds, scroll speeds, loop_horizontal/loop_vertical/randomize_on_loop; the backgrounds' scroll; view 0 and the minimap following the player as GameMaker's runner does, inside the room, view 0 at 40 px a step; show_map and M; obj_player's wrap and 400 px buffer_zone rules with random_positions; the off-screen arrow and dotted leap line), played by `ArenaRoom` (obj_game) in each play room: the level's imported backgrounds, the minimap camera in its 300x200 port, the 1920x1080 HUD layer, `PlayerArrows` (spr_direction_arrow, spr_big_arrow, the leap line); out of bounds costs a life through the ship's `LeaveArena` (lives unified with the session in UH-T54); the Meta/Map action is now M, as in obj_game | done: U.HARE-T52 |
| Side-scroll and shaft stages | `room_sidescroll`, `room_shaft`, `obj_spawnpoint` | Built on the arena, with no stage code of its own (the stage rooms differ from `room_gamescreen` only in data and in what reads room_width/room_height): `room_sidescroll` (level_05, 12000x1080, bg_tropicalocean, loop_horizontal with random_positions, off the top or bottom costs a life) and `room_shaft` (level_04, 1920x12000, bg_stormclouds tiled both ways scrolling left, wrapping past sprite_width sideways and past buffer_zone vertically, no scatter), view 0 with no speed limit and the minimap at 1300,860, reset_player at room_width/2 above the room (U.HARE-T52, U.HARE-T57); obj_bullet's and obj_rocket's buffer_zone room check as `ArenaWorld.OutOfPlay` on the running room (`ArenaRoom.Active`), replacing the lifetime and range stand-ins inside a play room. Each stage room's pre-placed `obj_player` at 1362,1098 is not rebuilt (BIBLE D19); `obj_spawnpoint` (1498,-33 in both) only sets `angle = 270`, draws nothing (`//draw_self()`) and is read by nothing, so it has no port; `obj_surface_reflection` is UH-T58's; BIBLE V29 | done: U.HARE-T21 |
| Level results | `obj_level_results`, `set_level_complete`, obj_player's `state_leave`, `check_rotate_clockwise`, obj_healthbar's `strTime`, `KILLS` | Built: Core's `LevelResults` (the panel's layout and five lines; numbers and text in `arena.Results`) and `HudClock` (strTime), `ShipFlight.Leave`/`AimExit` (3 degrees a step toward exit_x above the room at twice flight_speed, the leap pose, the black and orange smoke as `LeaveSmokePuffed`), `ShipHealth.MakeInvulnerableToLeave`, the arena's `SendPlayerOff`/`StepLeave` (exit_angle, then off the top once); Runtime's `LevelResultsView` on the HUD, `ArenaRoom` (shown on `LevelCompleted`, Enter through an Input System action, the next level with the score reset), the ship frozen while paused; `KILLS` counted from `EnemyDirector.Killed` (D11); a ship dead at Enter leaves too (D21); BIBLE V31 | done: U.HARE-T22 |
| HUD: health, energy, weapon icons, speedometer | `obj_healthbar`, `obj_energybar`, `obj_weapon_gui`, `obj_speedometer` | Built: Core's `GameHud` (numbers, sprites and icons in `arena.Hud`; the pips' column and size are `HealthTuning`'s, shared with obj_losthealth): obj_healthbar whole (spr_extralives and "x LIVES", `strTime` from the room's `HudClock`, COINS beside spr_coin_small, one pip per point of MAXHP, full or back-and-empty), obj_weapon_gui (perched the ground primary's icon and the shotgun, flying the air weapon's and the mine, spr_weapon_select on the secondary while Ctrl is held, each pool's "ammo / max"), obj_energybar (back, fill scaled and blended by thirds, cover, `ceil(value)%`, frozen while paused or dead) and obj_speedometer (`speed * 33.79` mph or kph, running down by 0.95 a step while perched, as `string()` prints it); Runtime's `GameHudView` on the arena's HUD layer, stepped every room step and drawn every frame; BIBLE V32 | done: U.HARE-T25 |
| HUD: score, objective, debug overlay | `obj_game` Draw GUI and F12, `get_state_string`, `debug`, `track_name` | Built: Core's `GameHud` draws obj_game's Draw GUI first in its frame: `current_score` (fnt_score) and `strObjective` (fnt_point, from U.HARE-T57's objective engine) right-aligned at `view_wview - 200`, and with `debug` the overlay down the left (FPS, the ship's x,y, direction, image_angle, get_state_string, spin, its current rock's id, x,y and spin or "Null", `track_name`), positions and captions in `arena.Hud`; `GameSession.Debug`/`TrackName` and `ToggleDebug`, F12 through `Meta/Debug` in `ArenaRoom`; Runtime's `GameHudView`; BIBLE V33 (obj_healthbar's clock, lives, pips and obj_energybar's colours came with U.HARE-T25) | done: U.HARE-T59 |
| Floating text, tips, alerts | `obj_float_text`, `show_float_text`/`_ext`/`_color`, `obj_tip`, `obj_alert`, every `current_score += n` | Built: Core's `Popups/` (`FloatText`/`FloatTextField`, `TipBox`, `AlertMarker`; numbers, colours, obj_map's and obj_drop_intro's tip text and obj_map's four missions in `popups`), Runtime's `FloatTextView` (on the GUI layer where view 0 shows the room, under the HUD), `TipView` (slides in with sfx_flick, any key or a left press destroys it) and `AlertMarkerView` (its red `ef_ring` ping); `ArenaRoom.Score` floats `string(n)` and adds `n` through the session for every scoring enemy death and rock break, so the score and `o_points` now move in play; obj_tip and obj_alert are made by their rooms (U.HARE-T19/T62/T63); BIBLE V35 | done: U.HARE-T27 |
| Hotkeys and pause | `obj_game` key events, `globals.gml`'s `paused`, every `if(!paused)` Step | Built: every key is a HareControls `Meta` action. `ArenaRoom` (the play rooms' obj_game) answers M (`ToggleMinimap`), F12 (`ToggleDebug`), P (Core's `TogglePause`: `if(!level_complete) paused = !paused`), R (`RestartLevel`: reset_player, then setup_level), U (`ToggleUnits`, saved to the profile), F11 (`AddScore` of `session.DebugScorePoints`, 100) and, every step they are held, the keypad's + and - (`StepMasterVolume`, 0.01 within 0..1, onto the mixer by `HareAudio.MasterVolume` and saved on release); the room flow answers Esc (to room_title) and F1 (fullscreen, also the startup room's any key). `paused` is the session's, mirrored on `Gml.Paused`, which the ship, rocks, flotsam, enemies, pickups, weather, debris and now the ship's bullets, rockets and mines honour (D22). L's `level_loops` and Z's music change do nothing in the original and are not ported (V37); BIBLE V37, D22 | done: U.HARE-T60 |
| Reticle and pointer | `obj_mouse`, obj_player's `obj_mouse.sprite_index` lines, `option_showcursor` | Built: Core's `Pointer/GameMouse` (Create, Step with `on_gui`, the rooms' sprite and visibility, obj_player's green/blue/red reticle by state and `jump_arc`; numbers and rooms in `mouse`), Runtime's `MouseView` on the ship's `AimPoint` in the play rooms (HareControls Player/Aim in the others) and `SystemCursor`; the room flow makes each room's obj_mouse and hides the system cursor while the rooms run; the title's pointer waits on obj_logo (shown by it since U.HARE-T62), and obj_title/obj_map/obj_upgrade (U.HARE-T26/T19/T24) take over making theirs (obj_title's since U.HARE-T26); BIBLE V38 | done: U.HARE-T61 |
| Title, menus, quit dialog | `obj_title`, `obj_btn_*`, `obj_yesno_dialog` | Built: Core's `Rooms/TitleScreen` (obj_title's key and click, the buttons obj_logo's Create makes: Campaign, Level 01..07, Boss 1, Enemy Generator, Empty Level, Quit Game at x 1440; obj_logo's 0.02-a-step caption alpha; each button's mouse enter/leave with sfx_hover and its pressed event: sfx_roit, the game mode through the session, the fade through the flow, the `obj_fade_title.mode == "off"` check; obj_yesno_dialog with Yes, No, Esc and Enter; obj_btn_quickstart ported, never placed; all of it data in `title`), Runtime's `TitleRoom` (obj_title in room_title: makes its obj_mouse, draws the captions under the fade, the dialog over it and the pointer over everything on a UI Toolkit panel, `game_end()` through `TitleRoom.QuitGame`); since U.HARE-T62 the menu comes with obj_logo after obj_stand_title's rise; BIBLE V39, D23 | done: U.HARE-T26 |
| Title and intro presentation | `obj_drop_*`, `obj_stand_title`, `obj_logo`, `obj_intro`, room_title's and room_intro's backgrounds | Built: Core's `Rooms/DropScene` (obj_drop_title/obj_drop_intro falling in at 10 a step, shaking `irandom_range(-3,3)` about startX, stopping past `room_height/4` with the c_orange `ef_smokeup` trail every step and Alarm 0's c_black puff every 20; the title's Alarm 1 (200 steps) and fall out of the room with sfx_land; obj_drop_emitter's 4-8 obj_drop_cloud every 20 steps rushing up 60-80 a step at alpha 0.1-0.3; bg_nightsky scrolling up 5 a step under bg_gradient3), `TitleScreen` (obj_stand_title turned on by a key, a click or the drop leaving: 40 a step up from 2592 to 1032, then obj_logo at 475,455 with `obj_mouse.visible = true` and `background_vspeed[0] = 0`; its moon, eye and mouse-following pupil) and `IntroScreen` (A/D steering within 300 of the edges, the controls tip as it stops, any other key sending it out into a quick game at level 1 in room_gamescreen), all of it data in `title` and `intro`; Runtime's `DropSceneView`, `TitleRoom` (the world under the menu) and `IntroRoom` (obj_intro in room_intro; Esc to the title is the room flow's); BIBLE V39, V40 | done: U.HARE-T62 |
| Story and typewriter | `obj_story`, `obj_conversation`, `obj_typewriter*`, `load_scenario` | Built: Core's `Story/` (`StoryScreen`: obj_typewriter_story typing the introduction a character a step with sfx_type2 and a second's pause at each `^`, obj_scroll_world raising the earth and bg_nightsky a pixel every 3 steps and turning it, any key but Esc or the earth's arrival starting the campaign and fading to room_map; `Typewriter` for all three typewriters; `ConversationBox`: obj_conversation's slide in and out, typing with sfx_type, next_line with sfx_flick and sfx_woof; `StoryTuning.LoadScenario`/`LevelSetup.LoadScenario`), the text in the `dialogue` section and the rest in `story`; Runtime's `StoryRoom` (obj_story in room_story) and `ConversationView`, ready for the map (U.HARE-T19/T63); setup_level running load_scenario is U.HARE-T64's; BIBLE V41 | done: U.HARE-T28 |
| Map room | `obj_map`, `obj_earthball`, `obj_destination`, `obj_alert`, `obj_white_dot`, `obj_scenario_details`, `obj_btn_upgrade`, `obj_uss_warren`, `obj_scanner` | Built: Core's `Map/MapScreen` (obj_map's A/D turning the earth 2 a step with sfx_flick and the sky scrolling, obj_destination's pixel-exact touch of a marker through `Art/GmlPreciseMask` stopping on it with "Scenario N" and the comm box sliding in, any key sending obj_white_dot from the ship to obj_destination and its arrival going to room_intro, obj_btn_upgrade's hover and press to room_upgrade), data in `map` and `popups.Alert.Missions`; owner-directed: ten missions (one per level), the launched mission kept by room_intro (`ChooseMission`, `IntroMissionKept`), the boss (level_08) locked until the rest are won, wins in `campaign.json` (`Session/CampaignProgress`, Runtime's `CampaignStore`); Runtime's `MapRoom` (obj_map in room_map: sky, earth, ship, markers, dot, panel, button, comm box, tip, pointer); BIBLE V42, D24. obj_scanner's beam is U.HARE-T63's; returning a won map mission to room_map is U.HARE-T64's. (`obj_btn_level*` are title buttons, U.HARE-T26.) | done: U.HARE-T19 (scanner: U.HARE-T63) |
| Map scenery | `obj_earthball`, `obj_scanner`, `obj_uss_warren`, `obj_white_dot`, `obj_scenario_details` | None | UH-T63 |
| Game modes and difficulty | `gm_*`, `obj_btn_diff*`, `level_enemygenerator` | None | UH-T64 |
| Garage and upgrades | `obj_upgrade*`, schematics, `SKILLPOINTS` | None | UH-T24 |
| Helper hawk | `obj_helper`, `create_helper` | Built: Core's `Helper/HelperField` (create_helper's one helper, here one per ship; obj_helper's Create, alarms, held arrows, Up/Down speed steps, Page Down's solo mouse follow, the return out of view and Delete's rocket explosion; numbers in `helper`), HareControls' `Helper` map (the arrows, Page Down, Delete; the arrows taken off the ship's `Rotate`, which in the original is A/D only), read by `PlayerShipController`; Runtime's `HelperDirector` and `HelperView` (spr_helper, the ef_ring/ef_smoke/ef_smokeup/ef_spark effects, sfx_hawk, `RocketExplosion`), obj_carrot's and obj_coin's obj_helper collisions through the pickups' `CollectPickup` for the helper's ship; BIBLE V43 | done: U.HARE-T66 |
| Flashbomb | `obj_flashbomb`, `obj_rabbit_schematic` | None | UH-T67 |
| Boss: Scrap Mental | `obj_scrapmental`, `obj_shrapnel_*` | None | UH-T68 (supersedes UH-T32) |
| Music and jukebox | `obj_jukebox`, `play_random_music` | None | UH-T29 |
| Sound effects | 69 `sfx_*` | All imported, none skipped: `tools/import-gml-audio.ps1` + `GmlAudioBuilder` into `Assets/Unity.Hare/Audio` (PCM, decompressed on load, as GameMaker played them), named by `HareAudioManifest`, played by `HareAudio` through the mixer's Sfx group; only the death, grenade, vulcan and reload sounds are wired to their events yet | import done: U.HARE-T47; wiring UH-T30 |
| Music tracks, mixer, volumes | 6 `music_*`, `master_volume`, `sound_global_volume` | The six tracks imported as streamed Vorbis on the Music group; `Audio/HareMixer.mixer` (Master > Music, Sfx) follows the profile's music and sfx volume and mutes and the session `HareAudio.MasterVolume` (`master_volume`); nothing plays the music yet (UH-T29) and the numpad keys are UH-T60's | import done: U.HARE-T47 |
| Credits | `datafiles/credits.txt` | Data imported beside the clips (`Audio/credits.txt`, `HareAudioManifest.Credits`); no screen yet | data: U.HARE-T47; screen UH-T69 |
| Startup room | `obj_startup` | Core's `Rooms/StartupScreen` (Create's fullscreen and alarm[0..2], each alarm re-rolling its circuit board's speeds to `irandom_range(-2,2)` and re-arming for 1 to 12 s, the backgrounds' scroll, any key and the global left button to `room_title`; startup.room.gmx's five backgrounds and the note as data in `startup`), played by `StartupRoom` in the generated startup scene (tiled imported backgrounds, the red-on-black `fnt_point` note at `room_height - 60`) on the room flow's clock | done: U.HARE-T51 |
| Room flow and fades | `obj_fade_*`, `next_room` | Core's `Flow/RoomFlow` and `RoomFade` (room_goto, next_room, the fades, Esc to the title, F1 fullscreen, the rooms as data in `rooms`), played by `RoomFlowDirector` from the `Main.unity` bootstrap; one generated scene per room | done: U.HARE-T50 |
| Sprites, backgrounds | 213 (398 frames) / 18 | All imported, none skipped: `tools/import-gml-art.ps1` + `GmlArtBuilder` into `Assets/Unity.Hare/Art`, mapped by `HareArtManifest` (not yet drawn by the views, which still use `Resources/Hare/Sprites`) | done: U.HARE-T46 |
| Fonts | 8 | All imported, none skipped: `tools/import-gml-fonts.ps1` + `GmlFontBuilder` into one static bitmap FontAsset per font under `Resources/Hare/Fonts`, built from GameMaker's own baked glyph atlases (Arial and Berlin Sans FB are proprietary; no typeface file ships), and named text styles (`HareTextStyles`, `Theme.uss`) for the HUD, menus, dialogs, typewriters and conversations | done: U.HARE-T48 |
| Session state | `globals.gml` | Core's `Session/GameSession`: LIVES, COINS, SKILLPOINTS, KILLS, score and goal, difficulty, use_metric, master_volume, gamemode, objective (points, time limit, defeat, survive) with its target, time elapsed and limit, scenario and level, level_complete, paused and the game_restart reset, changed by `SessionCommand`s raising `SessionEvent`s, starting values in the content registry's `session` section; not yet held by a scene authority or read by a HUD (UH-T50, UH-T54, U.HARE-T22) | Core done: U.HARE-T49 |
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
| Multiplayer-ready seams and the shared Unity.Multiplayer kit | not in `Packages/manifest.json` | done: U.HARE-T35 (`com.mindattic.unity.multiplayer` and `com.mindattic.components.unity`; `Online.unity` beside `Main.unity` with the kit's NetBootstrap and drop-in front end; `HareNetPlayer`, a NetEntity carrying its seat, sending input as `[ToServer]` calls; `HareAuthority` holding Core's `SharedWorld` on the server; `NetSessionTest`/`NetBotTest` PlayMode tests; flight only so far) |
| Tests are EditMode only; no PlayMode, no scripted play with screenshots | no PlayMode asmdef | UH-T70 |
| No release polish (name, icon, build settings) | not started | UH-T71 |
| Scenes composed in code | `ProjectSetup` generates one scene per room and the bootstrap | done: U.HARE-T50 |

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

Not read in full, so the owning ticket must do it first: `obj_upgrade_*` costs and effects (UH-T24),
the weather emitters (UH-T18), and `obj_btn_*` handlers
(UH-T26, UH-T64). The sprite and background data were read in full by U.HARE-T46 and the sound data
and `credits.txt` by U.HARE-T47 (REVERSE_ENGINEERING.md, "Sprites and backgrounds" and "Sounds and the credits data
file").
