# Legacy Unity POC scripts (reference only)

Copied from `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\Unity\HARE\Assets\Scripts` (Unity 5.4.1f1,
UH-T4). Kept as `.cs.txt` so Unity never compiles them — they use Unity 5-era APIs removed in
Unity 6 (`Rigidbody2D.isKinematic` as a bool, `Transform.FindChild`, etc.) and would block the
build if dropped into `Assets/` as live scripts. They capture working ideas worth carrying
forward — the player's in-air/on-ground state machine with magnetic rock-landing
(`PlayerMovement.cs.txt`), mouse-aim weapon firing with raycast hit detection and
effect/sound state machine (`WeaponBase.cs.txt`) — but every new script should be written fresh
against current Unity APIs and the GML-sourced mechanics in `../REVERSE_ENGINEERING.md`, not by
resurrecting these files as-is.

| File | Original path |
|---|---|
| `Camera/SmoothCamera.cs.txt` | `Scripts/Camera/SmoothCamera.cs` |
| `Game/GameWorld.cs.txt` | `Scripts/Game/GameWorld.cs` |
| `Game/ResetGame.cs.txt` | `Scripts/Game/ResetGame.cs` |
| `Player/PlayerBase.cs.txt` | `Scripts/Player/PlayerBase.cs` |
| `Player/PlayerMovement.cs.txt` | `Scripts/Player/PlayerMovement.cs` |
| `Rock/RockBaseOld.cs.txt` | `Scripts/Rock/RockBaseOld.cs` |
| `Rock/RockMovement.cs.txt` | `Scripts/Rock/RockMovement.cs` |
| `Weapon/BulletBase.cs.txt` | `Scripts/Weapon/BulletBase.cs` |
| `Weapon/WeaponBase.cs.txt` | `Scripts/Weapon/WeaponBase.cs` |
