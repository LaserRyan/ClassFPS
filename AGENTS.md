# ClassFPS project handoff

## Overview

ClassFPS is an early single-player Unity FPS prototype. The current playable scene is
`Assets/MovementTest.unity` (directly under Assets, not Assets/Scenes).

- Unity: **6000.3.26f1**.
- Rendering: **URP 17.3.0**; use compatible Lit/Unlit shaders.
- Input: **Input System 1.20.0**, new backend only (`activeInputHandler: 1`).
  Current gameplay code reads `Keyboard.current` and `Mouse.current` directly.
- Scripts: `Assets/Scripts`; editor-only tools: `Assets/Scripts/Editor`.
- Reusable materials: `Assets/Materials`; prefabs folder: `Assets/Prefabs`.
- Git remote: `https://github.com/LaserRyan/ClassFPS.git`, branch `main`.

Keep changes focused. Preserve existing movement, damage, crosshair, cursor, and
weapon extensibility. Do not change packages or project settings without a concrete
need. Use Unity Editor APIs or explicit editor setup commands for scene changes;
do not manually edit scene YAML. Track Assets and its .meta files, Packages, and
ProjectSettings; keep generated Unity folders excluded through .gitignore.

## Recorded design decision

**Each hero has one unique weapon and a passive. No weapon switching is planned.**

The shared weapon abstraction supports differences between heroes, not a weapon
inventory or switching system. Preserve support for guns, melee weapons/swords,
spells, and shields without assuming every attack uses hitscan. Heroes and passives
are a future design direction; they are not implemented yet. Do not add weapon
switching, ammo, reloads, multiplayer, or hero systems without a new task requesting
them.

## Current implementation

### Movement and cursor

`FPSController.cs` on Player uses CharacterController, with no Rigidbody:

- Facing-relative WASD, normalized diagonals, delta-time movement, grounded jumping,
  gravity, obstacle collisions, and ceiling collision handling.
- Mouse yaw rotates Player; clamped pitch rotates its directly parented Main Camera.
  Input System mouse delta is displacement: do not multiply it by delta time.
- Gameplay start and clicking the Game view capture/hide the cursor. Escape and
  disabling the controller release/show it. Focus loss releases capture.
- Movement/look/jump input stops while released; gravity continues.
- `CanAcceptGameplayInput` includes the current frame's capture decision. The click
  that recaptures is suppressed, including for attacks.

Player's intended CharacterController configuration: height 2, radius **0.4**,
center (0, 1, 0), step offset 0.3, slope limit 45. Main Camera's local position is
(0, 1.6, 0), with zero local rotation. Attach the controller to Player itself, not a
separate child GameObject.

### Input, weapons, damage, and feedback

- `PlayerCombatInput.cs` routes mouse buttons to the equipped `Weapon` in LateUpdate,
  after movement/look Update. Left-click calls PrimaryAttack; right-click calls
  SecondaryAttack, whose default implementation does nothing. Holding left-click retries PrimaryAttack each frame; weapons gate cooldown/recovery. Capture clicks and holds through blocked gameplay require release before attacking.
- `Weapon.cs` is a small abstract base exposing Inspector damage, range, and cooldown.
  `WeaponAttackContext` carries the owner and aim ray without prescribing hit detection.
- `HitscanGun.cs` implements one primary hitscan attack using the camera-center ray
  from the camera position. It ignores Player hierarchy colliders and triggers,
  selects the nearest valid surface, and applies damage only to that surface.
  Walls therefore block damage. Default damage/range/cooldown: 25 / 100 m / 0.2 s.
- `IDamageable.cs` separates damage recipients from weapon implementations.
  `TargetHealth.cs` accepts damage, finds health through child-collider parent lookup,
  and deactivates the target root at zero health. Default health is 100; re-enabling
  resets health. Four accepted default shots defeat a target.
- `CombatCrosshair.cs` draws a centered crosshair with brief yellow firing feedback
  and red successful-damage feedback. No feedback is emitted for rejected cooldowns.

### Weapon ammo and reload

- `WeaponAmmo.cs` is an optional finite magazine component. HitscanGun requires it;
  SwordWeapon explicitly exposes null Ammo for infinite melee. Inspector defaults:
  12-round capacity, 1.5 s reload, automatic reload enabled. Reserve is unlimited.
- Shared Weapon.TryBeginAttack spends one round only after cooldown/block checks.
  Emptying the magazine starts automatic reload; primary attacks are rejected while
  reloading. Holding fire resumes when reload and cooldown permit. R calls virtual
  Weapon.Reload while captured/alive. Melee ignores R. SecondaryAttack/right-click
  remains independent for future hero/weapon interactions; no ability added now.
- Reload continues while cursor is released, uses scaled time, and is cancelled by
  gun/ammo disable or death/reset. Respawn refills magazines. No reload audio or
  animation, reserve pickups, inventory, or networking added.
