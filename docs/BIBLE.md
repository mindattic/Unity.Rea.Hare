---
codex: 1
project: Unity.Hare
code: UH
layer: bible
status: living
updated: 2026-10-23
---

# Unity.Hare: Project Bible

> The single source of truth for what Unity.Hare is, the rules that keep the rebuild faithful, how the code is
> organised, and every place it does not play exactly like the original, with the reason. `README.md` says how to
> build, run and test it; [`RECONSTRUCTION.md`](RECONSTRUCTION.md) is the inventory of the original, the parity
> matrix, the standards audit and the build order; [`REVERSE_ENGINEERING.md`](REVERSE_ENGINEERING.md) holds the
> per-object notes read from the GML source.
>
> Anything here that is planned but not yet on main is marked **(planned, UH-Tn)**. Nothing is claimed as built
> that is not.

## 1. The one sentence {#UH-§1}

A robotic rabbit flies, perches and fights in low orbit over a drifting asteroid field, shooting down swarms of
enemies that pour out of vortices and wormholes, rebuilt from the GameMaker 8.1 arcade shoot-'em-up *HARE* (in-game
title *H.O.P., High Orbit Patrol*) as a modern Unity 6 game.

## 2. The product promise {#UH-§2}

- **Two ways to fight.** In flight the ship turns with A/D, boosts and brakes on a shared energy bar and drops
  mines; perched on a rock it raises a shield wall and fires its ground weapons (vulcan, shotgun, flamer and the
  rest), leaping off to fly again.
- **One shared ammo economy.** Four pools (ground and air, primary and secondary), each with the original's
  maximum, cost and regeneration, refilling only once the trigger is let go.
- **The original's bestiary.** Ambient insects, the dagger, eight set-piece enemies (hydra, phalanx, meatball,
  wraith, sawblade, worm, trilobyte, mimic) and the enemy mine, each with its own collision rules, scores and death
  effects, spawned by `par_enemy_emitter` vortices or, in generator mode, by wormholes drawing on a difficulty list.
- **A living rock field.** Rocks drift, spin, darken under fire, heal, break into flotsam and are replaced, and are
  the ground the rabbit stands on.
