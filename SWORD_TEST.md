# Sword prototype

Open `Assets/MovementTest.unity`, stop Play mode, allow scripts to import, then choose
**Tools > ClassFPS > Set Up Sword Test**. The command saves the scene, equips the
sword through the existing PlayerCombatInput, disables the player's HitscanGun
components, hides the gun presentation, and creates
shared URP materials and a collider-free cube sword. Repeating it preserves sword
settings and reuses its visual. No gameplay switching or hero system is added.
The command has not been run automatically in this session.
Existing obstacles and targets are preserved; walk to an existing target for melee
testing. No extra targets are needed. Run the command twice before entering Play
mode: confirm one SwordWeapon and one Sword Presentation, unchanged tuned values,
disabled HitscanGun, inactive Gun Presentation, and no colliders under the sword.
The gun components, models, materials, and scripts remain available for later use.
No audio was added.

SwordWeapon Inspector settings:
- Damage: 35 by default (three accepted swings defeat a 100-health target).
- Range: melee reach, 2.2 metres from the camera.
- Attack Arc: 100 degrees, full angle of the overlapping spherical sector.
- Windup / Active Duration / Recovery: 0.18 / 0.16 / 0.30 seconds.
- Attack Cooldown: additional shared start-to-start gate, default zero; the entire
  swing always prevents overlap regardless of this value.

One click starts one swing; holding starts another as soon as recovery and cooldown finish. Damage begins after windup.
The active volume follows current camera position/look, ignores triggers and the
player hierarchy, and resolves IDamageable on collider parents. A receiver is
recorded once per swing, regardless of its child collider count. Closest collider
points determine range, angle, and obstacle visibility. Solid colliders on default
raycast layers block damage; Ignore Raycast and trigger colliders do not. A target
with an exposed child collider can be struck around cover. Other targets also block
visibility. An origin contained in a blocking collider cannot damage through it.
At low frame rates, a frame crossing the active interval still performs one query.

SwordPresentation only animates the model pivot using sword timing; moving or
replacing the model does not change damage geometry. Rest, windup, and slash angles
are configurable. Recovery returns to rest. Crosshair feedback uses Weapon's
existing event: yellow at accepted swing start, red when damage is accepted.
Disabling the weapon, releasing capture, losing focus, or disabling movement cancels
an unfinished swing. Re-enabling cannot resume it. Camera/player aim is not animated.
As with the gun, this camera-child visual can intersect nearby world geometry.

## Play mode checks

Regression for the ClosestPoint fix: leave non-convex MeshColliders enabled on
obstacles, swing near them and at an unobstructed target, and confirm the Console
does not report unsupported ClosestPoint calls. Verify a non-convex mesh wall blocks
damage from both sides. Primitive/convex target hitboxes retain closest-point tests;
concave mesh/terrain receivers use surface probes and may have incomplete coverage
on complex shapes. Prefer primitive child colliders for damage receiver hitboxes.

1. Run setup and enter Play mode. Check sword visibility, near-plane clearance,
   windup, slash, recovery, and unchanged movement/look/jumping.
2. Click a nearby target: no health loss during windup, then exactly 35 damage.
   Hold the button: swings repeat after recovery, without overlap. Release during recovery: no queued swing should follow.
3. Add multiple child colliders to one TargetHealth root: each swing still deals
   only 35 total. Confirm three swings defeat a default target and re-enable resets it.
4. Test targets at the reach/arc boundaries, off-center, behind, and beyond reach.
   Multiple visible targets in the sector may each be hit once.
5. Put a solid wall between camera and target, including at point-blank distance.
   Confirm no damage; test thin walls and a camera origin inside a blocker as well.
6. Escape or lose focus during windup/active: no subsequent damage. Click to capture:
   no swing. Release the capture click; a fresh subsequent press/hold can swing. Disable SwordWeapon mid-swing and
   re-enable it: no delayed damage and presentation returns to rest.
7. Check red feedback only on accepted damage and no feedback on rejected clicks.
   Lower the frame rate and verify swings do not miss their entire active interval.

For precise timing checks, temporarily use Windup = 1, Active Duration = 0.5,
Recovery = 1. Watch TargetHealth.CurrentHealth using the Inspector in Debug mode:
health must stay at 100 through windup, fall to 65 during active, and stay at 65
through the rest of the swing. Keep the target in the volume for the whole active
phase to check repeated-hit suppression. Move into range only during recovery:
health must stay unchanged. Restore the original timings afterward.

For range checks, use a temporary unit cube target with TargetHealth, at the same
height as the camera. With Range = 2.2, put its front face 2.1 metres ahead of the
camera: it should take damage. At 2.3 metres it should not. Test off-center at
40 degrees (inside the default half-arc of 50) and 60 degrees (outside), keeping
the nearest point comfortably inside reach. Add two overlapping child BoxColliders
to the same root and confirm only 35 damage per swing. Use the existing wall or
a temporary solid cube between camera and target to verify zero damage through
cover. Make temporary test edits in Play mode so they are discarded on exit.

Runtime and Editor C# compiled separately against installed Unity 6000.3.26f1
assemblies. This is not an interactive Unity physics/rendering test; the checks
above still require Play mode. Existing scene YAML, movement, gun behavior, packages,
and project settings were not edited.