- `WeaponAmmoHUD.cs` draws a bottom-right current/capacity counter or infinity for
  melee, plus reload status. It follows EquippedWeaponChanged and Ammo.Changed;
  health HUD/crosshair are preserved. No per-frame ammo polling.
- **Tools > ClassFPS > Set Up Weapon Ammo HUD** adds/reuses player gun resources and
  one HUD, preserving tuning and saving through Editor APIs. Run this in Unity to
  configure existing scene guns; the agent has not executed setup or edited YAML.
- `AMMO_TEST.md` covers setup, manual/automatic reload, holds, disable/death/respawn,
  infinity, audio, and cursor checks. Runtime compilation in Editor/development/
  release configurations and separate Editor compilation passed. Unity gameplay
  and HUD rendering remain unverified by the agent.

### Player health and HUD

- `PlayerHealth.cs` implements `IDamageable` on Player. Inspector Maximum Health
  defaults to 100; a new session starts full and current health stays within zero
  and maximum. Invalid/nonpositive damage is ignored. Health itself has no death
  behavior; optional PlayerLifecycle now supplies player death/respawn separately.
- `HealthChanged(current, maximum)` drives the separate `PlayerHealthHUD.cs`.
  It caches fill/text from events and draws a bottom-left bar using OnGUI, matching
  the crosshair's existing UI approach. It subscribes on enable, unsubscribes on
  disable, and refreshes immediately on binding. No per-frame health polling.
- Health has no input/UI/local-player/global dependencies. `SetMaximumHealth`
  permits future actor-specific values; `RestoreFullHealth` explicitly restores.
  Disable/re-enable preserves health. No networking or authority system is added;
  future multiplayer work must bind the local HUD and authorize damage separately.
- `PlayerHealthDebugInput.cs`: F8 applies configurable test damage (default 25);
  F9 restores full health. Per-press input runs in LateUpdate only in the Editor or
  DEVELOPMENT_BUILD and requires `CanAcceptGameplayInput`, including recapture-frame
  suppression. Release builds contain no test-input logic. No audio is added.
- **Tools > ClassFPS > Set Up Player Health HUD** adds/reuses health/debug components
  and one HUD child, binds the player reference, preserves tuned values, and saves
  through Editor APIs. It leaves equipped weapons, movement, targets, and obstacles
  unchanged. No health setup/scene changes have been executed by the agent.
- Exact instructions and pending gameplay checks: `PLAYER_HEALTH.md`. Validate
  setup repeat safety, full start, damage/restore/clamping, custom maximum, HUD
  subscription lifecycle, cursor/focus/recapture gating, and weapon/movement behavior.
- Agent checks: runtime C# compiled against installed Unity/Input System assemblies
  in Editor, development, and release configurations; Editor scripts compiled
  separately. All passed with Inspector-assigned-field warnings and an unused test
  damage field warning in release. No Unity Play mode or build execution was run.

### Death, respawn, and training bot

- `PlayerLifecycle.cs` subscribes to PlayerHealth, enters death once, blocks further
  damage and gameplay, resets attacks/presentation, and respawns after one scaled
  timestamp (Inspector delay defaults to 3 seconds). No coroutine timers accumulate.
- `FPSController.SetGameplayBlocked` stops movement/look/gravity while retaining
  Escape, focus, and cursor processing. `ResetAtSpawn` temporarily disables the
  CharacterController, teleports to spawn position/yaw, clears vertical velocity and
  camera pitch, and suppresses gameplay input for that frame. Respawn never requests
  cursor capture. Existing disabled weapons remain disabled across respawns.
- Weapon exposes shared reset/block hooks; SwordWeapon cancels unfinished swings.
  Lifecycle also hides active gun/sword presentation objects while dead and restores
  only those previously active on respawn; unequipped visuals stay hidden.
  GunPresentation and SwordPresentation expose visual resets. PlayerDeathHUD draws
  the death/countdown message separately from health. F8/F9 cannot bypass death.
- `TrainingBot.cs` reuses PlayerHealth for configurable actor health and IDamageable
  child-collider lookup, and destroys its root at zero. It never tracks Player;
  muzzle.forward determines shot direction. Inspector defaults: 1 s interval,
  4 m/s speed, 25 damage, 5 s lifetime. Existing shots survive bot destruction.
- `StraightProjectile.cs` is reusable by future spells: Launch takes owner,
  direction, speed, damage, lifetime. Start overlaps and nearest-hit sphere sweeps
  catch impacts, ignore owner/trigger colliders, and consume each projectile once.
  It needs no Rigidbody/collider and destroys itself on impact/timeout.
