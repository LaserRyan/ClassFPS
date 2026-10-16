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

**Each hero has one fixed primary weapon, one secondary action, and one passive.
No gameplay weapon switching is planned.**

The shared weapon abstraction supports differences between heroes, not a weapon
inventory or switching system. Preserve support for guns, melee weapons/swords,
spells, and shields without assuming every attack uses hitscan. Prototype hero
selection/configuration now exists; secondary abilities and passives remain future
work. Do not add weapon
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

### Configurable prototype heroes

- `HeroDefinition.cs` stores stable ID, display/class labels, maximum health, speed,
  jump height, gravity, fixed HeroKit prefab, optional avatar/icon, and reserved
  secondary/passive prefab slots. Prototype future slots are null and not executed.
- `WeaponDefinition.cs` plus GunWeaponDefinition/SwordWeaponDefinition own common
  tuning and type-specific magazine/reload or swing phase/arc settings. Weapon's
  effective Damage/Range/Cooldown and sword/ammo getters read the assigned asset.
  Legacy component tuning is a fallback only when no definition is assigned; custom
  Inspectors hide ignored fallback fields. No gameplay writes to shared assets.
- `HeroSelection.cs` adds an OnGUI selection before gameplay. While selecting,
  cursor stays released, movement/look/combat and player damage are blocked. After
  choosing, exactly one HeroKit is instantiated under the existing camera, shared
  health/movement are configured, and Player is placed at its existing spawnpoint.
  Optional avatar is presentation under Player; missing model/icon/audio is safe.
- Selection locks for the Play session. Death/respawn keeps the same hero/kit and
  refreshes/reset its instance state, rather than rebuilding or switching heroes.
  Lifecycle.RefreshWeapons supports the spawned kit. The selection/capture click
  cannot attack; normal hold-to-attack and release-after-death rules remain.
- **Tools > ClassFPS > Set Up Prototype Hero Selection** generates/reuses two hero
  assets in `Assets/Definitions/Heroes`, two tuning assets in
  `Assets/Definitions/Weapons`, and kit prefabs in `Assets/Prefabs/Heroes`. Initial
  values/presentation/audio migrate from legacy scene weapons before their removal.
  The gun hero copies current player stats; sword adds 50 health, 1 speed and 0.4
  jump height. IDs `prototype.gun` / `prototype.sword` remain stable across reruns.
- Setup preserves existing asset edits, bot, spawnpoint, HUDs, obstacles and targets.
  It does not leave disabled alternative weapons on Player. Validate Prototype Hero
  Assets checks stable unique IDs and kit/definition consistency inside Unity.
- Legacy Shooting/Sword/Gun Presentation/Player Weapon Audio setup commands reject
  hero mode explicitly. Use **Tools > ClassFPS > Use Legacy Gun Test** to deliberately
  remove selection and reconstruct legacy tests. Hero definitions/kits are retained.
- `HERO_TEST.md` documents asset configuration and both-selection gameplay checks.
  User has run hero setup: saved hero definitions, kit prefabs and the converted
  scene are now present. Follow-up fixes use a Unicode escape for the infinity HUD
  label and automatically migrate original pew references to pew2 via Editor APIs.
  Saved kit clip GUIDs were inspected and verified as pew2. Audible playback and
  full gameplay regression testing were not performed by the agent.
  Agent verification: standalone runtime compilation (Editor/development/release),
  separate Editor compilation, and source/doc whitespace checks. No Unity setup,
  asset-generation validator, Play mode, rendering/audio/physics or game build was
  executed by the agent. Run hero setup in Unity to generate/save the actual assets.

### Fire Spell Prototype

- `FireSpellWeapon.cs` is the third selectable primary, backed by FireSpellDefinition.
  The existing hero setup adds stable ID `prototype.fire`, class Caster Prototype,
  default 110 health / 5.25 speed / 1.2 jump / -20 gravity. Secondary/passive stay null.
- Fire tuning asset under `Assets/Definitions/Weapons/FireSpellPrototype.asset`:
  35 damage, 80 m maximum camera aim distance (Range), 0.35 s cast delay, 0.6 s
  cooldown measured **after projectile release**, 12 m/s, 0.2 m radius, 4 s lifetime.
  Progress/cooldowns and launched projectile values remain instance state only.
