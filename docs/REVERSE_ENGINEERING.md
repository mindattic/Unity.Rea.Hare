# HARE / Rabbit — GML source notes

Unity.Rea.Hare is a full Unity rebuild of a GameMaker 8.1 (`.gmx`) arcade shoot-'em-up about a
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

A second copy, `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\GameMaker\gmlHARE\HARE.gmx` (compared file by file by
U.HARE-T33), is the same project re-saved by a later GameMaker: Studio (every sprite, sound and font file gains
format fields such as `<type>` and `<audioGroup>`, and an empty, unused 32x32 `sprite213` is added). Its only
code differences: `obj_game` calls `play_random_music()`, but that script's whole body is commented out (so music
is still never heard; the rebuild plays it, BIBLE D33); `obj_game`'s commented-out ring creation is deleted and
`obj_ring`'s code is uncommented (still created by nothing); and `obj_drop_intro`'s key press no longer sets
`phase = "exit"`, which would leave `room_intro` with no way into play but Esc. The rebuild follows the `_PRE_2017`
copy, whose intro exit works (a broken thing fixed, AGENTS.md rule 1).

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
`par_enemy`, `par_rock`, `par_structure`, held-Keyboard, Key Press and Key Release, Mouse (global
button events), Draw. The held keys that turn the ship are A (65) and D (68) only; the arrow keys'
presses (37-40) each run `create_helper` (read for U.HARE-T66, below).

Player-related scripts: `player_boost`, `player_brake`, `player_leap`, `player_shield`,
`player_hurt`, `reset_player`, `fling_player`.

### The helper hawk (`obj_helper`, `create_helper`; read for U.HARE-T66)

