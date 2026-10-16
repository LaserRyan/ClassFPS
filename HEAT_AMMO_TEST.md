# Shared heat-based fire ammo

## Setup and tuning

Stop Play mode, open `Assets/MovementTest.unity`, allow script import, then run
**Tools > ClassFPS > Set Up Prototype Hero Selection**. This upgrades the existing
fire kit with one FireEnergy and a reusable flame line, persisting new definition
fields while preserving existing tuning/audio. Run twice; verify no duplicate
resource/line/HUD. Use **Validate Prototype Hero Assets**, then choose Fire Spell
Prototype in Play mode. Gun/sword and bot behavior are preserved.

Edit `Assets/Definitions/Weapons/FireSpellPrototype.asset` before Play:

| Setting | Provisional default |
| --- | --- |
| Maximum Energy | 100 |
| Fireball Cost | 25 |
| Flamethrower Consumption | 30 per second |
| Regeneration Delay | 2 seconds |
| Regeneration Rate | 25 per second |
| Overheat Duration | 3 seconds |
| Flame Damage Per Second | 30 |
| Flame Reach / Radius | 5 m / 0.35 m |

These are balance placeholders, not final values. Keep fireball cost at or below
maximum if it should be castable. Pool tuning is copied into instance state for
that Play session; restart Play after changing resource settings. No reserve/fuel
pool or manual reload exists for Fire. R still reloads guns and does nothing here.

MB1 spends fireball cost at accepted windup start. Insufficient energy causes no
cast/audio/projectile. A canceled windup refunds exactly the reserved cost, capped
at maximum; repeated resets cannot refund again. Released shots are committed and
never refund. Releasing MB1 allows an already accepted cast to finish. Cooldown
is still the post-release interval from `FIRE_SPELL_TEST.md`.

Held MB2 fires a short thick swept stream at the first blocking collider, ignoring
Player hierarchy/triggers. It drains the shared pool continuously, even against a
wall/miss. Damage equals DPS times the resource-funded part of the frame. There is
no splash. Configurable lingering burn is documented in BURN_TEST.md.
Only one attack can run at a time: an accepted cast blocks MB2
through release, and active flame blocks MB1 until MB2 is released. Simultaneous
fresh input gives MB1 priority. Blocked attacks spend no ammo. Already launched
fireballs continue independently, so they can remain in flight during later flame.
The starting checkout had no flamethrower/fuel implementation; this is its minimal
prototype behavior, integrated into the existing fire weapon rather than a new kit.

Normal regeneration starts after the delay since the last consumption, then adds
rate times elapsed game time up to maximum. Rejected attacks and refunds do not
restart that delay. Zero immediately starts one overheat deadline: no new attacks
can start, but an already paid fireball still releases unless separately canceled.
Flame stops at exhaustion after its final funded damage fraction. During lockout,
regeneration ignores the delay: default settings refill 75 energy in 3 seconds.

A windup refund does **not** erase or restart an overheat that already began. This
keeps lockout stable and prevents cancellation bypass. Once its timer expires,
both buttons must be observed released together before either can resume; holding
one button while releasing the other is insufficient. The HUD shows red OVERHEATED
with countdown, then a release-both prompt if needed. Release both and press again.

Death cancels/refunds windup and stops flame; respawn refills and clears overheat.
Input still requires releases for buttons held through death/focus/cursor recapture.
Normal/overheat regeneration can progress while the cursor is released; scaled time
pauses clocks. Gun/sword ammo, reload and audio conventions remain unchanged. No
flame audio clip or separate fuel display has been added.

## Gameplay checklist (Unity required)

Check exclusivity: hold MB2, then MB1; no cast or fireball cost until MB2 is released.
Start an MB1 windup, then hold MB2; no flame or flame drain until the cast finishes.
Press both together: MB1 wins initially. Repeat with zero cast delay: no flame in
the acceptance/release frame. These input/visual checks require Unity Play mode.

1. Select Fire. HUD begins at 100/100. MB1 acceptance immediately shows 75, before
   release; no second charge on release. MB2 visibly drains that same counter.
2. Stop spending: no refill for 2 seconds, then gradual refill at 25/s, capped at
   100. Spend again during regeneration: the 2-second delay restarts.
3. Temporarily set cast delay to 1 second. Start MB1, then Escape/focus loss, disable
   the weapon, or die before release: no projectile; reserved cost refunded once.
   Restore capture and repeat reset/disable: no extra refund. After projectile
   release, cancellation must not refund. Re-enable components after this test.
4. Set max=25 and cost=25 before Play. Accept MB1: pool reaches zero and heat begins,
   but the accepted fireball still releases. Hold both buttons: no further cast or
   stream during the countdown. Also test a long cast delay greater than lockout.
5. Hold MB2 at default drain/DPS, aiming at the bot. Resource reaches zero, flame
   stops and overheat starts once. For the final partial frame, damage must reflect
   only funded time, never a full frame beyond exhaustion. Test multiple frame rates
   and a low pool/drain combination for easier observation. Walls block stream damage;
   multiple child colliders do not multiply its per-frame damage.
6. During heat, watch gradual recovery immediately (not a full refill at the end).
   Attempts must not restart/extend the 3-second timer. By expiry the default pool
   should have approximately 75 energy, with both attacks still blocked if held.
7. Keep MB1 held, release/repress MB2: neither resumes. Swap held buttons: still
   locked. Release both after expiry, then freshly press either: it resumes.
   Buttons released earlier and still up at expiry also satisfy the observed release.
8. Cancel a last-ammo cast: refund appears but the existing heat timer continues
   with its original deadline. This is intentional; confirm no duplicate timer.
9. Die during heat and during a windup; repeat three respawns: full ammo, no heat or
   stale stream/cast, same hero. Held buttons still require release before reuse.
10. Select Gun/Sword in new sessions: magazine reload/infinity and audio unchanged.
    Verify UI lifecycle (disable/re-enable HUD), cursor recapture with held buttons,
    focus loss, and setup repeated after tuning (values retained).

## Automated versus manual checks

Run deterministic state checks in PowerShell:

```powershell
.\Tests\RunHeatAmmoChecks.ps1
```

The script uses installed Unity's bundled .NET compiler/runtime and writes binaries
only to a temporary directory. It does not launch Unity. The 44 assertions cover
shared spends, insufficient costs, delayed/incremental/capped regeneration, delay
restart, partial-frame funding, final-ammo acceptance, heat regeneration/lockout,
both-button release, refunds, no timer restart, invalid inputs and respawn reset.

Agent ran these state checks successfully, plus Runtime compilation with Editor,
development and release symbols and separate Editor compilation against installed
Unity/Input System assemblies. Source/document whitespace checks were run. These
checks do not exercise Unity cast cancellation callbacks, native physics, UI/input,
rendering, audio, setup repeatability or an executable game build. All gameplay
checks above remain for the user; setup/scene changes were not executed by the agent.
