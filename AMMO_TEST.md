# Weapon ammo and reload

## Setup

Stop Play mode, open `Assets/MovementTest.unity`, wait for import, then choose
**Tools > ClassFPS > Set Up Weapon Ammo HUD**. It adds/reuses WeaponAmmo on existing
player guns and one bottom-right ammo HUD, then saves with Editor APIs. Run twice
to verify no duplicates and preserved tuning. It does not equip or switch weapons.
Use existing Shooting/Sword Test commands for separate weapon test sessions.

On the gun's WeaponAmmo component configure **Magazine Capacity** (default 12),
**Reload Duration** (1.5 seconds), and **Automatic Reload** (enabled). The counter
shows loaded rounds / magazine capacity. Reserve ammo is unlimited in this step;
there is no pickup, reserve depletion, or reload sound/animation. A new Play session
and each player respawn start with a full magazine. Sword displays **Ammo: 8**,
never consumes ammo, and ignores R. HUD margin/size are configurable.

R reloads a partially used or empty magazine. A full magazine does not reload.
Emptying the magazine automatically starts reload, even if left-click is released.
Holding left-click resumes firing once both reload and cooldown permit. Rejected
cooldown/reload attempts do not spend ammo, deal damage, flash, recoil, or play audio.
Manual reload takes precedence over primary fire when both occur in one frame.

R requires captured, alive gameplay, including recapture suppression. An already
started reload continues while the cursor is released, using scaled time (pause
stops progress). Death or disabling the gun/ammo component cancels the unfinished
reload; respawn refills. A disabled WeaponAmmo component blocks finite-ammo attacks.
If auto-reload is disabled, an empty gun waits for R. Repeated R does not restart
an in-progress reload. Releasing left-click never queues a future shot.

## Architecture

WeaponAmmo holds resource/reload state separately from visuals/UI and notifies
Changed. Weapon exposes virtual Ammo/Reload endpoints; TryBeginAttack spends one
round only after the existing block/cooldown checks pass. HitscanGun requires the
optional resource; SwordWeapon overrides Ammo to null (infinite). Future finite
weapons can attach WeaponAmmo and use the shared attack gate; future melee types
should likewise expose null ammo. No weapon inventory or hero system is introduced.

WeaponAmmoHUD follows PlayerCombatInput.EquippedWeaponChanged and WeaponAmmo.Changed,
so cached text updates without polling. SecondaryAttack is still independently
routed from right-click and has no new behavior: future hero-specific secondary
actions may define their own costs/interaction with reload. No mouse button was
reassigned to reload and no secondary action was implemented in this step.

## Gameplay checks (requires Unity)

1. Gun: start at 12 / 12. Single shot gives 11 / 12. Hold to fire down to zero,
   show Reloading, then refill after 1.5 seconds. Continue holding to repeat cycles.
2. Set capacity to 3 before Play for a quick test. Count three gun sounds/accepted
   shots per magazine. Attempts during cooldown/reload must not consume extra rounds.
3. Shoot once then press R: no primary fire during reload, full magazine afterward.
   Repeated R must not extend the timer. R at full capacity must do nothing.
4. Disable Automatic Reload: empty the magazine, verify no shots or sound, then R.
5. Escape/focus loss: R cannot start reload. Existing reload may finish. Capture click
   and holding it must not attack; release then press/hold again. R on capture frame
   must not reload. Check right-click routing remains unchanged (currently no effect).
6. Die while reloading: no delayed refill while dead; respawn starts full. Repeat.
   Disable/re-enable gun mid-reload: no stale timer completion; press R to reload again.
7. Sword setup: infinity display, R has no effect on swing timing, and held swings
   still repeat correctly with one sound and once-per-receiver damage per swing.
8. Toggle HUD off, spend ammo, toggle it on: text immediately catches up. Run setup
   twice and check one HUD/resource, unchanged health HUD, and unchanged audio/cursor.

Agent verification: standalone runtime compilation in Editor/development/release
configurations plus separate Editor compilation against installed Unity/Input System
assemblies. This does not verify Unity rendering, input, reload timing, or scene
setup. Those gameplay checks and setup execution remain for the user.