- Held primary repeats after windup plus post-release cooldown, with no overlapping
  or queued casts. Releasing primary allows an accepted cast to finish; death,
  disable/removal, cursor release/focus loss or blocked gameplay cancels windup.
  Existing release-after-death/recapture protections remain. Fire now uses the shared
  heat-based ammo pool described below rather than infinite ammo.
- Release samples the current camera aim ray, then fires from the offset origin
  toward the nearest surface/max aim point. A swept-radius camera-to-origin check
  consumes blocked shots at a safe contact rather than spawning beyond nearby cover.
- StraightProjectile now delegates its existing start-overlap/nearest-sweep query to
  ProjectileCollision and exposes launch/impact events plus an optional radius
  override. The bot's five-argument Launch uses its original serialized radius and
  remains straight-firing. Owner checks tolerate destroyed casters; launched shots
  outlive death/removal. Each projectile damages one receiver once via IDamageable.
- FireballPresentation sizes its body/trail to collision radius and spawns a brief
  FireImpactPresentation on impact; temporary objects expire. FireSpellPresentation
  handles charge and optional cast/release audio; impact audio is optional on the
  impact prefab. All clips start null, 2D/non-spatial. No gun/sword sound assigned.
- Re-run **Set Up Prototype Hero Selection** to create/reuse fire hero/weapon assets,
  FireSpellPrototypeKit/Fireball/FireImpact prefabs under `Assets/Prefabs/Heroes`,
  and two fire materials. Existing prototype tuning/prefabs and scene objects remain.
  Hero UI scrolls if needed for three entries; lifecycle cancels/resets/hides fire
  presentation during death and retains the selected kit through respawn.
- `FIRE_SPELL_TEST.md` covers setup, cast timing, holds/releases, aim/close walls,
  bot/child hitboxes, expiry, cancellation, optional audio and repeated respawn.
  User has run setup; the saved scene now references the fire hero and its generated
  definition/material/prefab assets are present. Cast/release/impact clips remain
  unassigned. A separate looping travel sound is feasible but not implemented yet.
  Agent checks: Runtime compilation in Editor/development/release configurations,
  separate Editor compilation, source/docs whitespace. Unity setup/Play mode,
  physics/rendering/audio and game build tests remain for the user. No splash,
  burn, homing, secondary/passive mechanics, networking or friendly-fire rules added.

### Shared fire energy / overheat

- `FireEnergy.cs` owns one per-kit `HeatAmmoState` used by both FireSpellWeapon
  actions. No separate flamethrower fuel component/display existed in this checkout;
  this milestone adds the minimal continuous MB2 flame against the same resource.
- FireSpellDefinition holds provisional maximum 100, fireball cost 25, flame drain
  30/s, regeneration delay 2 s, regeneration rate 25/s, overheat duration 3 s.
  Flame tuning is 30 damage/s, 5 m reach, 0.35 m swept radius. All editable in the
  existing fire definition; instantiated pool configuration is a per-session snapshot.
- MB1 reserves its cost on acceptance; insufficient ammo rejects before cast effects.
  Cancellation before release refunds the reserved amount once, capped at maximum.
  Release commits the reservation. Spending the last ammo starts heat but never
  cancels the accepted cast. Existing cooldown still starts at projectile release.
- MB2 is held input, continuous first-blocking-surface volume damage. Drain returns
  funded seconds, so the final partial frame deals only DPS times funded time before
  exhaustion stops flame. Attacks are mutually exclusive: a paid cast blocks flame,
  and held active flame blocks casts until MB2 is released. Simultaneous fresh input
  gives MB1 priority. No splash or separate fuel pool is added. Configurable burn
  is described in the later fire prototype burn section.
- Consumption restarts normal regeneration delay; afterward regeneration is incremental.
  Zero triggers one overheat deadline. During heat, regenerate immediately while both
  new attacks stay locked; defaults restore 75 energy in 3 seconds. At expiry both
  buttons must be observed released before either resumes. Refunds preserve an
  already-started heat timer; they do not erase/restart it. Rejected attempts do not
  extend heat or delay regeneration. Resource/cooldown clocks use scaled game time.
