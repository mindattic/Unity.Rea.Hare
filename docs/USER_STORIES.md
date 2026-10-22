---
codex: 1
project: Unity.Hare
code: UH
layer: stories
status: living
updated: 2026-10-21
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Unity.Hare — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._


## Epic A — Project Foundation & Unity Scaffold
- **UH-US-A1 ⬜** As a developer, I have a Unity project scaffolded under Unity.Hare with the folder layout (Scenes/Scripts/Prefabs/Art/Audio) the other MindAttic Unity games use, so the rebuild starts from a consistent baseline
- **UH-US-A2 ⬜** As a developer, I have the reverse-engineering notes from gmlHARE/gmlRabbit (objects, scripts, rooms) captured in docs/ so the GML source of truth survives even if R:\ becomes unavailable
- **UH-US-A3 ⬜** As a developer, I can import the existing Unity HARE proof-of-concept (Assets/Resources, Scripts/Player, Scripts/Weapon, Scripts/Rock) as a starting reference without being blocked by it


## Epic B — Player Ship: Movement & Control
- **UH-US-B1 ⬜** As a player, I can fly obj_player's ship with thrust/rotate controls (boost, brake, leap) matching the original's alarm-driven state machine
- **UH-US-B2 ⬜** As a player, I can take damage and die/respawn (player_hurt, reset_player) with the same health/lives rules as the GML original
- **UH-US-B3 ⬜** As a player, I can raise a shield (player_shield/obj_shield/obj_shieldwall) that blocks incoming damage for a limited time


## Epic C — Weapons: Air-to-Air Arsenal
- **UH-US-C1 ⬜** As a player, I can fire the primary air weapon (vulcan) with spin-up/spin-down and a looping fire sound
- **UH-US-C2 ⬜** As a player, I can fire a secondary air weapon (laser, railgun or rocket) and cycle between unlocked air weapons with next_air_weapon
- **UH-US-C3 ⬜** As a player, I can fire the shotgun and flamethrower weapons with their own spread/cone behaviour


## Epic D — Weapons: Ground/Surface Arsenal
- **UH-US-D1 ⬜** As a player, I can fire a separate ground weapon loadout (mine, grenade) at surface targets, cycling with next_ground_weapon
- **UH-US-D2 ⬜** As a player, ammo cost and resupply (set_ammo_cost, subtract_ground_ammo) work the same as the original for both air and ground loadouts


## Epic E — Enemies & AI
- **UH-US-E1 ⬜** As a player, I fight flying insect-style enemies (butterfly, firefly, fly, dagger) driven by their original ai_* flight patterns
- **UH-US-E2 ⬜** As a player, I fight the larger set-piece enemies (hydra, phalanx with phalanx_shield, meatball, wraith, sawblade, worm with segments/gibs, trilobyte, mimic with mimic_illusion)
- **UH-US-E3 ⬜** As a player, enemies spawn through the original emitter objects (obj_*_emitter) and generate_enemy_list so spawn pacing matches the source game
- **UH-US-E4 ⬜** As a player, enemies die with the right gib/explosion/death-particle effects per enemy type (meatball_gib, worm_gib, rocket_explosion, death_particle)


## Epic F — Rocks, Hazards & Environment
- **UH-US-F1 ⬜** As a player, I navigate around destructible rocks (rock1-5, create_new_rock, move_rocks, rock_destroyed) that behave like the original's par_rock family
- **UH-US-F2 ⬜** As a player, I see weather/ambient environment layers (clouds, rain, snow, sand, fog emitters) matching each room's backdrop


## Epic G — Levels, Map & Procedural Generation
- **UH-US-G1 ⬜** As a player, I can select a level from the overworld map (room_map, obj_map, obj_destination, obj_btn_level01-09) the way the original campaign does
- **UH-US-G2 ⬜** As a player, each level (level_01..level_10, level_default, level_composition, level_objective, level_setting) loads its own enemy composition, objective and time limit
- **UH-US-G3 ⬜** As a player, I play the side-scroll and shaft stage variants (room_sidescroll, room_shaft) in addition to the open gamescreen arena
- **UH-US-G4 ⬜** As a player, finishing a level shows level_results and level-complete state (set_level_complete) before returning to the map


## Epic H — Pickups, Economy & Upgrades
- **UH-US-H1 ⬜** As a player, I collect carrots, coins, chips and cartridges dropped by enemies/rocks (random_pickup, spawn_chips, obj_drop*) as the game's currency/ammo pickups
- **UH-US-H2 ⬜** As a player, I spend currency in the upgrade/garage screen (room_upgrade, obj_upgrade, obj_upgrade_vulcan1, obj_upgrade_shotgun1, obj_upgrade_missle1, obj_rabbit_schematic, obj_booster_schematic, obj_brake_schematic) to improve my ship


## Epic I — UI, HUD & Menus
- **UH-US-I1 ⬜** As a player, I see a HUD with health bar, energy bar, weapon GUI and speedometer during gameplay (obj_healthbar, obj_energybar, obj_weapon_gui, obj_speedometer)
- **UH-US-I2 ⬜** As a player, I navigate title and quit/confirm menus (obj_title, obj_btn_quickstart, obj_btn_campaign, obj_btn_quit, obj_yesno_dialog) with the same flow as the original
- **UH-US-I3 ⬜** As a player, I see floating damage/pickup text and tips (obj_float_text, obj_tip, obj_alert) during play


## Epic J — Story, Dialogue & Cutscenes
- **UH-US-J1 ⬜** As a player, I read the intro and story scenes (room_intro, room_story, obj_conversation, obj_typewriter_story, load_scenario) with a typewriter-style text reveal


