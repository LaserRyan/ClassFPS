# Placeholder gun presentation

## Unity setup

1. Stop Play mode, save your scene, and let Unity import/compile the new scripts. Use **Assets > Refresh** if needed.
2. Open `Assets/MovementTest.unity`, with the existing equipped HitscanGun and working Player.
3. Choose **Tools > ClassFPS > Set Up Gun Presentation** from Unity's top menu bar. This command uses Editor APIs and saves the active scene. It does not rerun shooting setup or change movement, damage, camera settings, targets, obstacles, or the equipped weapon.
4. The command selects **Player > Main Camera > Gun Presentation**. Its Inspector exposes resting Weapon Position/Rotation, Recoil Distance/Angle, Recovery Speed, and Muzzle Flash/Tracer Duration. Defaults are a 0.035 m backward kick, 3 degree upward tilt, recovery speed 16, 0.045 s flash, and 0.07 s tracer.
5. The new hierarchy contains **Placeholder Model**, **Muzzle > Muzzle Flash**, and **Shot Tracer**. The original Hitscan Gun logic remains on its original object.

The command reuses existing material assets or creates GunBody, GunTrim, GunFlash, and GunTracer in the existing Materials folder using URP Lit/Unlit shaders. Running it again selects the existing presentation without resetting its settings or duplicating it. If Tools is missing, check **Window > General > Console** for compilation errors.

All primitive colliders are removed, and visual objects use the Ignore Raycast layer. Gun meshes cast no shadows. The default pose clears the camera's near clipping plane even at maximum default recoil; no camera setting changes are needed. This is a normal camera child, so it can intersect world geometry when pressed against walls; it does not affect camera-ray damage.

## Separation and replacement

HitscanGun now emits a gun-specific `ShotFired` endpoint only after accepting the cooldown and resolving the shot. The endpoint is the existing camera ray's nearest valid hit or maximum range on a miss. Shared Weapon, PlayerCombatInput, FPSController, TargetHealth, and CombatCrosshair are unchanged.

GunPresentation listens to that event. It animates only its own transform and renders effects; it cannot apply damage, read input, or rotate Player/the camera. Its tracer is visual only, even though it travels from the offset muzzle toward the camera-ray endpoint. Recoil recovery uses exponential damping and accumulated kicks are capped at twice the configured recoil.

To replace the placeholder later, replace only the shapes under Placeholder Model with your own collider-free visual. Keep Gun Presentation and its component, or reassign Muzzle, Muzzle Flash, and Tracer references to the replacement. Muzzle position is camera-local relative to Gun Presentation and should match the new barrel tip. Use a separate presentation component for future swords, spells, or other weapon types.

The flash mesh and world-space LineRenderer are created once by the setup command, use shared saved materials, and are reused on shots. Timers hide both effects. Disabling the presentation clears effects/recoil and unsubscribes its event; disabling the gun clears effects/recoil on the next presentation update. There are no per-shot effect objects or material allocations to destroy.

## Play mode checks

1. Enter Play mode and click the Game view. The model should appear at bottom-right with no near-plane clipping. The capture click must not trigger flash, tracer, recoil, damage, or firing feedback.
2. Click again toward empty sky. Expect a brief muzzle flash, tracer toward maximum range, and a small backward/upward model kick recovering smoothly. The crosshair should flash yellow and the camera aim should remain fixed.
3. Shoot an exposed target. Expect the tracer endpoint on its hit surface, unchanged red damage feedback, and the existing four-hit defeat at default damage/health. The final tracer still uses the hit point even when the target deactivates.
4. Shoot a wall with a target behind it. Expect the tracer to end on the wall and no damage through it. Move around the wall and verify the exposed target still takes damage.
5. Click faster than the gun's Attack Cooldown. Rejected clicks must produce no flash, tracer, recoil, or damage. Holding left-click still does not enable automatic fire. Right-click still has no attack implementation.
6. Press Escape and move/click: movement, look, and attacks stay blocked until recaptured. The recapture click must not fire. Existing effects may finish fading, but no new effects should appear.
7. Check WASD, jumping, obstacle collisions, mouse look, and cursor restoration when FPSController is disabled. Disable GunPresentation during a shot and confirm the flash/line disappear. Re-enable and test another shot.
8. In Gun Presentation's Inspector, try different pose, recoil, recovery, and effect duration values. Restore a pose clear of the camera before saving; Play mode Inspector changes normally revert on exit.

## Checks performed

All runtime scripts compiled against installed Unity and Input System assemblies without UnityEditor references. All Editor scripts compiled separately against the runtime assembly. Diff whitespace checks passed. The setup command and Play mode rendering, recoil feel, tracer alignment, cleanup, and gameplay regression checks could not be run here; the open project's Unity lock file was present and no live Editor connection was available. Run the menu command to create and save the scene visuals/materials, then perform the checks above.
