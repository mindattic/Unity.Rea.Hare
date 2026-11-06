# Unity.Rea.Hare reconstruction: parity and standards audit

Audit of UH-T34 (owner 2026-10-23). It compares the original prototype with the rebuild as it stands on
main, and the project with Unity best practice and the shared MindAttic Unity standards. Every gap has
its own story and ticket, and the tickets are in build order: building them in order reaches full parity
and a finished proof of concept. The per-object behaviour notes stay in
[REVERSE_ENGINEERING.md](REVERSE_ENGINEERING.md); this file is the inventory, the gap list and the plan.

Stories and tickets live in the ticket database (`tickets ticket queue --repo Unity.Rea.Hare`); the queue
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
- `sfx_crash` is only played by commented-out lines (`obj_title`'s two, `obj_drop_title`'s one); nothing plays it.
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
- (Corrected by U.HARE-T33: `room_intro` *is* reached, by `obj_white_dot`'s `room_goto(room_intro)` when the map's
  dot arrives; the rebuild's map goes there the same way, U.HARE-T19/T62.)
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
| Weapons: vulcan, laser, railgun, rocket, shotgun, flamer, mine | `shoot_*`, `fire_*`, obj_player's B key (`bullet_asset`) | Built; the vulcan rounds drawn with `bullet_asset`, which B cycles (BIBLE V45) | done: UH-T8 to UH-T11, U.HARE-T93 |
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
| HUD: health, energy, weapon icons, speedometer | `obj_healthbar`, `obj_energybar`, `obj_weapon_gui`, `obj_speedometer` | Built: Core's `GameHud` (numbers, sprites and icons in `arena.Hud`; the pips' column and size are `HealthTuning`'s, shared with obj_losthealth): obj_healthbar whole (spr_extralives and "x LIVES", `strTime` from the room's `HudClock`, COINS beside spr_coin_small, one pip per point of MAXHP, full or back-and-empty), obj_weapon_gui (perched the ground primary's icon and the shotgun, flying the air weapon's and the mine, spr_weapon_select on the secondary while Ctrl is held, each pool's "ammo / max"), obj_energybar (back, fill scaled and blended by thirds, cover, `ceil(value)%`, frozen while paused or dead; its value and Alarm 0's fill/drain modes are `ShipFlight.Energy`/`EnergyMode`, U.HARE-T89, BIBLE V4, D25) and obj_speedometer (`speed * 33.79` mph or kph, running down by 0.95 a step while perched, as `string()` prints it); Runtime's `GameHudView` on the arena's HUD layer, stepped every room step and drawn every frame; BIBLE V32 | done: U.HARE-T25 |
| HUD: score, objective, debug overlay | `obj_game` Draw GUI and F12, `get_state_string`, `debug`, `track_name` | Built: Core's `GameHud` draws obj_game's Draw GUI first in its frame: `current_score` (fnt_score) and `strObjective` (fnt_point, from U.HARE-T57's objective engine) right-aligned at `view_wview - 200`, and with `debug` the overlay down the left (FPS, the ship's x,y, direction, image_angle, get_state_string, spin, its current rock's id, x,y and spin or "Null", `track_name`), positions and captions in `arena.Hud`; `GameSession.Debug`/`TrackName` and `ToggleDebug`, F12 through `Meta/Debug` in `ArenaRoom`; Runtime's `GameHudView`; BIBLE V33 (obj_healthbar's clock, lives, pips and obj_energybar's colours came with U.HARE-T25) | done: U.HARE-T59 |
| HUD: boost and brake flashes | `obj_booster_schematic`, `obj_brake_schematic` (made by `player_boost`/`player_brake`) | Built: Core's `GameHud.ShowSchematic` on the ship's `BoostStarted`/`BrakeStarted` (ArenaRoom follows the seat), each at `(view_wview/2, view_hview - height/2)` from alpha 0.7 down 0.005 a step, paused or not, gone the step after 0, drawn after the four HUD objects; numbers in `arena.Hud`; BIBLE V46 | done: U.HARE-T92 |
| Floating text, tips, alerts | `obj_float_text`, `show_float_text`/`_ext`/`_color`, `obj_tip`, `obj_alert`, every `current_score += n` | Built: Core's `Popups/` (`FloatText`/`FloatTextField`, `TipBox`, `AlertMarker`; numbers, colours, obj_map's and obj_drop_intro's tip text and obj_map's four missions in `popups`), Runtime's `FloatTextView` (on the GUI layer where view 0 shows the room, under the HUD), `TipView` (slides in with sfx_flick, any key or a left press destroys it) and `AlertMarkerView` (its red `ef_ring` ping); `ArenaRoom.Score` floats `string(n)` and adds `n` through the session for every scoring enemy death and rock break, so the score and `o_points` now move in play; obj_tip and obj_alert are made by their rooms (U.HARE-T19/T62/T63); BIBLE V35 | done: U.HARE-T27 |
| Hotkeys and pause | `obj_game` key events, `globals.gml`'s `paused`, every `if(!paused)` Step | Built: every key is a HareControls `Meta` action. `ArenaRoom` (the play rooms' obj_game) answers M (`ToggleMinimap`), F12 (`ToggleDebug`), P (Core's `TogglePause`: `if(!level_complete) paused = !paused`), R (`RestartLevel`: reset_player, then setup_level), U (`ToggleUnits`, saved to the profile), F11 (`AddScore` of `session.DebugScorePoints`, 100) and, every step they are held, the keypad's + and - (`StepMasterVolume`, 0.01 within 0..1, onto the mixer by `HareAudio.MasterVolume` and saved on release); the room flow answers Esc (to room_title) and F1 (fullscreen, also the startup room's any key). `paused` is the session's, mirrored on `Gml.Paused`, which the ship, rocks, flotsam, enemies, pickups, weather, debris and now the ship's bullets, rockets and mines honour (D22). L's `level_loops` and Z's music change do nothing in the original and are not ported (V37); BIBLE V37, D22 | done: U.HARE-T60 |
| Reticle and pointer | `obj_mouse`, obj_player's `obj_mouse.sprite_index` lines, `option_showcursor` | Built: Core's `Pointer/GameMouse` (Create, Step with `on_gui`, the rooms' sprite and visibility, obj_player's green/blue/red reticle by state and `jump_arc`; numbers and rooms in `mouse`), Runtime's `MouseView` on the ship's `AimPoint` in the play rooms (HareControls Player/Aim in the others) and `SystemCursor`; the room flow makes each room's obj_mouse and hides the system cursor while the rooms run; the title's pointer waits on obj_logo (shown by it since U.HARE-T62), and obj_title/obj_map/obj_upgrade (U.HARE-T26/T19/T24) take over making theirs (obj_title's since U.HARE-T26); BIBLE V38 | done: U.HARE-T61 |
| Title, menus, quit dialog | `obj_title`, `obj_btn_*`, `obj_yesno_dialog` | Built: Core's `Rooms/TitleScreen` (obj_title's key and click, the buttons obj_logo's Create makes: Campaign, Level 01..07, Boss 1, Enemy Generator, Empty Level, Quit Game at x 1440; obj_logo's 0.02-a-step caption alpha; each button's mouse enter/leave with sfx_hover and its pressed event: sfx_roit, the game mode through the session, the fade through the flow, the `obj_fade_title.mode == "off"` check; obj_yesno_dialog with Yes, No, Esc and Enter; obj_btn_quickstart ported, never placed; all of it data in `title`), Runtime's `TitleRoom` (obj_title in room_title: makes its obj_mouse, draws the captions under the fade, the dialog over it and the pointer over everything on a UI Toolkit panel, `game_end()` through `TitleRoom.QuitGame`); since U.HARE-T62 the menu comes with obj_logo after obj_stand_title's rise; BIBLE V39, D23 | done: U.HARE-T26 |
| Title and intro presentation | `obj_drop_*`, `obj_stand_title`, `obj_logo`, `obj_intro`, room_title's and room_intro's backgrounds | Built: Core's `Rooms/DropScene` (obj_drop_title/obj_drop_intro falling in at 10 a step, shaking `irandom_range(-3,3)` about startX, stopping past `room_height/4` with the c_orange `ef_smokeup` trail every step and Alarm 0's c_black puff every 20; the title's Alarm 1 (200 steps) and fall out of the room with sfx_land; obj_drop_emitter's 4-8 obj_drop_cloud every 20 steps rushing up 60-80 a step at alpha 0.1-0.3; bg_nightsky scrolling up 5 a step under bg_gradient3), `TitleScreen` (obj_stand_title turned on by a key, a click or the drop leaving: 40 a step up from 2592 to 1032, then obj_logo at 475,455 with `obj_mouse.visible = true` and `background_vspeed[0] = 0`; its moon, eye and mouse-following pupil) and `IntroScreen` (A/D steering within 300 of the edges, the controls tip as it stops, any other key sending it out into a quick game at level 1 in room_gamescreen), all of it data in `title` and `intro`; Runtime's `DropSceneView`, `TitleRoom` (the world under the menu) and `IntroRoom` (obj_intro in room_intro; Esc to the title is the room flow's); BIBLE V39, V40 | done: U.HARE-T62 |
| Story and typewriter | `obj_story`, `obj_conversation`, `obj_typewriter*`, `load_scenario` | Built: Core's `Story/` (`StoryScreen`: obj_typewriter_story typing the introduction a character a step with sfx_type2 and a second's pause at each `^`, obj_scroll_world raising the earth and bg_nightsky a pixel every 3 steps and turning it, any key but Esc or the earth's arrival starting the campaign and fading to room_map; `Typewriter` for all three typewriters; `ConversationBox`: obj_conversation's slide in and out, typing with sfx_type, next_line with sfx_flick and sfx_woof; `StoryTuning.LoadScenario`/`LevelSetup.LoadScenario`), the text in the `dialogue` section and the rest in `story`; Runtime's `StoryRoom` (obj_story in room_story) and `ConversationView`, ready for the map (U.HARE-T19/T63); setup_level running load_scenario is U.HARE-T64's; BIBLE V41 | done: U.HARE-T28 |
| Map room | `obj_map`, `obj_earthball`, `obj_destination`, `obj_alert`, `obj_white_dot`, `obj_scenario_details`, `obj_btn_upgrade`, `obj_uss_warren`, `obj_scanner` | Built: Core's `Map/MapScreen` (obj_map's A/D turning the earth 2 a step with sfx_flick and the sky scrolling, obj_destination's pixel-exact touch of a marker through `Art/GmlPreciseMask` stopping on it with "Scenario N" and the comm box sliding in, any key sending obj_white_dot from the ship to obj_destination and its arrival going to room_intro, obj_btn_upgrade's hover and press to room_upgrade), data in `map` and `popups.Alert.Missions`; owner-directed: ten missions (one per level), the launched mission kept by room_intro (`ChooseMission`, `IntroMissionKept`), the boss (level_08) locked until the rest are won, wins in `campaign.json` (`Session/CampaignProgress`, Runtime's `CampaignStore`); Runtime's `MapRoom` (obj_map in room_map: sky, earth, ship, markers, dot, panel, button, comm box, tip, pointer); BIBLE V42, D24. obj_scanner's beam is the map scenery row's (U.HARE-T63); returning a won map mission to room_map is U.HARE-T64's. (`obj_btn_level*` are title buttons, U.HARE-T26.) | done: U.HARE-T19 (scanner: U.HARE-T63, done) |
| Map scenery | `obj_earthball`, `obj_scanner`, `obj_uss_warren`, `obj_white_dot`, `obj_scenario_details` | Built: Core's `Map/MapScanner` (obj_scanner's spr_scanner_beam at x 1045, y 388, its sway `image_angle += sweepY` with sweepY rising and falling 0.004 a step between +-0.1 under GML's epsilon compare, and its `0.01 * irandom_range(16, 24)` flicker), the numbers in `map` (`Scanner*`), drawn by Runtime's `MapRoom.Scanner` over the earth (equal depth, creation order); the earth, ship, markers, destination, dot, panel, sfx_flick and the comm box's slides were checked against HARE.gmx and already match U.HARE-T19's (V42), and obj_map's own spr_room_loop at (-100, 63) is off the room; BIBLE V44 | done: U.HARE-T63 |
| Game modes and difficulty | `gm_*`, `obj_btn_diff*`, `level_enemygenerator` | Built: Core's `Arena/LevelSetup` (setup_level's `if(gamemode == ...)` for the whole session: quick start `level_01`..`10`/`level_default`, the generator `level_enemygenerator`, the campaign `load_scenario` with scenario 1 on levels 1-5 and the others on 6-10, a map mission its own level; `EntryRoom` sending the menus straight to the level script's room; `RoomAfterLevel`/`GameSession.EndsMission` taking a won map mission back to room_map), Core's `Upgrade/UpgradeScreen` and Runtime's `Upgrade/UpgradeRoom` (room_upgrade's bg_metal and obj_btn_map back to room_map, data in `upgrades.Screen`); obj_btn_quickstart, obj_btn_level09 and obj_btn_shaft are never placed in the original (BIBLE V47) | done: U.HARE-T64 |
| Garage and upgrades | `obj_upgrade*`, `obj_btn_upgrade_vulcan`, `obj_btn_upgrade_shotgun`, `obj_label`, `SKILLPOINTS` | Built: Core's `Upgrade/UpgradeScreen` (the weapon icons' and pips' hover messages, sfx_hover on a pip, a pip bought for its SKILLPOINT into `Session/CampaignProgress`, never twice) and `Content/UpgradeCatalog` (`upgrades.Upgrades`: the original's ten pips plus two vulcan ones, each with a row, place and message; `upgrades.Effects`; `SkillPointsPerMissionWon`; icons, labels, sprites, boxes and the "Skill Points x" line in `upgrades.Screen`); SKILLPOINTS = the starting 5 + a point per map mission's first win - pips bought; purchases saved in `campaign.json` (version 2, version 1 still read); `Weapons/ShipUpgrades` fitted by `FitUpgrades` as `ArenaRoom` sets the ship up: vulcan wait, shotgun re-arm and spread (`Shotgun`), rocket blast (`Rocket`, `RocketExplosion`); Runtime's `UpgradeRoom` draws and saves; the schematics are HUD (V46, V48), not garage objects; BIBLE V49, D27 | done: U.HARE-T24 |
| Helper hawk | `obj_helper`, `create_helper` | Built: Core's `Helper/HelperField` (create_helper's one helper, here one per ship; obj_helper's Create, alarms, held arrows, Up/Down speed steps, Page Down's solo mouse follow, the return out of view and Delete's rocket explosion; numbers in `helper`), HareControls' `Helper` map (the arrows, Page Down, Delete; the arrows taken off the ship's `Rotate`, which in the original is A/D only), read by `PlayerShipController`; Runtime's `HelperDirector` and `HelperView` (spr_helper, the ef_ring/ef_smoke/ef_smokeup/ef_spark effects, sfx_hawk, `RocketExplosion`), obj_carrot's and obj_coin's obj_helper collisions through the pickups' `CollectPickup` for the helper's ship; BIBLE V43 | done: U.HARE-T66 |
| Flashbomb | `obj_flashbomb`, `obj_rabbit_schematic` | Built: Core's `Arena/Flashbomb` (`FlashbombField`: obj_player's F making a bomb per press, no ammo or cooldown as in the original; Alarm 0's blink every 6 steps at 0.8, the four circles growing 10/8/6 and 4 a step, burst_alpha fading 0.01 a step to destroy; numbers in `arena.Flashbomb`), obj_rabbit_schematic as `GameHud`'s third schematic (`arena.Hud.RabbitSchematic*`), sfx_flashbomb; Runtime's `FlashbombView` and `FlashbombSweep` (each enemy in view inside the ring to an obj_rocket_explosion, each shot to an obj_tinyexplosion), driven by `ArenaRoom`; BIBLE V48, D26 | done: U.HARE-T67 |
| Boss: Scrap Mental | `obj_scrapmental`, `obj_shrapnel_*` | Built: Core's `Enemies/ScrapmentalEnemy` (`EnemyKind.Scrapmental`: the eye creeping 1 px a step beyond 100 px, spin, undulate -20..160 px 4 px every 8 steps, 12-16 slivers every 4-8 s, a sawblade or dagger every 3-6 s, RGB 255 darkened 1 a ship bullet and 6 a rocket with the shove, the defeat after 128 for 500 points, 24 pickups, the grenade explosion and death particles, alarm[4]'s 10 s before it goes; `ShrapnelPieceBody` (`BodyPart.Hull`): 36 spr_boss_hull1..8 plates orbiting on the undulate radius, 16 a bullet and 48 a rocket, loose after 0-3 s for 20 points once at 8-12 px a step, re-attaching at RGB 196 when reeled home; `ShrapnelHarpoonBody` (`BodyPart.Harpoon`): 16 arms 20 degrees apart turning 1 a step, firing at 8 px a step after 1-4 s (2-6 s after each reel), reeling at 4 px a step after 2000 px, a rock (`StrikeRock`) or a caught piece (`GrabPiece`); the numbers in `enemies.Scrapmental`), Runtime's `Enemies/Scrapmental` (eye and frame, `ShrapnelBack` fading 0.005 and sinking 1 px a step, `ShrapnelPiece`, `ShrapnelHarpoon` with its c_gray line; registered as level_08's o_defeat target through `LevelTargets`), scores through `EnemyDirector.Scored`/`ArenaRoom.Score`; BIBLE D28-D32 | done: U.HARE-T68 (supersedes UH-T32) |
| Music and jukebox | `obj_jukebox`, `play_random_music` | Built: Core's `Session/PlayRandomMusic` (`irandom_range(1, 5)` over `session.Tracks`, the original's five tracks in order, into `CurrentTrack`/`TrackName`, raising `MusicTrackChosen`); Runtime's `Audio/Jukebox` (obj_jukebox, made by `ArenaRoom.Begin`, asking whenever no music plays) and `HareAudio.PlayMusic` (once, not looped, on the persistent host's Music source); the original's `audio_is_playing(0)` and commented-out play fixed, BIBLE D33, V51 | done: U.HARE-T29 |
| Sound effects | 69 `sfx_*` | All imported, none skipped: `tools/import-gml-audio.ps1` + `GmlAudioBuilder` into `Assets/Unity.Rea.Hare/Audio` (PCM, decompressed on load, as GameMaker played them), named by `HareAudioManifest`, played by `HareAudio` through the mixer's Sfx group at the moment the original plays each; U.HARE-T30 wired the last ones (sfx_bounce off shield walls and the phalanx shield, sfx_brake and its stop on landing, sfx_hologram / sfx_hologram_pass, Ctrl's sfx_switch / sfx_switch_back, the laser's sfx_fire_laser and sfx_loop_laser hum, sfx_fire_railgun, sfx_fire_shotgun), with `HareAudio.PlayVoice` / `IsPlaying` / `Stop` for GML's audio_is_playing and audio_stop_sound. sfx_crash is only in commented-out code (see 'Content the original never reaches') | done: U.HARE-T47 (import), U.HARE-T30 (wiring) |
| Music tracks, mixer, volumes | 6 `music_*`, `master_volume`, `sound_global_volume` | The six tracks imported as streamed Vorbis on the Music group; `Audio/HareMixer.mixer` (Master > Music, Sfx) follows the profile's music and sfx volume and mutes and the session `HareAudio.MasterVolume` (`master_volume`); the jukebox plays them (U.HARE-T29) and the numpad keys are UH-T60's | import done: U.HARE-T47; played: U.HARE-T29 |
| Credits | `datafiles/credits.txt` | Data imported beside the clips (`Audio/credits.txt`, `HareAudioManifest.Credits`); `CreditsView` from the title's Credits button (BIBLE V52), its minigun entry built from the imported file | data: U.HARE-T47; screen: U.HARE-T69, done; data link: U.HARE-T112, done |
| Startup room | `obj_startup` | Core's `Rooms/StartupScreen` (Create's fullscreen and alarm[0..2], each alarm re-rolling its circuit board's speeds to `irandom_range(-2,2)` and re-arming for 1 to 12 s, the backgrounds' scroll, any key and the global left button to `room_title`; startup.room.gmx's five backgrounds and the note as data in `startup`), played by `StartupRoom` in the generated startup scene (tiled imported backgrounds, the red-on-black `fnt_point` note at `room_height - 60`) on the room flow's clock | done: U.HARE-T51 |
| Room flow and fades | `obj_fade_*`, `next_room` | Core's `Flow/RoomFlow` and `RoomFade` (room_goto, next_room, the fades, Esc to the title, F1 fullscreen, the rooms as data in `rooms`), played by `RoomFlowDirector` from the `Main.unity` bootstrap; one generated scene per room | done: U.HARE-T50 |
| Sprites, backgrounds | 213 (398 frames) / 18 | All imported, none skipped: `tools/import-gml-art.ps1` + `GmlArtBuilder` into `Assets/Unity.Rea.Hare/Art`, mapped by `HareArtManifest` (not yet drawn by the views, which still use `Resources/Hare/Sprites`) | done: U.HARE-T46 |
| Fonts | 8 | All imported, none skipped: `tools/import-gml-fonts.ps1` + `GmlFontBuilder` into one static bitmap FontAsset per font under `Resources/Hare/Fonts`, built from GameMaker's own baked glyph atlases (Arial and Berlin Sans FB are proprietary; no typeface file ships), and named text styles (`HareTextStyles`, `Theme.uss`) for the HUD, menus, dialogs, typewriters and conversations | done: U.HARE-T48 |
| Session state | `globals.gml` | Core's `Session/GameSession`: LIVES, COINS, SKILLPOINTS, KILLS, score and goal, difficulty, use_metric, master_volume, gamemode, objective (points, time limit, defeat, survive) with its target, time elapsed and limit, scenario and level, level_complete, paused and the game_restart reset, changed by `SessionCommand`s raising `SessionEvent`s, starting values in the content registry's `session` section; not yet held by a scene authority or read by a HUD (UH-T50, UH-T54, U.HARE-T22) | Core done: U.HARE-T49 |
| Campaign persistence | (not in the original) | Built: `Session/CampaignProgress` saved as `campaign.json` by Runtime's `CampaignStore` (BIBLE V50) | done: U.HARE-T65 (deliberate addition) |