- `create_helper` (obj_player's arrow presses): `if(!instance_exists(obj_helper)) helper = instance_create(x, y, obj_helper)`.
- obj_helper: spr_helper (32x32, origin 16,16, colkind 2 ellipse, bbox 0..31), depth 0, not persistent, no parent.
  Create: `audio_play_sound(sfx_hawk,1,0)`, `turn_speed = 2`, `speed = Speed = last_speed = max_speed = 4`,
  `firstPass = true`, `smoke_delay = 40`, `spark_delay = 5`, `poll_rate = 20`, `alarm[0] = 40`, `alarm[1] = 5`,
  `direction = player.image_angle; image_angle = direction`, `state = "free"`, `turning = false`, `solo = false`.
- Step (`if(!paused)`, else `speed = 0`): `speed = Speed`; "free": if `solo`, `mouse_direction =
  point_direction(x,y,mouse_x,mouse_y)` and, unless `a_near_b(direction, mouse_direction, 4)`,
  `check_rotate_clockwise(mouse_direction, direction)` ? `direction -= 2` : `direction += 2`, `image_angle = direction`;
  `firstPass`: `effect_create_below(ef_ring,x,y,1,c_white)`; `!in_view(x,y)`: `last_speed = speed; state = "return"`,
  else `effect_create_below(ef_smoke,x,y,0,c_gold)`. "return": `distance_to_object(player) > 400`: ef_smoke c_orange,
  `direction = image_angle = point_direction(x,y,player.x,player.y)`, `speed = Speed = max_speed` (or `player.speed`
  if greater; a commented-out 2-degree turn sits beside it); otherwise `state = "free"; firstPass = true; speed =
  Speed = last_speed` (its sfx_hawk replay is commented out).
- Alarm 0 (re-armed every 40 steps): `effect_create_below(ef_smokeup, x - hspeed, y - vspeed, 0, c_white)` if
  unpaused. Alarm 1 (every 5): if unpaused and `turning`, `rndcolor = irandom_range(1,3)`, `rnd_x`/`rnd_y =
  irandom_range(-2,2)`, ef_spark below at `x - hspeed + rnd_x, y - vspeed + rnd_y` in c_amber/c_pink/c_cornflowerblue.
  Alarm 2 (`speed += 1; turn_speed = ceil(speed / 2); alarm[2] = poll_rate`) and Alarm 3 (`speed -= 1`) are never
  started: only the Up/Down key releases touch them, setting them to 0.
- Keyboard (held): left (37) `turning = true; direction += turn_speed; image_angle += turn_speed`, right (39) the
  same with `-=`, both `!paused` and "free". Key press: Up (38) `if(speed < max_speed) { speed += 1; Speed = speed }`,
  Down (40) `if(speed > 1) { speed -= 1; Speed = speed }`, both `!paused` and "free"; Page Down (34) `solo = !solo`;
  Delete (46) `instance_create(other.x, other.y, obj_rocket_explosion)`, `effect_create_above(ef_smokeup,x,y,0,c_black)`,
  `effect_create_above(ef_firework,x,y,0,c_orange)`, `instance_destroy()`. Key release: left/right `turning = false`;
  Up `alarm[2] = 0`, Down `alarm[3] = 0`.
- Collision par_projectile and par_enemy: the whole body (the same explosion as Delete) is commented out.
- obj_carrot and obj_coin each have a Collision obj_helper event identical to their obj_player one. Nothing else
  names obj_helper; `setup_level` does not clear it. `globals.gml` declares `globalvar helper`.

### The flashbomb (`obj_flashbomb`, `obj_rabbit_schematic`; read for U.HARE-T67)

`obj_player`'s key press F (70): `flashbomb = instance_create(0, 0, obj_flashbomb)`. Nothing else in HARE.gmx reads or
limits it: no ammo, no cooldown, no `paused`, `level_complete` or death check, so every press is another bomb (and
another sfx_flashbomb and obj_rabbit_schematic).

`obj_flashbomb` (spr_white, depth -1000, not persistent):
- Create: `on = true; count = 0`; `image_xscale = view_wview[0]; image_yscale = view_hview[0]; x = view_xview[0];
  y = view_yview[0]`; `alpha = 0.8; image_alpha = alpha`; `burst_alpha = 1`; `radius = radius2 = radius3 = radius4 = 0`;
  `flash_count = 12`; `rate = 6; alarm[0] = rate`; `instance_create(0, 0, obj_rabbit_schematic)`;
  `audio_play_sound(sfx_flashbomb, 1, 0)`.
- Alarm 0: `if(on) { on = false; image_alpha = alpha } else { on = true; image_alpha = 0; count += 1 }`; a commented-out
  block swept every enemy and shot in view once `count >= flash_count/2`, and a commented-out
  `if(count >= flash_count) instance_destroy()`; `if(count < flash_count) alarm[0] = rate`.
- Step: back to view 0's corner; `radius += 10; radius2 += 8; radius3 += 6; radius4 = 4`; `with(par_enemy)` in view and
  `collision_circle(player.x, player.y, obj_flashbomb.radius, par_enemy, true, false)` makes an obj_rocket_explosion and
  destroys it; `with(par_projectile)` the same with obj_tinyexplosion; `if(burst_alpha > 0) burst_alpha -= 0.01; else
  instance_destroy()`. The circle test names the parent, not `id`, so any enemy in the ring sets off all of them in
  view (BIBLE D26). The bomb lives about 102 steps: 8 of its 12 dark phases.
- Draw: `draw_self()`; `draw_set_alpha(burst_alpha)`; four `draw_circle_color(player.x, player.y, radius*, c_white,
  c_white, true)`; `draw_set_alpha(1)`.

`obj_rabbit_schematic` (spr_rabbit_schematic, depth 0): the same Create, Step and Draw GUI as `obj_booster_schematic`
(centred at the bottom of the view, alpha 0.7, -0.005 a step to destroy). Only obj_flashbomb makes it.

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
happens from `room_map` via `obj_destination` (the campaign's missions) and from the title via the
`obj_btn_level01`..`obj_btn_level07` buttons `obj_logo` places (`obj_btn_level09` is never placed; BIBLE V47).
Completion: `set_level_complete`, `obj_level_results`, `obj_levelstart`.

## Pickups & upgrades

Drops: `obj_carrot`, `obj_coin`, `obj_chip`, `obj_cartridge`, `obj_drop_cloud`/`obj_drop_emitter`
/`obj_drop_intro`/`obj_drop_title`, via `random_pickup`/`spawn_chips`. Upgrade/garage screen
(`room_upgrade`): `obj_upgrade`, `obj_upgrade_vulcan1`, `obj_upgrade_shotgun1`,
`obj_upgrade_missle`/`obj_upgrade_missle1`. (`obj_rabbit_schematic` is not the garage's either: only `obj_flashbomb`
makes it, U.HARE-T67. `obj_booster_schematic` and
`obj_brake_schematic` are not the garage's: room_upgrade places neither; `player_boost`/`player_brake` make them, the
HUD's fading boost and brake flashes, U.HARE-T92.)

## UI / menus

HUD: `obj_healthbar`, `obj_energybar`, `obj_weapon_gui`, `obj_speedometer`, `obj_scanner`,
`obj_clock`. Menus: `obj_title`, `obj_btn_quickstart`, `obj_btn_campaign`, `obj_btn_quit`,
`obj_btn_diff0`/`obj_btn_diff100`, `obj_yesno_dialog`/`obj_btn_yes`/`obj_btn_no`. Misc:
`obj_float_text`/`obj_losthealth`, `obj_tip`, `obj_alert`, `obj_fade_*` (per-room fade
transitions), `obj_logo`, `obj_mouse`.

### Title and intro presentation (read for U.HARE-T62)

- `room_title` / `room_intro` (1920x1080, 60 steps, view 0 = the room): backgrounds `bg_nightsky` (slot 0, tiled,
  `vspeed -5`) and `bg_gradient3` (slot 1, tiled, still). Title instances in order: `obj_drop_emitter` (-928,128),
  `obj_drop_title` (960,-608, scale 0.5), `obj_stand_title` (928,2592, scale 0.5), `obj_title`. Intro: `obj_drop_intro`
  (960,-608, scale 0.5), `obj_drop_emitter`, `obj_intro`. Only `obj_white_dot` goes to `room_intro`
  (`room_goto(room_intro)` on reaching the map's destination; corrected by U.HARE-T33).
- `obj_drop_emitter` (depth 0): Create `rate = 20; alarm[0] = 1`; Alarm 0 `repeat(irandom_range(4,8))
  instance_create(0,0,obj_drop_cloud)`, `alarm[0] = rate`.
- `obj_drop_cloud` (spr_cloud1, depth 0): Create `x = irandom_range(-sprite_width/2, room_width - sprite_width/2)`,
  `vspeed = -irandom_range(60,80)`, `image_alpha = 0.1 * irandom_range(1,3)`, `y = room_height + sprite_height *
  irandom_range(1,2)`, then `sprite_index` = spr_cloud1..4 by `irandom_range(1,4)`; Step `if(y < -sprite_height)
  instance_destroy()`.
- `obj_drop_title` (spr_drop 462x1154, origin 231,0): Create `vspeed = 10; startX = x; phase = "start"; smoke_delay = 20;
  alarm[0] = smoke_delay`; Alarm 0 `effect_create_below(ef_smokeup, x, y+40, 2, c_black)` and re-arm; Alarm 1
  `phase = "exit"`; Step `x = startX + irandom_range(-3,3)`, `effect_create_below(ef_smokeup, x, y+40, 2, c_orange)`,
  "start": `if(y > room_height/4) { vspeed = 0; phase = "hang"; alarm[1] = 200; }`, "exit": `vspeed = 10`, and past
  `room_height` `instance_destroy(); obj_stand_title.phase = "on"; with(obj_drop_emitter) instance_destroy();
  audio_play_sound(sfx_land,1,0)`.
- `obj_drop_intro`: the same without Alarm 1; on stopping it makes `obj_tip` with the controls list (`indent()` lines);
  keyboard D `if(startX < room_width - 300) startX += 1`, A `if(startX > 300) startX -= 1`; key press (any) `if
  (!keyboard_check(ord("A")) && !keyboard_check(ord("D"))) { if(phase == "hang") phase = "exit"; }`; past
  `room_height` in "exit": `gamemode = gm_quickstart; current_scenario = -1; current_level = 1;
  room_goto(room_gamescreen)`.
- `obj_stand_title` (spr_stand 493x1154, origin 246,1154): Create `phase = "off"`; Step while "on": `if(y >
  room_height - 30) vspeed = -40; else { vspeed = 0; instance_create(0,0,obj_logo); phase = "off"; }`; Draw
  `draw_sprite(spr_moon_surface,0,0,y+250)`, `draw_sprite_ext(spr_eyeback,0,x-6,y-368,0.5,0.5,0,c_white,1)`,
  `draw_sprite_ext(spr_pupil,0,x-12+(mouse_x/120),y-370+(mouse_y/60),0.5,0.5,0,c_white,1)`, `draw_self()`.
- `obj_logo` (spr_logo 551x510, origin 275,255): Create `x = view_xview[0] + width/2 + 200; y = view_yview[0] +
  height/2 + 200; image_alpha = 0; obj_mouse.visible = true; background_vspeed[0] = 0;` and the buttons; Step
  `image_alpha += 0.02` to 1; Draw `draw_self()`.
- `obj_title`: key press (any) `if(!keyboard_check(vk_f1))` and global left button `if(!keyboard_check(vk_f1) &&
  !keyboard_check(vk_escape))`: `with(obj_drop_title) instance_destroy(); with(obj_drop_emitter) instance_destroy();
  obj_stand_title.phase = "on";` (every press, and every step the button is held: BIBLE D23).
- `obj_intro` (spr_room_loop): Create sets the view port to the window; its typewriter text is commented out; Esc
  `room_goto(room_title)`.

## Story / dialogue

`room_intro`, `room_story`, `obj_conversation`, `obj_story`, `obj_typewriter`/
`obj_typewriter_story`, `obj_textdone`, `obj_scenario_details`, driven by `load_scenario`.

Read in full for U.HARE-T28 (the rules in BIBLE V41):

- `room_story` (1920x1080, 60 steps, view 0 1920x1080; background 0 `bg_nightsky` tiled, not moving): instances
  `obj_scroll_world`, `obj_story`, `obj_fade_story`, in that order.
- `obj_story` (spr_room_loop): Create sets the view port to the window and makes `obj_typewriter_story` at 30,60 with
  26 `typewriter.text += ...` lines (the `dialogue` section's `story.01`..`story.26`). Esc `room_goto(room_title)`; any
  key `if (!keyboard_check(vk_escape)) { next_room = room_map; obj_fade_story.mode = "fadein";
  obj_fade_story.firstPass = true; gamemode = gm_campaign; current_scenario = -1; current_level = 1; }` (a first press
  that would finish the typing instead is commented out).
- `obj_typewriter_story` (depth -20): Create `type_speed = 1; pause_delay = room_speed; alpha = 1; position = 0;
  output = ""; print_complete = false; text = ""; alarm[0] = type_speed`. Alarm 0 `if(!print_complete) { position += 1;
  next_char = string_char_at(text,position); output = string_copy(text,0,position); raw_output = output;
  output = string_replace_all(output,"^", ""); if next_char is not one of " #^.,": audio_stop_sound(sfx_type2),
  audio_play_sound(sfx_type2,1,0); alarm[0] = pause_delay after a ^, else type_speed; if(raw_output == text)
  print_complete = true; }`. Draw GUI fnt_medium, `draw_text_outline_alpha(x, y, 1, output, alpha)`.
- `obj_scroll_world` (spr_earthball 1066x1062, origin 533,531): Create `x = room_width/2; y = room_height +
  sprite_get_height(spr_earthball); scroll_delay = 3; alarm[0] = scroll_delay; auto_speed = 0.01; finished = false`.
  Alarm 0 `if(y > room_height) { y -= 1; background_y[0] -= 1; } else if(!finished) { finished = true; y =
  room_height; (obj_story's key press) }`, always `alarm[0] = scroll_delay`. Step `image_angle =
  normalize_angle(image_angle - auto_speed)`.
- `obj_conversation` (spr_conversation_box 1920x256, depth -1000): Create, Step, Alarm 0, key press, global left
  pressed and Draw GUI as BIBLE V41 has them; its `conversation[0]` and `current_line` are never read. `next_line`
  (script): `mode = "slideout"; firstPass = true; with(obj_textdone) instance_destroy(); audio_play_sound(sfx_flick)`,
  then sfx_woof if `frame_name == "Admiral Zee"`, else sfx_flick. Made only by obj_map's Create
  (`current_conversation`); obj_destination sets `mode = "slidein"` on reaching a new mission and obj_map's A and D set
  `"slideout"`.
- `obj_textdone` (spr_textdone 11x13, 4 frames, depth -1001): Create `image_xscale = image_yscale = 8; x =
  view_wview - 90; y = view_hview - 90; image_speed = 0.1`, Draw GUI `draw_self()`. No object or script creates it; the rebuild
  completes the intended feature and shows it once a typed line is finished (U.HARE-T87, BIBLE V41).
- `obj_typewriter` (depth -20): Create `type_speed = 2`; Alarm 0 `if(output != text) { position += 1; output =
  string_copy(text,0,position); } if(fadeout) { alpha -= 0.01; if(alpha <= 0) instance_destroy(); } alarm[0] =
  type_speed`; Draw GUI fnt_point, outline 1. Only commented-out code makes one (obj_intro at 20, room_height - 450;
  obj_drop_intro would fade it).
- `load_scenario`: `switch(current_scenario)` 1 runs `level_01`..`level_05` for `current_level` 1..5; 2, 3 and 4 run
  `level_06`..`level_10`; `default` runs `level_default`. Called by setup_level in `gm_campaign`.

## Map (room_map, U.HARE-T19)

- room_map 1920x1080 at 60, background 0 `bg_nightsky` tiled (hspeed set by obj_map/obj_destination). Instances in
  order: obj_earthball (901,1253; Create moves it to 960,1080), obj_uss_warren (1143,513; Create y = 372),
  obj_scanner (1045,532), obj_destination (903,722; Create y = 549), obj_map (-100,63), obj_scenario_details (92,179).
- globals.gml: `current_mission = 0`, `mission_count = 4`, `rotate_direction = "counterclockwise"`,
  `current_scenario = -1`, `current_conversation` (obj_map's comm box, read by obj_destination).
- obj_map Create: obj_fade_map, obj_mouse (spr_pointer), `whitedot_exists = false`, obj_btn_upgrade (100,40),
  `mode = "idle"`, `mission[0..3]` obj_alert at 16/150/270/310 degrees with scenario_id 1-4, obj_conversation, obj_tip
  ("Mission Command#" + two indented lines). Keys: D (68) and A (65) press, only `mode == "idle" && !whitedot_exists`:
  sfx_flick, "moving", clockwise/counterclockwise, `background_hspeed[0] = 1 / -1`, current_mission -1/+1 with wrap,
  comm box "slideout" (the old per-press turn and ef_smoke are commented out). Esc (27): `room_goto(room_title)`. Any
  key (1): if not A, D or Esc held, `current_scenario != -1` and no dot: obj_white_dot at the ship toward
  obj_destination (`image_angle = direction`), `obj_earthball.auto_speed = 0`, sfx_takeoff.
- obj_earthball Step: idle `image_angle -= auto_speed` (0.01), normalize_angle. obj_destination Step: moving hides
  obj_scenario_details, `current_scenario = -1`, turns the earth `-turn_speed` counterclockwise else `+turn_speed` (2),
  normalize. Collision with obj_alert: if `other.scenario_id != last_scenario`: last_scenario, "idle",
  `background_hspeed[0] = 0`, panel visible with `"Scenario " + string(scenario_id)`, current_scenario, comm box
  "slidein". Its Draw is commented out. spr_destination 64x130, origin 32,130, precise, bbox 25..35 x 0..129 (opaque
  throughout); spr_alertmarker 32x64, origin 16,32, six frames, precise per frame, bbox set by hand to 0..31 x 23..40.
- obj_white_dot (spr_dropdot 9x9, origin 4,4, five frames, rectangle box 0..8 x 3..5; depth 1): Create
  `image_speed = 0.1; fade_speed = 0.003`; Step `if(distance_to_point(destX, destY) > 1) { move_towards_point(destX,
  destY, 1); image_alpha -= fade_speed; } else room_goto(room_intro)`.
- obj_scenario_details: spr_tip_box, alpha 0.7, margin 20, `visible = false`; Draw GUI the box at 0.7 then the text
  (fnt_point, outline 1) at x+20, y+20. obj_btn_upgrade: spr_button_area_small (depth -5), "Upgrades" at y-10 (fnt_point,
  outline 2, c_white / c_cornflowerblue), mouse enter sfx_hover, message "Upgrade H.A.R.E. Unit's systems" at
  (room_width/2, room_height-30) while over, left press sfx_click and obj_fade_map to room_upgrade.
- obj_scanner: spr_scanner_beam at alpha 0.2, `y = 1080 - 531 - 211 + 50`; Step sways image_angle by sweepY (+/-0.1 at
  0.004 a step) and flickers `image_alpha = 0.01 * irandom_range(16, 24)` (U.HARE-T63). spr_scanner_beam is 210x211
  with its origin at (210, 0), the top-right corner, so the beam swings about the corner under the ship: 0 to 2.6
  degrees and back every 102 steps (GML's `<`/`>` are false within the 0.00001 epsilon). Same depth (0) as
  obj_earthball, created after it, so drawn over the earth's rim. obj_map's own draw_self (spr_room_loop 96x96, origin
  48,48, at -100,63) is off the room.
- room_intro's obj_drop_intro ends in `gamemode = gm_quickstart; current_scenario = -1; current_level = 1`, so the
  picked scenario never reaches load_scenario (owner-directed fix, BIBLE V42); D24 records the stale
  current_scenario on re-entering the map.

## Audio

`play_random_music`, `obj_jukebox` for music track rotation. HARE.gmx ships 152 sound assets
under `sound/`; Rabbit ships 102 (the `sfx/` folder alongside `HARE.gmx`'s loose assets includes
`vulcan_spinup/loop/spindown.wav`, `sfx_shotgunblast.wav`, `reload_full/half.wav`,
`empty_clip*.wav`, `whirl1-6.wav`, `sfx_switch_back.wav`).

The jukebox (U.HARE-T29): `setup_level` runs `jukebox = instance_create(0,0,obj_jukebox)` (its own
`play_random_music()` and `audio_stop_music()` lines are commented out, as are obj_game's, obj_title's and
obj_startup's). obj_jukebox (no sprite, not persistent) has a Create of two commented lines (`check_rate = room_speed * 5`,
`alarm[0] = 1`) and a Step that calls `play_random_music()`. That script: `if(!audio_is_playing(0))`, then
`randomtrack = irandom_range(1,5)` picks `current_track`/`track_name`: 1 music_pinballspring "Pinball Spring",
2 music_inaheartbeat "In a Heartbeat", 3 music_latinindustries "Latin Industries", 4 music_presenterator
"Presenterator", 5 music_wallpaper "Wallpaper" (an else falls back to Pinball Spring); its
`audio_play_sound(current_track, 1, 0)` is commented out. Sound index 0 is `sfx_whoosh`, the first sound in
HARE.project.gmx, which nothing plays, so the check is always true and the track is rerolled every step and never
heard (BIBLE D33). music_overcast is in no branch. globals.gml starts `current_track = music_pinballspring;
track_name = "Pinball Spring"`; only obj_game's F12 debug overlay reads track_name.

## Boss

`room_boss1`, `obj_structure1`/`obj_structure1_cover`, `obj_uss_warren` (a named structure,
likely the boss itself or a stage hazard).

## Shared/particle base objects

`par_emitter`, `par_enemy`, `par_enemy_emitter`, `par_pickup`, `par_projectile`, `par_rock`,
`par_structure`, `par_structure_cover` — GM8.1 "parent" objects other objects inherit shared
behaviour from. Worth mirroring as Unity base `MonoBehaviour`/interfaces per category
(enemy, projectile, pickup, rock, structure) rather than object-by-object duplication.

## Sprites and backgrounds (U.HARE-T46)

Read in full by `tools/import-gml-art.ps1`; its output, `Assets/Unity.Rea.Hare/Art/gml-art.json`, is the per-sprite
record (origin, bounding box, mask, frames, image_speed and where it came from).

- `HARE.project.gmx` lists **213 sprites** (398 frames) and **18 backgrounds**, in a resource tree of groups
  (`game/enemy`, `game/gui/weapons`, `map`, `title`, `weather`, ...). Every listed resource has its `.gmx` and every
  frame image exists at the sprite's declared size, so nothing is skipped. (The earlier count of 214 sprites was
  the `sprites/` folder including `images/`.)
- **Sprite fields used**: `xorig`/`yorigin` (origin, px from the top left), `width`/`height`, `colkind`
  (collision mask: 0 precise = 22 sprites, 1 rectangle = 159, 2 ellipse = 32, no diamonds), `sepmasks`,
  `bboxmode` (0 automatic, 1 full image, 2 manual) with `bbox_left/right/top/bottom` (inclusive pixels, already
  resolved for automatic mode), `HTile`/`VTile` (never set on a sprite), `frames/frame`. `coltolerance` is 0
  everywhere; `TextureGroups` and `For3D` mean nothing outside GameMaker.
- **Backgrounds**: none is a tileset; all 18 repeat both ways (`HTile`/`VTile` = -1). Most are 1920x1080; the
  scrolling skies are taller (`bg_uphigh` 1920x1440, `bg_moonview`/`bg_worldcurve` 1920x1280, `bg_atmosphere`
  1920x1275, `bg_metal`/`bg_stormclouds`/`bg_tropicalocean` 1920x1200); `bg_black` is 32x32.
- **image_speed** is not in the sprite file: it is a per-instance variable, so it is read from the objects. 17
  objects set it as a plain number in Create: `obj_alert` 0.2, `obj_bullet` 0.1, `obj_butterfly` 0.1,
  `obj_enemy_mine`/`obj_mine` 0.07, `obj_flame` 0.3, `obj_grenade_explosion` 0.2, `obj_muzzleflash` 0.3,
  `obj_oval_explosion` 0.3, `obj_player` 0.2 (its sprite is set in code, `asset_*`, so it maps to no sprite),
  `obj_rocket` 0.5, `obj_rocket_explosion` 0.5, `obj_shieldwall` 0.3, `obj_textdone` 0.1, `obj_tinyexplosion` 0.2,
  `obj_trilobyte` 0.1, `obj_white_dot` 0.1. Any other sprite animates at GameMaker's default 1 frame per step.
- **Room speed** decides how fast those frames go: `room_gamescreen`, `room_shaft`, `room_sidescroll` and
  `room_boss1` run at 120 steps a second, `startup`, `room_intro`, `room_title`, `room_map`, `room_story` and
  `room_upgrade` at 60. So `spr_alertmarker` and `spr_dropdot` (map) and `spr_textdone` (story) animate at 60 x
  their image_speed; everything else at 120.

## Sounds and the credits data file (U.HARE-T47)

Read in full by `tools/import-gml-audio.ps1`; its output, `Assets/Unity.Rea.Hare/Audio/gml-audio.json`, is the per-sound
record (every sound.gmx field, the source file's real format, channels, rate and length, and the clip it becomes).

- `HARE.project.gmx` lists **75 sounds**: 69 `sfx_*` and 6 `music_*`, in a resource tree of groups: `music` (the six
  tracks), `player` (`sfx_hurt1`-`sfx_hurt8`), `vulcan` (`sfx_vulcan_spinup`, `sfx_vulcan_loop`,
  `sfx_vulcan_spindown`) and 58 at the top. Every listed sound has its `.sound.gmx` and its file under
  `sound/audio`. The folder also holds `AlbumArtSmall.jpg` and `Folder.jpg`, which no sound uses (left out).
- **sound.gmx fields**: every `sfx_*` is `kind` 0 (normal sound), `compressed` 0, `streamed` 0; every `music_*` is
  `kind` 3 (multimedia player), `compressed` 1, `streamed` 1, from an MP3. All 75 have `volume` 1, `pan` 0,
  `effects` 0, `preload` -1 (checked), `uncompressOnLoad` 0, export `sampleRate` 44100, `bitRate` 192 and
  `bitDepth` 16. The target `type` is 1 (stereo) except `sfx_hurt8` and `sfx_vulcan_spindown`, 0 (mono).
- **The source WAVs vary** where GameMaker's export did not: 44 are mono 44.1 kHz 16-bit, 13 stereo 44.1 kHz,
  4 stereo 48 kHz, 3 mono 22.05 kHz 8-bit, 2 mono 96 kHz 24-bit, 2 mono 11.025 kHz (8- and 16-bit). GameMaker
  resampled all of them to its 44.1 kHz export rate; the import does the same. The longest sfx are
  `sfx_vortex_suck` (5.0 s), `sfx_loop_laser` (3.6 s), `sfx_vulcan_loop` (2.4 s), `sfx_boost` (2.2 s).
- **`sfx_whoosh.wav` is not a WAV**: it starts with an ID3 tag and its first frame is MPEG-1 Layer III at
  320 kbps, 44.1 kHz. No object, script or room plays `sfx_whoosh`. It is imported as `sfx_whoosh.mp3` (BIBLE D9).
- **Volume**: `globals.gml` sets `master_volume = 1`; `obj_game`'s Keyboard events for numpad minus (109) and plus
  (107) move it 0.01 a step, clamped to 0-1, and call `sound_global_volume(master_volume)`. It is never saved.
  Music is chosen by `play_random_music` into `current_track` (U.HARE-T29).
- **`datafiles/credits.txt`** (the project's only data file, `exportAction` 2): two lines, "Jim Rogers - Mingun
  sound effect" and `http://soundbible.com/1920-Minigun.html`, the source of the vulcan's minigun sound. It is
  mirrored byte for byte to `Assets/Unity.Rea.Hare/Audio/credits.txt` for the credits screen (U.HARE-T69; the
  screen does not read it yet, U.HARE-T112).

## Fonts (U.HARE-T48)

- `HARE.project.gmx` lists **seven fonts**: `fnt_basic`, `fnt_medium`, `fnt_point`, `fnt_score`, `fnt_small`,
  `font7`, `font8`. `fonts/` also holds `font4.font.gmx` and its atlas, which the project does not list, so
  GameMaker never built it into the game.
- **font.gmx**: `name` (the typeface), `size` (points), `bold`, `italic`, `charset` 1, `aa` 3 (anti-aliased), one
  range `32,127`, then the bake: a `glyphs` table (per character its cell `x`, `y`, `w`, `h` in the atlas, `shift`
  the pen advance, `offset` the left bearing), an empty `kerningPairs`, and the atlas `image` (`<name>.png`, white
  glyphs with coverage in alpha, cells packed edge to edge). Every cell's top is the top of the line; only its height
  varies, longer for descenders.
- **Two typefaces**: `fnt_medium` Berlin Sans FB 16, `fnt_point` Berlin Sans FB 12, `fnt_score` Berlin Sans FB 36,
  `fnt_small` Berlin Sans FB 11; `fnt_basic`, `font4`, `font7`, `font8` Arial 12, the last three byte-identical bakes
  of `fnt_basic` (same atlas bytes, same glyph table). No font is bold or italic.
- **Who draws with what** (`draw_set_font`, comments excluded): `fnt_point` 40 objects (the HUD's objective, clock,
  coins and speed unit, `obj_energybar`, every menu and map/level button, `obj_YesNo`/`obj_yesno_dialog` messages,
  `obj_level_results` rows, `obj_levelstart`, `obj_scenario_details`, `obj_startup`, `obj_tip`, `obj_label`,
  `obj_typewriter`, `obj_float_text`, `obj_conversation`'s speaker name, the upgrade buttons); `fnt_score` 14 (the
  score in `obj_game`, lives in `obj_healthbar`, the speed in `obj_speedometer`, the title buttons
  `obj_btn_campaign`/`_quickstart`/`_quit`/`_yes`/`_no`, the dialog and results headings, `obj_levelstart`,
  `obj_upgrade`, `obj_conversation`'s speech); `fnt_medium` only `obj_typewriter_story`; `fnt_small` only
  `obj_weapon_gui` (the ammo counters). Nothing draws with `fnt_basic`, `font4`, `font7` or `font8`; text drawn
  before any `draw_set_font` gets GameMaker's own default, Arial 12, which `fnt_basic` bakes.
- **Layout**: `draw_text` puts each cell's top on the y it is given and steps the pen by `shift`, drawing the cell
  `offset` right of it; `string_width` is the sum of the `shift`s; a new line is `string_height("M")` lower, the
  height of the `M` cell. Nearly every call goes through `draw_text_outline` (U.HARE-T45).
- **Licences**: Arial is Monotype's (shipped with Windows), Berlin Sans FB The Font Bureau's (shipped with Microsoft
  Office); both are proprietary and may not be redistributed as font files. The original only ever shipped the baked
  atlases above, and that is all the rebuild imports (BIBLE §6).

## Rebuild backlog

The full implementation plan is tracked as epics/stories/tickets in the MindAttic ticket
database under project code `UH` (`tickets story list --repo Unity.Hare`,
`tickets ticket list --repo Unity.Hare`), not duplicated here — see `docs/USER_STORIES.md`
(generated) for the current list. Epics: Foundation, Player Movement, Air Weapons, Ground
Weapons, Enemies & AI, Rocks/Environment, Levels & Map, Pickups & Upgrades, UI/HUD/Menus,
Story/Dialogue, Audio, VFX, Boss Encounters.
