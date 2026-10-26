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
 Unity 6000.4.3f1 · URP 2D · scenes generated in code (ProjectSetup: one per room plus the Main.unity bootstrap, U.HARE-T50)
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
 Unity.Hare.Online          → Runtime, Core, Unity.Multiplayer  (U.HARE-T35: the online authority, the NetEntity
                              player, the kit's front end; compiled only with MindAttic Components installed)
 Unity.Hare.Editor          → Runtime, Core  (ProjectSetup, HareArtImporter)
 Unity.Hare.Editor.Online   → Online, Editor (OnlineSceneBuilder: the online scene, prefab and prefab list)
 Tests/Core.Tests           → Core           (NUnit, dotnet test, outside Unity)
 Unity.Hare.Tests.EditMode  → Runtime, Core  (Unity Test Framework: the components)
 Unity.Hare.Tests.PlayMode  → Runtime, Core  (scripted play, UH-T70)
 Unity.Hare.Tests.Online    → Online         (PlayMode on Unity.Multiplayer's test kit: NetSessionTest, NetBotTest)
 Unity.Hare.Tests.Online.EditMode → Online, Editor.Online (the online scene's wiring)
```

References only point down: Runtime → Core, Editor → Runtime/Core, tests → what they test. Core references nothing
Unity.

### 5.2 Where it is today

UH-T37 made the split (466c012). UH-T39 moved the player and weapon rules into Core and UH-T40 the enemy rules and
spawn pacing; what is left in Runtime is presentation.

| Assembly | Folder | Contents | Status |
|---|---|---|---|
| `Unity.Hare.Core` | `Assets/Unity.Hare/Core/` | `noEngineReferences`: `EnemyKind`, `EnemyList` (`generate_enemy_list`); `GameClock` (the 120 steps/s and 100 px/unit conversions), `PlayerId`, `Vec2`/`RoomBounds`, `IRandomSource`/`SeededRandom` (every random draw); `Enemies/` (`EnemyWorld` of `EnemyBody`s changed only by `EnemyCommand`s that return `EnemyEvent`s: `DaggerEnemy`, `HydraEnemy`, `SawbladeEnemy`, `MeatballEnemy`, `PhalanxEnemy` and its shield, `WraithEnemy`, `MimicEnemy` and its illusion, `TrilobyteEnemy` and `EnemyMineBody`, `WormEnemy` and its segments, the three insect bodies, `EmitterBody`/`WormholeBody`/`WormholeEmitterBody` the vortex and generator pacing, `EnemyComposition`, `EnemyTable` the per-enemy stats as data, `GmlMath` the `ai_*` helper scripts, `Gibs`); `Player/` (`PlayerWorld` and `PlayerShip`, the `PlayerCommand`s and `PlayerEvent`s, `ShipFlight` the flight/energy/shield machine, `ShipHealth` HP, lives and the timers, `FlightTuning`/`HealthTuning`); `Weapons/` (`AmmoPools` the four pools and regen alarms, `GunRack` the vulcan/shotgun/flamer/mine launcher firing rules, `GroundLoadout`/`AirLoadout` the weapon cycles, `WeaponTable` the fire intervals, cycles and damage as data, `ShipLoadout` the guns a ship is built with as validated data); `Art/` (U.HARE-T46: `GmlSpriteGeometry` with `GmlMaskKind` and `GmlBoundingBox`, a GML sprite's origin as a pivot, its collision mask as an outline and its image_speed as a frame rate); `Audio/` (U.HARE-T47: `GmlSoundSettings`, what a sound.gmx's kind, compressed/streamed flags, target type and preload become — mixer channel, load type, format, mono, preload — and `MixerLevels`, linear 0-1 volume to the mixer's decibels); `Text/` (U.HARE-T48: `GmlFontMetrics`, a font.gmx glyph table in TextCore's terms — `string_height("M")` lines, each cell's top on the line's top, `shift`/`offset` as advance and bearing, `string_width` — and `Pack`, the gapped atlas repack); `Session/` (U.HARE-T49: `GameSession`, `globals.gml`'s session state — LIVES, COINS, SKILLPOINTS, KILLS, current_score and goal_score, difficulty, use_metric, master_volume, gamemode, the objective with its target, time elapsed and limit, current_scenario and current_level, level_complete and paused — owned by whoever holds the authority, never a global; changed only by `SessionCommand`s (`RestartGame` is game_restart, `ChooseQuickstart`/`ChooseCampaign`/`ChooseGenerator`/`SelectScenario` the menus, `BeginLevel` a level script's objective block, `TickSession` obj_healthbar's second count and obj_game's objective check, `AddScore`, `LoseLife`, `CollectCoin`, `SpendSkillPoint`, `RecordKill`, `DefeatTarget`, `CompleteLevel`, `AdvanceLevel`, `SetPaused`, `StepMasterVolume`, `ToggleUnits`) that return a `SessionResult` of `SessionEvent`s (`ScoreChanged`, `LifeLost`, `ExtraLifeEarned`, `LevelCompleted`, `LevelAdvanced`, `TimeLimitWarning`, `SessionReset`, ...); its starting values and rule numbers are the content registry's `session` section, `SessionTuning`, with LIVES read from `health.StartingLives`); `Flow/` (U.HARE-T50: `RoomFlow`, the original's room flow — the current room, `next_room` and the room's `RoomFade` (`obj_fade_game`/`_map`/`_story`/`_title`/`_upgrade`'s shared Create and Step: image_alpha 1 fading out by `fadespeed` 0.03 a step on entry, a button's `mode = "fadein"` going back to opaque, then once, `executeOnce`, `room_goto(next_room)` or, in a play room, `current_level += 1; setup_level()`) — owned by whoever holds the authority, changed only by `FlowCommand`s (`GoToRoom` is room_goto, `EnterRoom` a room that came up outside the flow such as room_restart, `FadeToRoom`/`StartFadeIn` the buttons' fade-in with the optional `obj_fade_title.mode == "off"` check, `PressEscape` the controllers' Esc `room_goto(room_title)`, `PressFullscreen` their F1 `window_set_fullscreen(!window_get_fullscreen())`, `StepRoom` one step at the room's room_speed) that return a `FlowResult` of `FlowEvent`s (`RoomChanged`, `RoomEntered`, `FadeInStarted`, `FullscreenToggled`, `LevelAdvanceRequested`); `RoomId`/`RoomList` the nine rooms by GML name (room_boss1 is never entered, so it is not one), `RoomTable` the registry's `rooms` section: each room's size, room_speed, fade and which of Esc and F1 its controller answers); `Online/` (U.HARE-T35: `SharedWorld`, the one world an online game's authority holds — every seat's ship in one `PlayerWorld`, where each ship is in the room, and the one fixed step that advances them all; `PlayerFor`/`SeatOf` map the kit's 0-based seat index to a `PlayerId`, seat 0 being P1 like local play's one ship, so a reconnecting player gets the same ship back; `OnlineTuning`, the registry's `online` section) | in place |
| `Unity.Hare.Runtime` | `Assets/Unity.Hare/Scripts/` | `Art/` (`HareArtManifest`, the generated map from every GML sprite and background name to its imported frames, clip, mask and atlas, loaded from `Resources/Hare/HareArtManifest`; U.HARE-T46), `Audio/` (U.HARE-T47: `HareAudioManifest`, the generated map from every GML sound name to its clip, mixer channel and sound.gmx volume, with the AudioMixer and the original's credits, loaded from `Resources/Hare/HareAudioManifest`; `HareAudio`, which plays a GML sound by name through its channel's group, routes every AudioSource the game makes, and puts the profile's music/sfx volumes and mutes and the session master volume on the mixer; `HareArt.Clip`/`Play` now forward to it), `Player/` (PlayerShipController, PlayerHealth, ShieldWall, PlayerDamageSource, `PlayerShipBuilder` which composes the armed ship from a `ShipLoadout`), `Weapons/` (VulcanWeapon, Shotgun, Flamethrower, MineLauncher, AirWeaponLoadout, GroundWeaponLoadout, PlayerAmmo, Bullet, Rocket, LaserBeam, RailgunShot, Mine, the explosions, and `Gml`, the unit and step helpers in `WeaponTargets.cs`), `Enemies/` (`EnemyDirector` the scene's authority over the enemy world and `EnemyViews`; the views `EnemyBodyView`/`SetPieceEnemy` with Dagger, the eight set pieces and their parts, EnemyMine, the insect views, Vortex/EnemyEmitter/Wormhole/WormholeEmitter, EnemySpawner, FadingDebris), `Rocks/` (Rock, RockField, Flotsum), `Effects/` (DeathEffects, GmlEffects, TinyExplosion, MuzzleFlash, OvalExplosion, DeathParticle, SpriteAnimation, HareArt), `Settings/` (`ActiveProfile`, `HareSettings`; U.HARE-T43), `Flow/` (U.HARE-T50: `RoomFlowDirector`, the one object that plays Core's `RoomFlow`: made by the Main.unity bootstrap and kept across room loads, it steps the room at its room_speed on real time (at most `MaxStepsPerFrame` steps a frame), sends the Meta map's Menu (Esc) and Fullscreen (F1) keys as commands, loads a room's scene by its GML name on `RoomChanged`, flips `Screen.fullScreen` on `FullscreenToggled`, draws the fade's opacity black on the room camera's `ScreenOverlayLayer`, reports a room scene that loads outside the flow (obj_player's death reloading it, or Play pressed on a room scene in the Editor) as `EnterRoom`, and raises `LevelAdvance` for the level setup, U.HARE-T57), `Rendering/` (`HareSortingLayers` the back-to-front sorting layer plan, `CameraRig`/`LetterboxCamera` the 2D camera rig and its 1920x1080 reference-resolution letterboxing, `ScreenOverlayLayer` the room fade's layer (U.HARE-T50) and the flashbomb's (UH-T67); none of it references a render pipeline, U.HARE-T44), `UI/` the UI foundation (U.HARE-T45) every HUD, menu, results, upgrade and story screen builds on: `HarePanel` builds the PanelSettings (ScaleWithScreenSize, MatchWidthOrHeight, `HareTheme.tss` — one line, `@import url("unity-theme://default")`, the same pattern Unity.GridGame's `GaidenTheme.tss` uses — so native controls keep their baseline chrome in a player build) and, inside its root VisualElement, a "stage" pinned to exactly the 1920x1080 reference resolution, entirely in code (no hand-authored PanelSettings asset); `HareUiFit` locks the panel's match axis to whichever one `LetterboxCamera` pillarboxes or letterboxes and re-centres the stage inside the spare logical room that produces, so the UI's 1920x1080 area always exactly covers the visible, non-letterboxed screen (an Opus review of this diff caught the first cut only scaling, not confining, the UI); `Theme.uss` (`Resources/Hare/UI/Theme.uss`) is the shared stylesheet; `HareFonts` resolves each of the eight `draw_set_font` faces to the bitmap FontAsset the font pipeline builds from GameMaker's own bake (U.HARE-T48, [§6](#UH-§6)) and applies it at its native size; `HareTextStyles` names the text styles screens use (`HudScore`, `HudText`, `HudAmmo`, `MenuTitle`, `MenuText`, `Typewriter`, `StoryTypewriter`, `Conversation`, `ConversationName`, `FloatText`, `Default`), each a `Theme.uss` class in the face the original draws that text with; and `HareUi` is the widget kit: `OutlineText`/`OutlineTextAlpha`/`OutlineTextColor`/`OutlineTextColorAlpha` reproduce the `draw_text_outline` family exactly (a stacked `Label` per offset in the full `(2*size+1)^2` square, each pinned to all four edges so it matches the box's own size instead of shrinking to its text — not a CSS shadow approximation), `Button`, `Window`, `Dialog`, and `Bar`/`SetBar` whose low/mid colour thresholds (a third, two thirds) and colours (`c_red`, `c_yellow`, and `obj_energybar`'s own oddly-named `c_lightgreen`, `rgb(168,157,4)` in `globals.gml`) are reconstructed from `obj_energybar`'s Draw event, not invented | references Core one way; the player and weapon scripts are thin views (`PlayerSeat` and `SeatView` send commands and play events) that still read input directly (UH-T42); the enemy views send ticks and weapon contacts to Core and play what comes back |
| `Unity.Hare.Editor` | `Assets/Unity.Hare/Editor/` | `ProjectSetup` (URP 2D assets, project settings, sorting layers, `Main.unity`), `HareArtImporter` (sprite pivots from `sprites.txt`, particle textures); `Art/` the art pipeline (U.HARE-T46, [§6](#UH-§6)): `GmlArtData` (reads and validates `Art/gml-art.json`), `GmlArtImporter` (texture import settings from it), `GmlArtBuilder` (animation clips, sprite atlases, the runtime `HareArtManifest`); `Audio/` the audio pipeline (U.HARE-T47, [§6](#UH-§6)): `GmlAudioData` (reads and validates `Audio/gml-audio.json`), `GmlAudioImporter` (clip import settings from it), `GmlAudioBuilder` (the AudioMixer, through the editor's internal `AudioMixerController` by reflection since Unity has no public mixer-authoring API, and the runtime `HareAudioManifest`); `Fonts/` the font pipeline (U.HARE-T48, [§6](#UH-§6)): `GmlFontData` (reads and validates `Fonts/gml-fonts.json`), `GmlFontBuilder` (one static bitmap FontAsset per GML font, its repacked atlas inside it) | in place |
| `Unity.Hare.Editor.Rendering` | `Assets/Unity.Hare/Editor/Rendering/` | The URP seam (U.HARE-T44): `UrpSeam` (the only code in the project that references `UnityEngine.Rendering.Universal` — the 2D pipeline asset, URP's per-camera data, the global Light2D) and `SortingLayerSetup` (writes `HareSortingLayers`' order into `ProjectSettings/TagManager.asset`). Its own asmdef carries the URP package references so the restriction is enforced by the compiler: `Unity.Hare.Editor` and `Unity.Hare.Runtime` no longer reference the URP packages at all | in place |
| `Unity.Hare.Tests.EditMode` | `Assets/Unity.Hare/Tests/EditMode/` | NUnit tests of the components: the glue of the player and weapon views (`PlayerSeatTests`, projectiles, the shield wall), enemies, rocks, effects; `UI/` (U.HARE-T45): `HarePanelTests`, `HareUiFitTests`, `HareFontsTests`, `HareUiTests` (the widget kit, in particular `OutlineText`'s layer count and colours against the GML algorithm); `Art/GmlArtPipelineTests` (U.HARE-T46: every sprite and background imported and in the manifest, pivots on the GML origins, clip timing, masks, atlas membership, background tiling; this assembly also references `Unity.Hare.Editor` for `GmlArtData`); `Audio/GmlAudioPipelineTests` (U.HARE-T47: all 75 sounds imported and named in the manifest, each clip's load type, format, rate and mono against its sound.gmx, the mixer's groups and exposed volumes, the moved weapon sounds' GUIDs, the credits); `Fonts/GmlFontPipelineTests` (U.HARE-T48: all eight fonts with their face, size and style, users and licences, no typeface file anywhere under `Assets`, every atlas cell byte for byte GameMaker's with clear gaps, each FontAsset a saved static bitmap font carrying exactly the GML glyph table, rebuilds byte-identical, every named style's `Theme.uss` rule in its face at its native size); `UI/HareFontsTests` now proves every face resolves and applies at its native size | in place; pure rule tests live in `Tests/Core.Tests` once their rules are in Core |
| `Unity.Hare.Tests.PlayMode` | `Assets/Unity.Hare/Tests/PlayMode/` | `EnemyPlayTests`: the enemy world under Unity's own physics loop (`EnemyDirector`'s driver steps it once a `FixedUpdate`), proving a seed reproduces a run end to end, composition placement and per-enemy contact damage (UH-T40); `PlayerShipPlayTests`: the main scene's ship wakes armed and fires (UH-T77); `HareUiPlayTests` (U.HARE-T45): a PanelSettings/UIDocument built by `HarePanel` actually lays out a `HareUi` widget tree once Unity's UI Toolkit loop is running, not just in a detached EditMode tree; `HareAudioPlayTests` (U.HARE-T47): the live mixer takes the profile's music/sfx volumes and mutes and the session master volume, and a GML sound plays through the Sfx group; `HareTextPlayTests` (U.HARE-T48): every named style resolves to its face at its native size on a live panel, and, rendered to a texture with a graphics device (`tools/unity.ps1 -Graphics`), each face's glyphs match GameMaker's bake pixel for pixel with nothing bleeding in around them and a whole line's ink lands on `draw_text`'s own pen positions; it writes `Screenshots/fonts.png` | more scripted play planned, UH-T70 |
| `Unity.Hare.Online` | `Assets/Unity.Hare/Online/` | U.HARE-T35, on the shared Unity.Multiplayer kit: `HareNetPlayer`, the NetEntity player (the NetworkManager's Player Prefab, `Resources/Hare/Online/HareNetPlayer.prefab`) carrying its seat — its owner sends the same Input System actions local play reads as `[ToServer]` calls (`Turn`, `Aim`, `PressBoost`, `PressBrake`, `ReleaseShield`), which run on the server as the same Core commands `PlayerShipController` sends (`SetTurn`, `SetAim`, `PressBoostKey`, `PressBrakeKey`, `ReleaseShieldKey`), and every peer draws the `[Synced]` seat, position, heading, aim, energy and flight state; its name and colour come from the session's roster; `HareAuthority`, the one authority, on the server only (one per server session, so a test process can run a host and its clients side by side), which holds the session's `SharedWorld`, seats each player, frees a seat's ship once the kit releases the seat, steps the world once per `FixedUpdate` and writes each ship's state into its player; `HareOnlineGame`, the online scene's component, which adds **Play solo** (leaves for `Main.unity` with none of the kit left behind) and **How to play** to the kit's drop-in front end and sizes the session from the `online` section; `HareBot`, the `[NetBotDriver]` that turns and boosts through the same calls a player makes | compiles only when MindAttic Components is installed (`MINDATTIC_COMPONENTS`, as the kit's own front end does); flight only so far: weapons, health, rocks, enemies and the session follow the same path as they move under the authority |
| `Unity.Hare.Editor.Online` | `Assets/Unity.Hare/Editor/Online/` | `OnlineSceneBuilder` (**Unity.Hare > Rebuild Online Scene**, batch `BuildAll`): writes `Scenes/Online.unity` — the arena's camera and light (`ProjectSetup.AddArenaCameraAndLight`, shared with the main scene), the kit's NetworkManager with Unity Transport, `NetBootstrap` and `NetFrontEnd` (through the kit's own `NetFrontEndSetup`), `HareNetPlayer` as the Player Prefab and in `Online/HareNetPrefabs.asset`, and `HareOnlineGame` — second in Build Settings after `Main.unity`, and `mp-host.json` (the server scene and the smoke test's bots) | in place |
| `Unity.Hare.Tests.Online` / `.Online.EditMode` | `Assets/Unity.Hare/Tests/Online/` | PlayMode on Unity.Multiplayer's test kit, over real Unity Transport on a free loopback port: `HareNetPlayerTests` (`NetSessionTest`, a host and a client: each ship carries its seat, only the server holds the world, a client's turn and boost run on the server's world and come back to its own client, its name and colour come from the roster, and a cheating client's oversized turn is clamped to the rules' turn rate), `HareBotTests` (`NetBotTest`: a dedicated server and two `HareBot`s join by code through the in-process rendezvous service and fly), `OnlineScenePlayTests` (the generated scene hosts from its own `NetBootstrap`, seats and flies the host's ship, and Play solo lands in the local game with no NetworkManager left); EditMode `OnlineSceneTests` (the scene's kit components and Player Prefab, the local game first in Build Settings with no NetworkManager, the kit's prefab validator) | the test assemblies carry `MINDATTIC_NGO_TESTABLE`, committed in `ProjectSettings.asset` with NGO under `testables` |
| `Tests/Core.Tests` | `Tests/Core.Tests/` | dotnet test project compiling `Core/**/*.cs` directly (C# 9, as Unity builds it): `EnemyListTests`, the enemy rules (`EnemyBasicsTests`, `EnemySetPieceTests`, `EnemySpawnerTests`, driven by `EnemyRig` on a seeded random), and the player and weapon rules (`PlayerAmmoTests`, `PlayerHealthTests`, `PlayerFlightTests`, `GunTests`, `LoadoutTests`), `GmlSpriteGeometryTests` (U.HARE-T46), `GmlSoundSettingsTests` (U.HARE-T47), `GmlFontMetricsTests` (U.HARE-T48) `SharedWorldTests` (U.HARE-T35: seats and ids, spawn points, the one step, reseating and releasing, the key commands, the `online` section) and `RoomFlowTests` (U.HARE-T50: the rooms table, room_goto, the fades step by step, Esc and F1 per room, the `rooms` section) | in place; run by `tools/run-core-tests.ps1` |

The Input System action asset (UH-T42), Unity.Shared profiles and settings (UH-T43) and the UI foundation
(U.HARE-T45, `Scripts/UI/`) are in place, and so is the multiplayer kit (U.HARE-T35, §5.3). The pipeline-agnostic
URP seam, sorting layers, 2D camera rig and 1920x1080 reference resolution are done (U.HARE-T44, §5.2 above); the
room fade plays on its `ScreenOverlayLayer` (U.HARE-T50, `obj_fade_*` reverse-engineered into Core's `RoomFade`);
the flash timing is still UH-T67's own rule, read from the GML source when that ticket reverse-engineers `obj_flashbomb`. The UI foundation
has no screen content of its own yet — no HUD, menu, results, upgrade or story screen reads from it — that is
every one of UH-T59/UH-T61/UH-T62/UH-T63/UH-T64's own job; U.HARE-T45 only builds the PanelSettings, theme and
widget kit they will build on, plus the `draw_set_font` face seam U.HARE-T48 fills with the original's eight fonts
(as GameMaker's baked glyph atlases, not typeface files: [§6](#UH-§6)) and their named text styles.

### 5.3 Commands, events and the one authority (player and weapons done in UH-T39; enemies UH-T40; authority UH-T35)

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
The enemies are Core's too (UH-T40): an `EnemyWorld` holds every `EnemyBody` (the nine enemies and their parts, the mine),
insect, vortex and wormhole emitter, and changes only through `EnemyCommand`s (`SpawnEnemy`, `TickEnemy`, `StrikeBullet`,
`StrikeRocket`, `StrikeExplosion`, `StrikeBeam`, `StrikeShield`, `Separate`, `AddEmitter`, `AddWormholeEmitter`,
`PlaceComposition`, `SetTarget` for each seat's ship, `HurtPlayer` and the rest) that return an `EnemyResult` of whether
it took effect and the `EnemyEvent`s it raised (`EnemySpawned`, `EnemyKilled`, `RoundFired`, `TinyExplosionRaised`,
`DeathBurstRaised`, `DebrisRaised`, `VortexReleased`, ...). Every random draw, the gib scatter included, comes from the
world's `IRandomSource`, so `EnemyDirector.Reset(seed)` (or a `SeededRandom` in a Core test) reproduces a run. The enemies
chase the nearest `PlayerTarget`, one per seat. The player and enemy hit rules meet in `HurtPlayer`: an enemy's
contact, or an enemy round, hurts a ship through that ship's own `TakeHit`, and the seat plays the events. On the Unity
side `EnemyDirector` owns the world, runs each view's commands and plays the events (it spawns rounds, effects, debris
and the views of anything Core made); `EnemyBodyView` and its subclasses only turn ticks and weapon contacts into
commands and show where Core says the body is. Rocks and projectiles still hold their own state and announce through
C# events (`GrenadeExplosion.Exploded`). The GML global `paused` is the static `Gml.Paused`, passed into the enemy world
before each command.

Weapon rhythm is data in `WeaponTable`: the vulcan's `VulcanBaseRateSteps`/`VulcanMinRateSteps`/
`VulcanSpinUpDecrementSteps` ramp, the other guns' and air weapons' fire intervals in steps, the ground and air
cycles (`GroundCycle`, `AirCycle`) and per-round damage, so changing a rate or the cycle order is a one-line change
there and Core tests cover it.

The ship's guns are data too (UH-T77): `ShipLoadout` lists each `GunMount` (gun and trigger), by default obj_player
Create's (`ground_weapon = weapon_vulcan` at the head of Q's vulcan, laser, railgun, shotgun, flamer cycle,
`ground_weapon_secondary = weapon_shotgun`, `air_weapon_secondary = weapon_mine`; the rocket and mine air primary is
`AirLoadout`'s), and refuses a loadout the original could not fire. `PlayerShipBuilder` composes the ship from it
(controller, health, ammo, one component per gun, both loadouts), seated in a shared world when an authority passes
one, and `ProjectSetup` saves that ship into `Main.unity` with the weapon sounds wired. Until the player switches,
the ground slot holds the cycle's first gun whatever order the components woke in.

**Online (U.HARE-T35)** is built on the shared Unity.Multiplayer kit (with MindAttic Components for its menus), beside
local play rather than in place of it. `Main.unity` is untouched and first in Build Settings, so a launch plays the
local game exactly as before, with no NetworkManager in it. `Online.unity`, generated by `OnlineSceneBuilder`, holds
the kit's NetworkManager, `NetBootstrap` and drop-in `NetFrontEnd` (Host, Join by IP, Join by code, the lobby with its
join code and roster, reconnect to the same seat, the Esc menu), whose main menu also offers **Play solo** back to
the local game. There the state has one authority: `HareAuthority`, on the server only, holds Core's `SharedWorld`
(every seat's ship in one `PlayerWorld` and where it is) and steps it once per fixed step. Each player is a
`HareNetPlayer` NetEntity carrying its seat (`PlayerId` = seat + 1, so P1 is seat 0 as in local play, and a reconnect
within the kit's grace window flies the same ship); its owner's input travels as `[ToServer]` calls that the server
runs as the very commands local play sends (`PlayerShipController` now sends `PressBoostKey`, `PressBrakeKey` and
`ReleaseShieldKey` too, the GML key events it used to branch on itself), and presentation on every peer only draws
the `[Synced]` state. Only flight is online so far; weapons, health, rocks, enemies and the `GameSession` follow
the same path as they move under the one-step authority below. A dedicated server and bots run the same scene
(`mp-host.json`, `HareBot`).

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

**The rebuild today (U.HARE-T41)** loads one validated data file, `Resources/Hare/Content/GameContent.json`, into
Core's `ContentRegistry` (`Assets/Unity.Hare/Core/Content/`): a plain, dependency-free JSON reader (`MiniJson`)
feeds a reflective binder (`ContentBinder`) that overlays the file's fields onto each section's compiled-in
defaults (so a field the file omits, or a missing file entirely, keeps the original's own figure), then every
section validates itself and every problem — not just the first — comes back in one `ContentValidationException`.
`ContentBootstrap` (Runtime) loads the file before any scene's Awake runs and falls back to the compiled-in
defaults, logging the full validation report, if it fails; `GameContent.Current` is what every rule reads instead
of a literal. Two data files of its own stand alongside it: `Resources/Hare/Sprites/sprites.txt` (each sprite's GML
origin, read by `HareArtImporter`) and the original's sprites and particle shapes under `Resources/Hare/`; its
sounds are the audio pipeline's (below).

**The art pipeline (U.HARE-T46)** imports all of the original's art, repeatably, from `HARE.gmx` itself:

1. `tools/import-gml-art.ps1` reads `HARE.project.gmx` (the resource tree, so each sprite keeps its group), every
   `sprites/*.sprite.gmx` and `background/*.background.gmx`, and the objects and rooms for timing. It mirrors the
   frames into `Assets/Unity.Hare/Art/Sprites/<group>/<sprite>/<sprite>_<n>.png` and the backgrounds into
   `Art/Backgrounds/`, copying only changed bytes and deleting what the original no longer has, and writes
   `Art/gml-art.json`: per sprite its origin, size, `colkind` mask, bounding box, tiling, `image_speed` (with the
   objects it came from), room speed, frames and the Unity assets it becomes; per background its size and tiling;
   and a `skipped` list of anything not imported and why (empty today). Rerunning it on the same source changes
   nothing. It then runs `GmlArtBuilder` headless (`-NoUnity` skips that).
2. `GmlArtImporter` (an `AssetPostprocessor` that depends on `gml-art.json`) gives every frame its import settings:
   a single sprite at 100 PPU, pivot on the GML origin (`GmlSpriteGeometry.Pivot`), full-rect mesh, bilinear (HARE's
   `option_interpolate`), uncompressed, no mipmaps; an alpha-traced physics shape only for precise masks (GameMaker
   also clips a precise test to the bounding box, which the manifest carries for the collider code to apply).
   Backgrounds become sprites pivoted top left (where `draw_background` draws from), repeating on each axis that
   tiles.
3. `GmlArtBuilder.BuildAll` (menu **Unity.Hare > Build Art From gml-art.json**) writes a looping SpriteRenderer
   clip per animated sprite (`Art/Animations/`, each frame held `1 / image_speed` steps at the sprite's room speed,
   `GmlSpriteGeometry.FramesPerSecond`), a Sprite Atlas V2 per group (`Art/Atlases/Sprites_<group>.spriteatlasv2`,
   packed for player builds; the seven sprites over 1024 px on a side keep their own textures), and
   `Resources/Hare/HareArtManifest.asset`, the runtime map from every GML sprite and background name to its
   frames, clip, mask (`GmlSpriteAsset.MaskOutline`, in world units around the origin) and atlas.

`image_speed` is a per-instance variable in GameMaker, so the tool takes it from the Create event of each object
whose sprite it is, and the room speed from the rooms those objects are reachable from (placed there, or named in
code reachable from there). Reachability by name is coarse: when a sprite turns up in rooms of both speeds the clip
uses the combat rooms' 120 and says so in the sprite's `notes`. The views still draw the older copies under
`Resources/Hare/Sprites` through `HareArt`; moving them onto `HareArtManifest` is each view ticket's job.

**The audio pipeline (U.HARE-T47)** imports all of the original's sound, and its one data file, the same way:

1. `tools/import-gml-audio.ps1` reads `HARE.project.gmx` (the sound groups `music`, `player`, `vulcan`) and every
   `sound/*.sound.gmx`, checks each file under `sound/audio` by its content (the WAV header's format, channels, rate
   and length), mirrors the clips into `Assets/Unity.Hare/Audio/Sounds/<group>/<sound>.<ext>` and
   `datafiles/credits.txt` into `Audio/credits.txt`, and writes `Audio/gml-audio.json` (every sound.gmx field, the
   source facts, the clip, and a `skipped` list: only the two album-art JPEGs). Rerunning it changes nothing; it
   then runs `GmlAudioBuilder` headless (`-NoUnity` skips that). The six weapon WAVs that were hand-copied before
   were moved into it with their `.meta`, so `Main.unity`'s references to them still hold; the `Resources/Hare/Audio`
   copies of `sfx_death` and `sfx_explode` are gone (`HareArt.Clip` reads the manifest).
2. `GmlAudioImporter` (an `AssetPostprocessor` that depends on `gml-audio.json`) applies Core's
   `GmlSoundSettings`: the six streamed, compressed `music_*` stream as Vorbis (quality 0.7, `vorbisQuality` in the
   data file, Unity's nearest to GameMaker's 192 kbps); the uncompressed `sfx_*` are PCM decompressed on load, the
   latency and quality GameMaker gave them; every clip is resampled to GameMaker's 44.1 kHz export rate, preloaded
   (GameMaker's preload box) and 2D; GameMaker's mono target forces `sfx_hurt8` and `sfx_vulcan_spindown` to mono.
3. `GmlAudioBuilder.BuildAll` (menu **Unity.Hare > Build Audio From gml-audio.json**) builds `Audio/HareMixer.mixer`
   (Master, with Music and Sfx under it; the three group volumes exposed as `MasterVolume`, `MusicVolume`,
   `SfxVolume`), only adding what is missing to an existing one, and writes `Resources/Hare/HareAudioManifest.asset`:
   every GML sound name to its clip, channel and sound.gmx volume and pan, plus the mixer, its two groups and the
   credits `TextAsset`.

At run time `HareAudio` plays a sound by GML name through its channel's group and puts the volumes on the mixer
(`MixerLevels.ToDecibels`, 20 log10 of the 0-1 volume, silence at -80 dB): Music and Sfx from the profile's
`MusicVolume`/`MuteMusic` and `SfxVolume`/`MuteSfx` (applied on the first frame; call `HareAudio.ApplyProfile` again
after the options change them), Master from the session's `HareAudio.MasterVolume`, the original's `master_volume`.

**The font pipeline (U.HARE-T48)** imports the original's eight fonts the same way, as what the game actually shipped:
GameMaker never ships a typeface, it rasterises each font resource once into a glyph atlas (`fonts/<name>.png`,
white glyphs on transparency) and a glyph table (`fonts/<name>.font.gmx`: each character's cell, its `shift` advance
and `offset` bearing), and draws text from those.

1. `tools/import-gml-fonts.ps1` reads `HARE.project.gmx` (which fonts are in the resource tree), every
   `fonts/*.font.gmx` and every object and script for its `draw_set_font` calls (outside comments), mirrors the
   atlases into `Assets/Unity.Hare/Fonts/Atlases/<name>.png`, and writes `Fonts/gml-fonts.json`: per font its face,
   size, bold/italic, charset, anti-aliasing, ranges, glyphs, kerning, atlas, the objects that draw with it and
   notes; the licence of each typeface; and a `skipped` list (empty). Rerunning it changes nothing; it then runs
   `GmlFontBuilder` headless (`-NoUnity` skips that).
2. `GmlFontBuilder.BuildAll` (menu **Unity.Hare > Build Fonts From gml-fonts.json**) builds
   `Resources/Hare/Fonts/<name>.asset`, one static bitmap (`SMOOTH`, not SDF) FontAsset per font. Its glyph table is
   the font.gmx's (`GmlFontMetrics`): the face's point size is the font.gmx size, so a style at that `font-size` draws
   1:1; each cell's top sits on the line's top, as `draw_text` draws it; the baseline is the bottom of the `M` cell,
   so lines are `string_height("M")` apart. Its atlas, stored inside the asset, holds every cell byte for byte as
   GameMaker baked it, repacked with 16 px of clear space around each (`GmlFontMetrics.Pack`, [V17](#UH-§8)). A
   rebuild rewrites the asset in place, byte-identical for the same data.

| Font | Face (font.gmx) | Drawn by | Named styles |
|---|---|---|---|
| `fnt_point` | Berlin Sans FB 12 | 40 objects: the HUD's small text, every menu, dialog and level button, the in-game typewriter, conversation names, floating text, tips, labels, results rows | `HudText`, `MenuText`, `Typewriter`, `ConversationName`, `FloatText`; `.hare-root`'s default |
| `fnt_score` | Berlin Sans FB 36 | `obj_game` (score), `obj_healthbar` (lives), `obj_speedometer`, the title buttons, the yes/no dialogs, `obj_level_results`, `obj_levelstart`, `obj_upgrade`, `obj_conversation` (speech) | `HudScore`, `MenuTitle`, `Conversation`; window titles and button captions |
| `fnt_medium` | Berlin Sans FB 16 | `obj_typewriter_story` | `StoryTypewriter` |
| `fnt_small` | Berlin Sans FB 11 | `obj_weapon_gui` (ammo) | `HudAmmo` |
| `fnt_basic` | Arial 12 | nothing (GameMaker's own default face) | `Default`, text drawn before any `draw_set_font` |
| `font4`, `font7`, `font8` | Arial 12 | nothing; byte-identical bakes of `fnt_basic`, and `font4` is not even in `HARE.project.gmx` | none; imported so every face name resolves |

No font is bold or italic; all are anti-aliased (`aa` 3), characters 32-127, with no kerning pairs.

**Licences.** Arial (Monotype, shipped with Windows) and Berlin Sans FB (The Font Bureau, shipped with Microsoft
Office) are proprietary: their licences cover use on the machine they came with, not redistributing the font files.
So the rebuild ships no typeface file at all (`GmlFontPipelineTests` fails if a `.ttf`/`.otf` appears under
`Assets`), only GameMaker's rasterised glyphs of them, exactly what the original game distributed. Whether shipping
those rasterisations publicly is fine is U.HARE-T69's licence pass to confirm; if it is not, the faces are swapped
for open ones there, and every screen follows through `HareTextStyles` with no change of its own.

| Data | Holds | Original source | Status |
|---|---|---|---|
| ship | flight, brake and boost speeds, turn rates, energy, shield, leap tolerance, health and hurt timing | `obj_player` Create, `player_*` | done |
| weapons and ammo | each weapon's rate, speed, spread, reach, damage, muzzle geometry; the four ammo pools' max, cost, regen | `shoot_*`, `fire_*`, `set_ammo_cost`, `obj_player` Create | done |
| enemies, generator | per kind: points, death effect size, bbox, push, difficulty points, emitter/wormhole scale | `obj_*` Create and collision events, `create_wormhole`, `generate_enemy_list` | partial — each kind's own movement speeds, alarms and hit rules are still literals in its `*Enemy`/`*Body` class, not yet in `EnemyCatalog` |
| emitter/wormhole timings | `par_enemy_emitter`'s alarms and default wait range; `obj_wormhole`/`obj_wormhole_emitter`'s one_tick, opening fraction and re-arm ranges | `EmitterBody`, `WormholeBody`, `WormholeEmitterBody` | done |
| rocks | sprites, speeds, spin, `spin_max`, heal and spin-wound rates, hit darkening and pushes, flotsam | `par_rock`, `create_new_rock`, `rock_destroyed`, `obj_flotsum` | still `[SerializeField]`/`const` on the components |
| levels | room, rock counts and stationary share, enemy min/max per kind, objective, time limit, setting | `level_01`..`level_10`, `level_default`, `level_composition_*`, `level_objective_*`, `level_setting_*` | schema only (`LevelCatalog`), empty until U.HARE-T50/U.HARE-T64 |
| rooms | each room's size, room_speed, which `obj_fade_*` it has and whether its controller answers Esc and F1; `fadespeed`, the first room, Esc's room, `next_room`'s starting value, and the rebuild's per-frame step limit | the `.room.gmx` files, `obj_fade_*` Create, `obj_title`/`obj_game`/`obj_map`/`obj_story`/`obj_intro` key events, `globals.gml` | done (`RoomTable`, U.HARE-T50) |
| text | story, conversation, tips, credits | `load_scenario`, `obj_conversation`, `obj_tip`, `credits.txt` | schema only (`DialogueCatalog`), empty until U.HARE-T28 |
| upgrades | garage/upgrade screen's SKILLPOINTS costs | `obj_upgrade_*`, the schematics | schema only (`UpgradeCatalog`), empty until U.HARE-T24 |

**Player options (U.HARE-T43)** live in a Unity.Shared profile, the same way across every MindAttic game:
`Profiles.Current.Settings.Common` (display name, colour, music/SFX volume, mutes, reduce motion) and Hare's own
`game` block, typed by `HareSettings` (`difficulty`, `useMetric`, `fullscreen` — the original's globals `difficulty`,
`use_metric`). The persisted volumes are Unity.Shared's shared music/SFX volumes, on the mixer's Music and Sfx groups;
the original's `master_volume` stays what it was, a session value that starts at 1 and is never saved, on the
mixer's Master group (`HareAudio.MasterVolume`, U.HARE-T47; `obj_game`'s numpad keys that move it are U.HARE-T60's).
`Scripts/Settings/ActiveProfile.cs` reaches the current profile; Unity.Shared's own `ProfilesBootstrap` roots the
store at `Application.persistentDataPath` before the first scene loads. Campaign progress will be a save
(planned, UH-T65) under `ActiveProfile.SavesFolder` (the profile's `Saves/` folder).

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
| D9 | `sfx_whoosh.wav` is an MP3 under a `.wav` name (an ID3 tag, then 320 kbps MPEG-1 Layer III frames); no code plays it | `sound/audio` | imported by its content as `sfx_whoosh.mp3`, kept compressed in memory rather than inflated to PCM (`GmlSoundSettings`' already-compressed source rule); still played by nothing, as in the original | fixed (U.HARE-T47) |
| D10 | `set_time_limit_string` writes the ", " separator whenever there are whole minutes, so a limit of whole minutes reads "Survive 1 minute, " | `set_time_limit_string` | writes the separator only between minutes and seconds ("Survive 1 minute"; `GameSession.DescribeTimeLimit`); every shipped limit is 90 s, which reads "1 minute, 30 seconds" either way | fixed (U.HARE-T49) |
| D11 | `KILLS` is declared, reset and shown on `obj_level_results`, but nothing ever increments it, so the results screen always reads 0 kills | `globals.gml`, `obj_level_results` | the session counts kills (`RecordKill` raises `KillsChanged`); the enemy views send it when an enemy dies, wired with the results screen | fixed in Core (U.HARE-T49); wiring U.HARE-T22 |
| D12 | `fling_player` has no state check and `player_hurt`'s death leaves `current_rock` set, so a dead ship whose rock broke or drifted out was flung back into flight | `fling_player`, `rock_destroyed`, `par_rock` Step | death lets go of the rock and `Fling` is refused while dead (`ShipFlight.Fling`) | fixed (U.HARE-T53) |
| D13 | Space and Shift raised the shield while perched: the first port bound Space to boost and boost's key raises the shield on the ground, but in the original Space is the crouch-and-leap key (press: crouch perched, boost in flight; release: `player_leap`) and only W and S shield | `obj_player` key events 32/87/83 | Space is the `Leap` action with the right mouse button: crouch or boost/brake on the press, `player_leap` on the release; W and S still shield | fixed (U.HARE-T53) |
| D14 | `LIVES` can go below zero: the out-of-bounds rule spends the last life and `reset_player` flies the ship back in with `LIVES = 0`, and dying then does `LIVES -= 1` unchecked, so obj_healthbar reads "x -1" for the 320-step death wait before `game_restart` | `player_hurt`, `obj_player` Step | a spent life never takes `LIVES` below 0 (`ILivesLedger.Spend`, the session's `SpendLife`); the death wait still ends in `game_restart` because no life is left | fixed (U.HARE-T54) |

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
| V6 | The laser and railgun are perched (ground) primaries on Q's cycle, fired with the left mouse; in flight the left mouse fires the air weapon (`next_air_weapon`, E, toggles rocket and mine) and Ctrl + left mouse the mine | the same: `LaserGun` and `RailgunGun` are ground primaries on Q's cycle (left mouse, perched), `AirWeaponLoadout` fires the rocket or mine on the left mouse in flight, cycled with E, and Ctrl + left mouse drops the mine; the right mouse is flight's, not a weapon's. The first port fired laser, railgun and rocket on the right mouse | the weapons were ported (UH-T9) before the ground/air split was read from the source (UH-T11); brought back in line by UH-T74 | matches |
| V7 | Raw key and mouse events | a generated `HareControls` wrapper over an `InputActionAsset` (`Scripts/Input/HareControls.inputactions`), read by the same five scripts; a `Meta` action map (Leap, Shield, Flashbomb, Helper, Pause, Restart, Map, Debug, Menu) is scaffolded as data with keyboard and, where it makes sense, gamepad defaults, but nothing reads it yet. Restart (R) and Map (Tab) have no key recovered from the GML source (their systems are not built), so these are new placeholder defaults, not reverse-engineered ones, pending their own tickets. U.HARE-T50 reverse-engineered the rest of the room controllers' keys: Menu is Esc (`obj_game`, `obj_map`, `obj_story`, `obj_intro`: `room_goto(room_title)`), a new Fullscreen action is F1 (`obj_title`, `obj_game`), and Debug moved from its F1 placeholder to F12, `obj_game`'s `debug = !debug` key; the room flow reads Menu and Fullscreen | the known original bindings (A/D turn, W boost, S/Shift brake, Space/right mouse crouch-and-leap and Alt/middle mouse spin as the Player map's `Leap` and `Spin` since U.HARE-T53, Ctrl + left mouse secondary, Q/E cycle) are unchanged; a Gamepad control scheme was added alongside Keyboard&Mouse | matches (known bindings), U.HARE-T42; placeholder Meta defaults to settle in U.HARE-T53/U.HARE-T60/U.HARE-T67/U.HARE-T59/U.HARE-T19/U.HARE-T26 |
| V8 | Tuning as Create variables | `[SerializeField]` defaults and constants | interim, before data files | temporary, UH-T41 |
| V9 | Rules in GML objects | player, weapon and enemy rules in Core (UH-T39, UH-T40); the rocks and projectiles still hold theirs in Runtime | the rules are testable outside Unity and driven by commands | temporary for rocks and projectiles |
| V10 | `engine random` (`irandom_range`, `choose`) | the enemies draw from the world's seeded `IRandomSource`; the player's world takes an injected range and the rocks and visual effects still use `UnityEngine.Random` | testable, seedable randomness belongs in Core | enemies done (UH-T40); rocks and effects remain |
| V11 | Gibs and the wraith blur stand still while `paused` | the same (`FadingDebris`, `DeathParticle`); the first port kept them moving | matches gmlHARE's Step gating (UH-T16) | matches |
| V12 | `spr_grenade_explode` has 9 frames at image speed 0.2, so the blast lasts 45 steps | the same (`GrenadeExplosion.Frames = 9`); the first port assumed 10 frames and 50 steps | read from the sprite (UH-T16) | matches |
| V13 | Ten rooms in HARE.project.gmx, entered with `room_goto` | one scene per room (`Scenes/Rooms/<GML room name>.unity`: the camera rig and light, plus room_gamescreen's backdrop and obj_player in the three play rooms) and the `Main.unity` bootstrap, which holds only the `RoomFlowDirector`, all generated by `ProjectSetup` and listed in Build Settings bootstrap first, then the rooms in the project's order; room_boss1 has no scene, since nothing enters it ([RECONSTRUCTION](RECONSTRUCTION.md)) | scenes composed in code (rule 5); the bootstrap keeps the flow alive across room loads the way GameMaker's globals outlive a room | kept, U.HARE-T50 |
| V14 | A fixed 1920x1080 GameMaker window; nothing to scale to another aspect | the game camera holds the original's 1920x1080 reference aspect at whatever resolution it actually renders at (today: fullscreen at the display's own resolution; a later windowed or resizable mode, UH-T43/UH-T71, gets the same math for free), with a second camera clearing solid black behind it so a display wider or narrower than 16:9 gets pillarbox or letterbox bars instead of a stretched or cropped view (`CameraRig`, `LetterboxCamera`) | every display isn't 16:9, which the original's fixed-size GM8.1 window never had to handle; the composition itself is unchanged | kept, U.HARE-T44 |
| V15 | Backgrounds are stored as the source PNGs, drawn exactly | imported compressed at high quality (BC7 on desktop, `GmlArtImporter`); sprites stay uncompressed | eighteen full-screen backgrounds uncompressed would be over 150 MB of texture memory, all referenced by the one `HareArtManifest`; high-quality block compression is visually indistinguishable on these painted and photographic backdrops, and the sprites, whose edges and small sizes do show compression, are left exact | kept, U.HARE-T46 |
| V16 | The six `music_*` MP3s, streamed by GameMaker's multimedia player; the `sfx_*` WAVs at whatever rate and depth each file has, exported at 44.1 kHz | the music streams as Vorbis at quality 0.7 (re-encoded from the MP3s at import); every clip is resampled to 44.1 kHz | Unity re-encodes every compressed clip to its own format, and Vorbis is its streaming codec on every platform; the sfx resampling is what GameMaker's own export did | kept, U.HARE-T47 |
| V17 | `draw_text` draws each glyph as an exact quad of GameMaker's edge-to-edge packed atlas; boxes and alignment (`fa_center`, `fa_right`) are measured by `string_width`, the sum of the `shift`s | the same glyph pixels at the same pen positions (proven pixel for pixel by `HareTextPlayTests`), from an atlas repacked with 16 px of clear space around every cell; UI Toolkit's own measured box for a run of text can differ from `string_width` by a few pixels | UI Toolkit draws a bitmap glyph's quad about 5 px wider on each side than its cell, so on GameMaker's packed atlas every character dragged slivers of its neighbours onto the screen; the box measurement is UI Toolkit's layout, not the drawn text, so a screen that must centre or right-align exactly as the original did places the text by `GmlFontMetrics.StringWidth` | kept, U.HARE-T48 |
| V18 | No online play: one player at one keyboard | Online play beside the local game (`Online.unity`, U.HARE-T35): the kit's drop-in front end (uGUI and TextMesh Pro, from MindAttic Components, not Hare's UI Toolkit; TextMesh Pro's Essential Resources ship its OFL LiberationSans font, the only typeface file in the project) and its `online` tuning section (`MaxPlayers` 4, `SpawnSpacing` 1.5 units between seats along the room's centre line, `AimResendDegrees` 0.5), none of it reconstructed since the original has none; local play is unchanged | the owner's rule 9 (online-ready on the shared kit); the front end is the kit's, shared by every MindAttic game, so it keeps the kit's own look | kept, U.HARE-T35 |
| V19 | Each room steps once per frame at its room_speed (60 or 120); a slow frame slows the game down | the room flow steps on real time at the room's room_speed, catching up at most `rooms.MaxStepsPerFrame` (4) steps in one frame, so the fade lasts what it did (35 steps, 0.58 s in a 60-step room) whatever the display's refresh rate | Unity's frame rate is not the room speed; the cap keeps one long frame (a scene load) from skipping most of a fade | kept, U.HARE-T50 |
| V20 | Landing on a rock (`par_rock` collision) does `with(current_rock) motion_add(direction, 6)` and sets `player.depth = current_rock.depth`; landing and the hit/fling spin are per step | the push is left out; the ship's depth is not changed (`PlayerLook` draws it at depth 0, over every rock); landing tests the ship's sprite masks (`spr_rabbit_leap`'s ellipse in flight, `spr_rabbit_idle`'s rectangle for the move-out) against the rock's collider by physics query (`PerchProbe`), and `spin = 3` / `was_spinning`'s 3 a step are 360 degrees a second (`FlightTuning.SpinDegreesPerSecond`, `SpinRecoverDegreesPerSecond`) | the push is a no-op in the original: the rock's Step restores `speed = Speed` before it ever moves; the ship has no collider of its own so enemy hits stay as they are | kept, U.HARE-T53 |
| V21 | `LIVES` is a global; `player_hurt`'s death and the out-of-bounds rule both spend it, `reset_player` drops the ship in at `(room_width / 2, -sprite_height)` heading `270 + irandom_range(-30, 30)` at speed 5 with `sfx_flyover`, and with no life left `game_restart` starts the game over from its first room. `reset_player` sets `invulnerable = false`, `color = c_white` and `alarm_flashing = 0`, so a respawned ship does not flash: its `justspawned` protection (flash_time, 180 steps) shows only as the orange `ef_smoke` jet its Step puffs behind it. Alarm 5's flash (colorCycle 0..4: pink, amber, cornflower, cornflower, white, every 3 steps from a step after the hit) runs only while a hit's invulnerability lasts | the same: `LIVES` lives in the room flow's `GameSession`, which the ship spends through `SessionLives` (both deaths count against it), `game_restart` is a new `GameSession` and the flow's first room (`RoomFlowDirector.RestartGame`); the flash is Core's `ShipHealth.Color`. The `justspawned` jet (an orange `ef_smoke` 36 px behind the ship every step, `ShipLook`'s `SpawnJetPuffed`) and the ship's sprite, pose (`PlayerShip.Pose`) and colour are drawn by `PlayerLook` (U.HARE-T55) | U.HARE-T54's ticket asked for the respawn to flash; the source says it does not, and the rebuild follows the source | matches, U.HARE-T54, U.HARE-T55 |
| V22 | obj_player's Draw, Step and Alarms 2/3: the spr_rabbit_* pose (frame 0) at `scale` 1 in Alarm 5's `color`, the held `ground_weapon_asset` under it at gunX/gunY turned to mouse_angle while `state_idle` (the last ground weapon `next_ground_weapon` equipped or any `shoot_*` fired, so a Ctrl + left mouse shotgun blast leaves the shotgun in hand), Alarm 2's white `ef_smokeup` every 40 steps while flying, Alarm 3's `ef_spark` every 8 steps while `turning`, `irandom_range(1, 3)` choosing amber, pink or cornflower blue (the `c_yellow` default is never drawn), each one step behind the ship (`x - hspeed`) and `effect_create_below` | the same, drawn by `PlayerLook` from Core's `ShipLook` (its numbers in the `look` section); `ef_smoke` and `ef_spark` are ported from the runner's `Eff_Effect04`/`Eff_Effect07` with `pt_shape_spark` (`GmlEffects`). One difference: A and D share one Rotate axis, so holding both cancels the turn and its sparks, where the two GML key events both ran | the Input System's composite axis (V7); with one key held, which is how the ship is flown, the trail is the original's | kept, U.HARE-T55 |
| V23 | The weapons' and explosions' effects: `shoot_vulcan` and `shoot_shotgun` create `obj_muzzleflash` (spr_muzzleflash_new, alpha 0.95, image_speed 0.3, depth -2, gone at Animation End; its Step's fade is commented out) at the muzzle turned to the bullet's direction, the shotgun's to its *last* pellet's (aim plus that pellet's spread); `shoot_shotgun` adds `ef_smoke` size 1 `c_black` and `shoot_flamer` `ef_smokeup` size 0 `c_black`, both below; `obj_laser`'s Step puffs `ef_flare` size 0 cornflower blue below at the muzzle every step; `obj_railgun`'s Step puffs `ef_smoke` size 0 in railColor below at the muzzle and the rail's end every step while `railAlpha > 0` (its `image_alpha > 0.25` gate reads the sprite-less instance's own image_alpha, always 1, so it never closes), and `generate_swirl` puts an `obj_rail_swirl` (spr_railgun_swirl, depth -1, image_angle = the aim, blended railColor, image_alpha = railAlpha each step) every 56 px along the rail where `0 <= x <= room_width` and `0 <= y <= room_height`; `obj_rocket_explosion` animates spr_bigexplosion at 0.5 (40 steps) with `sfx_explode` when `in_view`; `obj_oval_explosion` (spr_explosion_oval, 0.3, `sfx_explode` in view) is created by nothing | the same: `MuzzleFlash`, the weapons' `GmlEffects` calls (`ef_flare` joins it, the runner's `Eff_Effect08`: ef_spark's particle in `pt_shape_flare`), `RailgunShot`'s swirls (inside the arena room's bounds) and smoke, `RocketExplosion`'s animation and sound; `OvalExplosion` is ported but, as in the original, nothing spawns it. Sprites not copied under `Resources/Hare/Sprites` are drawn from the imported art's `HareArtManifest`. The rail's smoke is kept running for the whole fade rather than "fixed" to stop at `railAlpha` 0.25: the source may have meant that, but what it does is a working effect, so it is matched | the effect objects are read straight from HARE.gmx (U.HARE-T31) | matches, U.HARE-T31 |
| V24 | The pickups and the kicked-up scraps: every par_enemy kill branch that calls `random_pickup(x, y)` rolls `irandom_range(1, 2)`: 1 is an `obj_carrot` on a further 1-in-4, 2 always an `obj_coin` (its own 1-in-2 roll is commented out). The carrot (spr_pickup_health) never falls (its `y += 1` is commented out), hovers every 8 steps (`hoverY` swinging -4..4, `y += hoverY`), pings a size-0 red `ef_ring` every 120 steps and gives the ship that touches it `HP += 1` below MAXHP with `sfx_powerup` and a size-1 red ring. The coin rolls 1-100: blue (<= 75, 5), silver (< 98, 10), gold (20); it falls 1 px a step, sparkles an amber/pink/cornflower `ef_spark` a step behind its motion every 5 steps, is gone past `room_height + sprite_height`, and adds its value to COINS with a ring in its colour, the hundredth coin being `LIVES += 1`, `COINS = 0` and `sfx_extralife`. Both are pulled by `move_towards_point(player, 4)` while `distance_to_point(player) < 48` from their bounding box, and GameMaker keeps that speed and heading once the ship is out of range (and while `paused`, since only the objects' own code checks it). `player_leap` and the `par_rock` landing kick up a size-0 grey `ef_firework` and `spawn_chips`' 6-12 `obj_chip` 32 px along the ship's image_angle (the landing's the one it flew in at, before `normal_detect` turns it); every vulcan round leaves an `obj_cartridge` and every shotgun blast a `spr_ammo_shell` one at gunX/gunY. Chips and casings rise and fall on their Alarm 0 (every 3 / 2 steps) and spin every step; nothing collides with them; `setup_level` clears the pickups but not them | the same: Core's `PickupField` (`Pickups/`, numbers in the content's `pickups` section, the original's random draws in its order from one seeded source) with `RestoreHealth`/`PlayerHealed` on the ship and the session's `CollectCoin`; Runtime's `PickupDirector` steps it after the enemies, draws `PickupView`/`ScrapView`, and the ship's own mask (`PerchProbe.ShipMaskHits`: spr_rabbit_leap's ellipse in flight, spr_rabbit_idle's box perched) decides what it touches. `ef_ring` joins `GmlEffects` from the runner's `Eff_Effect01` with its `pt_shape_ring`. With several ships the magnet pulls toward the nearest; COINS are counted only under the room flow (a room run alone has no session). `obj_helper`'s collisions and the Scrap Mental's scattered `random_pickup` wait for those objects | read straight from HARE.gmx (U.HARE-T23) | matches, U.HARE-T23 |

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