## End-to-end verification (U.HARE-T33, 2026-10-30)

- **Build**: `tools/build.ps1` (headless `HareBuild.BuildWindows`) built `Build/Hare/Hare.exe` with 0 errors. A normal
  launch opens fullscreen on the startup room's circuit boards with no exception in the player log; `Hare.exe -demo
  -muted` (U.HARE-T70's `ScriptedRun`: startup, title, story, map, intro, a level, a laser kill that scores, the level
  results, the side scroll, the shaft and the garage) exits 0 with "ScriptedRun finished.".
- **Rooms**: of the original's ten rooms all but `room_boss1` (entered by nothing, above) are rebuilt and reachable
  with real input: startup (first room) -> title (any key or click) -> story (Campaign) -> map (any key or the earth's
  arrival) -> intro (a mission's dot) -> the level's room (any key) -> results (Enter) -> the next level, or the map
  for a won mission; garage from the map's Upgrades and back by its map button; the title's level 1-7, Boss 1, Enemy
  Generator and Empty Level buttons straight into play (level 4 the shaft, level 5 the side scroll); game over back
  to startup; Esc to the title from map, story, intro and play; the title's Credits and Quit (yes/no) dialogs.
- **Mechanics**: every number in REVERSE_ENGINEERING.md (speeds, HP, damage, cooldowns, ammo pools, alarms,
  scores, angles, level data, map and story tuning) matches the rebuild or a recorded BIBLE D/V entry. Gaps filed:
  U.HARE-T109 (weapon switch, mine and rocket launch and non-vulcan dry-fire sounds never played), U.HARE-T110 (mine
  arming ring and beep, dagger launch ring and smoke trail, enemy mine ping raised but never shown), U.HARE-T111 (a
  won map mission's return to the map skips the score reset), U.HARE-T112 (the credits screen does not read
  `credits.txt`). Doc errors found by the same pass (stale `Assets/Unity.Rea.Hare` paths, `room_intro` "entered by
  nothing", the level buttons' room, stale rows above) are corrected in place.
- **Source**: the ticket's `GameMaker\gmlHARE` copy and the `_PRE_2017\gmlHARE` copy the notes are read from differ
  only as recorded in REVERSE_ENGINEERING.md, "Source locations". Rabbit.gmx is the earlier build, reference only.

## End-to-end re-verification (U.HARE-T121, 2026-11-06)

Re-run after U.HARE-T118's story reconciliation and its gap ticket U.HARE-T120 (control rebinding), at 74abdfd.

- **Build**: `tools/build.ps1` built `Build/Hare/Hare.exe` with 0 errors; `Hare.exe -demo -muted` exits 0 with
  "ScriptedRun finished." and no exception in the player log; `tools/capture.ps1 -Rebuild` rebuilt the capture
  player and shot all eleven rooms and modes (startup, title, story, map, intro, a level, a scoring laser kill, the
  level results, the side scroll, the shaft, the garage), exit 0.
- **Rooms**: unchanged since U.HARE-T33: every room but `room_boss1` (entered by nothing) is reachable with real
  input along the same paths. New since then: the title's Controls link (bottom left, U.HARE-T120, BIBLE V55) opens
  `ControlsView`; Esc or Back returns to the title.
- **U.HARE-T33's gaps**: all closed and present in the code: U.HARE-T109 (sfx_switch, sfx_fire_grenade,
  sfx_set_rocket, sfx_empty_clip played), U.HARE-T110 (dagger, mine and enemy mine effects drawn and played),
  U.HARE-T111 (the score reset after a won mission's return to the map), U.HARE-T112 (`CreditsView` reads
  `credits.txt`; the Credits row above corrected), and U.HARE-T113 (the phalanx's per-step spark) found on the way.
- **Mechanics**: the commits since U.HARE-T33 (the gap fixes, the Unity.Rea.Hare rename, U.HARE-T116's bughunt,
  U.HARE-T120) change no number REVERSE_ENGINEERING.md records; the default bindings are still the original's keys,
  so a player who never opens the Controls screen plays the original's controls.
- **Gaps filed**: U.HARE-T122 (U.HARE-T120's rebinding has no test and is not in the scripted run or capture, so it
  was never verified by play as rule 7 asks; a live drive of the screen on the shared desktop was not possible),
  U.HARE-T123 (while a binding listens, the room flow's always-on Fullscreen action still fires, so F1 toggles the
  window during a rebinding).

## Standards audit

Findings against AGENTS.md "Rebuild standard", Unity best practice and the shared Unity standards.
Reference architectures: Unity.SpaceGod and Unity.GridGame.

| Finding | Evidence | Ticket |
|---|---|---|
| No assembly split; Core is not engine-free and not tested outside Unity | one `Unity.Rea.Hare` asmdef with `noEngineReferences: false`; one EditMode test asmdef; no dotnet test project | UH-T37 |
| No README, no BIBLE | neither file in the repo | done: UH-T38 (README.md, docs/BIBLE.md) |
| Rules live in MonoBehaviours (player, weapons) | `PlayerShipController`, `PlayerHealth`, `PlayerAmmo`, loadouts | UH-T39 |
| Rules live in MonoBehaviours (enemies), engine random | `SetPieceEnemy`, enemy scripts, `UnityEngine.Random` | done: UH-T40 (Core enemy world, seeded random) |
| Tuning and content hard-coded | `SerializeField` constants and literals throughout | UH-T41 |
| Input System used as raw polling, no actions asset, no gamepad, no rebinding | `Keyboard.current` / `Mouse.current` in five scripts | UH-T42 (actions asset, gamepad scheme); done: U.HARE-T120 (rebinding: the title's Controls screen, `ControlsView`, rebinds each action per scheme with `PerformInteractiveRebinding`; `HareBindings` saves the overrides to the profile's `controls.json` and reapplies them at boot; a deliberate addition, BIBLE V55) |
| Unity.Shared profiles and settings not adopted | not in `Packages/manifest.json` | done: UH-T43 (`com.mindattic.unity.shared`, `ActiveProfile`, `HareSettings`) |
| URP present but no pipeline-agnostic seam, no reference resolution handling | `ProjectSetup` generates the 2D renderer only | done: UH-T44 (`Unity.Rea.Hare.Editor.Rendering`'s `UrpSeam`/`SortingLayerSetup`, the `Backdrop`/`Default`/`Overlay` sorting layers, `CameraRig`/`LetterboxCamera`, 1920x1080 reference resolution) |
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

Not read in full, so the owning ticket must do it first: `obj_upgrade_*` costs and effects (read by U.HARE-T24: one point a pip, no effect in the original, BIBLE V49/D27),
the weather emitters (UH-T18), and `obj_btn_*` handlers
(UH-T26, UH-T64). The sprite and background data were read in full by U.HARE-T46 and the sound data
and `credits.txt` by U.HARE-T47 (REVERSE_ENGINEERING.md, "Sprites and backgrounds" and "Sounds and the credits data
file").

## Appendix: per-asset inventory

U.HARE-T94 (rebuild standard point 1: record every original asset). One row per resource in `HARE.gmx`'s
`HARE.project.gmx`, generated by `tools/gen-reconstruction-inventory.py` (re-run it with `--write` after
a port lands; do not hand-edit the rows). **Used by** lists what in the original names the asset (room
instance, object event or parent, script call; first four). **Status**: `ported` (Unity code or content names
it, or a family stem plus an index; the files are shown); `ported, by comment reference` (only comments in
the Unity code name it: the original's name is kept in the doc comments of the C# port that replaces it, so
this is documentary evidence, not a symbol match); `in progress`; `imported, owner ported` (the sprite or
background is in the project and its owning object is ported, but no code names the asset itself);
`not ported, <ticket or why>`; `not reached` and `imported, not reached` (nothing in the original uses
it, so parity does not need it; any Unity mention is noted). The original is scanned by name, so an asset
the original builds from a prefix at run time shows as unused there; the family tables above cover those.

### Objects (150)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `obj_YesNo` | (not in the resource tree) | none | not reached | nothing in the original uses it |
| `obj_alert` | map | `obj_destination`, `obj_map` | ported | `Scripts/Popups/AlertMarkerView.cs` |
| `obj_booster_schematic` | game/gui | `player_boost` | ported, by comment reference | `Core/Arena/GameHud.cs`, `Core/Player/PlayerEvents.cs` +1 |
| `obj_brake_schematic` | game/gui | `player_brake` | ported, by comment reference | `Core/Arena/GameHud.cs`, `Core/Player/PlayerEvents.cs` +1 |
| `obj_btn_campaign` | title | `obj_logo` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_diff0` | title | `obj_logo` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_diff100` | title | `obj_logo` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level01` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level02` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level03` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level04` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level05` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level06` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level07` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level08` | title | `obj_logo` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_btn_level09` | (not in the resource tree) | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Session/SessionCommands.cs` |
| `obj_btn_map` | upgrade | `obj_upgrade` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_no` |  | `obj_YesNo`, `obj_yesno_dialog` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_quickstart` | title | `obj_logo` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_quit` | title | `obj_logo` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_btn_shaft` | title | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Session/SessionCommands.cs` |
| `obj_btn_upgrade` | map | `obj_map` | ported | `Core/Map/MapTuning.cs`, `Resources/Hare/Content/GameContent.json`; garage button, U.HARE-T24 (eb2d6e8) |
| `obj_btn_upgrade_shotgun` | upgrade/shotgun | `room_upgrade` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json`; garage upgrade button, U.HARE-T24 (eb2d6e8) |
| `obj_btn_upgrade_vulcan` | upgrade/vulcan | `room_upgrade` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json`; garage upgrade button, U.HARE-T24 (eb2d6e8) |
| `obj_btn_yes` |  | `obj_YesNo`, `obj_btn_no`, `obj_yesno_dialog` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_bullet` | game/weapon/projectile | `obj_dagger`, `obj_enemy_mine`, `obj_hydra`, `obj_meatball` +17 more | ported, by comment reference | `Core/Arena/ArenaWorld.cs`, `Core/Enemies/DaggerEnemy.cs` +23 |
| `obj_butterfly` | game/weather | `obj_butterfly_emitter` | ported, by comment reference | `Core/Enemies/EnemyCommands.cs`, `Core/Enemies/InsectBodies.cs` +2 |
| `obj_butterfly_emitter` | game/weather | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Scripts/Enemies/InsectEmitter.cs` |
| `obj_carrot` | game/pickup | `random_pickup` | ported | `Scripts/Pickups/PickupView.cs` |
| `obj_cartridge` | game/weapon/vulcan | `shoot_shotgun`, `shoot_vulcan` | ported | `Scripts/Pickups/PickupView.cs` |
| `obj_chip` | game/rock | `spawn_chips` | ported | `Scripts/Pickups/PickupView.cs` |
| `obj_cloud` | game | `obj_cloud_emitter` | ported, by comment reference | `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `obj_cloud_emitter` | title | `room_boss1`, `room_gamescreen`, `room_shaft`, `room_sidescroll` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Weather/WeatherField.cs` +2 |
| `obj_coin` | game/pickup | `random_pickup` | ported | `Scripts/Pickups/PickupView.cs` |
| `obj_conversation` | conversation | `obj_map`, `next_line` | ported | `Core/Story/Typewriter.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `obj_dagger` | game/enemy/dagger | `obj_scrapmental`, `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/DaggerEnemy.cs`, `Scripts/Enemies/Dagger.cs` +1 |
| `obj_death_particle` | game/effect | `spawn_death_particles` | ported, by comment reference | `Scripts/Effects/DeathParticle.cs` |
| `obj_destination` | map | `room_map`, `obj_map` | ported, by comment reference | `Core/Content/ContentRegistry.cs`, `Core/Map/MapCommands.cs` +8 |
| `obj_drop_cloud` | title | `obj_drop_emitter` | ported | `Scripts/Rooms/DropSceneView.cs` |
| `obj_drop_emitter` | intro | `room_intro`, `room_title`, `obj_drop_title`, `obj_moon_surface` +1 more | ported, by comment reference | `Core/Arena/MoonSurface.cs`, `Core/Rooms/DropScene.cs` +5 |
| `obj_drop_intro` | intro | `room_intro` | ported, by comment reference | `Core/Flow/RoomFlow.cs`, `Core/Popups/PopupTuning.cs` +9 |
| `obj_drop_title` | title | `room_title`, `obj_title` | ported, by comment reference | `Core/Rooms/DropScene.cs`, `Core/Rooms/DropSceneTuning.cs` +2 |
| `obj_earthball` | map | `room_map`, `obj_alert`, `obj_destination`, `obj_map` | ported | `Scripts/Map/MapRoom.cs` |
| `obj_enemy_mine` | game/weapon/projectile | `obj_trilobyte` | ported, by comment reference | `Core/Enemies/EnemyBody.cs`, `Core/Enemies/EnemyCommands.cs` +5 |
| `obj_energybar` | game/gui | `obj_game`, `obj_player`, `player_boost`, `player_brake` +2 more | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/GameHud.cs` +5 |
| `obj_fade_game` | game | `obj_game` | ported | `Scripts/Flow/RoomFlowDirector.cs` |
| `obj_fade_map` | map | `obj_map` | ported, by comment reference | `Core/Flow/RoomId.cs`, `Core/Flow/RoomTable.cs` +1 |
| `obj_fade_story` | story | `room_story`, `obj_scroll_world`, `obj_story` | ported, by comment reference | `Core/Flow/RoomId.cs`, `Core/Flow/RoomTable.cs` +4 |
| `obj_fade_title` | title | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +11 more | ported, by comment reference | `Core/Flow/FlowCommands.cs`, `Core/Flow/RoomFlow.cs` +7 |
| `obj_fade_upgrade` | upgrade | `obj_upgrade` | ported, by comment reference | `Core/Flow/RoomId.cs`, `Core/Flow/RoomTable.cs` +2 |
| `obj_firefly` | game/weather | `obj_firefly_emitter` | ported, by comment reference | `Core/Enemies/EnemyCommands.cs`, `Core/Enemies/InsectBodies.cs` +2 |
| `obj_firefly_emitter` | game/weather | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Scripts/Enemies/InsectEmitter.cs` |
| `obj_flame` | game/effect | `shoot_flamer`, `shoot_flamethrower` | ported, by comment reference | `Core/Weapons/WeaponPhysicalTable.cs`, `Scripts/Weapons/Flame.cs` +2 |
| `obj_flashbomb` | game/weapon | `obj_player` | ported | `Scripts/Arena/FlashbombView.cs` |
| `obj_float_text` | game/gui | `show_float_text`, `show_float_text_color`, `show_float_text_ext` | ported | `Scripts/Popups/FloatTextView.cs` |
| `obj_flotsum` | game/rock | `rock_destroyed` | ported, by comment reference | `Scripts/Rocks/Flotsum.cs`, `Scripts/Rocks/RockField.cs` |
| `obj_fly` | game/weather | `obj_fly_emitter` | ported, by comment reference | `Core/Enemies/EnemyCommands.cs`, `Core/Enemies/InsectBodies.cs` +2 |
| `obj_fly_emitter` | game/weather | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Scripts/Enemies/InsectEmitter.cs` |
| `obj_fog` | game/weather | `obj_fog_emitter` | ported, by comment reference | `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `obj_fog_emitter` | game/weather | `obj_game` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Weather/WeatherField.cs` +1 |
| `obj_game` | game | `room_boss1`, `room_gamescreen`, `room_shaft`, `room_sidescroll` | ported | `Scripts/Arena/ArenaRoom.cs` |
| `obj_grenade_explosion` | game/effect | `obj_meatball`, `obj_scrapmental`, `obj_worm`, `obj_worm_segment` +1 more | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Core/Enemies/MeatballEnemy.cs` +7 |
| `obj_healthbar` | game/gui | `room_boss1`, `obj_game`, `player_hurt` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/GameHud.cs` +10 |
| `obj_helper` | game | `obj_carrot`, `obj_coin`, `create_helper` | ported | `Scripts/Helper/HelperView.cs` |
| `obj_hydra` | game/enemy/hydra | `obj_wormhole`, `par_enemy_emitter`, `create_wormhole`, `generate_enemy_list` +1 more | ported, by comment reference | `Core/Enemies/HydraEnemy.cs`, `Scripts/Enemies/Hydra.cs` |
| `obj_intro` | intro | `room_intro` | ported | `Scripts/Rooms/IntroRoom.cs` |
| `obj_jukebox` | game | `setup_level` | ported | `Scripts/Audio/Jukebox.cs` |
| `obj_label` | upgrade | `room_upgrade` | ported | `Scripts/Upgrade/UpgradeRoom.cs` |
| `obj_laser` | game/weapon/laser | `obj_player`, `shoot_laser` | ported, by comment reference | `Core/Player/PlayerEvents.cs`, `Core/Weapons/GunRack.cs` +3 |
| `obj_level_results` | game/gui | `set_level_complete`, `setup_level` | ported | `Scripts/Arena/LevelResultsView.cs` |
| `obj_levelstart` | game/gui | `setup_level` | ported | `Scripts/Arena/LevelStartView.cs` |
| `obj_logo` | title | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +12 more | ported | `Scripts/Rooms/TitleRoom.cs` |
| `obj_losthealth` | game/gui | `player_hurt` | ported | `Scripts/UI/LostHealthView.cs` |
| `obj_map` | map | `room_map`, `obj_alert`, `obj_destination`, `obj_earthball` | ported | `Scripts/Map/MapRoom.cs` |
| `obj_meatball` | game/enemy/meatball | `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/MeatballEnemy.cs`, `Scripts/Enemies/Meatball.cs` +2 |
| `obj_meatball_gib` | game/enemy/meatball | `meatball_gib` | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Scripts/Enemies/FadingDebris.cs` |
| `obj_mimic` | game/enemy/mimic | `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/MimicEnemy.cs`, `Scripts/Enemies/Mimic.cs` +1 |
| `obj_mimic_illusion` | game/enemy/mimic | `obj_mimic` | ported, by comment reference | `Core/Enemies/EnemyBody.cs`, `Core/Enemies/MimicEnemy.cs` +1 |
| `obj_mine` | game/weapon/projectile | `shoot_mine` | ported, by comment reference | `Scripts/Arena/FlashbombView.cs`, `Scripts/Arena/LevelReset.cs` +3 |
| `obj_moon_surface` | title | none | not reached | nothing in the original uses it; Unity mentions it in `Scripts/Arena/MoonSurfaceView.cs` |
| `obj_mouse` |  | `obj_game`, `obj_logo`, `obj_map`, `obj_player` +2 more | ported | `Scripts/Input/MouseView.cs` |
| `obj_muzzleflash` | game/weapon/vulcan | `shoot_shotgun`, `shoot_vulcan` | ported, by comment reference | `Scripts/Effects/MuzzleFlash.cs`, `Scripts/Weapons/Shotgun.cs` +1 |
| `obj_oval_explosion` | game/effect | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Arena/MoonSurface.cs`, `Scripts/Arena/MoonSurfaceView.cs` +1 |
| `obj_phalanx` | game/enemy/phalanx | `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/PhalanxEnemy.cs`, `Scripts/Enemies/Phalanx.cs` |
| `obj_phalanx_shield` | game/enemy/phalanx | `obj_bullet`, `obj_phalanx`, `obj_rocket` | ported, by comment reference | `Core/Enemies/EnemyBody.cs`, `Core/Enemies/PhalanxEnemy.cs` +6 |
| `obj_player` | game | `room_boss1`, `room_gamescreen`, `room_shaft`, `room_sidescroll` +13 more | ported | `Scripts/Arena/PlayerArrows.cs` |
| `obj_rabbit_schematic` | game/gui | `obj_flashbomb` | ported, by comment reference | `Core/Arena/Flashbomb.cs`, `Core/Arena/GameHud.cs` +1 |
| `obj_rail_swirl` | game/weapon/railgun | `obj_railgun`, `generate_swirl` | ported, by comment reference | `Scripts/Weapons/RailgunShot.cs` |
| `obj_railgun` | game/weapon/railgun | `shoot_railgun` | ported, by comment reference | `Scripts/Weapons/EnemyWeaponTarget.cs`, `Scripts/Weapons/RailgunShot.cs` |
| `obj_rain_emitter` | game/weather | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` |
| `obj_raindrop` | game/weather | `obj_rain_emitter` | ported, by comment reference | `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `obj_ring` | game/ring | `obj_game` | not reached | named only in commented-out code in obj_game (the ring gate); see 'Content the original never reaches' |
| `obj_ring_back` | game/ring | `obj_ring` | not reached | only the never-placed obj_ring uses it; see 'Content the original never reaches' |
| `obj_rock1` | game/rock | `room_boss1` | ported, by comment reference | `Scripts/Rocks/Rock.cs` |
| `obj_rock2` | game/rock | none | not reached | nothing in the original uses it |
| `obj_rock3` | game/rock | none | not reached | nothing in the original uses it |
| `obj_rock4` | game/rock | none | not reached | nothing in the original uses it |
| `obj_rock5` | game/rock | none | not reached | nothing in the original uses it |
| `obj_rocket` | game/weapon/projectile | `obj_dagger`, `obj_enemy_mine`, `obj_hydra`, `obj_meatball` +14 more | ported, by comment reference | `Core/Arena/ArenaWorld.cs`, `Core/Enemies/EnemyBody.cs` +14 |
| `obj_rocket_explosion` | game/effect | `obj_dagger`, `obj_enemy_mine`, `obj_flashbomb`, `obj_helper` +15 more | ported | `Scripts/Player/PlayerDamageSource.cs` |
| `obj_sand` | game/weather | `obj_sand_emitter` | ported, by comment reference | `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `obj_sand_emitter` | game/weather | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` |
| `obj_sawblade` | game/enemy/sawblade | `obj_scrapmental`, `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/SawbladeEnemy.cs`, `Scripts/Enemies/Sawblade.cs` +1 |
| `obj_scanner` | map | `room_map` | ported | `Scripts/Map/MapRoom.cs` |
| `obj_scenario_details` | map | `room_map`, `obj_destination` | ported | `Scripts/Map/MapRoom.cs` |
| `obj_scrapmental` | game/enemy/scrapmental | `level_08` | in progress, U.HARE-T68 | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` +1; Scrap Mental boss; level 8's defeat target |
| `obj_scroll_world` | story | `room_story`, `obj_story` | ported | `Scripts/Story/StoryRoom.cs` |
| `obj_shieldwall` | game | `obj_bullet`, `obj_dagger`, `obj_mimic`, `obj_phalanx` +8 more | ported, by comment reference | `Core/Enemies/DaggerEnemy.cs`, `Core/Enemies/EnemyBody.cs` +12 |
| `obj_shrapnel_back` | game/enemy/scrapmental | `obj_scrapmental` | ported, by comment reference | `Core/Enemies/ScrapmentalEnemy.cs`, `Scripts/Enemies/Scrapmental.cs` |
| `obj_shrapnel_harpoon` | game/enemy/scrapmental | `obj_scrapmental` | ported, by comment reference | `Core/Enemies/EnemyBody.cs`, `Core/Enemies/ScrapmentalEnemy.cs` +2 |
| `obj_shrapnel_piece` | game/enemy/scrapmental | `obj_scrapmental`, `obj_shrapnel_harpoon` | ported, by comment reference | `Core/Enemies/EnemyBody.cs`, `Core/Enemies/ScrapmentalEnemy.cs` +2 |
| `obj_snow` | game/weather | `obj_snow_emitter` | ported, by comment reference | `Core/Weather/WeatherField.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `obj_snow_emitter` | game/weather | `obj_game` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Weather/WeatherField.cs` +1 |
| `obj_spawnpoint` | game | `room_boss1`, `room_gamescreen`, `room_shaft`, `room_sidescroll` | not ported, by design | only sets angle = 270, draws nothing and nothing reads it (BIBLE V29, U.HARE-T21) |
| `obj_speedometer` | game/gui | `obj_game` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/GameHud.cs` +2 |
| `obj_stand_title` | title | `room_title`, `obj_drop_title`, `obj_title` | ported | `Scripts/Rooms/TitleRoom.cs` |
| `obj_startup` | startup | `startup` | ported | `Scripts/Rooms/StartupRoom.cs` |
| `obj_story` | story | `room_story` | ported | `Scripts/Story/StoryRoom.cs` |
| `obj_surface_reflection` | game | `room_sidescroll` | ported | `Scripts/Arena/SurfaceReflectionView.cs` |
| `obj_textdone` | conversation | `next_line` | ported | `Scripts/Story/TextDoneView.cs` |
| `obj_tinyexplosion` | game/effect | `obj_bullet`, `obj_dagger`, `obj_flashbomb`, `obj_hydra` +17 more | ported, by comment reference | `Core/Arena/Flashbomb.cs`, `Core/Enemies/EnemyEvents.cs` +6 |
| `obj_tip` |  | `obj_drop_intro`, `obj_map` | ported | `Scripts/Popups/TipView.cs` |
| `obj_title` | title | `room_title` | ported | `Scripts/Rooms/TitleRoom.cs` |
| `obj_trilobyte` | game/enemy/trilobyte | `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/TrilobyteEnemy.cs`, `Scripts/Enemies/Trilobyte.cs` |
| `obj_typewriter` | intro | `obj_drop_intro`, `obj_intro` | ported | `Core/Story/Typewriter.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_typewriter_story` | story | `obj_story` | ported | `Core/Story/Typewriter.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_upgrade` | upgrade | `room_upgrade` | ported | `Scripts/Upgrade/UpgradeRoom.cs` |
| `obj_upgrade_missle` | upgrade/missle | `room_upgrade` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_upgrade_missle1` | upgrade/missle | `room_upgrade` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_upgrade_shotgun1` | upgrade/shotgun | `room_upgrade` | ported | `Resources/Hare/Content/GameContent.json` |
| `obj_upgrade_vulcan1` | upgrade/vulcan | `room_upgrade` | ported | `Core/Content/UpgradeCatalog.cs`, `Resources/Hare/Content/GameContent.json` |
| `obj_uss_warren` | map | `room_map`, `obj_map` | ported | `Scripts/Map/MapRoom.cs` |
| `obj_weapon_gui` | game/gui | `obj_game`, `next_air_weapon`, `next_ground_weapon` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/GameHud.cs` +2 |
| `obj_white_dot` | map | `obj_map` | ported | `Scripts/Map/MapRoom.cs` |
| `obj_worm` | game/enemy/worm | `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/WormEnemy.cs`, `Scripts/Enemies/Worm.cs` +1 |
| `obj_worm_gib` | game/enemy/worm | `worm_gib` | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Scripts/Enemies/FadingDebris.cs` |
| `obj_worm_head_gib` | game/enemy/worm | `obj_worm` | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Core/Enemies/WormEnemy.cs` +1 |
| `obj_worm_segment` | game/enemy/worm | `obj_worm` | ported, by comment reference | `Core/Enemies/EnemyBody.cs`, `Core/Enemies/WormEnemy.cs` +1 |
| `obj_wormhole` | game/enemy/emitter | `create_wormhole` | ported, by comment reference | `Core/Enemies/Vortexes.cs`, `Core/Enemies/WormholeTiming.cs` +2 |
| `obj_wormhole_emitter` | game/enemy/emitter | `setup_level` | ported, by comment reference | `Core/Enemies/EnemyCommands.cs`, `Core/Enemies/Vortexes.cs` +4 |
| `obj_wraith` | game/enemy/wraith | `create_wormhole`, `generate_enemy_list`, `setup_level` | ported, by comment reference | `Core/Enemies/WraithEnemy.cs`, `Scripts/Enemies/Wraith.cs` +1 |
| `obj_wraith_blur` | game/enemy/wraith | `obj_wraith` | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Core/Enemies/WraithEnemy.cs` +1 |
| `obj_yesno_dialog` |  | `obj_btn_no`, `obj_btn_quit`, `obj_game` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `par_enemy` | game/enemy | `obj_dagger`, `obj_flame`, `obj_flashbomb`, `obj_helper` +22 more | ported, by comment reference | `Core/Arena/Flashbomb.cs`, `Core/Enemies/DaggerEnemy.cs` +36 |
| `par_enemy_emitter` | game/enemy/emitter | `setup_level` | ported, by comment reference | `Core/Enemies/EmitterTiming.cs`, `Core/Enemies/EnemyCommands.cs` +8 |
| `par_pickup` | game/pickup | `obj_carrot`, `obj_coin`, `setup_level` | ported, by comment reference | `Core/Pickups/Pickup.cs`, `Core/Pickups/PickupCommands.cs` +5 |
| `par_projectile` | game/weapon/projectile | `obj_bullet`, `obj_enemy_mine`, `obj_flashbomb`, `obj_helper` +3 more | ported, by comment reference | `Core/Arena/Flashbomb.cs`, `Core/Enemies/TrilobyteEnemy.cs` +5 |
| `par_rock` | game/rock | `obj_bullet`, `obj_player`, `obj_rock1`, `obj_rock2` +10 more | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Enemies/EnemyBody.cs` +23 |
| `par_structure` | game/structure | `obj_bullet`, `obj_player`, `draw_beam`, `move_rocks` +2 more | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Player/Landing.cs` +11 |
| `par_structure_cover` | game/structure | `setup_level` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Scripts/Arena/LevelReset.cs` +3 |

### Scripts (88)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `a_near_b` | functions | `obj_dagger`, `obj_helper`, `obj_hydra`, `obj_mimic` +8 more | ported, by comment reference | `Core/Enemies/DaggerEnemy.cs`, `Core/Helper/HelperTuning.cs` +4 |
| `add_spin` | rock | `obj_player`, `par_rock` | ported, by comment reference | `Scripts/Rocks/Rock.cs` |
| `ai_butterfly` | weather | `obj_butterfly` | ported, by comment reference | `Core/Enemies/InsectBodies.cs`, `Scripts/Enemies/Butterfly.cs` |
| `ai_firefly` | weather | `obj_firefly` | ported, by comment reference | `Core/Enemies/InsectBodies.cs`, `Scripts/Enemies/Firefly.cs` |
| `ai_fly` | weather | `obj_fly` | ported, by comment reference | `Core/Enemies/InsectBodies.cs`, `Scripts/Enemies/Fly.cs` |
| `angle_between_two_lines` |  | none | not reached | nothing in the original uses it |
| `check_rotate_clockwise` | functions | `obj_dagger`, `obj_enemy_mine`, `obj_helper`, `obj_hydra` +5 more | ported, by comment reference | `Core/Enemies/DaggerEnemy.cs`, `Core/Enemies/HydraEnemy.cs` +2 |
| `create_helper` |  | `obj_player` | ported, by comment reference | `Core/Content/ContentRegistry.cs`, `Core/Helper/HelperCommands.cs` +4 |
| `create_new_rock` | rock | `par_rock`, `rock_destroyed` | ported, by comment reference | `Scripts/Rocks/Rock.cs`, `Scripts/Rocks/RockField.cs` |
| `create_wormhole` |  | `obj_wormhole_emitter` | ported, by comment reference | `Core/Enemies/EnemyCommands.cs`, `Core/Enemies/EnemyStats.cs` +3 |
| `draw_beam` | weapons/ground/laser | `obj_laser` | ported, by comment reference | `Scripts/Rocks/Rock.cs`, `Scripts/Structures/Structure.cs` +2 |
| `draw_dottedline` | weapons/ground/laser | `obj_player`, `draw_beam` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/ArenaWorld.cs` |
| `draw_text_outline` | gui | `obj_YesNo`, `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100` +31 more | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/GameHud.cs` +8 |
| `draw_text_outline_alpha` | gui | `obj_typewriter`, `obj_typewriter_story` | ported, by comment reference | `Core/Story/Typewriter.cs`, `Scripts/Story/StoryRoom.cs` +1 |
| `draw_text_outline_color` | gui | `obj_energybar`, `obj_float_text`, `obj_speedometer`, `obj_startup` | ported, by comment reference | `Core/Popups/FloatText.cs`, `Core/Popups/PopupTuning.cs` +4 |
| `draw_text_outline_color_alpha` | gui | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +17 more | ported, by comment reference | `Core/Map/MapTuning.cs`, `Core/Upgrade/UpgradeScreen.cs` +2 |
| `fire_primary` | player | `obj_player` | ported | `Core/Weapons/ShipLoadout.cs`, `Scripts/Weapons/PerchedWeapon.cs` |
| `fire_secondary` | player | `obj_player` | ported | `Core/Weapons/ShipLoadout.cs`, `Scripts/Weapons/PerchedWeapon.cs` |
| `fling_player` | player | `obj_player`, `par_rock`, `rock_destroyed` | ported, by comment reference | `Core/Player/FlightTuning.cs`, `Core/Player/Landing.cs` +7 |
| `generate_enemy_list` |  | `setup_level` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Content/LevelCatalog.cs` +11 |
| `generate_swirl` | weapons/ground/railgun | `shoot_railgun` | ported, by comment reference | `Scripts/Weapons/RailgunShot.cs` |
| `get_state_string` |  | `obj_game` | ported, by comment reference | `Core/Arena/GameHud.cs` |
| `globals` |  | `obj_title` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/GameHud.cs` +31 |
| `in_view` | functions | `obj_bullet`, `obj_enemy_mine`, `obj_flame`, `obj_flashbomb` +9 more | ported, by comment reference | `Core/Helper/HelperCommands.cs`, `Core/Helper/HelperEvents.cs` +14 |
| `indent` |  | `obj_drop_intro`, `obj_map`, `obj_story` | ported, by comment reference | `Core/Content/DialogueCatalog.cs`, `Core/Popups/PopupTuning.cs` |
| `level_01` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_02` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_03` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_04` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_05` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_06` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_07` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_08` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_09` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_10` | levels | `globals`, `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_composition_00` | levels/composition | `load_level` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `level_composition_01` | levels/composition | `load_level` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `level_default` | levels | `load_scenario`, `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `level_enemygenerator` | levels | `setup_level` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `level_objective_00` | levels/objective | `load_level` | ported, by comment reference | `Core/Arena/ArenaTuning.cs` |
| `level_objective_01` | levels/objective | `load_level` | ported, by comment reference | `Core/Arena/ArenaTuning.cs` |
| `level_setting_00` | levels/setting | `load_level` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `level_setting_01` | levels/setting | `load_level` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `line_collision` | functions | none | not reached | nothing in the original uses it |
| `load_level` |  | none | not reached | nothing in the original uses it; Unity mentions it in `Core/Arena/ArenaTuning.cs` |
| `load_scenario` |  | `setup_level` | ported | `Core/Arena/LevelStart.cs` |
| `meatball_gib` |  | `obj_meatball` | ported, by comment reference | `Core/Enemies/Gibs.cs`, `Core/Enemies/MeatballEnemy.cs` +1 |
| `move_rocks` |  | `setup_level` | ported, by comment reference | `Scripts/Arena/ArenaRoom.cs`, `Scripts/Player/PlayerShipController.cs` +4 |
| `next_air_weapon` | player | `obj_player` | ported, by comment reference | `Core/Arena/GameHud.cs`, `Core/Player/PlayerCommands.cs` +7 |
| `next_ground_weapon` | player | `obj_player` | ported, by comment reference | `Core/Arena/GameHud.cs`, `Core/Player/PlayerCommands.cs` +10 |
| `next_line` | conversation | `obj_conversation` | ported, by comment reference | `Core/Story/ConversationBox.cs`, `Core/Story/TextDone.cs` +1 |
| `normal_detect` | functions | `obj_bullet`, `obj_player`, `obj_rocket` | ported, by comment reference | `Core/Enemies/PhalanxEnemy.cs`, `Core/Player/Landing.cs` +7 |
| `normalize_angle` |  | `obj_destination`, `obj_earthball`, `obj_player`, `obj_scroll_world` +4 more | ported, by comment reference | `Core/Story/StoryScreen.cs`, `Core/Story/StoryTuning.cs` |
| `play_random_music` |  | `obj_game`, `obj_jukebox`, `setup_level` | ported | `Core/Session/SessionTuning.cs` |
| `player_boost` | player | `obj_player`, `player_leap` | ported, by comment reference | `Core/Arena/GameHud.cs`, `Core/Player/Landing.cs` +5 |
| `player_brake` | player | `obj_player` | ported, by comment reference | `Core/Arena/GameHud.cs`, `Core/Player/Landing.cs` +5 |
| `player_hurt` | player | `obj_player`, `player_boost` | ported, by comment reference | `Core/Player/FlightTuning.cs`, `Core/Player/Landing.cs` +13 |
| `player_leap` | player | `obj_player` | ported, by comment reference | `Core/Pickups/PickupCommands.cs`, `Core/Pickups/PickupField.cs` +9 |
| `player_shield` | player | `obj_player` | ported, by comment reference | `Core/Player/PlayerCommands.cs`, `Core/Player/PlayerEvents.cs` +3 |
| `random_pickup` |  | `obj_dagger`, `obj_hydra`, `obj_meatball`, `obj_mimic` +7 more | ported, by comment reference | `Core/Enemies/DaggerEnemy.cs`, `Core/Enemies/EnemyEvents.cs` +6 |
| `random_positions` |  | `obj_player` | ported, by comment reference | `Core/Arena/ArenaCommands.cs`, `Core/Arena/ArenaEvents.cs` +5 |
| `reset_player` | player | `obj_game`, `obj_player`, `setup_level` | ported | `Scripts/Player/PlayerHealth.cs` |
| `rock_destroyed` | rock | `par_rock` | ported, by comment reference | `Core/Player/PlayerCommands.cs`, `Core/Player/ShipFlight.cs` +4 |
| `rotate_clockwise` | player | `obj_player` | ported, by comment reference | `Core/Player/ShipFlight.cs`, `Core/Player/ShipLook.cs` +1 |
| `rotate_counterclockwise` | player | `obj_player` | ported, by comment reference | `Core/Player/ShipFlight.cs`, `Core/Player/ShipLook.cs` +1 |
| `script69` | (not in the resource tree) | none | not reached | nothing in the original uses it; the script file is empty |
| `script73` | (not in the resource tree) | none | not reached | nothing in the original uses it; the script file is empty |
| `set_ammo_cost` |  | `obj_player`, `fling_player`, `player_leap` | ported, by comment reference | `Core/Player/PlayerCommands.cs`, `Core/Player/PlayerShip.cs` +6 |
| `set_level_complete` |  | `obj_game`, `obj_wormhole_emitter` | ported, by comment reference | `Core/Arena/ArenaTuning.cs`, `Core/Arena/LevelResults.cs` +10 |
| `set_objective_string` |  | `level_01`, `level_02`, `level_03`, `level_04` +10 more | ported, by comment reference | `Core/Arena/LevelStart.cs`, `Core/Session/GameSession.cs` +1 |
| `set_time_limit_string` |  | `level_05`, `level_06`, `level_07` | ported, by comment reference | `Core/Session/GameSession.cs` |
| `setup_level` |  | `obj_fade_game`, `obj_game`, `obj_player` | ported | `Core/Arena/LevelScript.cs` |
| `shoot_flamer` | weapons/ground | `fire_primary`, `fire_secondary` | ported, by comment reference | `Core/Weapons/WeaponPhysicalTable.cs`, `Core/Weapons/WeaponTable.cs` +1 |
| `shoot_flamethrower` | (not in the resource tree) | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Scripts/Weapons/Flamethrower.cs` |
| `shoot_laser` | weapons/ground/laser | `fire_primary`, `fire_secondary` | ported, by comment reference | `Core/Weapons/WeaponPhysicalTable.cs`, `Scripts/Weapons/LaserGun.cs` |
| `shoot_mine` | weapons/air | `fire_primary`, `fire_secondary` | ported, by comment reference | `Core/Weapons/WeaponTable.cs`, `Scripts/Weapons/AirWeaponLoadout.cs` +2 |
| `shoot_railgun` | weapons/ground/railgun | `fire_primary`, `fire_secondary` | ported, by comment reference | `Core/Weapons/WeaponPhysicalTable.cs`, `Core/Weapons/WeaponTable.cs` +1 |
| `shoot_rocket` | weapons/air | `fire_primary` | ported, by comment reference | `Core/Weapons/WeaponTable.cs`, `Scripts/Weapons/AirWeaponLoadout.cs` |
| `shoot_shotgun` | weapons/ground | `fire_primary`, `fire_secondary` | ported, by comment reference | `Core/Pickups/Pickup.cs`, `Core/Pickups/PickupCommands.cs` +5 |
| `shoot_vulcan` | weapons/ground | `fire_primary`, `fire_secondary` | ported, by comment reference | `Core/Pickups/Pickup.cs`, `Core/Pickups/PickupCommands.cs` +6 |
| `show_float_text` | gui | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Popups/FloatText.cs` |
| `show_float_text_color` | gui | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Popups/FloatText.cs` |
| `show_float_text_ext` | gui | `obj_dagger`, `obj_enemy_mine`, `obj_hydra`, `obj_meatball` +10 more | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Core/Popups/FloatText.cs` +3 |
| `spawn_chips` | player | `obj_player`, `player_leap` | ported, by comment reference | `Core/Pickups/Pickup.cs`, `Core/Pickups/PickupCommands.cs` +5 |
| `spawn_death_particles` | player | `obj_scrapmental`, `player_hurt` | ported, by comment reference | `Core/Enemies/EnemyEvents.cs`, `Core/Enemies/ScrapmentalEnemy.cs` +2 |
| `stop_vulcan_sfx` | player | `obj_player`, `fire_primary`, `fling_player` | ported, by comment reference | `Core/Weapons/GunRack.cs`, `Scripts/Weapons/VulcanWeapon.cs` |
| `subtract_ground_ammo` |  | `fire_primary` | ported, by comment reference | `Core/Weapons/AmmoPools.cs`, `Core/Weapons/GunRack.cs` +1 |
| `worm_gib` |  | `obj_worm`, `obj_worm_segment` | ported, by comment reference | `Core/Enemies/Gibs.cs`, `Core/Enemies/WormEnemy.cs` |

### Rooms (10)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `room_boss1` |  | none | not reached | nothing in the original uses it; Unity mentions it only in comments: `Core/Flow/RoomId.cs`, `Core/Weather/WeatherTuning.cs` |
| `room_gamescreen` |  | `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01`, `obj_btn_level02` +25 more | ported | `Core/Flow/RoomId.cs` |
| `room_intro` |  | `obj_white_dot` | ported | `Core/Flow/RoomId.cs` |
| `room_map` |  | `obj_btn_map`, `obj_scroll_world`, `obj_story`, `obj_title` | ported | `Core/Flow/RoomId.cs` |
| `room_shaft` |  | `level_04` | ported | `Core/Flow/RoomId.cs` |
| `room_sidescroll` |  | `level_05` | ported | `Core/Flow/RoomId.cs` |
| `room_story` |  | `obj_btn_campaign`, `obj_btn_quickstart` | ported | `Core/Flow/RoomId.cs` |
| `room_title` |  | `obj_btn_yes`, `obj_game`, `obj_intro`, `obj_map` +2 more | ported | `Core/Flow/RoomId.cs` |
| `room_upgrade` |  | `obj_btn_upgrade` | ported | `Core/Flow/RoomId.cs` |
| `startup` |  | none | not reached | nothing in the original uses it; Unity mentions it in `Core/Content/ContentRegistry.cs`, `Core/Flow/RoomId.cs` +2 |

### Sprites (213)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `spr_alertmarker` | map | `obj_alert` | ported | `Core/Popups/PopupTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_ammo_cartridge` | game/weapon | `obj_cartridge` | ported | `Core/Pickups/Pickup.cs` |
| `spr_ammo_shell` | game/weapon | `shoot_shotgun` | ported | `Core/Pickups/Pickup.cs` |
| `spr_angle_arrow` | game/rabbit/addon | `obj_player` | imported, owner ported | drawn by `obj_player`, which Unity names; no code names this sprite itself |
| `spr_angle_arrow_red` | game/rabbit/addon | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_asteroid_experiment` | game/rock | `obj_mimic`, `par_rock`, `create_new_rock` | ported | `Scripts/Enemies/MimicIllusion.cs`, `Scripts/Rocks/Rock.cs` |
| `spr_big_arrow` | game/gui | `obj_player` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `spr_bigexplosion` | game/explosion | `obj_rocket_explosion` | ported | `Scripts/Weapons/RocketExplosion.cs` |
| `spr_black` |  | `obj_fade_game`, `obj_fade_map`, `obj_fade_story`, `obj_fade_title` +1 more | ported, by comment reference | `Scripts/Flow/RoomFlowDirector.cs`, `Scripts/Rooms/TitleRoom.cs` |
| `spr_boss_hull1` | game/enemy/boss | `obj_scrapmental`, `obj_shrapnel_piece` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull2` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull3` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull4` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull5` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull6` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull7` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_boss_hull8` | game/enemy/boss | `obj_scrapmental` | ported | family `spr_boss_hull` + index in `Core/Enemies/ScrapmentalEnemy.cs` |
| `spr_brain` | game/enemy/boss | `obj_scrapmental`, `obj_shrapnel_back` | ported | `Scripts/Enemies/Scrapmental.cs` |
| `spr_brake_schematic` |  | `obj_brake_schematic` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_bullet` | game/weapon/projectile | `obj_bullet`, `obj_player` | ported | `Core/Weapons/WeaponPhysicalTable.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `spr_bullet_blue` | game/weapon/projectile | `obj_player` | ported | `Core/Weapons/WeaponPhysicalTable.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_bullet_flash` | game/weapon/projectile | `obj_player` | ported | `Core/Weapons/WeaponPhysicalTable.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_bullet_orange` | game/weapon/projectile | `obj_player` | ported | `Core/Weapons/WeaponPhysicalTable.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_bullet_vulcan` | game/weapon/projectile | `obj_player` | ported | `Core/Weapons/WeaponPhysicalTable.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_butterfly` | weather | `obj_butterfly`, `obj_butterfly_emitter` | ported | `Scripts/Enemies/Butterfly.cs` |
| `spr_button` | title | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +15 more | imported, owner ported | drawn by `obj_btn_yes`, which Unity names; no code names this sprite itself |
| `spr_button_area` | title | `obj_btn_campaign`, `obj_btn_quickstart`, `obj_btn_quit` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_button_area_small` | title | `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01`, `obj_btn_level02` +11 more | ported | `Core/Map/MapTuning.cs`, `Core/Rooms/TitleTuning.cs` +1 |
| `spr_button_back` | title | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +15 more | imported, owner ported | drawn by `obj_btn_yes`, which Unity names; no code names this sprite itself |
| `spr_button_over` | title | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +15 more | imported, owner ported | drawn by `obj_btn_yes`, which Unity names; no code names this sprite itself |
| `spr_carrot` | game/pickup | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_chip1` | game/gibs/chips | `obj_chip` | ported | family `spr_chip` + index in `Core/Pickups/Pickup.cs` |
| `spr_chip2` | game/gibs/chips | `obj_chip` | ported | family `spr_chip` + index in `Core/Pickups/Pickup.cs` |
| `spr_chip3` | game/gibs/chips | `obj_chip` | ported | family `spr_chip` + index in `Core/Pickups/Pickup.cs` |
| `spr_chip4` | game/gibs/chips | `obj_chip` | ported | family `spr_chip` + index in `Core/Pickups/Pickup.cs` |
| `spr_chip5` | game/gibs/chips | `obj_chip` | ported | family `spr_chip` + index in `Core/Pickups/Pickup.cs` |
| `spr_cloud1` | game/environment | `obj_cloud`, `obj_cloud_emitter`, `obj_drop_cloud`, `obj_drop_emitter` +2 more | ported | `Core/Rooms/DropSceneTuning.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `spr_cloud2` | game/environment | `obj_cloud`, `obj_drop_cloud`, `obj_fog` | ported | `Core/Rooms/DropSceneTuning.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `spr_cloud3` | game/environment | `obj_cloud`, `obj_drop_cloud`, `obj_fog` | ported | `Core/Rooms/DropSceneTuning.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `spr_cloud4` | game/environment | `obj_cloud`, `obj_drop_cloud`, `obj_fog` | ported | `Core/Rooms/DropSceneTuning.cs`, `Core/Weather/WeatherTuning.cs` +1 |
| `spr_coin` | game/pickup | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_coin_blue` | game/pickup | `obj_coin` | ported | `Core/Pickups/Pickup.cs` |
| `spr_coin_gold` | game/pickup | `obj_coin` | ported | `Core/Pickups/Pickup.cs` |
| `spr_coin_silver` | game/pickup | `obj_coin` | ported | `Core/Pickups/Pickup.cs` |
| `spr_coin_small` | game/pickup | `obj_healthbar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_comm_frame` | conversation | `obj_conversation` | ported | `Core/Story/ConversationBox.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_comm_frame_back` | conversation | `obj_conversation` | ported | `Core/Story/ConversationBox.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_conversation_box` | conversation | `obj_conversation` | ported | `Core/Story/ConversationBox.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_cyprid` | game/enemy | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_dagger` | game/enemy | `obj_dagger` | ported | `Scripts/Enemies/Dagger.cs` |
| `spr_dagger2` | game/enemy | `obj_dagger` | ported | `Scripts/Enemies/Dagger.cs` |
| `spr_death_particle_large` | game/rabbit/addon | `obj_death_particle`, `spawn_death_particles` | ported | `Scripts/Effects/DeathParticle.cs` |
| `spr_death_particle_medium` | game/rabbit/addon | `spawn_death_particles` | ported | `Scripts/Effects/DeathParticle.cs` |
| `spr_death_particle_small` | game/rabbit/addon | `spawn_death_particles` | ported | `Scripts/Effects/DeathParticle.cs` |
| `spr_destination` | map | `obj_destination` | ported, by comment reference | `Core/Map/MapScreen.cs`, `Core/Map/MapTuning.cs` |
| `spr_direction_arrow` | game/rabbit/addon | `obj_player` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `spr_drop` | title | `obj_drop_intro`, `obj_drop_title` | ported | `Core/Rooms/DropSceneTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_dropdot` | map | `obj_white_dot` | ported | `Core/Map/MapTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_earthball` | map | `obj_destination`, `obj_earthball`, `obj_scanner`, `obj_scroll_world` +1 more | ported | `Core/Map/MapTuning.cs`, `Core/Story/StoryTuning.cs` +1 |
| `spr_earthball_simple` | map | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_enemy_emitter` | game/enemy/emitter | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_enemy_emitter_large` | game/enemy/emitter | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_energybar_back` | game/gui/energybar | `obj_energybar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_energybar_cover` | game/gui/energybar | `obj_energybar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_energybar_fill` | game/gui/energybar | `obj_energybar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_explode2` | game/explosion | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_explode3` | game/explosion | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_explosion` | game/explosion | `obj_tinyexplosion` | ported | `Scripts/Effects/TinyExplosion.cs` |
| `spr_explosion_oval` | game/explosion | `obj_oval_explosion` | ported | `Scripts/Effects/OvalExplosion.cs` |
| `spr_extralives` | game/gui | `obj_healthbar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_eyeback` | title | `obj_stand_title` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `spr_fireball` | game/weapon/projectile | `obj_meatball` | imported, owner ported | drawn by `obj_meatball`, which Unity names; no code names this sprite itself |
| `spr_firefly` | weather | `obj_firefly`, `obj_firefly_emitter`, `ai_firefly` | ported | `Scripts/Enemies/Firefly.cs` |
| `spr_firefly_dark` | weather | `ai_firefly` | ported | `Scripts/Enemies/Firefly.cs` |
| `spr_firefly_strip` | weather | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_flotsum1` | game/gibs/asteroid | `obj_flotsum`, `obj_meatball_gib`, `obj_worm_gib` | ported | `Scripts/Rocks/Flotsum.cs` |
| `spr_flotsum2` | game/gibs/asteroid | `obj_flotsum` | ported | `Scripts/Rocks/Flotsum.cs` |
| `spr_flotsum3` | game/gibs/asteroid | `obj_flotsum` | ported | `Scripts/Rocks/Flotsum.cs` |
| `spr_flotsum4` | game/gibs/asteroid | `obj_flotsum` | ported | `Scripts/Rocks/Flotsum.cs` |
| `spr_flotsum5` | game/gibs/asteroid | `obj_flotsum` | ported | `Scripts/Rocks/Flotsum.cs` |
| `spr_fly` | weather | `obj_fly`, `obj_fly_emitter` | ported | `Scripts/Enemies/Fly.cs` |
| `spr_fly2` | weather | `obj_fly` | ported | family `spr_fly` + index in `Scripts/Enemies/Fly.cs` |
| `spr_ghost` | game/enemy | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_grenade` | game/weapon/projectile | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_grenade_explode` | game/explosion | `obj_grenade_explosion` | ported | `Scripts/Weapons/GrenadeExplosion.cs` |
| `spr_health_pip` | game/gui/healthbar | `obj_healthbar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_health_pip_back` | game/gui/healthbar | `obj_healthbar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_health_pip_empty` | game/gui/healthbar | `obj_healthbar` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_healthbar_back` | game/rabbit/addon | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_healthbar_cover` | game/rabbit/addon | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_healthbar_fill` | game/rabbit/addon | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_helper` |  | `obj_helper` | ported | `Core/Helper/HelperTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_hydra_body` | game/enemy | `obj_hydra` | ported | `Scripts/Enemies/Hydra.cs` |
| `spr_hydra_turret` | game/enemy | `obj_hydra` | ported | `Scripts/Enemies/Hydra.cs` |
| `spr_icon_flamer` | game/gui/weapons | `next_ground_weapon` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_icon_laser` | game/gui/weapons | `next_ground_weapon` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_icon_mine` | game/gui/weapons | `obj_weapon_gui`, `next_air_weapon` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_icon_missle` | game/gui/weapons | `obj_upgrade_missle` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_icon_railgun` | game/gui/weapons | `next_ground_weapon` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_icon_rocket` | game/gui/weapons | `obj_weapon_gui`, `next_air_weapon` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_icon_shotgun` | game/gui/weapons | `obj_btn_upgrade_shotgun`, `obj_weapon_gui`, `next_ground_weapon` | ported | `Core/Arena/GameHud.cs`, `Core/Upgrade/UpgradeScreen.cs` +1 |
| `spr_icon_vulcan` | game/gui/weapons | `obj_btn_upgrade_vulcan`, `obj_weapon_gui`, `next_ground_weapon` | ported | `Core/Arena/GameHud.cs`, `Core/Upgrade/UpgradeScreen.cs` +1 |
| `spr_laser_pulse` | game/weapon/projectile | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_leaf1` | weather | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_leaf2` | weather | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_logo` | title | `obj_logo` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_lost_health` | game/gui/healthbar | `obj_losthealth` | ported | `Core/Player/LostHealthPip.cs` |
| `spr_meatball` | game/enemy | `obj_meatball` | ported | `Scripts/Enemies/Meatball.cs` |
| `spr_meatball_eyes` | game/enemy | `obj_meatball` | ported | `Scripts/Enemies/Meatball.cs` |
| `spr_meatball_gib1` | game/gibs/meatball | `obj_meatball_gib` | ported | family `spr_meatball_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_meatball_gib2` | game/gibs/meatball | `obj_meatball_gib` | ported | family `spr_meatball_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_meatball_gib3` | game/gibs/meatball | `obj_meatball_gib` | ported | family `spr_meatball_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_meatball_gib4` | game/gibs/meatball | `obj_meatball_gib` | ported | family `spr_meatball_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_meatball_gib5` | game/gibs/meatball | `obj_meatball_gib` | ported | family `spr_meatball_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_meatball_gib6` | game/gibs/meatball | `obj_meatball_gib` | ported | family `spr_meatball_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_meatball_pupils` | game/enemy | `obj_meatball` | ported | `Scripts/Enemies/Meatball.cs` |
| `spr_mimic` | game/enemy | `obj_mimic` | ported | `Scripts/Enemies/Mimic.cs` |
| `spr_mine` | game/weapon/projectile | `obj_enemy_mine`, `obj_mine` | ported | `Scripts/Weapons/Mine.cs` |
| `spr_moon_surface` | title | `obj_moon_surface`, `obj_stand_title` | ported | `Core/Arena/MoonSurface.cs`, `Core/Rooms/TitleTuning.cs` +2 |
| `spr_muzzleflash` | game/weapon/projectile | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_muzzleflash_new` | game/weapon/projectile | `obj_muzzleflash` | ported | `Scripts/Effects/MuzzleFlash.cs` |
| `spr_pellet` | game/weapon/projectile | `shoot_shotgun` | ported | `Scripts/Weapons/Shotgun.cs` |
| `spr_phalanx_body` | game/enemy | `obj_phalanx` | ported | `Scripts/Enemies/Phalanx.cs` |
| `spr_phalanx_shield` | game/enemy | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_pickup_health` | game/pickup | `obj_carrot` | ported | `Core/Pickups/Pickup.cs` |
| `spr_playerexplosion` | game/explosion | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_pointer` | game/gui | `obj_map`, `obj_title`, `obj_upgrade` | ported | `Core/Pointer/MouseTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_portrait_general_zee` | conversation | `obj_conversation` | ported | `Core/Story/ConversationBox.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_powder_fire` |  | `obj_flame` | ported | `Scripts/Weapons/Flame.cs` |
| `spr_pupil` | title | `obj_stand_title` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `spr_rabbit_crouch` | game/rabbit | `obj_player` | ported | `Core/Player/Lives.cs` |
| `spr_rabbit_crouch_hurt` | game/rabbit | `obj_player` | ported | `Core/Player/Lives.cs` |
| `spr_rabbit_idle` | game/rabbit | `obj_player`, `globals` | ported | `Core/Player/Lives.cs` |
| `spr_rabbit_idle_hurt` | game/rabbit | `obj_player` | ported | `Core/Player/Lives.cs` |
| `spr_rabbit_leap` | game/rabbit | `obj_player` | ported | `Core/Player/Lives.cs` |
| `spr_rabbit_leap_hurt` | game/rabbit | `obj_player` | ported | `Core/Player/Lives.cs` |
| `spr_rabbit_leap_large` | game/rabbit | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_rabbit_schematic` |  | `obj_rabbit_schematic` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_rabbit_stand_large` | game/rabbit | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_railgun_swirl` | game/weapon/projectile | `obj_rail_swirl` | ported | `Scripts/Weapons/RailgunShot.cs` |
| `spr_rain` | weather | `obj_rain_emitter`, `obj_raindrop` | ported | `Core/Weather/WeatherTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_raindrop` | weather | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_rainemitter` | weather | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_raingun_ring` | game/weapon/projectile | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_results` | game | `obj_level_results` | ported | `Core/Arena/LevelResults.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `spr_reticle_blue` | game/rabbit/addon | `obj_mouse`, `obj_player` | ported | `Core/Pointer/MouseTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_reticle_green` | game/rabbit/addon | `obj_player` | ported | `Core/Pointer/MouseTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_reticle_red` | game/rabbit/addon | `obj_player` | ported | `Core/Pointer/MouseTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_ring_back` |  | `obj_ring_back` | not reached (owner `obj_ring_back`) | asset imported; only the never-placed obj_ring uses it; see 'Content the original never reaches' |
| `spr_ring_front` |  | `obj_ring` | not reached (owner `obj_ring`) | asset imported; named only in commented-out code in obj_game (the ring gate); see 'Content the original never reaches' |
| `spr_rock1` | game/rock | `obj_mimic`, `obj_mimic_illusion`, `obj_rock1`, `par_rock` +1 more | ported | `Scripts/Rocks/Rock.cs` |
| `spr_rock2` | game/rock | `obj_mimic`, `obj_mimic_illusion`, `obj_rock2`, `par_rock` +1 more | ported | `Scripts/Rocks/Rock.cs` |
| `spr_rock3` | game/rock | `obj_mimic`, `obj_mimic_illusion`, `obj_rock3`, `par_rock` +1 more | ported | `Scripts/Rocks/Rock.cs` |
| `spr_rock4` | game/rock | `obj_mimic`, `obj_mimic_illusion`, `obj_rock4`, `par_rock` +1 more | ported | `Scripts/Rocks/Rock.cs` |
| `spr_rock5` | game/rock | `obj_mimic`, `obj_rock5`, `par_rock`, `create_new_rock` | ported | `Scripts/Rocks/Rock.cs` |
| `spr_rocket` | game/weapon/projectile | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_rocket_anime` | game/weapon/projectile | `obj_rocket` | ported | `Scripts/Weapons/Rocket.cs` |
| `spr_rocketbooster_schematic` |  | `obj_booster_schematic` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_room_loop` |  | `obj_game`, `obj_intro`, `obj_map`, `obj_startup` +3 more | imported, owner ported | drawn by `obj_startup`, which Unity names; no code names this sprite itself |
| `spr_sand1` | weather | `obj_sand`, `obj_sand_emitter` | ported | `Core/Weather/WeatherTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_sand2` | weather | `obj_sand` | ported | `Core/Weather/WeatherTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_sawblade` | game/enemy | `obj_sawblade` | ported | `Scripts/Enemies/Sawblade.cs` |
| `spr_scanner` | map | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_scanner_beam` | map | `obj_scanner`, `obj_uss_warren` | ported | `Core/Map/MapTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_scenario_box` | map | `obj_alert` | imported, owner ported | drawn by `obj_alert`, which Unity names; no code names this sprite itself |
| `spr_schrapnel_eye` | game/enemy/boss | `obj_scrapmental` | ported | `Scripts/Enemies/Scrapmental.cs` |
| `spr_shield_hitbox` |  | `obj_phalanx_shield`, `obj_shieldwall` | ported, by comment reference | `Core/Enemies/PhalanxEnemy.cs`, `Scripts/Enemies/PhalanxShield.cs` +1 |
| `spr_shieldwall` | game/rabbit/addon | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_shieldwall_anime` | game/rabbit/addon | `obj_phalanx_shield`, `obj_shieldwall` | ported | `Scripts/Enemies/PhalanxShield.cs`, `Scripts/Player/ShieldWall.cs` |
| `spr_shrapnel_frame` | game/enemy/boss | `obj_scrapmental` | ported | `Scripts/Enemies/Scrapmental.cs` |
| `spr_shrapnel_harpoon` | game/enemy/boss | `obj_shrapnel_harpoon` | ported | `Scripts/Enemies/Scrapmental.cs` |
| `spr_sliver` | game/weapon/projectile | `obj_scrapmental`, `obj_worm` | ported | `Scripts/Enemies/Scrapmental.cs` |
| `spr_snow1` | weather | `obj_snow`, `obj_snow_emitter` | ported | `Core/Weather/WeatherTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_snow2` | weather | `obj_snow` | ported | `Core/Weather/WeatherTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_spawn_point` | game/gui | `obj_spawnpoint` | not ported, by design (owner `obj_spawnpoint`) | asset imported; only sets angle = 270, draws nothing and nothing reads it (BIBLE V29, U.HARE-T21) |
| `spr_stand` | title | `obj_stand_title` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_stem` | game/enemy | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_structure1` | game/structure | `setup_level` | ported | `Scripts/Structures/Structure.cs` |
| `spr_structure1_cover` | game/structure | `setup_level` | ported | `Scripts/Structures/StructureCover.cs` |
| `spr_tech_schematic` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_teleporter` | game/weapon/projectile | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_textdone` |  | `obj_textdone` | ported | `Core/Story/TextDone.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_tip_box` |  | `obj_YesNo`, `obj_drop_intro`, `obj_scenario_details`, `obj_tip` +1 more | ported | `Core/Map/MapTuning.cs`, `Core/Popups/PopupTuning.cs` +3 |
| `spr_trilobyte` | game/enemy | `obj_trilobyte` | ported | `Scripts/Enemies/Trilobyte.cs` |
| `spr_upgrade_circle_empty` | upgrade | `obj_upgrade_missle1`, `obj_upgrade_shotgun1`, `obj_upgrade_vulcan1` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_upgrade_circle_filled` | upgrade | `obj_upgrade_missle1`, `obj_upgrade_shotgun1`, `obj_upgrade_vulcan1` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_upgrade_frame` | upgrade | `obj_btn_upgrade_shotgun`, `obj_btn_upgrade_vulcan`, `obj_upgrade_missle` | imported, owner ported | drawn by `obj_btn_upgrade_vulcan`, which Unity names; no code names this sprite itself |
| `spr_uss_warren` | map | `obj_map`, `obj_uss_warren` | ported | `Core/Map/MapTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_vortex` | game/enemy | `obj_wormhole`, `obj_wormhole_emitter`, `par_enemy_emitter`, `create_wormhole` | ported | `Scripts/Enemies/Vortex.cs` |
| `spr_weapon_flamer` | game/weapon | `next_ground_weapon`, `shoot_flamer` | ported | `Core/Player/ShipLook.cs` |
| `spr_weapon_laser` | game/weapon | `next_ground_weapon`, `shoot_laser` | ported | `Core/Player/ShipLook.cs` |
| `spr_weapon_pickup` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_weapon_railgun` | game/weapon | `next_ground_weapon`, `shoot_railgun` | ported | `Core/Player/ShipLook.cs` |
| `spr_weapon_rocket` | game/weapon | `obj_player`, `next_air_weapon` | imported, owner ported | drawn by `obj_player`, which Unity names; no code names this sprite itself |
| `spr_weapon_select` |  | `obj_weapon_gui` | ported | `Core/Arena/GameHud.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_weapon_select_light` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_weapon_shotgun` | game/weapon | `next_ground_weapon`, `shoot_shotgun` | ported | `Core/Player/ShipLook.cs` |
| `spr_weapon_vulcan` | game/weapon | `obj_player`, `next_ground_weapon`, `shoot_vulcan` | ported | `Core/Player/ShipLook.cs` |
| `spr_white` |  | `obj_flashbomb` | ported | `Core/Arena/Flashbomb.cs`, `Resources/Hare/Content/GameContent.json` |
| `spr_white_dot` | map | none | imported, not reached | asset imported; nothing in the original uses it |
| `spr_worm_gib1` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib2` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib3` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib4` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib5` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib6` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib7` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_gib8` | game/gibs/worm | `obj_worm_gib` | ported | family `spr_worm_gib` + index in `Scripts/Enemies/FadingDebris.cs` |
| `spr_worm_head` | game/enemy | `obj_worm`, `obj_worm_head_gib` | ported | `Scripts/Enemies/FadingDebris.cs`, `Scripts/Enemies/Worm.cs` |
| `spr_worm_segment` | game/enemy | `obj_worm`, `obj_worm_segment` | ported | `Scripts/Enemies/Worm.cs`, `Scripts/Enemies/WormSegment.cs` |
| `spr_wraith_float` | game/enemy | `obj_wraith`, `obj_wraith_blur` | ported | `Scripts/Enemies/Wraith.cs` |
| `spr_wraith_stunned` | game/enemy | `obj_wraith` | ported | `Scripts/Enemies/Wraith.cs` |
| `spr_yesno_button` |  | `obj_btn_no`, `obj_btn_yes` | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |

### Backgrounds (18)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `bg_atmosphere` | backdrops | `level_09`, `level_10` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_black` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `bg_circuitboard` |  | `startup` | ported | `Core/Rooms/StartupTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_circuitboard2` |  | `startup` | ported | `Core/Rooms/StartupTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_circuitboard3` |  | `startup` | ported | `Core/Rooms/StartupTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_gradient2` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `bg_gradient3` |  | `room_intro`, `room_title`, `level_enemygenerator` | ported | `Core/Arena/ArenaTuning.cs`, `Core/Rooms/DropSceneTuning.cs` +1 |
| `bg_metal` |  | `room_upgrade` | ported | `Core/Upgrade/UpgradeScreen.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_moonview` | backdrops | `level_02` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_nightsky` |  | `room_gamescreen`, `room_intro`, `room_map`, `room_story` +3 more | ported | `Core/Arena/ArenaTuning.cs`, `Core/Map/MapTuning.cs` +3 |
| `bg_orange` | backdrops | `level_default` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_rabbit_schematic` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `bg_rabbit_schematic_transparent` |  | `startup` | ported | `Core/Rooms/StartupTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_semiopaque80` |  | `startup` | ported | `Core/Rooms/StartupTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_stormclouds` |  | `room_shaft`, `level_04` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_tropicalocean` |  | `room_sidescroll`, `level_05` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_uphigh` | backdrops | `room_boss1`, `level_03` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `bg_worldcurve` | backdrops | `level_01`, `level_07`, `level_08`, `level_setting_00` +1 more | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |

