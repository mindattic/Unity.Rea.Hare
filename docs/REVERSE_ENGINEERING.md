# HARE / Rabbit — GML source notes

Unity.Hare is a full Unity rebuild of a GameMaker 8.1 (`.gmx`) arcade shoot-'em-up about a
robotic rabbit that fights enemies in low orbit. GML hit a ceiling the original author couldn't
get past, even though the game was functionally complete. These notes capture what the GML
source actually contains, so the rebuild has a durable reference even if the source drives
(`R:\...`) become unavailable. They record facts observed in the source, not implementation
decisions — those live in the ticket backlog (`UH-*` in the MindAttic ticket database).

> The parity and standards audit (UH-T34) lives in [RECONSTRUCTION.md](RECONSTRUCTION.md): it recounts the
> original (150 objects, 96 scripts, 10 rooms, 214 sprites, 75 sounds), lists what the rebuild still lacks and
> gives the build order. Counts below that disagree with it are superseded by it.

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

The ammo economy in gmlHARE (checked for UH-T12):

- obj_player Create holds four pools (current/max/cost/regen): `ground_ammo_primary` 150/150/1/10,
  `ground_ammo_secondary` 12/12/4/2, `air_ammo_primary` 20/20/1/4, `air_ammo_secondary` 8/8/1/1.
  `fire_primary`/`fire_secondary` fire only if the pool holds at least the cost, then subtract it
  and clamp at 0 (`subtract_ground_ammo` for the ground primary, inline copies for the others);
  otherwise `sfx_empty_clip`.
- `set_ammo_cost` runs on landing (`par_rock`/`par_structure` collisions), `player_leap` and
  `fling_player`, not on a weapon switch. Every cost is 1 except the shotgun as ground secondary
  (4). It writes maxima only for the vulcan as ground primary (120), the shotgun as ground
  secondary (12) and rockets as air primary (20), and never touches the current count, so the
  vulcan keeps Create's 150 until a regen tick snaps it to 120. The rocket branch sets
  `air_ammo_secondary_cost`, a slip that is harmless because every air cost is 1.
- Alarm 9 (`alarm_ammo_regen`) refills both primaries with `sfx_reload_half`/`sfx_reload_full`;
  Alarm 10 refills both secondaries silently; each re-arms itself every `room_speed`. Neither is
  set in Create: the global left release starts both, the left press stops only Alarm 9.

Projectile/VFX objects: `obj_bullet`, `obj_laser`, `obj_railgun`, `obj_rail_swirl`,
`obj_rocket`/`obj_rocket_explosion`, `obj_muzzleflash`, `obj_oval_explosion`,
`obj_tinyexplosion`.

## Enemies

Flying/insect-style, each with an `ai_*` script and an `_emitter` spawner object: `obj_butterfly`
(`ai_butterfly`), `obj_firefly` (`ai_firefly`), `obj_fly` (`ai_fly`), `obj_dagger`.

What they actually do in gmlHARE (checked for UH-T13):

- Butterfly, firefly and fly have no parent and no collision events, so they are ambient swarm
  life, not `par_enemy`: nothing shoots them and they never touch the ship. Each emitter creates
  its whole swarm in Create (100 butterflies, 28 fireflies, 32 flies); nothing in HARE.gmx's rooms
  or level scripts places those emitters. Each insect starts at a random point in the room's lower
  half with dX = dY = 1, and Alarm 0 (first `irandom_range(5, 30)`) runs its `ai_*` script and
  re-arms; Step adds dX/dY every step.
- `ai_butterfly` (re-arm 5-60 steps): dX, dY in [-2, 2]; upper half forces dY = 2, within 120 px
  of the floor dY = -2, within 10 px of a side wall dX = +/-2; image_xscale follows dX.
  `ai_firefly` (5-30): dX in [-3, 3], dY in [-1, 1]; upper half dY = 1; walls dX = +/-3; a 1-in-5
  blink toggles `spr_firefly`/`spr_firefly_dark`. `ai_fly` (5-30): dX in [-4, 4], dY in [-2, 2];
  upper half dY = 1; walls dX = +/-4. Fireflies and flies have no floor rule. `obj_fly`'s
  `image_index = choose(spr_fly, spr_fly2)` sets a frame index, so `spr_fly2` never shows.