- **The whole game.** Title, story, map, ten levels across arena, shaft and side-scroll rooms, the Scrap Mental
  boss, the garage and the HUD all come across (most planned, see [RECONSTRUCTION, build order](RECONSTRUCTION.md#build-order)).

## 3. What it is not {#UH-§3}

- **Not a redesign.** No new modes, levels, enemies or progression beyond the original (the one addition is a
  campaign save, UH-T65). The scope is the original played as it was meant to play (see
  [§4 rule 1](#UH-LAW-1)), and the repo is finished when the last reconstruction story is.
- **Not a line-by-line port.** The code is rebuilt from scratch to current Unity practice; only the behaviour is
  kept.
- **Not tied to 120 steps a second.** The original's per-step numbers become per-second rates and fractional
  steps on Unity's clock (see [V1](#UH-§8)).

## 4. Design rules {#UH-§4}

The owner's rebuild standard is in [`AGENTS.md`](../AGENTS.md); these are its rules as they apply here.

### {#UH-LAW-1} Gameplay matches the original; broken things are fixed

Every value, unit and behaviour comes from gmlHARE's source (`R:\...\gmlHARE\HARE.gmx`), never from a guess. Where
the original is plainly broken (a random that is not random, a draw call commented out, a feature that can never be
reached, sound that ships switched off) the rebuild plays the way the game was clearly meant to, and the fix is a row
in [§7 Fixed defects](#UH-§7). Everything that works matches exactly. Anything else that differs is a row in
[§8 Deliberate deviations](#UH-§8) with its reason. A difference with no row is a bug.

### {#UH-LAW-2} Reverse-engineer before rebuilding

An object, script or room is read in full and recorded in `REVERSE_ENGINEERING.md` (the per-object notes) or
`RECONSTRUCTION.md` (the inventory) before its rebuild starts.

### {#UH-LAW-3} Core is pure C#

The rules live in an engine-free Core assembly (`noEngineReferences`, no `UnityEngine`) tested with `dotnet test`.
State changes only through commands, which produce events; presentation reads state and plays events and never
mutates the game.

### {#UH-LAW-4} Hard assembly boundaries

Core, Runtime, Editor and Tests are separate asmdefs with one-way references (see [§5](#UH-§5)).

### {#UH-LAW-5} Content and tuning are data

Levels, enemies, weapons, text and every tuning number live in data files or a settings registry, validated on load
(see [§6](#UH-§6)).

### {#UH-LAW-6} Built in code

The scene, UI and objects are composed in code (`ProjectSetup` today), never wired by hand in the Inspector.
Regenerate `Main.unity` rather than editing it; art import settings come from `HareArtImporter`, not the Inspector.

### {#UH-LAW-7} Modern Unity only

Unity 6, URP behind a pipeline-agnostic layer, the Input System through an action asset, UI Toolkit or the shared
MindAttic components, Unity.Shared profiles and settings, and multiplayer-ready seams: commands carry a player id,
one authority holds the state, no global current-player singleton, built on the Unity.Multiplayer kit.

### {#UH-LAW-8} Done means seen working

Core tests green, a build that launches, and the feature seen in play (a screenshot or a scripted run) before a
ticket closes.

## 5. Architecture {#UH-§5}

### 5.1 Target

```
 Unity 6000.4.3f1 · URP 2D · scenes generated in code (ProjectSetup, later the room-flow builder, UH-T50)
 ┌────────────────────────────────────────────────────────────────────────────────────┐
 │ Unity.Hare.Runtime  (MonoBehaviours, presentation, input, audio, HUD)               │
 │   reads Core state, plays Core events, turns input into Core commands               │
 └──────────────▲───────────────────────────────────────┬─────────────────────────────┘
                │ events / state                         │ commands (with player id)
 ┌──────────────┴───────────────────────────────────────▼─────────────────────────────┐
 │ Unity.Hare.Core  (noEngineReferences)                                               │
 │   ship flight, weapons and ammo, enemies and spawn pacing, rocks, levels, session   │
 │   state, settings registry, data loading and validation                             │
 └────────────────────────────────────────────────────────────────────────────────────┘
 Unity.Hare.Editor          → Runtime, Core  (ProjectSetup, HareArtImporter)
 Tests/Core.Tests           → Core           (NUnit, dotnet test, outside Unity)
 Unity.Hare.Tests.EditMode  → Runtime, Core  (Unity Test Framework: the components)
 Unity.Hare.Tests.PlayMode  → Runtime, Core  (scripted play, UH-T70)
```

References only point down: Runtime → Core, Editor → Runtime/Core, tests → what they test. Core references nothing
Unity.

### 5.2 Where it is today

UH-T37 made the split (466c012). UH-T39 moved the player and weapon rules into Core; the enemy rules and spawn pacing
still live in Runtime MonoBehaviours and move in UH-T40.

| Assembly | Folder | Contents | Status |
|---|---|---|---|
| `Unity.Hare.Core` | `Assets/Unity.Hare/Core/` | `noEngineReferences`: `EnemyKind`, `EnemyList` (`generate_enemy_list`, with the random injected); `GameClock` (the 120 steps/s and 100 px/unit conversions), `PlayerId`; `Player/` (`PlayerWorld` and `PlayerShip`, the `PlayerCommand`s and `PlayerEvent`s, `ShipFlight` the flight/energy/shield machine, `ShipHealth` HP, lives and the timers, `FlightTuning`/`HealthTuning`); `Weapons/` (`AmmoPools` the four pools and regen alarms, `GunRack` the vulcan/shotgun/flamer/mine launcher firing rules, `GroundLoadout`/`AirLoadout` the weapon cycles, `WeaponTable` the fire intervals, cycles and damage as data) | in place; enemy rules and spawn pacing move in UH-T40 |
| `Unity.Hare.Runtime` | `Assets/Unity.Hare/Scripts/` | `Player/` (PlayerShipController, PlayerHealth, ShieldWall, PlayerDamageSource), `Weapons/` (VulcanWeapon, Shotgun, Flamethrower, MineLauncher, AirWeaponLoadout, GroundWeaponLoadout, PlayerAmmo, Bullet, Rocket, LaserBeam, RailgunShot, Mine, the explosions, and `Gml`, the unit and step helpers in `WeaponTargets.cs`), `Enemies/` (the insects, Dagger, SetPieceEnemy and its eight set pieces, EnemyMine, EnemyEmitter, Vortex, Wormhole, WormholeEmitter, EnemySpawner, EnemyComposition, FadingDebris), `Rocks/` (Rock, RockField, Flotsum), `Effects/` (DeathEffects, GmlEffects, TinyExplosion, DeathParticle, SpriteAnimation, HareArt) | references Core one way; the player and weapon scripts are thin views (`PlayerSeat` and `SeatView` send commands and play events) that still read input directly (UH-T42); the enemies still hold their rules (UH-T40) |
| `Unity.Hare.Editor` | `Assets/Unity.Hare/Editor/` | `ProjectSetup` (URP 2D assets, project settings, `Main.unity`), `HareArtImporter` (sprite pivots from `sprites.txt`, particle textures) | in place |
| `Unity.Hare.Tests.EditMode` | `Assets/Unity.Hare/Tests/EditMode/` | NUnit tests of the components: the glue of the player and weapon views (`PlayerSeatTests`, projectiles, the shield wall), enemies, rocks, effects | in place; pure rule tests live in `Tests/Core.Tests` once their rules are in Core |
| `Unity.Hare.Tests.PlayMode` | `Assets/Unity.Hare/Tests/PlayMode/` | empty | scripted play, UH-T70 |
| `Tests/Core.Tests` | `Tests/Core.Tests/` | dotnet test project compiling `Core/**/*.cs` directly (C# 9, as Unity builds it): `EnemyListTests`, and the player and weapon rules (`PlayerAmmoTests`, `PlayerHealthTests`, `PlayerFlightTests`, `GunTests`, `LoadoutTests`) | in place; run by `tools/run-core-tests.ps1` |

The Input System action asset is planned in UH-T42, Unity.Shared settings in UH-T43, the pipeline-agnostic URP seam
in UH-T44, the UI foundation in UH-T45 and the multiplayer kit in UH-T35.

### 5.3 Commands, events and the one authority (player and weapons done in UH-T39; enemies UH-T40, authority UH-T35)

**Today** the player's rules are Core's: a `PlayerWorld` holds one `PlayerShip` per `PlayerId` (flight and energy,
health and lives, the four ammo pools, the guns, the two weapon loadouts), and state changes only through
`PlayerCommand`s (`Boost`, `Leap`, `TakeHit`, `FireGun`, `NextAirWeapon`, `TickAmmo` and the rest, each carrying the
player id), which return a `CommandResult` of whether it took effect and the `PlayerEvent`s it raised (`ShipLanded`,
`PlayerDamaged`, `GunFired`, `AmmoReloaded`, `AirWeaponFired`, ...). On the Unity side `PlayerSeat` sits on a ship and
holds its world and id (local play seats one player in a world of its own; there is no global current player), and the
views (`PlayerShipController`, `PlayerHealth`, `PlayerAmmo`, `VulcanWeapon`, `Shotgun`, `Flamethrower`,
`MineLauncher`, the loadouts, all `SeatView`s) turn input into commands and play the events: they spawn a bullet when
`GunFired` arrives, a shield wall on `ShieldRaised`, a sound on `VulcanCue`. Each view still advances its own part of
the world from `Update`/`FixedUpdate` (`TickAmmo`, `TickGun`, `StepFlight`, ...) until the one-step authority below.
The enemies, rocks and projectiles still hold their own state and announce through C# events (`GrenadeExplosion.Exploded`).
The GML global `paused` is the static `Gml.Paused`.

Weapon rhythm is data in `WeaponTable`: the vulcan's `VulcanBaseRateSteps`/`VulcanMinRateSteps`/
`VulcanSpinUpDecrementSteps` ramp, the other guns' and air weapons' fire intervals in steps, the ground and air
cycles (`GroundCycle`, `AirCycle`) and per-round damage, so changing a rate or the cycle order is a one-line change
there and Core tests cover it.

**The target**, as in Unity.SpaceGod and Unity.Chain:

- **One world.** The whole game state (session, ships, ammo, enemies, rocks, projectiles) is one Core world, held by
  one authority, and nothing else changes it.
- **Commands in.** Input becomes plain immutable commands carrying the player's id (turn, boost, brake, shield,
  aim, fire primary, fire secondary, switch weapon, leap, pause), so a networked player's NetEntity can carry them
  to the authority unchanged.
- **One step.** The authority alone steps the world on Unity's fixed step, in the original's per-step order
  (alarms, Step, collisions), so GML's per-step rules keep their order at any frame rate.
- **Events out.** Everything that happens is an event (fired, hit, killed, rock broken, ammo changed, weapon
  switched, spawned, paused); presentation draws, plays sound and spawns effects from them and never writes back.
- **No current player.** GML's `player` global becomes a seat; nothing looks up "the" player.

### 5.4 Units and time

The original runs `room_gamescreen` at 120 steps a second with 1 px = 1 unit. The rebuild keeps both, converted in
one place (`Gml` in `Scripts/Weapons/WeaponTargets.cs`):

- `Gml.StepsPerSecond = 120`; `Gml.Steps(n)` turns a step count into seconds. A per-step rate becomes a per-second
  rate (x 120); an alarm becomes a countdown in fractional steps advanced by `dt x 120`, so timers fire on the same
  step count the original used whatever the frame rate.
- `Gml.PixelsPerUnit = 100`; `Gml.Px(n)` turns GML pixels into world units. The sprites import at the same 100 PPU.
- GML directions are degrees with 0 = +x and counter-clockwise positive (`Gml.Dir`); GML y grows down, so GML
  y = 0 is the room rectangle's top (`Gml.Room`, 3840 x 2160 px centred on the origin).

## 6. Data files {#UH-§6}

**The original has one:** `datafiles/credits.txt` (sound credits, UH-T69). Everything else is code: the levels are
GML scripts (`level_01`..`level_10` on `level_default`, the composition, objective and setting scripts), tuning is
object Create variables, and session state is `globals.gml`.

**The rebuild today** keeps tuning as `[SerializeField]` defaults and `const` fields on the components, each
carrying the original's figure with its GML name in the comment, and two data files of its own:
`Resources/Hare/Sprites/sprites.txt` (each sprite's GML origin, read by `HareArtImporter`) and the original's
sprites, particle shapes and sounds under `Resources/Hare/` and `Audio/`.

**The target (planned, UH-T41, UH-T20):** one validated data file per concern, loaded by Core and rejected on load
with a message if any value is out of range:

| Data | Holds | Original source |
|---|---|---|
| ship | flight, brake and boost speeds, turn rates, energy, shield, leap tolerance, health and hurt timing | `obj_player` Create, `player_*` |
| weapons and ammo | each weapon's rate, speed, spread, reach, damage; the four ammo pools' max, cost, regen | `shoot_*`, `fire_*`, `set_ammo_cost`, `obj_player` Create |
| enemies | per kind: speeds, timers, points, hit rules, RGB damage, death effect size, spawn scale | `obj_*` Create and collision events, `create_wormhole` |
| rocks | sprites, speeds, spin, `spin_max`, heal and spin-wound rates, hit darkening and pushes, flotsam | `par_rock`, `create_new_rock`, `rock_destroyed`, `obj_flotsum` |
| levels | room, rock counts and stationary share, enemy min/max per kind, objective, time limit, setting | `level_01`..`level_10`, `level_default`, `level_composition_*`, `level_objective_*`, `level_setting_*` |
| generator | the point cost per kind, difficulty buttons | `generate_enemy_list`, `obj_btn_diff*` |
| text | story, conversation, tips, credits | `load_scenario`, `obj_conversation`, `obj_tip`, `credits.txt` |

Player options (resolution, window, volumes) live in a Unity.Shared profile (planned, UH-T43); campaign progress in
a save (planned, UH-T65).

## 7. Fixed defects {#UH-§7}

Places where the original's code is plainly broken. The rebuild plays the way the game was clearly meant to.
Status: **fixed** (on main), **planned** (the ticket that fixes it).

| # | Defect in the original | Where | What the rebuild does | Status |
|---|---|---|---|---|
| D1 | A near-zero spin roll zeroes the caller's `spin`, not the new rock's, so the "no barely-spinning rocks" rule never applies | `create_new_rock`, `setup_level` | zeroes the rock's own spin within 0.03 (`Rock`) | fixed (UH-T17) |
| D2 | A negative spin wound flips sign every quarter second: Alarm 1's negative branch tests `spin < -original_spin` and does `spin *= -0.96` | `par_rock` Alarm 1 | a negative spin slows by 0.96 like a positive one | fixed (UH-T17) |
| D3 | A bullet or rocket push on a negatively spinning rock snaps its spin to `-spin_max` at once | `add_spin` | the negative branch mirrors the positive one, adding up to `spin_max` | fixed (UH-T17) |
| D4 | Replacement rocks leave the room and thin the field: `create_new_rock` sets the speed sign for its side, then a random heading over it, so half the new rocks fly straight out and the still ones park outside the room for good | `create_new_rock` | a new rock heads into the room at 0.1-1.5 px/step (`RockField`) | fixed (UH-T17) |
| D5 | Flotsam freezes after a pause: `obj_flotsum` stores `Speed = speed` every step but never restores it, so after the first pause every shard stops for good | `obj_flotsum` Step | flotsam stops while paused and drifts on after (`Flotsum`) | fixed (UH-T17) |
| D6 | `setup_level` calls `instance_create(obj_wormhole_emitter, 0, 0)` and `create_wormhole` `instance_create(obj_wormhole, 0, 0)` with the arguments swapped | `setup_level`, `create_wormhole` | creates the emitter and each wormhole properly | fixed (UH-T15) |
| D7 | `create_wormhole` sizes its placement margin with the caller's undefined `scale` | `create_wormhole` | uses the wormhole's own scale (`Wormhole.Create`) | fixed (UH-T15) |
| D8 | The generator's globals `enemy_count` and `current_enemy` are never reset, so a later generator run would carry on from where the last one stopped | `generate_enemy_list`, `obj_wormhole_emitter` | the list and its cursor belong to each emitter (`WormholeEmitter`, `EnemyList`) | fixed (UH-T15) |

Not restored, on purpose:

- `obj_fly`'s `image_index = choose(spr_fly, spr_fly2)` sets a frame index on a one-frame sprite, so `spr_fly2`
  never shows. The fly is ambient and the second sprite is a variant, not a missing feature; the rebuild shows
  `spr_fly` as the original does (`Fly`). Owner's call if it should alternate.
- `obj_tinyexplosion`'s commented-out sound: see [V2](#UH-§8).
- Content nothing reaches (`room_boss1`, `obj_ring`, the unplaced insect and weather emitters,
  `obj_oval_explosion`): recorded in [RECONSTRUCTION](RECONSTRUCTION.md#content-the-original-never-reaches);
  restoring them would mean inventing where they go.

## 8. Deliberate deviations {#UH-§8}

Working behaviour of the original the rebuild does differently, and why. Status: **kept** (the deviation stays),
**to settle** (a ticket brings it back in line or decides), **temporary** (an intermediate step until the ticket),
**matches** (a difference an earlier port step had, now back in line; kept as a record).

| # | Original | Rebuild | Why | Status |
|---|---|---|---|---|
| V1 | Every rate is per step at 120 steps/s (60 in the menu rooms), and the game slows down when a step overruns | per-second rates and fractional-step alarms on Unity's clock (`Gml.StepsPerSecond`, `Gml.Steps`, [§5.4](#UH-§5)) | frame-rate independence; at full speed every figure lands on the same step count as the original | kept |
| V2 | `obj_tinyexplosion`'s `audio_play_sound(sfx_bullet_explosion)` is commented out | kept silent (`TinyExplosion`) | the rule fixes sound that ships switched off, but this one was muted on purpose: it would play on every bullet impact, a mix choice, not a defect | kept |
| V3 | 1 px = 1 GML unit | 100 px = 1 world unit (`Gml.PixelsPerUnit`), sprites imported at 100 PPU | Unity's default sprite scale; physics and cameras work in world units | kept |
| V4 | `obj_player`'s flight, brake and boost speeds and energy rates | the original's ratios (brake half, boost double) at placeholder absolutes (5 units/s flight), and placeholder energy drain and fill | ported before the art set the world scale and before `obj_energybar` | temporary, UH-T39, UH-T41 |
| V5 | The vulcan fires at a fixed `fire_rate = 9` steps | fires at the fixed 9 steps (`WeaponTable.VulcanBaseRateSteps` = `VulcanMinRateSteps` = 9) | the ramp the earlier Rabbit build used and gmlHARE kept commented out beside the fixed rate | matches, UH-T73 |
| V6 | The laser and railgun are perched (ground) primaries on Q's cycle, fired with the left mouse; in flight the left mouse fires the air weapon (`next_air_weapon`, E, toggles rocket and mine) and Ctrl + left mouse the mine | `AirWeaponLoadout` fires laser, railgun and rocket on the right mouse, cycled with E; the mine stays on Ctrl + left mouse in flight; Q's cycle skips laser and railgun | the weapons were ported (UH-T9) before the ground/air split was read from the source (UH-T11) | to settle, UH-T39, UH-T42 |
| V7 | Raw key and mouse events | Input System device polling (`Keyboard.current`, `Mouse.current`) in five scripts | interim, before the action asset | temporary, UH-T42 |
| V8 | Tuning as Create variables | `[SerializeField]` defaults and constants | interim, before data files | temporary, UH-T41 |
| V9 | Rules in GML objects | rules in Runtime MonoBehaviours, only `EnemyKind`/`EnemyList` in Core | interim, before the Core move | temporary, UH-T39, UH-T40 |
| V10 | `engine random` (`irandom_range`, `choose`) | `UnityEngine.Random` in the components; `EnemyList` takes an injected random | testable, seedable randomness belongs in Core | temporary, UH-T40 |
| V11 | Gibs and the wraith blur stand still while `paused` | the same (`FadingDebris`, `DeathParticle`); the first port kept them moving | matches gmlHARE's Step gating (UH-T16) | matches |
| V12 | `spr_grenade_explode` has 9 frames at image speed 0.2, so the blast lasts 45 steps | the same (`GrenadeExplosion.Frames = 9`); the first port assumed 10 frames and 50 steps | read from the sprite (UH-T16) | matches |
| V13 | One room shown through views, no scenes | `Main.unity` (camera and light only) generated by `ProjectSetup`; one scene per room later | scenes composed in code; the room flow comes with UH-T50 | temporary, UH-T50 |

### Keeping these tables current

Every ticket that changes how the rebuild plays relative to the original updates §7 or §8 in the same commit: add a
row for a new difference, change a row's status when it is settled, and delete nothing (a settled row says
**matches**). Every fixed defect recorded in `REVERSE_ENGINEERING.md` also gets a row here. A difference found with
no row is a bug: file a ticket.

## 9. Documents {#UH-§9}

| File | What it is |
|---|---|
| [`README.md`](../README.md) | what the game is, status, layout, how to build, run and test, controls |
| [`RECONSTRUCTION.md`](RECONSTRUCTION.md) | the original's inventory and game flow, the parity matrix, the standards audit, the build order |
| [`REVERSE_ENGINEERING.md`](REVERSE_ENGINEERING.md) | per-object and per-script notes read from the GML source |
| [`USER_STORIES.md`](USER_STORIES.md) | generated from the ticket database; never edit by hand |
| [`legacy-poc/`](legacy-poc/README.md) | the old Unity 5 proof of concept's scripts, as non-compiling reference |
| [`AGENTS.md`](../AGENTS.md) | the rebuild standard and where the original lives |

## 10. Glossary {#UH-§10}

- **Step**: one GML game tick; `room_gamescreen` runs 120 a second.
- **Alarm**: a GML per-instance countdown in steps that fires an event at zero.
- **Perched / Idle**: `state_idle`, the ship standing on a rock or structure; ground weapons and the shield work
  only here.
- **Flying / Moving**: `state_moving`; boost, brake and the air weapons work only here.
- **Ground / air weapon**: the ship's state when it fires, not the target.
- **RGB**: a rock's or meatball's grey `image_blend`, its health: hits darken it, it heals over time, it breaks at
  128 or below.
- **Emitter / vortex**: `par_enemy_emitter`, the spinning portal that keeps a kind topped up to its maximum.
- **Wormhole**: generator mode's one-shot portal that releases one enemy from the difficulty list.
- **The original**: GameMaker 8.1 `HARE.gmx` at `R:\...\gmlHARE`; `gmlRabbit` is an earlier build kept as
  reference only.