### Sounds (75)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `music_inaheartbeat` | music | `play_random_music` | ported | `Core/Session/SessionTuning.cs` |
| `music_latinindustries` | music | `play_random_music` | ported | `Core/Session/SessionTuning.cs` |
| `music_overcast` | music | none | imported, not reached | asset imported; nothing in the original uses it; Unity mentions it only in comments: `Core/Session/SessionTuning.cs` |
| `music_pinballspring` | music | `globals`, `play_random_music` | ported | `Core/Session/SessionTuning.cs` |
| `music_presenterator` | music | `play_random_music` | ported | `Core/Session/SessionTuning.cs` |
| `music_wallpaper` | music | `play_random_music` | ported | `Core/Session/SessionTuning.cs` |
| `sfx_beep` |  | `obj_enemy_mine`, `obj_game`, `obj_mine` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_bend` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_blip` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_blipgun` |  | `obj_hydra`, `shoot_shotgun`, `shoot_vulcan` | ported, by comment reference | `Scripts/Enemies/Hydra.cs` |
| `sfx_boost` |  | `obj_level_results`, `obj_player`, `player_boost` | ported | `Core/Arena/LevelResults.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_bounce` |  | `obj_bullet`, `obj_rocket` | ported | `Scripts/Weapons/Bullet.cs` |
| `sfx_bounce_grenade` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_brake` |  | `obj_player`, `player_brake` | ported | `Scripts/Player/PlayerShipController.cs` |
| `sfx_bullet_explosion` |  | `obj_tinyexplosion` | ported, by comment reference | `Scripts/Effects/TinyExplosion.cs` |
| `sfx_click` |  | `obj_btn_map`, `obj_btn_quickstart`, `obj_btn_shaft`, `obj_btn_upgrade` | ported | `Core/Map/MapTuning.cs`, `Core/Rooms/TitleTuning.cs` +1 |
| `sfx_coin` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_crash` |  | `obj_drop_title`, `obj_title` | not reached | only in commented-out audio_play_sound lines of obj_title and obj_drop_title; see 'Content the original never reaches' (U.HARE-T30) |
| `sfx_death` |  | `spawn_death_particles` | ported | `Scripts/Effects/DeathParticle.cs` |
| `sfx_disabled` |  | `obj_ring`, `player_boost`, `player_brake`, `player_leap` +1 more | ported, by comment reference | `Core/Player/ShipFlight.cs` |
| `sfx_empty_clip` |  | `fire_primary`, `fire_secondary` | ported | `Scripts/Weapons/VulcanWeapon.cs` |
| `sfx_explode` |  | `obj_flame`, `obj_grenade_explosion`, `obj_oval_explosion`, `obj_player` +1 more | ported | `Scripts/Effects/OvalExplosion.cs`, `Scripts/Weapons/GrenadeExplosion.cs` +1 |
| `sfx_extralife` |  | `obj_coin` | ported | `Scripts/Pickups/PickupDirector.cs` |
| `sfx_fire` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fire_cannon` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fire_grenade` |  | `obj_trilobyte`, `shoot_flamer`, `shoot_flamethrower`, `shoot_mine` | ported, by comment reference | `Scripts/Weapons/AirWeaponLoadout.cs`, `Scripts/Weapons/GrenadeExplosion.cs` +1 |
| `sfx_fire_laser` |  | `obj_laser`, `obj_player`, `shoot_laser` | ported | `Scripts/Weapons/LaserGun.cs` |
| `sfx_fire_railgun` |  | `shoot_railgun` | ported | `Scripts/Weapons/RailgunGun.cs` |
| `sfx_fire_shotgun` |  | `shoot_shotgun` | ported | `Scripts/Weapons/Shotgun.cs` |
| `sfx_flashbomb` |  | `obj_flashbomb` | ported | `Core/Arena/Flashbomb.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_flick` |  | `obj_conversation`, `obj_map`, `obj_tip`, `next_line` | ported | `Core/Map/MapTuning.cs`, `Core/Popups/PopupTuning.cs` +2 |
| `sfx_flyover` |  | `obj_rocket`, `reset_player` | ported | `Scripts/Player/PlayerHealth.cs` |
| `sfx_fuse1` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fuse2` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fuse3` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fuse4` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fuse5` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_fuse6` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_grenade_beep` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_hawk` |  | `obj_helper` | ported | `Core/Helper/HelperTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_hologram` |  | `player_shield` | ported | `Scripts/Player/PlayerShipController.cs` |
| `sfx_hologram_pass` |  | `obj_player` | ported | `Scripts/Player/PlayerShipController.cs` |
| `sfx_hover` |  | `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100`, `obj_btn_level01` +18 more | ported | `Core/Map/MapTuning.cs`, `Core/Rooms/TitleTuning.cs` +2 |
| `sfx_hurt1` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt2` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt3` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt4` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt5` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt6` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt7` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_hurt8` | player | `player_hurt` | ported | family `sfx_hurt` + index in `Scripts/Player/PlayerHealth.cs` |
| `sfx_land` |  | `obj_drop_title`, `obj_player` | ported | `Core/Rooms/DropSceneTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_loop_laser` |  | `obj_laser`, `obj_player` | ported | `Scripts/Weapons/LaserGun.cs` |
| `sfx_powerup` |  | `obj_carrot`, `obj_coin`, `obj_ring` | ported | `Scripts/Pickups/PickupDirector.cs` |
| `sfx_reload_full` |  | `obj_player` | ported | `Scripts/Weapons/PlayerAmmo.cs` |
| `sfx_reload_half` |  | `obj_player` | ported | `Scripts/Weapons/PlayerAmmo.cs` |
| `sfx_roit` |  | `obj_YesNo`, `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100` +13 more | ported | `Core/Rooms/TitleTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_select` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_set_rocket` |  | `shoot_rocket` | ported, by comment reference | `Scripts/Weapons/AirWeaponLoadout.cs` |
| `sfx_shoot_vortex` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_sonar_ping` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_switch` |  | `obj_levelstart`, `obj_player`, `next_air_weapon`, `next_ground_weapon` | ported | `Core/Arena/ArenaTuning.cs`, `Resources/Hare/Content/GameContent.json` +1 |
| `sfx_switch_back` |  | `obj_player` | ported | `Scripts/Player/PlayerShipController.cs` |
| `sfx_takeoff` |  | `obj_map`, `obj_player`, `player_leap` | ported | `Core/Map/MapTuning.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_turn` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_type` |  | `obj_conversation` | ported | `Core/Story/Typewriter.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_type2` |  | `obj_typewriter_story` | ported | `Core/Story/Typewriter.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_vortex_blow` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_vortex_suck` |  | none | imported, not reached | asset imported; nothing in the original uses it |
| `sfx_vulcan_loop` | vulcan | `shoot_vulcan`, `stop_vulcan_sfx` | ported | `Scripts/Weapons/VulcanWeapon.cs` |
| `sfx_vulcan_spindown` | vulcan | `stop_vulcan_sfx` | ported | `Scripts/Weapons/VulcanWeapon.cs` |
| `sfx_vulcan_spinup` | vulcan | `fire_primary` | ported | `Scripts/Weapons/VulcanWeapon.cs` |
| `sfx_whoosh` |  | none | imported, not reached | asset imported; nothing in the original uses it; Unity mentions it only in comments: `Scripts/Audio/Jukebox.cs` |
| `sfx_woof` |  | `next_line` | ported | `Core/Story/ConversationBox.cs`, `Resources/Hare/Content/GameContent.json` |
| `sfx_zoom` |  | none | imported, not reached | asset imported; nothing in the original uses it |