- `obj_dagger` is the only `par_enemy` of the four and has no `ai_*` script. Spawned at rest with
  random heading and blade angle, it rotates the blade 0.5 degrees/step (`check_rotate_clockwise`)
  until within 2 degrees (`a_near_b`), launches (`ef_ring`) and flies for 120-240 steps (Alarm 1),
  gaining 1 px/step every 8 steps up to 8 (Alarm 0) with smoke every 20 steps (Alarm 2), then
  brakes 1 px/step every 8 steps, picks a new heading and rotates again. More than 96 px outside
  the room it reverses. Bullets, rockets, rocket explosions and the shield wall kill it for 10
  points and a `random_pickup`; the laser, railgun and flame destroy it without either. It pushes
  2 px/step away from overlapping `par_enemy`, and hurts the ship through `obj_player`'s
  `par_enemy` collision.

Set-piece enemies: `obj_hydra`, `obj_phalanx` + `obj_phalanx_shield`, `obj_meatball` (+
`obj_meatball_gib` on death), `obj_wraith` (+ `obj_wraith_blur`), `obj_sawblade`, `obj_worm`
(`obj_worm_segment`, `obj_worm_head_gib`, `obj_worm_gib`), `obj_trilobyte`, `obj_mimic` (+
`obj_mimic_illusion`), `obj_scrapmental`, `obj_enemy_mine`.

What the set pieces actually do in gmlHARE (checked for UH-T14):

- Every enemy's `ID` is -1 and so is the `owner` of every bullet an enemy fires, so each
  "`other.owner != ID`" bullet event means "not an enemy's round": enemy fire passes through
  enemies. Points go only to `other.owner == player.id` bullets; rocket kills always score.
  `obj_rocket` itself only collides with bullets and shields, so a rocket goes off on an enemy only
  if that enemy's own `obj_rocket` event destroys it. The laser, railgun and flame destroy any
  `par_enemy` from their side (no points, pickup or gibs), parts included.
- `obj_hydra` (10 pts): body spins `rot_dir` (+/-1 a step, flipping every 100-1000 then 100-6000
  steps); 2-5 turrets retarget every 200-400 steps and turn 2 degrees/step the long way (the
  original negates `check_rotate_clockwise`), each firing one 2 px/step bullet from 28 px out on
  arrival. Chases at 1 px/step beyond 100 px. Each player bullet removes a turret (`arms -= 1`);
  the hit after the last kills. Rockets and rocket explosions kill outright.