- **Tools > ClassFPS > Set Up Respawn and Training Bot** reuses health HUD setup,
  adds/reuses lifecycle, death HUD, spawnpoint and bot, creates URP materials and
  `Assets/Prefabs/TrainingProjectile.prefab`, and saves through Editor APIs. First
  setup places Player at (7, 0.05, 0), beside the bot's x=10 firing lane. Bot starts
  at (10, 0, 8), facing -Z. Repeated setup preserves tuning and poses.
- Existing Set Up Shooting Test now explicitly enables the gun, disables sword,
  and restores gun/hides sword presentation, allowing separate weapon tests using
  Editor setup only. No gameplay switching was added.
- `TRAINING_TEST.md` has exact setup, damage/death/repeated respawn/cursor tests,
  projectile wall/timeout/high-speed tests, and gun/sword bot defeat checks.
- Agent verification: standalone runtime compilation in Editor/development/release
  configurations and separate Editor compilation passed. Source/docs whitespace
  checked. No Unity setup/Play mode/physics/rendering/build execution was performed.
  Existing user scene changes were preserved; no scene YAML was manually edited.

### Player weapon test audio

- `PlayerWeaponAudio.cs` is a separate player-only audio subscriber. Accepted
  HitscanGun.ShotFired plays `Assets/Audio/pew2.wav`; accepted SwordWeapon.SwingStarted
  plays `Assets/Audio/Shing.wav` once at swing start, including windup. Damage/hit
  feedback does not trigger sound. Rejected attacks/recapture clicks stay silent.
- **Tools > ClassFPS > Set Up Player Weapon Audio** adds/reuses a Player child with
  one AudioSource and the audio component, wires existing weapons/clips, and saves
  with Editor APIs. Source is non-spatial (spatialBlend 0), no loop/play-on-awake.
  Inspector clip and per-weapon volume tuning is preserved on repeated setup.
- Audio unsubscribes/stops on disable and stops on player death. Training bot and
  projectile audio remain absent. No spatial audio system or audio packages added.
- Run setup in Unity after script import; agent has not executed setup or listened
  in Play mode. Check one sound per accepted shot/swing, held attacks repeat at readiness, no sound
  on damage feedback or rejected clicks, silent bot, and no duplicate audio after
  death/respawn. Instructions also appear in `TRAINING_TEST.md`.

### Gun visuals

- `GunPresentation.cs` is separate from input, damage, and the shared weapon base.
  It subscribes to HitscanGun's gun-specific `ShotFired` endpoint event, emitted only
  for accepted shots. HitscanGun's original camera-based hit detection is preserved.
- A camera child named Gun Presentation contains the replaceable Placeholder Model,
  a muzzle/flash, and a reusable world-space LineRenderer tracer. Primitive colliders
  are removed; visual objects use Ignore Raycast. Materials are shared URP assets.
- Tracers run from the offset muzzle to the camera-ray hit point or maximum range.
  They are visual only and cannot damage anything.
- Small bounded backward/upward model recoil recovers exponentially. Camera/Player
  aim is unaffected. Inspector settings expose pose, recoil, recovery, and durations.
- Effects are reused and hidden by timers; disabling presentation unsubscribes and
  clears effects/recoil. No per-shot effect objects or materials are allocated.
- Default pose clears the near plane. This uses a normal camera child, so the model
  can intersect world geometry when pressed against walls; no overlay camera exists.

### Scene appearance

The saved scene currently contains shooting components, gun presentation, and three
targets under Shooting Targets. Reusable materials already exist for gray Ground,
blue PlatformSmall, orange PlatformTall, purple Wall, red/green/yellow targets, and
gun body/trim/flash/tracer. One yellow target is behind the wall from Player's spawn.

## Editor setup commands

Stop Play mode, open `Assets/MovementTest.unity`, and allow scripts to import. If
needed choose **Assets > Refresh**. Commands are in Unity's top menu bar:

1. **Tools > ClassFPS > Set Up Shooting Test**: equips/configures shooting, colors
   obstacles, creates targets/materials, and saves the scene. Reuses existing objects.
2. **Tools > ClassFPS > Set Up Gun Presentation**: creates the camera-mounted visual,
   effects, and materials, connects references, and saves the scene. If presentation
   already exists, selects it without resetting configuration.

Both setups have already been applied in the current saved scene. Use them for
reconstruction as needed, rather than rerunning them as a requirement every session.
If Tools is missing, check **Window > General > Console** for compilation errors.

Detailed setup/configuration and test procedures:
- `SHOOTING_TEST.md`
- `GUN_PRESENTATION.md`

## Validation and next steps

Available checks completed during this session:

- Runtime C# compiled against installed Unity and Input System assemblies without
  UnityEditor references; Editor scripts compiled separately against runtime.
- Git diff whitespace checks passed.
- Saved scene/component and material presence were inspected after setup.

No connected live Unity Editor tool was available. Standalone compilation is not a
Unity Play mode test. Rendering, recoil feel, physics, and gameplay regressions were
not interactively verified by the agent. Do not report those as tested.