### Fonts (8)

| Name | Group | Used by | Status | Unity / notes |
|---|---|---|---|---|
| `fnt_basic` |  | none | imported, not reached | asset imported; nothing in the original uses it; Unity mentions it in `Scripts/UI/HareFonts.cs` |
| `fnt_medium` |  | `obj_typewriter_story` | ported | `Scripts/UI/HareFonts.cs` |
| `fnt_point` |  | `obj_YesNo`, `obj_btn_campaign`, `obj_btn_diff0`, `obj_btn_diff100` +36 more | ported | `Scripts/UI/HareFonts.cs` |
| `fnt_score` |  | `obj_YesNo`, `obj_btn_campaign`, `obj_btn_no`, `obj_btn_quickstart` +10 more | ported | `Scripts/UI/HareFonts.cs` |
| `fnt_small` |  | `obj_weapon_gui` | ported | `Scripts/UI/HareFonts.cs` |
| `font4` | (not in the resource tree) | none | imported, not reached | asset imported; nothing in the original uses it; Unity mentions it in `Scripts/UI/HareFonts.cs` |
| `font7` |  | none | imported, not reached | asset imported; nothing in the original uses it; Unity mentions it in `Scripts/UI/HareFonts.cs` |
| `font8` |  | none | imported, not reached | asset imported; nothing in the original uses it; Unity mentions it in `Scripts/UI/HareFonts.cs` |

