# Fire Spell Prototype

## Setup and values

Stop Play mode, open `Assets/MovementTest.unity`, allow scripts to import, and run
**Tools > ClassFPS > Set Up Prototype Hero Selection** again. It extends the existing
selection with Fire Spell Prototype and creates/reuses its assets/prefabs. Existing
gun/sword settings, models/audio, HUDs, bot, spawnpoint, targets and obstacles remain.
Repeat setup to check no duplicates; existing fire asset/prefab edits are preserved.
Use **Validate Prototype Hero Assets** afterward. Secondary/passive slots stay null.

- `Assets/Definitions/Heroes/FireSpellPrototype.asset`: ID `prototype.fire`, display
  Fire Spell Prototype, class Caster Prototype; defaults 110 health, 5.25 speed,
  1.2 jump height, -20 gravity. Kit/avatar/icon follow the existing hero architecture.
- `Assets/Definitions/Weapons/FireSpellPrototype.asset`: damage **35**, Range (maximum
  camera aim distance) **80 m**, cast delay **0.35 s**, cooldown **0.6 s after release**,
  projectile speed **12 m/s**, radius **0.2 m**, lifetime **4 s**, projectile prefab.
- `Assets/Prefabs/Heroes/FireSpellPrototypeKit.prefab`: cast origin local pose and
  optional charge/cast presentation. FireSpellPresentation has optional Cast Clip
  and Release Clip plus volume; both clips are deliberately unassigned.
- `Assets/Prefabs/Heroes/Fireball.prefab`: optional fireball body/trail and impact
  presentation reference. Body diameter and trail width follow launch radius.
- `Assets/Prefabs/Heroes/FireImpact.prefab`: brief visual pulse, optional Impact Clip
  and volume (unassigned), and visual duration/size. Audio is 2D/non-spatial like
  existing player test audio. No new audio files are required or auto-assigned.

Weapon assets are read-only to runtime code; cast state/timestamps are on the weapon
instance, projectile lifetime/size/damage on each launched instance. Ammo is unlimited
(infinity HUD); R has no effect. Right-click/secondary/passive behavior is not added.

## Cast rules

Each accepted cast waits its windup, then releases exactly one projectile. Cooldown
is measured from **projectile release** until the next cast can begin; default
continuous cadence is 0.35 + 0.6 = approximately 0.95 seconds plus frame scheduling.
Release of left-click does not cancel an accepted cast. It prevents subsequent casts.
Clicks during windup/cooldown are not queued; holding retries when ready.

Death, weapon disable/removal, cursor release, focus loss, or blocked gameplay cancels
unfinished casts. Pause (timeScale=0) also blocks gameplay input and cancels windup.
Fresh press/release protections from shared input remain: release a held capture
click or a hold through death/focus loss before attacking again. Launched projectiles
are independent scene objects and survive caster death/removal until impact/expiry.

The camera ray picks the nearest non-owner solid surface, or the max aim point.
Aim is sampled at release so it follows the crosshair during windup. A radius sweep
from camera to cast origin prevents offset-muzzle wall bypass; if blocked, the shot
is consumed immediately at the safe first contact and produces an impact there.
Thereafter sphere sweeps and start-overlap checks hit the nearest blocking surface,
ignore caster hierarchy/triggers, and apply IDamageable damage only once via parent
lookup. Lifetime bounds travel independently of aim distance (default max travel
about 48 m). No homing, splash, burn, teams or friendly-fire rules are implemented.
The impact pulse is visual only and does not imply splash damage.

## Gameplay checklist (Unity required)

1. Enter Play, choose Fire Spell Prototype. Capture/release the initial click, then
   attack. Verify fire hero health/stats, infinity ammo, one selected kit/weapon, and
   no gun/sword components (including inactive). All three choices should fit or scroll.
2. Single click: charge during windup, then one fireball and yellow release feedback.
   Set cast delay to 1 second before Play to make timing obvious; no projectile or
   damage should occur immediately. Release the button during windup: cast still fires.
3. Hold primary: no overlapping charges; repeated releases respect windup plus
   post-release cooldown. Release during cooldown: no extra cast. Rapid taps during
   cooldown/windup do not queue more projectiles. Test zero cast delay as well.
4. Aim at targets at different distances and off-center camera angles. The fireball
   leaves the offset origin toward the crosshair point; aiming can change during cast.
   Step beside the bot lane and defeat it with three accepted default 35-damage hits.
   Multiple child hitboxes must not produce more than one damage application per ball.
5. Press against a wall and aim across it: no ball should appear on the far side or
   damage a hidden target. Test a wall between camera and offset origin, a wall at
   the origin, thin walls, corners, and increased speed/radius. Test an intervening
   obstacle on the muzzle-to-aim path even when the camera can see the target.
6. Fire into empty space: balls/trails disappear after Lifetime, with no impact pulse.
   On a wall/target hit: ball disappears, one short impact pulse, then no leftover
   projectile/trail/impact objects. The pulse deals no extra damage.
7. During windup press Escape, lose focus, disable FireSpellWeapon, or die: no delayed
   release. Click/hold to recapture: no cast until released and freshly pressed.
   Disable/remove the caster after a fireball is released: the ball still travels
   and hits safely. Test caster destruction using temporary Play-mode edits only.
8. Repeat death/respawn three times: same fire hero and configuration, full health,
   no duplicate kit/subscriptions, charge hidden/cancelled on death, no stale casts.
9. Null optional presentation/audio references: gameplay still works. Assign optional
   test clips only if desired; check one cast/release/impact sound per event, silence
   on rejected attempts, stopped windup sound on cancellation. Audio checks are manual.
10. New Play sessions: select Gun/Sword again and verify their existing pew2/Shing,
    held attacks, reload/infinity, occlusion and respawn. Verify bot firing direction,
    damage, speed and projectile timeout behave as before.

## Verification status

Agent checks: standalone Runtime compilation for UNITY_EDITOR, DEVELOPMENT_BUILD,
and release; separate Editor compilation; source/document whitespace checks.
No live Unity Play mode, physics/rendering, audio playback, setup execution or game
build was performed by the agent. The Editor command must generate/import the new
assets in Unity; all gameplay checks above and the Editor asset validator remain
for the user. No scene YAML was edited manually.
