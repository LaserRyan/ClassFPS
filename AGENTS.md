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
  SecondaryAttack, whose default implementation does nothing. Fire is per click.
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

- `SwordWeapon.cs` extends the existing `Weapon` base and uses the existing per-click
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