### Data files (1)

| Name | Used by | Status | Unity / notes |
|---|---|---|---|
| `credits.txt` | no object or script reads it | imported | `Audio/credits.txt` (the sound credits; shown by the credits room, UH-T69) |

### Resource-tree folders

Every group in the resource tree below the top level, so an empty folder is recorded too.

| Kind | Folder | Direct resources | Sub-folders |
|---|---|---|---|
| sound | `(top level: sound)` | 58 | 3 |
| sound | `music` | 6 | 0 |
| sound | `player` | 8 | 0 |
| sound | `vulcan` | 3 | 0 |
| sprite | `(top level: sprites)` | 18 | 6 |
| sprite | `conversation` | 4 | 0 |
| sprite | `game` | 1 | 10 |
| sprite | `game/enemy` | 20 | 2 |
| sprite | `game/enemy/boss` | 12 | 0 |
| sprite | `game/enemy/emitter` | 2 | 0 |
| sprite | `game/environment` | 4 | 0 |
| sprite | `game/explosion` | 7 | 0 |
| sprite | `game/gibs` | 0 | 4 |
| sprite | `game/gibs/asteroid` | 5 | 0 |
| sprite | `game/gibs/chips` | 5 | 0 |
| sprite | `game/gibs/meatball` | 6 | 0 |
| sprite | `game/gibs/worm` | 8 | 0 |
| sprite | `game/gui` | 4 | 3 |
| sprite | `game/gui/energybar` | 3 | 0 |
| sprite | `game/gui/healthbar` | 4 | 0 |
| sprite | `game/gui/weapons` | 8 | 0 |
| sprite | `game/pickup` | 7 | 0 |
| sprite | `game/rabbit` | 8 | 1 |
| sprite | `game/rabbit/addon` | 14 | 0 |
| sprite | `game/rock` | 6 | 0 |
| sprite | `game/structure` | 2 | 0 |
| sprite | `game/weapon` | 8 | 1 |
| sprite | `game/weapon/projectile` | 18 | 0 |
| sprite | `map` | 10 | 0 |
| sprite | `title` | 11 | 0 |
| sprite | `upgrade` | 3 | 0 |
| sprite | `weather` | 15 | 0 |
| background | `(top level: background)` | 13 | 1 |
| background | `backdrops` | 5 | 0 |
| script | `(top level: scripts)` | 22 | 8 |
| script | `conversation` | 1 | 0 |
| script | `functions` | 5 | 0 |
| script | `gui` | 7 | 0 |
| script | `levels` | 12 | 3 |
| script | `levels/composition` | 2 | 0 |
| script | `levels/objective` | 2 | 0 |
| script | `levels/setting` | 2 | 0 |
| script | `player` | 16 | 0 |
| script | `rock` | 3 | 0 |
| script | `weapons` | 0 | 2 |
| script | `weapons/air` | 2 | 0 |
| script | `weapons/ground` | 3 | 2 |
| script | `weapons/ground/laser` | 3 | 0 |
| script | `weapons/ground/railgun` | 2 | 0 |
| script | `weather` | 3 | 0 |
| font | `(top level: fonts)` | 7 | 0 |
| object | `(top level: objects)` | 5 | 8 |
| object | `conversation` | 2 | 0 |
| object | `game` | 9 | 9 |
| object | `game/effect` | 6 | 0 |
| object | `game/enemy` | 1 | 11 |
| object | `game/enemy/dagger` | 1 | 0 |
| object | `game/enemy/emitter` | 3 | 0 |
| object | `game/enemy/hydra` | 1 | 0 |
| object | `game/enemy/meatball` | 2 | 0 |
| object | `game/enemy/mimic` | 2 | 0 |
| object | `game/enemy/phalanx` | 2 | 0 |
| object | `game/enemy/sawblade` | 1 | 0 |
| object | `game/enemy/scrapmental` | 4 | 0 |
| object | `game/enemy/trilobyte` | 1 | 0 |
| object | `game/enemy/worm` | 4 | 0 |
| object | `game/enemy/wraith` | 2 | 0 |
| object | `game/gui` | 11 | 0 |
| object | `game/pickup` | 3 | 0 |
| object | `game/ring` | 2 | 0 |
| object | `game/rock` | 8 | 0 |
| object | `game/structure` | 2 | 0 |
| object | `game/weapon` | 1 | 4 |
| object | `game/weapon/laser` | 1 | 0 |
| object | `game/weapon/projectile` | 5 | 0 |
| object | `game/weapon/railgun` | 2 | 0 |
| object | `game/weapon/vulcan` | 2 | 0 |
| object | `game/weather` | 14 | 0 |
| object | `intro` | 4 | 0 |
| object | `map` | 10 | 0 |
| object | `startup` | 1 | 0 |
| object | `story` | 4 | 0 |
| object | `title` | 22 | 0 |
| object | `upgrade` | 4 | 3 |
| object | `upgrade/missle` | 2 | 0 |
| object | `upgrade/shotgun` | 2 | 0 |
| object | `upgrade/vulcan` | 2 | 0 |
| room | `(top level: rooms)` | 10 | 0 |

### Totals

| Kind | Count | Status counts |
|---|---|---|
| Objects | 150 | in progress, U.HARE-T68 1, not ported, by design 1, not reached 16, ported 63, ported, by comment reference 69 |
| Scripts | 88 | not reached 8, ported 22, ported, by comment reference 58 |
| Rooms | 10 | not reached 2, ported 8 |
| Sprites | 213 | imported, not reached 35, imported, owner ported 9, not ported, by design (owner `obj_spawnpoint`) 1, not reached (owner `obj_ring_back`) 1, not reached (owner `obj_ring`) 1, ported 163, ported, by comment reference 3 |
| Backgrounds | 18 | imported, not reached 3, ported 15 |
| Sounds | 75 | imported, not reached 22, not reached 1, ported 47, ported, by comment reference 5 |
| Fonts | 8 | imported, not reached 4, ported 4 |
