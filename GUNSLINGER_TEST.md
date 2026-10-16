# Gunslinger setup and gameplay checks

Stop Play mode, open `Assets/MovementTest.unity`, let scripts import, then run
**Tools > ClassFPS > Set Up Prototype Hero Selection** and **Validate Prototype
Hero Assets**. The setup upgrades the existing gun hero in place; `prototype.gun`,
GunPrototype.asset, GunPrototypeKit.prefab and their references remain stable.
The default selection label becomes Gunslinger. Custom display/class labels remain.
Fire and Sword remain available; restart Play mode to choose another hero.

Setup generates/reuses GunslingerDash and GunslingerLifesteal prefabs in
`Assets/Prefabs/Heroes` and assigns them to the existing hero secondary/passive slots.
Only the selected hero instantiates these behaviors; death/respawn retains them.
The gun's placeholder model gains one collider-free revolver cylinder. Existing
recoil, muzzle, tracer, clip assignments, poses, magazine and weapon tuning remain.
Run setup twice: expect one behavior in each slot, one cylinder, one dash HUD and
unchanged tuning/audio. Setup saves through Editor APIs, not manual scene YAML edits.

## Editable tuning

- `Assets/Definitions/Heroes/GunPrototype.asset`: health, movement, jump, gravity,
  kit and optional behavior prefab references. Keep stable ID `prototype.gun`.
- `Assets/Definitions/Weapons/GunPrototype.asset`: revolver damage, range, shot
  cooldown, magazine capacity, reload duration and automatic reload. Existing values
  are preserved, including the current magazine size; a six-round cylinder is optional tuning.
- `Assets/Definitions/Heroes/GunslingerAbilities.asset`: dash distance **5 m**,
  duration **0.2 s**, cooldown **5 s from activation**, lifesteal **15%**.
- `Assets/Prefabs/Heroes/GunPrototypeKit.prefab`: existing GunPresentation and
  PlayerWeaponAudio tuning; placeholder art can be replaced independently.
- `Assets/Prefabs/Heroes/GunslingerDash.prefab`: DashPresentation optional audio
  source/clip and particle system. New audio source is 2D, no loop or play-on-awake;
  clip and effect start unassigned. Missing optional references are safe.

Assets hold configuration only. Dash timers/direction, ammo/reload, health and all
other progress stay on runtime instances. Timers use scaled game time.

## Short Play mode checklist (pending)

1. Select Gunslinger. Hold MB1: verify repeated cooldown-gated hitscan shots, ammo
   consumption, R/manual and empty/automatic reload, held-fire resume, recoil,
   crosshair/tracer and one assigned sound per accepted shot. Walls block targets;
   child hitboxes work. Check Fire and Sword separately for regressions.
2. Take damage (F8 in Editor). Default 25 actual damage heals 3.75 health. Reduce a
   target to 10 health; a killing 25-damage shot heals 1.5, not 3.75. Test near-full
   health, full health, misses, walls, blocked damage and multiple child hitboxes.
   No healing from Fire/Sword/burn/bot damage; health HUD refreshes normally.
3. Tap MB2: travel horizontally about 5 m over 0.2 s in facing direction, regardless
   of pitch/WASD. Turn during dash: look changes, travel direction stays fixed. Hold
   MB2 past five seconds: no repeat. Press during cooldown: no queued activation.
   Indicator counts down from activation and shows Ready at expiry.
4. Dash directly and obliquely into walls, thin barriers and inside/outside corners;
   verify collision and stopped dash on side contact, with no penetration. Check
   stairs, walkable/steep slopes and low ceilings at ordinary and low frame rates.
   Collision-shortened dashes still spend the cooldown. Unity's controller handles
   each move; side contact ends remaining dash travel.
5. Dash while rising/falling: retain vertical velocity and gravity. Shoot during
   dash; reload before/during dash and verify uninterrupted progress. Bot hits still
   damage the player; dash has no damage or immunity.
6. Escape, focus loss, movement/kit/dash disable and death cancel travel/effects.
   Recapture clicks cannot attack or dash. Hold MB2 through death/respawn/focus loss;
   release then freshly press before another dash. Repeat death/respawn several
   times: same hero/kit, restored resources, ready dash, no duplicate HUD/audio or
   accumulated movement. A dead attacker cannot be healed by an on-hit notification.

## Completed automated checks

- `Tests/RunGunslingerChecks.ps1`: **33 assertions**, compiling real PlayerHealth,
  TargetHealth and GunslingerRules against small Unity shims. Covers actual removed
  health, target/player overkill, killing-hit healing, zero/blocked damage, clamping,
  health notifications, dead/disabled/self rejection, fresh-press policy, cooldown,
  no overlap/queue, capped long-frame travel, cancellation and respawn reset.
- `Tests/RunHeatAmmoChecks.ps1`: **44 existing resource assertions** passed.
- `Tests/RunCompilationChecks.ps1`: Runtime compiled against installed Unity
  6000.3.26f1/Input System assemblies in Editor, development and release configurations;
  Editor scripts compiled separately. Expected Inspector-field/unused debug warnings.
- Source/docs whitespace checks passed.

The shims do not test Unity input routing, object lifecycle, native physics or rendering.
No live Unity Editor setup, asset validator, Play mode, audio listening or game build
was run by the agent. Run setup to generate/save the new ability asset and prefabs,
then perform the manual checklist above.

Precise on-hit effects use optional `IHealthDamageReceiver.ApplyHealthDamage`.
PlayerHealth and TargetHealth return the health removed before death notifications;
future mitigation must be applied before computing this result. Receivers implementing
only legacy IDamageable retain normal hit feedback but grant no lifesteal because
their actual health damage is unknown. Healing emits only HealthChanged, never damage
or on-hit events, and cannot revive or clear burn.
