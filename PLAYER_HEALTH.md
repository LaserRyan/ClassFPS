# Player health and HUD

The next step is now available: `TRAINING_TEST.md` documents the optional
PlayerLifecycle and training-bot setup. With lifecycle attached, zero health blocks
controls and starts a respawn countdown. The health-only behavior below describes
PlayerHealth in isolation; F8/F9 are blocked while dead.

## Setup

1. Stop Play mode and open `Assets/MovementTest.unity`.
2. Wait for script import; use **Assets > Refresh** if needed. Check the Console for
   compilation errors if the Tools menu is missing.
3. Choose **Tools > ClassFPS > Set Up Player Health HUD**.
4. The command saves the active scene and selects Player. It adds/reuses
   PlayerHealth, PlayerHealthDebugInput, and one child named Player Health HUD.
   It binds the HUD to this player's health. Existing maximum health, HUD tuning,
   debug damage amount, weapons, obstacles, and movement settings are preserved.
5. Run it a second time: there should still be one of each component and one HUD.
   Setup enables health/debug/HUD components; disable debug input afterward if desired.

No Canvas, EventSystem, new packages, or input actions are needed. Like the existing
crosshair, the HUD uses OnGUI. The bottom-left green bar and current/max text remain
visible with the cursor released. PlayerHealthHUD exposes margin, size, and fill
color. HealthChanged events update its cached text/fill; there is no health polling.
Disabling the HUD unsubscribes; re-enabling binds and reads the latest health.

## Health behavior

PlayerHealth implements IDamageable and belongs on the Player root, so existing
parent lookup also works for child colliders. Maximum Health defaults to 100 and
is configurable in the Inspector. A new play session starts at full health.
Current health is clamped to [0, maximum]. Zero health has no death behavior:
Player stays enabled and can still move/attack. Re-enabling PlayerHealth does not
reset health; starting a new session or explicitly restoring does.

TakeDamage returns true only for accepted positive finite damage while enabled
and above zero. Invalid/negative/zero damage is ignored. RestoreFullHealth is an
explicit state API, not a respawn operation. SetMaximumHealth(value, restoreToFull)
supports future actor configuration; by default it preserves/clamps current health.
Health has no camera, input, HUD, singleton, networking, or local-player dependency.
A future multiplayer layer must choose ownership/authority and bind only the local
HUD; none of that is implemented here. TargetHealth retains its existing behavior.

## Development test controls

In the Unity Editor or a build with **Development Build** enabled:
- **F8**: apply Test Damage (default 25) through TakeDamage.
- **F9**: restore full health.

Each key press applies once, with no held-key repeats. These controls run only when
FPSController.CanAcceptGameplayInput is true: Game view focus, cursor capture, active
movement, and unpaused gameplay are required. Escape/focus loss blocks them. They
are also suppressed during the frame that a click recaptures the cursor. To test
that specific case, press F8/F9 on the same frame as the capture click. The component
can be disabled independently. Non-development builds compile out all test input
logic. No production input bindings or gameplay healing controls were added.

## Gameplay checklist (requires Unity)

1. Run setup twice and check no duplicates, preserved tuning, and unchanged equipped
   gun/sword. Enter Play mode: health reads **100 / 100** and the bar is full.
2. With Game view captured, press F8 once: **75 / 100**, approximately 75% fill.
   Hold F8: no repeat. Three more presses reach **0 / 100** with an empty bar.
   Further presses stay at zero. Movement and weapon attacks still work at zero.
3. Press F9: full bar and **100 / 100**. Press again: no visible change. Set Test
   Damage above the maximum and press F8: clamps at zero; F9 restores to maximum.
4. Escape then press F8/F9: no health change. Regain focus, click to capture: no
   attack or debug damage on that capture frame. A subsequent F8 works. Repeat after
   switching applications and after disabling/re-enabling FPSController.
5. Exit Play mode, set Maximum Health to **150**, re-enter: **150 / 150**. F8 gives
   **125 / 150**. Restore with F9. Runtime maximum changes clamp current health and
   refresh the HUD. Check negative/invalid Inspector maximum clamps to at least 1.
6. Disable the HUD, apply damage, re-enable it: it immediately shows current health.
   Disable/re-enable PlayerHealth: no automatic healing. Stop/start Play: full health.
7. Check HUD readability at different Game view resolutions, crosshair visibility,
   movement/jumping/collisions/look, cursor recapture, and gun/sword damage regressions.
8. Optionally verify F8/F9 work in a development build and do nothing in a regular
   build. No enemy dummy, death, respawning, or audio is included in this step.

## Agent verification

Standalone runtime compilation is checked with UNITY_EDITOR, DEVELOPMENT_BUILD,
and neither development symbol, plus separate Editor compilation against installed
Unity 6000.3.26f1/Input System assemblies. This does not run Unity's lifecycle,
rendering, physics, or input. Setup execution and all gameplay/build checks above
must be performed in Unity; the agent has not run them or saved scene changes.
