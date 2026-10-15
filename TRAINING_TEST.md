# Player respawn and straight-firing training bot

## Setup (Unity Editor)

1. Stop Play mode, open `Assets/MovementTest.unity`, wait for script import.
2. Choose **Tools > ClassFPS > Set Up Respawn and Training Bot**.
   This first reuses/configures the existing player health HUD, then adds/reuses
   PlayerLifecycle and Death HUD, an assigned Player Spawnpoint, and one Training Bot.
   It generates shared URP materials and `Assets/Prefabs/TrainingProjectile.prefab`
   through Editor APIs, then saves the scene. No scene YAML edits are needed.
3. On first lifecycle setup, Player is placed at the spawnpoint: **(7, 0.05, 0)**,
   facing +Z. The bot is at **(10, 0, 8)**, facing -Z. Its lane is x=10 at y=1.3.
   Spawn is three metres beside that lane, with a clear side approach to the bot.
   These coordinates fit the current ground and avoid the existing platforms/wall.
4. Run setup again. Confirm one PlayerLifecycle, one Death HUD, one spawnpoint,
   one TrainingBot, and one projectile prefab. Existing tuning and transforms are
   preserved on repeat; player repositioning happens only when lifecycle is first
   added. Existing weapons, targets, and obstacles are preserved.
5. Select Player: tune PlayerLifecycle Respawn Delay (default **3 seconds**) and its
   Spawnpoint reference. Position spawnpoints at foot height, with clear space, and
   use their yaw for facing; camera pitch resets to level on respawn.
6. Select Training Bot: PlayerHealth Maximum Health defaults to **100**. TrainingBot
   exposes Firing Interval (**1 s**), Projectile Speed (**4 m/s**), Projectile Damage
   (**25**), Projectile Lifetime (**5 s**), Muzzle, and Projectile Prefab.
   Muzzle's local +Z is the firing direction. Neither bot nor shots track the player.

If Tools is missing, check Console compilation errors and use Assets > Refresh.
A missing runtime spawnpoint falls back to the initial player pose; setup assigns
one explicitly. Keep lifecycle, health, and controller enabled for normal testing.

## Take damage, die, respawn, repeat

1. Enter Play mode. Capture the Game view with a click; that capture click must not
   attack. Walk right from x=7 into the x=10 firing lane. Watch orange projectiles
   hit you and health decrease by 25 each time. Step left/right to dodge: shots
   continue straight past and do not turn or follow you.
2. Stay in the lane for four accepted hits at default health. At zero, controls and
   attacks stop immediately; no falling, look movement, or sword damage should
   continue. The health bar reads zero and a death/countdown message appears once.
3. Wait three game-time seconds. Player returns to the assigned spawnpoint, faces
   +Z with level camera, restores full health, clears weapon state/recoil, and can
   move and attack again. Respawn should not inherit jump/fall velocity.
4. Enter the lane again. Repeat at least three cycles: one countdown per death,
   full health each respawn, and no duplicate damage/subscriptions or stale swings.
5. Die during a sword windup/active swing and while jumping/falling. There must be
   no delayed sword damage, carried velocity, retained slash pose, flash, or tracer.
6. While dead, press Escape or switch applications. On respawn, the cursor must stay
   released until clicked. The click that recaptures must not attack. Repeat without
   releasing: existing capture should remain usable after respawn.
7. Player ignores incoming damage throughout death/countdown. F8/F9 remain Editor/
   development-only and require active captured gameplay; they cannot skip death.
   Pressing F8 and F9 together on a lethal hit must not bypass the countdown.
8. Pause via timeScale=0 if testing pause: countdown and projectile movement use
   scaled time, so both wait until gameplay resumes.

## Kill the bot with each weapon

Run separate Play sessions so the destroyed scene bot is restored each time.

- **Sword:** Stop Play; choose **Tools > ClassFPS > Set Up Sword Test**, then run
  training setup if not already configured. Approach the bot beside its lane,
  then turn toward its body/head within 2.2 m and click. Three accepted default
  35-damage swings defeat its 100 health. Child hitboxes must not cause duplicate
  sword damage. On defeat the bot stops firing immediately and is destroyed.
- **Gun:** Stop Play; choose **Tools > ClassFPS > Set Up Shooting Test**. This now
  re-enables the gun, disables the sword, hides its presentation, and shows an
  existing gun presentation. If missing, use **Set Up Gun Presentation** afterward.
  Four accepted default 25-damage shots defeat the bot. Shoot body and head to
  verify child-collider lookup. Existing target health and crosshair still work.

Already-fired projectiles continue until impact/timeout even after the bot dies.
Restart Play mode to bring back the saved bot; no in-game bot respawn was added.

## Collision and regression checks (Unity required)

- Put a solid cube across the lane during Play mode. Shots disappear against it;
  Player/targets behind it take no damage. Remove it to resume the lane test.
- Test a thin wall and temporarily raise projectile speed (for example 100 m/s):
  the swept query must hit it and CharacterController instead of tunneling through.
- Put the muzzle overlapping a wall, then overlapping a target: overlap-at-start
  checks must consume the shot; bot's own child hitboxes are ignored.
- Give a receiver several child colliders: a projectile deals damage once, then
  disappears. Confirm timeout by firing into empty space for longer than Lifetime.
- Bot firing must not rotate toward Player, through repeated movement/deaths.
- Verify movement, jump, mouse limits, Escape/focus/capture handling, crosshair,
  health HUD, sword timing/occlusion, and existing gun/target behavior.
- Run setup twice after tuning bot health/interval and spawnpoint position. Settings
  should remain intact; it should not create new targets, bots, or HUDs.

## Implementation and verification

PlayerLifecycle listens to HealthChanged, guards the dead transition, blocks damage
and gameplay, and owns a single timestamp (no coroutines or duplicate timers).
FPSController keeps cursor handling alive while blocked; respawn temporarily disables
CharacterController for teleport, resets yaw/pitch/vertical velocity, and invalidates
that frame's gameplay input. It never requests capture on respawn. Weapon's shared
reset/block hook cancels SwordWeapon state; presentation reset clears both visuals.

TrainingBot reuses the existing actor-state PlayerHealth component rather than
TargetHealth, whose zero-health behavior deactivates a reusable target. TrainingBot
listens to health changes and destroys its own root. Child colliders naturally
resolve PlayerHealth via the existing IDamageable parent lookup.
StraightProjectile is independent of the shooter/input: Launch accepts an owner,
direction, speed, damage, and lifetime, so a future spell can reuse it. It uses
start overlaps plus nearest-hit sphere sweeps each physics step, skips the owner,
guards impact before callbacks, and needs no Rigidbody or projectile collider.

Agent checks: runtime C# compiled against installed Unity 6000.3.26f1/Input System
assemblies in Editor, development, and release configurations; Editor scripts
compiled separately. All passed (serialized-reference/unused release-debug-field
warnings only). Source/document whitespace checks were run. No Unity Play mode,
physics/rendering, scene setup execution, or standalone game build was performed
by the agent. All interactive checks above remain for the user. No networking,
audio, hero selection, inventory/switching controls, teams, or win conditions added.