Next work should start with Play mode validation:

1. Check WASD, diagonal speed, jumping, grounded contact, collisions, and look limits.
2. Check initial capture, Escape, focus loss, recapture-click suppression, and cursor
   restoration when FPSController is disabled. Attacks must stop with gameplay input.
3. Check cooldown, target damage/defeat, child colliders, and walls blocking shots.
4. Check gun position/near-plane clearance, flash, hit/miss tracer endpoints, bounded
   recoil/recovery without aim changes, and disabling presentation during an effect.
5. Tune visual settings only after observing them. Later hero work must follow the
   one-unique-weapon-plus-passive decision, with no switching system.

## Git workflow and session state

### Sword prototype implementation (2026-10-15)

- Follow-up fix: guard `Collider.ClosestPoint` to primitive/convex colliders only;
  non-convex meshes and terrain use collider surface probes for target contacts.
  Occlusion casts run both directions to catch back-facing mesh surfaces. Complex
  concave damage receivers use sampled contacts; prefer primitive child hitboxes
  for predictable melee coverage. Runtime/Editor compilation passed after this fix.
  Retest ordinary targets and non-convex mesh walls in Play mode; not agent-verified.
  User has since run Unity setup/testing; do not assume the scene is still gun-only.

- `SwordWeapon.cs` extends the existing `Weapon` base and uses the existing shared
  `PlayerCombatInput`; no inventory, switching controls, hero system, or audio exists.
- Inspector tuning: inherited damage/reach (`Range`)/cooldown, plus full attack arc,
  windup, active duration, and recovery. Defaults: 35 damage, 2.2 m reach, 100-degree
  arc, 0.18 / 0.16 / 0.30 s phases, zero additional cooldown.
- During active time, an overlapping spherical sector follows camera aim. Parent
  `IDamageable` lookup and a per-swing receiver set prevent duplicate damage from
  repeated frames or child colliders. Visibility queries block damage through solid
  obstacles; containing blockers are checked explicitly. Player colliders and
  triggers are ignored. The query uses default raycast layers.
- Swing duration gates attacks. Disabling the weapon, releasing cursor capture,
  focus loss, or disabling movement cancels pending damage. A low-frame-rate step
  crossing the active interval still evaluates the attack once.
- `SwordPresentation.cs` separately animates a collider-free placeholder pivot
  through windup/slash/recovery; pose angles are exposed. Damage remains independent
  of the model. Accepted swings and damage reuse `Weapon.AttackFeedback`.
- **Tools > ClassFPS > Set Up Sword Test** (`SwordTestSetup.cs`) equips the sword,
  disables player HitscanGun components, hides gun presentation, and creates/reuses
  the sword and shared URP materials. It preserves existing targets, obstacles,
  gun scripts/assets, and tuned sword settings. It saves through Unity Editor APIs.
  Repeated runs do not add duplicate swords or visuals. Existing targets can be
  approached on foot; no additional targets are created.
- Setup has **not** been executed by the agent; the saved scene still has its prior
  gun configuration until the user runs this command. No scene YAML was edited.
- Runtime C# and Editor C# compiled separately against installed Unity 6000.3.26f1
  assemblies (runtime also references installed Input System). Standalone compiler
  warnings for Inspector-assigned fields are expected. Whitespace checks passed.
- No connected live Editor was available. Setup execution/repeat safety, rendering,
  physics, and gameplay were **not** interactively tested. `SWORD_TEST.md` documents
  exact setup and remaining Play mode checks: reach/arc, delayed active damage,
  recovery, held clicks, repeated hits, multiple child colliders, walls, capture and
  recapture, cancellation, and movement regressions. Run those before visual tuning.

Recent commits at handoff:
- `4924eaf` Add placeholder gun, shot effects, and visual recoil
- `4d92286` Add hitscan shooting, target health, and obstacle colors
- `ae4b789` Initial FPS movement prototype

Save Unity assets before staging. For this handoff document:

```powershell
cd C:\Users\vitic\ClassFPS\ClassFPS
git add AGENTS.md
git diff --cached --stat
git commit -m "Document project handoff and hero weapon design"
git push origin main
```

For subsequent feature work, stage intended Assets/code/docs, review the staged diff,
then commit and push. Avoid blindly staging unrelated files. At inspection, one
untracked root file had a name beginning `can shooting, target health, and obstacle
colors` followed by an unusual character. It was left untouched; inspect its contents
before deciding whether it should be kept or removed.

Git was originally initialized under the sandbox account. If the user's PowerShell
reports dubious ownership, the directory-specific fix is:

```powershell
git config --global --add safe.directory C:/Users/vitic/ClassFPS/ClassFPS
```

Do not overwrite the existing repository/remote or force-push. This handoff document
has not been committed or pushed automatically.