## Epic K — Audio: Music & SFX
- **UH-US-K1 ⬜** As a player, I hear the original's music tracks with random selection between levels (play_random_music, obj_jukebox)
- **UH-US-K2 ⬜** As a player, I hear weapon and UI sound effects ported from the gmlHARE/gmlRabbit sound libraries (102-152 original sound assets)


## Epic L — VFX & Particles
- **UH-US-L1 ⬜** As a player, I see muzzle flashes, explosions (oval_explosion, tinyexplosion, rocket_explosion, grenade_explosion) and trail/swirl effects (rail_swirl) matching each weapon


## Epic M — Boss Encounters
- **UH-US-M1 ⬜** As a player, I fight a boss encounter in room_boss1 with its own structure/cover objects (obj_structure1, obj_structure1_cover, obj_uss_warren) and phase behaviour

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| UH-T1 | 2026-10-21 | docs | closed | Start the permanent ticket log in the ticket database | — |
| UH-T2 | 2026-10-21 | chore | open | Scaffold Unity.Hare project (folders, .gitignore, Unity version, docs layout) | UH-US-A1 |
| UH-T3 | 2026-10-21 | docs | open | Write reverse-engineering notes for gmlHARE/gmlRabbit object, script and room catalog | UH-US-A2 |
| UH-T4 | 2026-10-21 | chore | open | Bring in Unity HARE POC scripts as reference under Assets/Legacy or similar | UH-US-A3 |
| UH-T5 | 2026-10-21 | feature | open | Implement player ship movement controller (thrust, rotate, boost, brake, leap) | UH-US-B1 |
| UH-T6 | 2026-10-21 | feature | open | Implement player health, damage, death and respawn | UH-US-B2 |
| UH-T7 | 2026-10-21 | feature | open | Implement player shield ability | UH-US-B3 |
| UH-T8 | 2026-10-21 | feature | open | Implement vulcan weapon (spin-up/spin-down, looping fire, ammo) | UH-US-C1 |
| UH-T9 | 2026-10-21 | feature | open | Implement laser, railgun and rocket weapons plus air-weapon cycling | UH-US-C2 |
| UH-T10 | 2026-10-21 | feature | open | Implement shotgun and flamethrower weapons | UH-US-C3 |
| UH-T11 | 2026-10-21 | feature | open | Implement ground weapons (mine, grenade) and next_ground_weapon cycling | UH-US-D1 |
| UH-T12 | 2026-10-21 | feature | open | Implement shared ammo-cost and ammo-subtraction economy for air/ground weapons | UH-US-D2 |
| UH-T13 | 2026-10-21 | feature | open | Implement flying insect enemies and their ai_* movement scripts | UH-US-E1 |
| UH-T14 | 2026-10-21 | feature | open | Implement set-piece enemies: hydra, phalanx+shield, meatball, wraith, sawblade, worm, trilobyte, mimic | UH-US-E2 |
| UH-T15 | 2026-10-21 | feature | open | Implement enemy spawn emitters and generate_enemy_list pacing | UH-US-E3 |
| UH-T16 | 2026-10-21 | feature | open | Implement per-enemy death effects (gibs, explosions, death particles) | UH-US-E4 |
| UH-T17 | 2026-10-21 | feature | open | Implement destructible rock field (spawn, movement, destruction) | UH-US-F1 |
| UH-T18 | 2026-10-21 | feature | open | Implement ambient weather/environment emitters (cloud, rain, snow, sand, fog) | UH-US-F2 |
| UH-T19 | 2026-10-21 | feature | open | Implement level-select map screen and destination/level buttons | UH-US-G1 |
| UH-T20 | 2026-10-21 | feature | open | Port level_01..level_10 definitions and the level_default/composition/objective/setting data model | UH-US-G2 |
| UH-T21 | 2026-10-21 | feature | open | Implement side-scroll and shaft stage modes alongside the open arena mode | UH-US-G3 |
| UH-T22 | 2026-10-21 | feature | open | Implement level results screen and level-complete flow back to the map | UH-US-G4 |
| UH-T23 | 2026-10-21 | feature | open | Implement pickup drops and collection (carrot, coin, chip, cartridge) | UH-US-H1 |
| UH-T24 | 2026-10-21 | feature | open | Implement garage/upgrade screen and weapon/ship upgrade purchases | UH-US-H2 |
| UH-T25 | 2026-10-21 | feature | open | Implement gameplay HUD (health, energy, weapon GUI, speedometer) | UH-US-I1 |
| UH-T26 | 2026-10-21 | feature | open | Implement title screen and menu navigation (quickstart, campaign, quit, yes/no dialog) | UH-US-I2 |
| UH-T27 | 2026-10-21 | feature | open | Implement floating text, tips and alert popups | UH-US-I3 |
| UH-T28 | 2026-10-21 | feature | open | Implement story/cutscene system (conversation, typewriter reveal, scenario loading) | UH-US-J1 |
| UH-T29 | 2026-10-21 | feature | open | Implement music playback and random track selection (jukebox) | UH-US-K1 |
| UH-T30 | 2026-10-21 | feature | open | Port and wire up weapon/UI/enemy sound effects | UH-US-K2 |
| UH-T31 | 2026-10-21 | feature | open | Implement weapon and explosion VFX (muzzle flash, explosions, rail swirl) | UH-US-L1 |
| UH-T32 | 2026-10-21 | feature | open | Implement boss room, structure/cover objects and boss fight flow | UH-US-M1 |
