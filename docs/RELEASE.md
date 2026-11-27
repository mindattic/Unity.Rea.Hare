---
layer: release
status: living
---
# Unity.Rea.Hare — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
A Unity 6 rebuild of the GameMaker 8.1 arcade shoot-'em-up *HARE* (*H.O.P., High Orbit Patrol*): a robotic rabbit flies, perches and fights in low orbit over a drifting asteroid field (BIBLE).

## 2. The milestone: Unity.Rea.Hare faithful port {#milestone}
- **Milestone:** MS-73, "Unity.Rea.Hare faithful playable port"
- **Published means:** Development and Release Windows players at `R:\MindAttic\Apps\Unity.Rea.Hare\{Debug,Release}`
  (internal; not on a store).
- **The natural stopping point:** the port is already built; this milestone confirms it as a deployed, playable,
  bug-fixed Release player that plays as the original did, then stops. Owner rule: resurrect the original
  faithfully, fix bugs only, never change the gameplay.

## 3. Done when (acceptance checks)
1. build — `run.ps1 -Redeploy` builds both Windows players clean on main.
2. exists — `R:\MindAttic\Apps\Unity.Rea.Hare\Release` holds a player that launches.
3. signoff — the owner plays the Release player end to end: title, campaign through the levels, game over or victory.

## 4. Epics, in priority order
| Priority | Epic (ticket) | What it delivers | Features |
|---|---|---|---|
| 900 | U.HARE-T138 Faithful port | Confirmed bugs fixed, a recorded Release play-through | U.HARE-T139 Port bug fixes (990), U.HARE-T140 Release play-through (980) |
| 800 | U.HARE-T141 Windows release | Players deployed on R:, owner sign-off | U.HARE-T142 Players on R: (890), U.HARE-T143 Owner sign-off (880) |

## 5. Out of scope (not before this milestone)
- Any change or improvement to gameplay, controls feel or content: the owner rule is resurrect the original faithfully and fix bugs only.
- New enemies, levels, weapons or modes beyond the original.
- Mobile, console or store builds.

## 6. Current verified state
- End-to-end re-verification passed: build and scripted run, every room reachable, mechanics unchanged (e5c25c5, U.HARE-T121).
- Control rebinding is built and tested (U.HARE-T120, T129, T130); one real bug left in it (U.HARE-T123).
- The shared run menu deploys to R: (75d6643, MAT-T311); nothing is under R:\MindAttic\Apps yet.

## 7. Decision record
- 2026-11-27 — charter drafted from README/BIBLE/RECONSTRUCTION/tickets; Legion vote: Approve, plurality quorum reached, 9/9 voters answered, all Approve; no dissent (the rest had no provider key).
- Triage: 3 open -> done 0, duplicate 0, wont-do 0, kept 3 (U.HARE-T133 bughunt reworded to a bounded task); new tasks 2 (Release play-through, owner sign-off).