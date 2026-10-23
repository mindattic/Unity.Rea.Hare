# HARE / Rabbit — GML source notes

Unity.Hare is a full Unity rebuild of a GameMaker 8.1 (`.gmx`) arcade shoot-'em-up about a
robotic rabbit that fights enemies in low orbit. GML hit a ceiling the original author couldn't
get past, even though the game was functionally complete. These notes capture what the GML
source actually contains, so the rebuild has a durable reference even if the source drives
(`R:\...`) become unavailable. They record facts observed in the source, not implementation
decisions — those live in the ticket backlog (`UH-*` in the MindAttic ticket database).

## Source locations

- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRabbit\Rabbit\10-10-13_6.31am` —
  earlier prototype, GM8.1 project `Rabbit.gmx`. 99 objects, 52 scripts, 102 sound assets, 11 rooms.
- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlHARE` — later, more complete build,
  GM8.1 project `HARE.gmx`. 138 objects, 88 scripts, 152 sound assets, 10 rooms. This is the
  authoritative version for the rebuild; Rabbit is kept as an earlier reference only.
- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\Unity\HARE` — an existing, early Unity
  proof-of-concept (not a full port). 8 scripts total: `Camera/SmoothCamera`,
  `Game/GameWorld`, `Game/ResetGame`, `Player/PlayerBase`, `Player/PlayerMovement`,
  `Rock/RockBaseOld`, `Rock/RockMovement`, `Weapon/BulletBase`, `Weapon/WeaponBase`. Its
  `Assets/Resources` layout (Actors/Rabbit, Audio/Music, Audio/SoundEffects, Environment,
  Prefabs/Rock, Prefabs/Weapon, Props, GUI) is a reasonable starting folder shape.

`.gmx` is GameMaker 8.1's XML project format: each object is one `objects/obj_*.object.gmx`
file (events encoded as `eventtype`/`enumb` pairs — 0 Create, 2 Alarm, 3 Step, 4 Collision,
5 Keyboard(held), 6 Mouse, 8 Draw, 9 Key Press, 10 Key Release), each script is one
`scripts/*.gml` file, and each room is one `rooms/*.room.gmx` file.

## Rooms (HARE.gmx) — confirmed game modes

| Room | Background | Role |
|---|---|---|
| `room_title` | `bg_gradient3`/`bg_nightsky` | Title screen |
| `room_map` | `bg_nightsky` | Overworld / level-select map |
| `room_gamescreen` | `bg_nightsky` | Main open-arena combat room |
| `room_sidescroll` | `bg_tropicalocean` | Side-scrolling stage variant |
| `room_shaft` | `bg_stormclouds` | Vertical "shaft" descent stage variant |
| `room_boss1` | `bg_uphigh` | Boss encounter |
| `room_story` | `bg_nightsky` | Dialogue / cutscene |
| `room_upgrade` | `bg_metal` | Garage / upgrade screen between levels |
| `startup` | — | Game bootstrap room |

Rabbit (the earlier build) additionally has `room3` and `room_titlescreen` / `room_gamescreen2`,
superseded by HARE's versions.

## Player (`obj_player`, HARE.gmx)

2499-line object file — the largest single object, effectively the whole player state machine.
Confirmed event surface: Create, 11 Alarms (0–10, used for timers: weapon cooldowns, boost,
spin-up/down, etc.), Step, Collision with `obj_bullet`, `obj_rocket`, `obj_rocket_explosion`,
`par_enemy`, `par_rock`, `par_structure`, held-Keyboard, Key Press and Key Release (arrow-style
directional input), Mouse (global button events), Draw.

Player-related scripts: `player_boost`, `player_brake`, `player_leap`, `player_shield`,
`player_hurt`, `reset_player`, `fling_player`.

## Weapons

Air-to-air loadout: `shoot_vulcan` (spin-up/spin-down + looping fire sound, per `sfx/` assets
`vulcan_spinup.wav`/`vulcan_loop.wav`/`vulcan_spindown.wav`), `shoot_laser`, `shoot_railgun`,
`shoot_rocket`, `shoot_shotgun`, `shoot_flamer`/`shoot_flamethrower`. Cycling via
`next_air_weapon`.

Ground/surface loadout: `obj_mine`/`shoot_mine`, grenades (`obj_grenade_explosion`). Cycling via
`next_ground_weapon`. Shared ammo economy: `set_ammo_cost`, `subtract_ground_ammo`.

What those names actually do in gmlHARE (checked for UH-T11):

- "Ground" and "air" are the ship's state, not the target: `fire_primary`/`fire_secondary` fire
  the `ground_*` weapon while perched (`state_idle`) and the `air_*` weapon while flying.
- `next_ground_weapon` (Q, key press 81) steps `ground_weapon`, the perched primary: vulcan,
  laser, railgun, shotgun, flamer, back to vulcan, with `sfx_switch` and `can_shoot = true`.
  `next_air_weapon` toggles `air_weapon` between rocket and mine.
- The mine is the default air secondary (`air_weapon_secondary = weapon_mine`, 8 ammo, 1 per
  mine, +1/s). `obj_mine` drifts at 1 px/step on a random heading, arms after 1 s (ring + beep),
  then chases the nearest `par_enemy` within 300 px at 2 px/step (or holds still), detonates as
  `obj_rocket_explosion` under 24 px or after 8-10 s, and is destroyed by any `obj_bullet`.
  `shoot_mine` plays `sfx_fire_grenade` and re-arms after the mine's `delay` of 30 steps.
- There is no grenade weapon. `obj_grenade_explosion` is a non-damaging 50-step blast
  (`spr_grenade_explode`, alpha 0.7, `sfx_explode`) spawned where rocks, meatballs, scrapmentals
  and worms are destroyed.

Projectile/VFX objects: `obj_bullet`, `obj_laser`, `obj_railgun`, `obj_rail_swirl`,
`obj_rocket`/`obj_rocket_explosion`, `obj_muzzleflash`, `obj_oval_explosion`,
`obj_tinyexplosion`.

## Enemies

Flying/insect-style, each with an `ai_*` script and an `_emitter` spawner object: `obj_butterfly`
(`ai_butterfly`), `obj_firefly` (`ai_firefly`), `obj_fly` (`ai_fly`), `obj_dagger`.

Set-piece enemies: `obj_hydra`, `obj_phalanx` + `obj_phalanx_shield`, `obj_meatball` (+
`obj_meatball_gib` on death), `obj_wraith` (+ `obj_wraith_blur`), `obj_sawblade`, `obj_worm`
(`obj_worm_segment`, `obj_worm_head_gib`, `obj_worm_gib`), `obj_trilobyte`, `obj_mimic` (+
`obj_mimic_illusion`), `obj_scrapmental`, `obj_enemy_mine`.

Spawning: per-enemy `obj_*_emitter` objects plus `generate_enemy_list` /
`level_enemygenerator` scripts drive pacing per level.

## Rocks & environment

Destructible rock field: `obj_rock1`..`obj_rock5`, `par_rock`, scripts `create_new_rock`,
`move_rocks`, `rock_destroyed`. Ambient/weather layers: `obj_cloud`/`obj_cloud1-4`/
`obj_cloud_emitter`, `obj_rain_emitter`/`obj_raindrop`, `obj_snow_emitter`, `obj_sand_emitter`,
`obj_fog_emitter`, `obj_flotsum`, `obj_earthball`/`obj_moon_surface`/`obj_surface_reflection`.

## Levels

`level_01`..`level_10` (HARE.gmx) are individual level-definition scripts, layered on
`level_default`, `level_composition_00`/`level_composition_01`, `level_objective_00`/`_01`,
`level_setting_00`/`_01`, and `load_level`/`setup_level`/`load_scenario`. Level selection
happens from `room_map` via `obj_destination` and `obj_btn_level01`..`obj_btn_level09`.
Completion: `set_level_complete`, `obj_level_results`, `obj_levelstart`.

## Pickups & upgrades

Drops: `obj_carrot`, `obj_coin`, `obj_chip`, `obj_cartridge`, `obj_drop_cloud`/`obj_drop_emitter`
/`obj_drop_intro`/`obj_drop_title`, via `random_pickup`/`spawn_chips`. Upgrade/garage screen
(`room_upgrade`): `obj_upgrade`, `obj_upgrade_vulcan1`, `obj_upgrade_shotgun1`,
`obj_upgrade_missle`/`obj_upgrade_missle1`, `obj_rabbit_schematic`, `obj_booster_schematic`,
`obj_brake_schematic`.

## UI / menus

HUD: `obj_healthbar`, `obj_energybar`, `obj_weapon_gui`, `obj_speedometer`, `obj_scanner`,
`obj_clock`. Menus: `obj_title`, `obj_btn_quickstart`, `obj_btn_campaign`, `obj_btn_quit`,
`obj_btn_diff0`/`obj_btn_diff100`, `obj_yesno_dialog`/`obj_btn_yes`/`obj_btn_no`. Misc:
`obj_float_text`/`obj_losthealth`, `obj_tip`, `obj_alert`, `obj_fade_*` (per-room fade
transitions), `obj_logo`, `obj_mouse`.

## Story / dialogue

`room_intro`, `room_story`, `obj_conversation`, `obj_story`, `obj_typewriter`/
`obj_typewriter_story`, `obj_textdone`, `obj_scenario_details`, driven by `load_scenario`.

## Audio

`play_random_music`, `obj_jukebox` for music track rotation. HARE.gmx ships 152 sound assets
under `sound/`; Rabbit ships 102 (the `sfx/` folder alongside `HARE.gmx`'s loose assets includes
`vulcan_spinup/loop/spindown.wav`, `sfx_shotgunblast.wav`, `reload_full/half.wav`,
`empty_clip*.wav`, `whirl1-6.wav`, `sfx_switch_back.wav`).

## Boss

`room_boss1`, `obj_structure1`/`obj_structure1_cover`, `obj_uss_warren` (a named structure,
likely the boss itself or a stage hazard).

## Shared/particle base objects

`par_emitter`, `par_enemy`, `par_enemy_emitter`, `par_pickup`, `par_projectile`, `par_rock`,
`par_structure`, `par_structure_cover` — GM8.1 "parent" objects other objects inherit shared
behaviour from. Worth mirroring as Unity base `MonoBehaviour`/interfaces per category
(enemy, projectile, pickup, rock, structure) rather than object-by-object duplication.

## Rebuild backlog

The full implementation plan is tracked as epics/stories/tickets in the MindAttic ticket
database under project code `UH` (`tickets story list --repo Unity.Hare`,
`tickets ticket list --repo Unity.Hare`), not duplicated here — see `docs/USER_STORIES.md`
(generated) for the current list. Epics: Foundation, Player Movement, Air Weapons, Ground
Weapons, Enemies & AI, Rocks/Environment, Levels & Map, Pickups & Upgrades, UI/HUD/Menus,
Story/Dialogue, Audio, VFX, Boss Encounters.