- Shared input observes button state and supports opt-in held secondary behavior;
  gun/sword secondary behavior is unchanged. Cursor/death/focus fresh-press gates
  cover both buttons. Lifecycle RestoreResources refills heat ammo and clears heat
  and its release latch on respawn, while preserving normal input release protections.
- Ammo HUD subscribes to resource changes: shared current/max, red OVERHEATED with
  countdown, then Release MB1 + MB2 when needed. No separate fuel display. Fire
  presentation reuses one line for flame and resets it on release/death/disable.
- Re-run **Set Up Prototype Hero Selection** to upgrade an existing fire prefab,
  persist new definition fields, and add one resource/line without duplicates. Existing
  assets, sounds and other heroes are preserved. Unity scene YAML was not edited.
- `HEAT_AMMO_TEST.md` has exact setup and gameplay checks. Automated checks: 44
  deterministic resource-state assertions via `Tests/RunHeatAmmoChecks.ps1`; Runtime
  and Editor compilation. Native Unity cast/physics/UI/input/audio and setup repeat
  checks remain manual. See final verification record in the test guide.

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

### Fire prototype burn

- FireSpellDefinition now exposes Burn Percent (default 20) and Burn Duration
  (default 3 seconds). Percent means additional damage per second relative to
  fireball base damage or flame base DPS, not target maximum health. Zero disables.
- BurnStatus lives on the IDamageable receiver, so child colliders share one burn.
  Accepted fire hits refresh duration without stacking; the stronger active rate
  wins. Burn outlives the caster. Disabled targets clear it; player full restoration
  (including respawn) clears it. Bot projectiles default to no burn.
- No burn visuals/audio added. See BURN_TEST.md for pending Unity gameplay checks.

- Deferred presentation (lower priority): outwardly visible burn indicators on affected actors and a burn status indicator on the player's health bar when hit. Neither is implemented yet.

### Latest session handoff (2026-10-16)

This section supersedes older historical statements above about missing hero,
secondary, ammo, burn, and git features.

- Three selectable prototypes exist: Gun, Sword, and Fire Spell. One fixed kit per
  hero, no gameplay switching. Fire MB1 casts a projectile; held MB2 fires flame.
- Fire shares one heat/energy pool, incremental regeneration, overheat lockout and
  release-both requirement. Accepted final-ammo fireballs finish; canceled windups
  refund once. Respawn resets resources. MB1/MB2 cannot execute together: active
  flame owns attacks until MB2 release; casts block flame through release.
- Burn percent/duration are editable on FireSpellPrototype.asset. Default 20% of
  base attack damage per second for 3 seconds. Flame uses base DPS. One refreshed
  burn per receiver; stronger active rate wins. No stacking. Full-health restoration
  clears player burn. Bot projectiles do not burn.
- Deferred, lower priority: visible burn indication on affected actors and a burn
  status on the player's health bar. No indicator implementation requested yet.
- Setup: Tools > ClassFPS > Set Up Prototype Hero Selection upgrades fire energy
  and presentation references without duplicate kits. Preserve existing asset tuning.
- Validation completed: standalone Runtime compilation in Editor/development/release
  configurations, separate Editor compilation, and 44 pure heat resource checks.
  No agent Unity Play mode, physics, rendering or audio validation. A Unity-generated
  prefab empty m_Name line has trailing whitespace; it was not manually edited.
- Pending gameplay checklists: HEAT_AMMO_TEST.md, BURN_TEST.md, FIRE_SPELL_TEST.md,
  HERO_TEST.md. Check exclusive inputs, refund/last-ammo casts, regeneration/overheat,
  burn totals/refresh/expiry, wall blocking, child colliders and repeated respawns.
- Feature commit 7434229 was successfully pushed to origin/main. The initially
  delayed Git Credential Manager push completed; local origin/main matches it.
  This handoff update will be a separate documentation commit.
- Unrelated untracked root file beginning 'can shooting, target health, and obstacle
  colors' remains untouched. Do not blindly stage it or delete it.