- `obj_phalanx` (20 pts): spins +/-2, creeps at 0.5 px/step beyond 100 px; any player bullet or
  rocket on the body kills. Its `obj_phalanx_shield` (spr_shield_hitbox, the half disc ahead of its
  angle) follows it, turns 0.2 degrees/step toward the ship, and bounces bullets and rockets along
  the normal (the projectiles' own events, once per shot); it destroys itself once the phalanx is gone.
- `obj_meatball` (50 pts): sprays 4-6 bullets (2 px/step, random directions) on step 1 and every
  80-200 steps; chases at 1 px/step beyond 100 px. Grey `RGB` starts at 255 and heals 1 per 120
  steps; above 128 a player bullet costs 5 and shoves it `hspeed/2`, a rocket 16 (the rocket still
  explodes). The next hit bursts it: 15-30 bullets, `obj_grenade_explosion`, `meatball_gib`
  (8-12 gibs). No rocket-explosion event.
- `obj_wraith` (50 pts): alpha breathes 0.3-0.6; floats toward the ship at 2 px/step until within
  300 px, then charges at a point 600 px past the ship from 2 px/step (+0.015, max 5) and, within
  32 px of it, reappears 350-400 px from the ship. A player bullet stuns it for 4-7 s; only a
  stunned wraith dies to rockets, which otherwise fly through. `obj_wraith_blur` afterimages
  every 10 steps.
- `obj_sawblade` (10 pts): beyond 400 px drifts in at 0.5 px/step; within, swarms at 2 px/step to
  a point re-picked every 30-60 steps (ship + 2x its velocity + up to 48 px jitter; GML (0, 0)
  before the first pick). Dies to bullets, rockets, rocket explosions and the shield wall.
- `obj_worm` (head 100 pts, segment 10): 1 + 5-7 `obj_worm_segment`s, each following the part ahead
  at the head's speed once that part's centre is 4 px outside its bounding box. Wanders at
  1 px/step (+/-90 jolts every 200-800 steps, 2-4 degree bends every 20); every 2-5 s spits 12-24
  `spr_sliver` bullets from 128 px out along its jaw. Only a part without a live child takes damage
  (RGB -16 a bullet, -42 a rocket, dies past 128), though any bullet is spent and any rocket
  explodes. A dying segment adds `(10 - segment_count) * 0.1` to the head's speed.
- `obj_trilobyte` (10 pts): crawls at 2 px/step, never chasing; +/-20 nudges every 240-620 steps,
  1-6 degree bends every 10, body turning 2 degrees/step; lays an `obj_enemy_mine` every
  240-620 steps. `obj_enemy_mine` (a `par_projectile`) arms after 3 s, homes at 1 px/step within
  200 px, and becomes an `obj_rocket_explosion` within 24 px, after its 20-30 s fuse, or when any
  bullet or rocket hits it (1 pt for the ship's).
- `obj_mimic` (10 pts): drifts along direction 0 at -1.5..1.5 px/step under an
  `obj_mimic_illusion` rock; within 275 px of the ship the disguise fades 0.02 a step. While it is
  complete the illusion stops every bullet and explodes every rocket harmlessly and the mimic lets
  them through; once it slips, shots kill the mimic, and a bullet kill also spawns an
  `obj_rocket_explosion`.

Spawning: per-enemy `obj_*_emitter` objects plus `generate_enemy_list` /
`level_enemygenerator` scripts drive pacing per level.

What the spawners actually do in gmlHARE (checked for UH-T15):

- There are no per-enemy emitter objects; one `par_enemy_emitter` serves every type.
  `setup_level` rolls `rnd_X = irandom_range(X_min, X_max)` per type and places that many emitters
  at random points (`instance = obj_X`, `instance_min/max = X_min/X_max`, waits 200-500 steps,
  scale 0.25, meatball 0.70, wraith/phalanx 0.35, worm 0.50). Mimics get no emitter: they are
  created outright.
- `par_enemy_emitter` is a spinning `spr_vortex` (550 px, alpha 0.5, angles -5/-6 a step) that Alarm 0
  walks idle → open → spawn → close → idle. Create arms it at 1-240 steps, but idle's first pass
  (which runs the same step) jumps to a random point at least 550 x scale px inside the room and
  re-arms the wait. Open (120 steps) grows 0.01 a step, spawn creates one enemy and waits 120, close
  shrinks for 120. Growth and spawning only happen unpaused and while
  `instance_number(instance) < instance_max`, so the cap is room-wide and kills get topped back up.
- Generator mode (`gm_generator`, `obj_btn_diff0`/`diff100`/`level09`): `generate_enemy_list(difficulty)`
  draws `irandom_range(1, 9)` uniformly (hydra, sawblade, dagger 1 point; mimic, trilobyte 2;
  phalanx 5; wraith 8; meatball 10; worm 15) until the points reach the difficulty.
  `obj_wormhole_emitter` opens 10% of the list at once, then one `obj_wormhole` every
  `1 + room_speed * irandom_range(0, 8)` steps (the first after `0..4` s); once the list is spent it
  destroys itself and calls `set_level_complete`. Each `obj_wormhole` (scale per `create_wormhole`:
  0.25, dagger/phalanx/wraith 0.35, worm 0.5, mimic/meatball 1.0) idles 240 steps, opens for 240,
  releases its enemy unconditionally, waits 240 and shrinks away.
- Source slips: `setup_level` calls `instance_create(obj_wormhole_emitter, 0, 0)` and
  `create_wormhole` calls `instance_create(obj_wormhole, 0, 0)` with arguments swapped, `create_wormhole`
  sizes its margin with the caller's undefined `scale`, and the globals `enemy_count`/`current_enemy`
  are never reset. The port does what they meant.

What the enemies' deaths show in gmlHARE (checked for UH-T16):

- No enemy has a Destroy event; every effect is written into the kill branch of its own collision
  event, so kills that come from the other side (laser, railgun, flame: `with(inst) instance_destroy()`)
  show nothing. Each kill branch calls `effect_create_above(ef_smokeup, x, y, size, c_black)` and
  `effect_create_above(ef_firework, x, y, size, c_orange)` at the enemy: size 0 for the dagger, hydra,
  sawblade, trilobyte, wraith and enemy mine, 1 for the phalanx and mimic, 2 for the meatball, worm head
  and worm segments. Rocket-explosion kills (dagger, hydra, sawblade) and shield-wall kills show no
  smoke or sparks; the shield wall's dagger and sawblade kills spawn an `obj_tinyexplosion` at the enemy.
- `obj_tinyexplosion` (spr_explosion, 7 frames at image_speed 0.2, drawn at alpha 0.5, depth -4, gone
  at Animation End after 35 steps) is created at the bullet (`other.x, other.y`) on every bullet kill and
  on the non-lethal bullet hits of the hydra, meatball, wraith (the stun), worm and segments (any bullet,
  plus a second on the kill), the mimic's bullet kill (with an `obj_rocket_explosion` there too), the
  disguised mimic illusion's stopped bullet, and at the rocket on a worm's rocket kill. Its
  `sfx_bullet_explosion` line is commented out; the port leaves it silent (on every bullet impact it
  reads as a mix choice, not a defect).
- `obj_grenade_explosion`: spr_grenade_explode has 9 frames, not 10, so it lasts 45 steps; it plays
  `sfx_explode` only when `in_view(x, y)`.
- The enemy mine's rocket and bullet hits add the size-0 pair; its fuse and proximity detonations are
  only the `obj_rocket_explosion`. Gibs and the wraith blur stand still while `paused`.
- `ef_firework` and `ef_smokeup` are GameMaker's built-in effects; their parameters come from the
  runner (YoYo Games' open GameMaker-HTML5 `Effects.js`, `Eff_Effect03`/`Eff_Effect05`), which scales
  speeds, gravity, size changes and lives by `Speed_Factor` = 30 / room speed (0.25 at 120 steps/s).
  Firework: 75 / 150 / 250 `pt_shape_flare` particles, size 0.1-0.2, alpha 1 to 0.4, gravity down.
  Smokeup: 6 / 11 / 16 `pt_shape_explosion` puffs within 5 / 15 / 30 px, rising, shrinking, alpha 0.4
  to 0, living 100 / 120 / 200 steps. The two 64 px shape textures are the runner's (Apache 2.0).
- `spawn_death_particles(x, y)` (sfx_death; 3-6 large, 12-18 medium, 32-48 small `obj_death_particle`
  within 32 px; each 1-8 px/step on a whole heading, spin -9..9, alpha 0.9 losing 0.002 a step for 450
  steps) belongs to the Scrap Mental boss and the rabbit's death, not to the regular enemies.
  `obj_oval_explosion` is never created by anything.

## Rocks & environment

Destructible rock field: `obj_rock1`..`obj_rock5`, `par_rock`, scripts `create_new_rock`,
`move_rocks`, `rock_destroyed`.

What the rock field actually does in gmlHARE (checked for UH-T17):

- Every rock is a `par_rock` instance (depth 3); `obj_rock1-5` are empty children nothing creates
  except `room_boss1`, which no script enters. Create picks `spr_rock1-5` or `spr_asteroid_experiment`
  (precise masks, 186-273 px wide), image_angle and direction 0-359, speed 0 for `stationary_percent`
  of rocks and `irandom_range(-15, 15) * 0.1` px/step otherwise (`Speed` keeps it through pauses),
  spin `irandom_range(-30, 30) * 0.01` degrees/step (0 within 0.03), `spin_max` 4, image_blend grey
  RGB 255. Alarm 0 every `room_speed / 4` (30 steps) heals RGB by 1; Alarm 1 every 30 steps slows a
  spin wound past `original_spin` by 0.96.
- Step (unpaused): speed = Speed, image_angle += spin; more than its sprite width outside the room it
  is destroyed (fling_player if the ship stands on it) and `create_new_rock` replaces it. The depth 1/2
  swap for a rock the ship perches on belongs to landing (UH-T53).
- Hits: any `obj_bullet` above RGB 128 darkens 4, `motion_add(other.direction, 0.1)` (speed capped at
  3), `add_spin(id, 0.06)` and is spent in an `obj_tinyexplosion` at the bullet plus the rock's
  hspeed/vspeed. An `obj_rocket` darkens 20, pushes 0.5, spins 0.25 and becomes an
  `obj_rocket_explosion` there. At RGB 128 or below the next hit breaks it: 10 points with float text
  (a bullet's only when `other.owner == player.id`, a rocket's always), `ef_smokeup`/`ef_firework`
  size 2, an `obj_grenade_explosion`, `rock_destroyed`. The bullet's own par_rock event still spends it
  in a tiny explosion; `obj_rocket` has no par_rock event, so a rocket that breaks a rock flies on.
  Nothing else touches rocks (laser, railgun, flame, rocket explosions, shield wall, enemies); the
  laser's `draw_beam` is drawn only up to the first rock or structure. 32 bullets or 7 rockets bring a
  fresh rock to its breaking hit.
- `rock_destroyed(id, x, y)`: 8-12 `obj_flotsum` within 40 px, fling_player if it was the ship's
  `current_rock`, `create_new_rock`. `obj_flotsum` (depth 0, `spr_flotsum1-5`, no collisions) drifts
  1-3 px/step on a random heading, turns -5..5 degrees/step and fades 0.002-0.01 a step until gone.
- `setup_level` destroys every rock, places `irandom_range(rock_min, rock_max)` at
  `irandom_range(player_size, room - player_size)` (player_size = spr_rabbit_idle's 96 px) with spin
  rolled again, then `move_rocks` destroys those in the top 800 px within 400 px of the centre line
  (the ship's spawn point) without replacing them. Per level: rocks 15-30 (default, 1, 2, 6, 7, 9,
  generator), 15-20 (3), 20 (4), 0 (5), 20-24 (8); stationary_percent 50 (default, 1, generator), 25
  (2), 15 (3), 75 (6), 30 (7), 60 (8).
- `create_new_rock`: a rock at x = -size or room_width + size, random height, speed `±irandom_range(0,
  15) * 0.1` by side, spin rolled again.
- Fixed defects (the port plays as meant): `create_new_rock` and `setup_level` zero the caller's
  `spin`, not the rock's, when the roll is near zero (the rock's now); Alarm 1's negative branch
  (`spin < -original_spin`, `spin *= -0.96`) flipped a negative spin's sign every quarter second (now
  it slows like a positive one); `add_spin`'s negative branch snapped any negative spin to
  `-spin_max` (now it mirrors the positive one); `create_new_rock` set a random heading after the
  speed sign it chose for its side, so half the new rocks left at once and the still ones parked
  outside the room for good, thinning the field (a new rock now heads into the room at 0.1-1.5
  px/step); `obj_flotsum` stored `Speed = speed` every step but never restored it, so a pause froze
  every shard for good (it drifts on now).

Ambient/weather layers: `obj_cloud`/`obj_cloud1-4`/
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
