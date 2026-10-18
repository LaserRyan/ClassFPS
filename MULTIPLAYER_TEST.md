# Local multiplayer milestone

Online Gunslinger revolver shots, reloads, lifesteal, health, death and respawns are
server-authoritative. Every selected hero can receive revolver damage and respawn.
Movement, jumping and Gunslinger dash now use server-authoritative CharacterController simulation,
with immediate owner prediction and snapshot reconciliation. Revolver shots use bounded capsule rewind.
Fire Wizard attacks, Sword attacks, projectiles and burn remain disabled online.
Offline play retains the existing behavior and one fixed kit per hero.

## Networking choice and authority

The project had Multiplayer Center tooling, but no networking runtime. This milestone
adds **Netcode for GameObjects 2.13.3**, with its **Unity Transport** dependency (current project lockfile: 2.7.4).
Unity's [package documentation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/index.html)
supports Unity 6+, Mono/IL2CPP and desktop platforms. It fits the existing GameObject
architecture and provides player ownership, replicated variables, connection approval
and RPC permission checks without cloud services, Relay or accounts. Versions are pinned
in Packages; existing package versions and project settings are preserved.

The host/server approves a stable hero ID from its configured catalog, allocates an
unused spawn slot, and spawns exactly one owned NetworkObject per connection. It owns
the hero/respawn state. Clients send only IDs, never hero assets or stat values.
Unknown IDs are rejected at approval and in change requests. Both peers must have the
same configured catalog/assets and test scene; NGO scene management is intentionally off.

Movement inputs run at 30 fixed steps/second on the server. Clients never send positions,
velocities, movement durations or shot origins. Input commands contain bounded movement axes,
yaw/pitch, action bits, a sequence, life/kit generation and a timestamp. Ownership, finite values,
sequence/life, timestamp age, queue size and a 30 Hz token budget are checked before simulation.
The host uses the same motor once; clients predict that motor and replay unacknowledged input
on authoritative snapshots. Replay never fires damage, audio or attack effects. Death/respawn,
hero changes and disconnect clear histories and pending commands. This is a prototype prediction
implementation, not a general anti-cheat solution; reliable command transport can show corrections
under packet loss, and moving-player collisions are harder to predict than static geometry.

Observers render timestamped world poses with a default 100 ms interpolation delay. Collision
roots keep the newest server pose. Revolver rays originate at the server's actor/eye position.
Living target CharacterController capsules have a bounded position history; analytical ray/capsule
queries interpolate it without moving live physics objects. Static walls use current server physics
and block historical targets. Client view time is limited by measured RTT, interpolation delay and
NGO's 50 ms server-clock buffer and 35 ms tolerance, with a default maximum rewind of 200 ms. Future/nonfinite or stale input is rejected.
History clears per life/kit, dead targets are excluded, and current health governs actual damage.
There are no bone/headshot hitboxes or historical moving-world geometry in this milestone.

This improves shots at moving displayed targets, but can award a hit shortly after a target reaches
cover (within the rewind bound). Extremely delayed shots beyond that bound still miss. Movement
prediction may correct visibly under latency/loss; test those conditions before tuning competitive play.
Hero models remain replaceable independently of the gameplay capsule, camera and attack code.

## Development opponent and new movement/rewind checks (2026-10-18)

Rebuild the executable and restart both peers: the RPC/snapshot schema and protocol changed.
Existing setup commands remain repeatable; no additional scene objects, project layers or packages
are required. Open MultiplayerTest, select **Network Prototype Session**, enable **Allow Test Opponent**
on the **host**, and enable **Allow Test Hero Changes** if testing F6. Both are off by default.
**Interpolation Delay** defaults to 0.1 seconds; **Maximum Rewind** defaults to 0.2 seconds on this
component. The server advertises interpolation delay to clients; do not tune it differently per client.
Hero stats, gun and Gunslinger dash/lifesteal values remain in the existing definition assets.

In Editor or a Development Build, press **F7** to release the cursor and open opponent controls:
spawn one Gunslinger, toggle strafe, fire once, toggle repeated shooting, reset, or remove.
A free grounded spawn near the requester is chosen by the server; blocked spawn attempts reject.
Fire once obeys ammo/reload/cooldown and can be rejected while not ready. Reset stops actions and
respawns the opponent with full resources. The opponent uses the real server motor, gun, lifesteal,
health/death/respawn and world presentation. It has no local camera, listener, HUD or input.
The creator and host can control it; creator disconnect removes it. Server settings and build gates
reject unauthorized/release requests even if client UI is bypassed. F6/F7 menus are mutually exclusive;
closing requires normal recapture and fresh attack input. This is testing infrastructure, not enemy AI.

Manual checks (not completed by automated host fixtures):

1. Rebuild a Development exe; connect Editor host + exe client using the existing steps below.
2. On each peer, move/jump/strafe into walls and around corners. Owners should respond immediately;
   observers should see smooth movement/aim, correct heroes and no extra camera/listener/HUD.
3. Use F7 from the client: spawn, close/recapture, shoot stationary and strafing opponent. Watch
   confirmed hit markers, damage/death, ammo/reload and lifesteal. Repeat from the host.
4. Toggle opponent shooting to test receiving damage/death without controlling both windows.
   Repeated fire lets the opponent kill/respawn you while you inspect controls and fresh input.
5. Repeat held MB2, airborne dash, wall/corner dash, look during dash, shooting/reload during dash,
   focus/cursor/menu cancellation and repeated deaths. Prediction must not grant extra distance,
   shots, ammo, cooldown resets or immunity. Held MB2 must never repeat or queue.
6. Under transport latency/jitter/loss simulation, strafe against the opponent, shoot moving targets,
   and inspect visible corrections. Historical hits remain bounded; a target may take a recent hit
   just after cover. A static wall directly in the server ray must always block the shot.
7. Late join, change heroes, respawn repeatedly, disconnect/reconnect. Check one kit/world model,
   no stale input/shot/dash replay, and opponent removal on creator disconnect.
8. Release host: F6/F7 test controls must be absent; an Editor/dev client cannot enable them.
   Check offline Gunslinger, Fire Wizard and Sword behavior using Offline test/MovementTest.

Automated verification for this revision is recorded in the latest AGENTS.md entry. Standalone
compilation, policy assertions and isolated single-host physics fixtures do not establish two-peer
prediction feel, packet-loss behavior, graphical input, art/audio or full offline gameplay correctness.

## Setup and editable configuration

1. Save current Unity edits and allow the new package/scripts to import. If package
   resolution fails, check Window > Package Manager and the Console before continuing.
2. Existing hero setup must already be applied. For a fresh checkout, open
   `Assets/MovementTest.unity` and run **Tools > ClassFPS > Set Up Prototype Hero
   Selection**, then **Validate Prototype Hero Assets**. This retains current tuning.
3. Open **Assets/MultiplayerTest.unity**. The new scene/player prefab/list were generated
   with Editor APIs in an isolated validation copy and included in this change.
   For reconstruction or upgrades, use **Tools > ClassFPS > Set Up Local Multiplayer
   Test** from MovementTest or MultiplayerTest, then **Validate Local Multiplayer Test**.
   First creation copies MovementTest; reruns reuse the saved multiplayer scene and
   prefab. MovementTest is not overwritten. The test manager uses one explicit prefab
   list, avoiding NGO's auto-generated default list registering the player twice.
4. Select **Network Prototype Session** in the multiplayer scene. Configure its hero
   catalog, spawn origin/yaw/spacing, address and **Allow Test Hero Changes** here.
   Default address is `127.0.0.1`, fixed UDP port **7777**, and spawn spacing **3 m**.
   Hero stats, weapon tuning and world/first-person art remain in existing assets.
   Configure the test-change setting on the **host before starting a session**.

`Assets/Prefabs/Network/NetworkPlayer.prefab` copies the shared player/camera/HUD
hierarchy with movement, combat input, hero menu, debug input, offline lifecycle,
cameras, listeners and HUDs initially disabled. Only ownership enables local movement,
camera/listener and relevant HUDs. Remote components never process local input or
cursor state. The existing CharacterController dimensions are preserved. Presentation
still uses per-actor camera ownership: owner sees their first-person kit and other
actors' world bodies; observers see world equipment and hide first-person renderers.

Only the selected kit is instantiated. Online weapon gameplay remains blocked/disabled;
network input sends intents independently. Gunslinger's secondary prefab is instantiated
once in network-controlled presentation mode. Its travel is predicted locally and validated/simulated
on the server through the shared motor; authoritative snapshots correct prediction.
The dash HUD is owner-only. Other secondary/passive behaviors are not instantiated:
the server reads the selected Gunslinger passive definition to calculate lifesteal.
NetworkProtected rejects offline damage/healing/full-restore operations, including bot
shots and debug keys. Server snapshots drive normal health/ammo change notifications.
Training bots pause online and resume only for offline tests. Accepted-shot RPCs trigger
existing gun visual events exactly once, confirmed owner crosshair feedback, a separate
world tracer/kick and one owner 2D or observer spatial sound using the assigned gun clip.
No world weapon executes gameplay. Missing art/audio references are safe.

## Build and connect: Editor host + standalone client

1. Keep MultiplayerTest open in the Editor. If testing F6, enable **Allow Test Hero
   Changes** on the scene's Network Prototype Session and save before building.
2. Open **File > Build Profiles**, select **Windows** (Standalone Windows 64-bit),
   and switch platform if necessary. In the profile's **Scene List**, include
   `Assets/MultiplayerTest.unity` as the **first/only enabled scene** for this test.
   Do not accidentally build the default SampleScene. This is a profile configuration;
   the setup tool does not change your build settings automatically.
3. Enable **Development Build** for the development hero-change test. Build to
   `Builds/MultiplayerDev/ClassFPS.exe`. Keep the executable, its Data folder and the
   accompanying Unity runtime files together. Build before entering Editor Play mode.
4. Enter Play mode in the Editor, choose a hero in the connection menu, and click
   **Host**. Leave the Editor running. It should spawn one local player.
5. Launch the standalone executable. Choose a different hero, leave address
   **127.0.0.1**, and click **Join**. You should now have two players at distinct slots.
   Click the Game view/window to capture the cursor. WASD/jump/look affect only that
   instance's owned actor. Escape releases the cursor for connection controls.
   Click inside the 3D view outside the connection panel to recapture; clicks on the
   panel are reserved for its buttons and cannot recapture the cursor.
6. Turn toward the other spawn to see the other hero. Switch focus between windows
   and move/look in each. Runtime run-in-background is enabled for this test scene;
   focus loss still releases capture and suppresses input. The host must stay running.
7. **Disconnect / Cancel** returns the local instance to the connection menu. A client
   disconnect removes only its owned actor; host disconnect removes the whole session.
   Reconnect and verify one fresh owned player per connection, without duplicate models,
   cameras, listeners, HUDs, RPC state or render subscriptions.

If Unity/Windows asks about the executable's network access, allow the local test as
appropriate. If Join fails, check the address/port, that Host started, both builds use
the same scene/catalog, and Console/Player logs. The UI reports rejection/disconnect
reasons. This milestone provides no internet matchmaking or authentication.

## Development-only hero changes

**Allow Test Hero Changes defaults to false.** The server setting and the server's
`UNITY_EDITOR || DEVELOPMENT_BUILD` compile gate must both permit the action. The server
advertises availability at spawn; client-local Inspector settings do not grant permission.
Release clients omit F6/request/probe code, and release hosts reject the RPC regardless
of a serialized true setting. These checks live in NetworkTestRules, separately from
future match selection rules. Ordinary offline hero selection stays locked per Play session.

With a development client and permitted server:

1. Capture the Game view, then press **F6**. A local test menu releases capture and
   blocks movement/look. Other players' controls/menus are unaffected.
2. Select a hero. The server checks RPC owner permission, sender ID, catalog membership,
   the setting, development support, and expected hero revision/concurrent-change state.
   Only one request is pending locally; stale requests are rejected on the server.
3. The accepted state respawns at that connection's spawn slot. Old kit/world/avatar
   roots deactivate immediately and are destroyed; new instances apply stats, full
   health/resources and fresh cooldowns. Velocity/pitch reset. The new identity/appearance
   replicates to every observer and remains correct for future joiners.
4. Close with **Close** or F6, or wait for the response. Closing never captures the
   cursor automatically. Click to recapture; capture clicks and held inputs retain
   fresh-release protections. A timed-out/rejected request shows status in the panel.

For an actual **release-server gate test**, temporarily save the server scene with the
setting true, build a second standalone executable with **Development Build unchecked**,
then restore the saved setting to false afterward. Run that release executable as Host
and use the Editor as Join client. F6 should remain unavailable. On the Editor's owned
NetworkPrototypePlayer component, use the component context menu **Development > Probe
server test hero gate**. This explicit development validation probe sends the real RPC
without trusting the advertised menu permission. Expect **Server rejected** and unchanged
hero/position/health. The probe itself is omitted from release clients. Repeat against
a development host with the setting false. Never use this probe as future match logic.

## Manual two-instance checklist (pending)

- Verify one owned actor, enabled camera, AudioListener and HUD per instance; all
  remote FPS controllers, combat/debug input, hero menus and offline lifecycles stay off.
- Move/jump/turn/pitch in each window. Only the focused owner responds; the other
  instance receives movement, upright body yaw and pitched world weapon/hand.
- Test Gunslinger, Fire Wizard and Sword appearance, owner first-person visibility,
  other-body visibility, remote first-person hiding and simultaneous Game/Scene views.
- Gunslinger MB1/R use the server revolver path and MB2 requests a server-approved
  dash. Fire/Sword attacks and F8/F9 remain disabled online. No local projectile/burn
  is created; dash follows the dedicated checklist below.
- F6 settings off/on, closing without selecting, request waiting/rejection, rapid clicks,
  and repeated hero changes: correct stats/spawn, one kit/model, no stuck cursor/capture
  input, no duplicate HUD/audio. Confirm changes on both peers.
- Disconnect the client; change the remaining host hero; rejoin and inspect the host's
  current appearance and position. Also test client hero changes before later joins.
- Disconnect/reconnect each side, host exit, cancel an unfinished connection, and invalid
  address. No orphan players or listeners; connection menu and cursor recover.
- Test release-host rejection with the development context-menu probe above, even when
  the saved server setting is true. Test release client F6 remains unavailable.
- Open MovementTest independently, or choose **Offline test** in the lobby. Recheck
  selection, movement, dash, shooting/reload, Fire energy/burn, Sword, lifesteal,
  death/respawn, HUD, audio and cursor gates. Offline test keeps its selected hero when
  returning to the lobby and back; restart Play mode for a fresh offline selection.

## Server revolver configuration and protocol

- Primary tuning: `Assets/Definitions/Weapons/GunPrototype.asset` (damage, range,
  cooldown, magazine, reload duration, auto reload). Hero maximum health/movement:
  `Assets/Definitions/Heroes/GunPrototype.asset` and other hero assets. Lifesteal:
  `Assets/Definitions/Heroes/GunslingerAbilities.asset`, default 15% actual damage.
- Respawn delay: **Network Respawn Delay** on `NetworkPrototypePlayer` in
  `Assets/Prefabs/Network/NetworkPlayer.prefab`, default 3 seconds. Spawn origin/yaw/
  spacing remain on Network Prototype Session. All mutable values live on actors.
- Fixed-step movement commands also carry held fire/reload intent and yaw/pitch. The server
  validates owner, life/sequence/time and rate, then its own selected gun, alive state,
  cooldown/ammo/reload. It derives the eye from its simulated actor and prefab camera offset.
  The closest current wall bounds historical capsule queries; shooter and dead actors are
  excluded. Offline targets/bots remain blocking geometry, not network damage recipients.
- Server actual damage is the representable health removed, capped at remaining
  health. Selected passive healing uses that value, clamps at maximum and cannot
  revive. Health changes do not recursively trigger on-hit events.
- A single per-actor deadline handles death. Dead actor colliders and owner movement
  are disabled, and remote pose application stops. Respawn increments the same
  server revision used by hero changes, clears deadlines/ammo/reload/cooldown, rebuilds
  one selected kit, and resets position/yaw/pitch/vertical velocity. Old-life fire and
  input messages fail the revision guard. No coroutine/timer survives disconnect.
- Held MB1 is sampled with 30 Hz input; only fresh consumed commands can request shots.
  The server gun cooldown independently limits accepted shots. Movement and dash are predicted;
  damage, hit markers and ammo are authoritative. Cursor/focus/menu/death gates require release.
  Host and client use the same server combat path; no client position, eye offset, damage,
  target, hit result or movement duration is accepted.

## Manual combat checklist (two instances, not agent-verified)

Rebuild the executable after importing scripts. Open MultiplayerTest and optionally run
**Tools > ClassFPS > Set Up Local Multiplayer Test**, then **Validate Local Multiplayer
Test**. Host in Editor, Join from the executable at `127.0.0.1:7777` as described above.
Select Gunslinger in both windows initially. Escape releases the mouse; click outside
the connection panel to recapture, then release MB1 before firing.

1. Host shoots client: only accepted shots consume ammo and show owner recoil/flash/
   confirmed hit feedback. Client sees host world tracer/kick and spatial gun sound;
   no duplicate host audio. Client health HUD decreases; F8/F9 do nothing online.
2. Client shoots host: verify the same behavior in reverse. Then select Fire Wizard
   and Sword as targets through enabled development hero changes; both can die,
   while their attacks remain disabled. Gunslinger MB2 now uses the network dash path.
3. Shoot into/around walls: closest wall blocks damage. Dead bodies do not intercept
   shots intended for a living actor behind them. Misses give no damage feedback/heal.
4. Hold fire through cooldown and automatic reload, tap R early, spam R/MB1, and
   release/repress. HUD ammo/reloading follows accepted server state, with no queued
   burst. Escape, focus loss and F6 suppress firing; held buttons require release.
5. Damage the shooter first. A 25-health hit at 15% heals 3.75; a killing hit on a
   target with 10 remaining heals 1.5. Full-health clamping prevents excess health.
   Dead attackers cannot heal; kills are still eligible. No healing on walls/misses.
6. Kill each player repeatedly: controls stop, body disappears, owner gets countdown.
   After the server delay, health/magazine/cooldown and server spawn position reset;
   yaw/pitch/vertical velocity reset. Old movement/fire must not undo respawn.
7. Change hero during countdown and while reloading, attempt rapid repeated changes,
   disconnect during countdown, reconnect, and late join during death/reload. Expect
   current health/appearance/state, one kit/model and no old timer or delayed shot.
   Retest release-server rejection of development hero changes as described above.
8. Run MovementTest or lobby Offline test: Gunslinger shooting/reload/dash/lifesteal,
   Fire projectiles/flame/heat/burn, Sword, sounds, cursor and repeated respawns work.


## Network Gunslinger dash

A fresh MB2 press sets one edge in the fixed-step input command. The same shared motor
starts prediction immediately and validates dash on the server using the selected hero,
alive state, active travel and cooldown. No client distance/duration/cooldown is accepted.
Defaults remain **5 metres / 0.2 seconds / 5 seconds** in
`Assets/Definitions/Heroes/GunslingerAbilities.asset`; host definitions govern authority.
Cooldown starts on activation. Holding/rejected presses do not repeat or queue.

Captured horizontal body yaw ignores pitch and cannot steer while active. CharacterController
collision stops travel at walls; normal horizontal movement is overridden while gravity,
vertical velocity, mouse look, shooting, damage/lifesteal and reload continue. No immunity or
dash damage. The authoritative motor snapshots include dash state for prediction replay.

Owner HUD uses predicted/reconciled cooldown. Optional start/stop presentation is separate
from movement; owner effects are predicted once, host/observers get accepted events. Optional
audio remains owner 2D/observer spatial. Wall/cursor/focus/menu/disable/death/life changes stop
travel; cancellation preserves cooldown. Life reset clears it. Remote actors have no dash HUD.
Brief network jitter can reuse movement axes for at most 250 ms, but never repeats shots,
jumps, reload or dash presses; a cursor/focus cancellation command ends that lease immediately.

Setup: no new scene asset or prefab generation is needed with the existing Gunslinger
assets. **Validate Local Multiplayer Test** checks its dash definition and exactly one
HUD. Repeatable setup reuses the existing player/kit/prefabs. If reconstructing missing
Gunslinger assets, first run the existing hero setup in MovementTest. Rebuild the exe
and restart both peers: the combat snapshot and dash RPC wire schema changed.

Manual two-instance dash checklist (pending agent gameplay validation):

1. Host and standalone client each select Gunslinger. MB2 triggers once, holding does
   not repeat after cooldown, and clicks during cooldown do not queue. HUDs agree
   with the server's configured cooldown; the remote player has no extra HUD.
2. Dash facing forward/sideways relative to the arena, look up/down before pressing,
   then turn during travel. Capture direction at press, keep body upright, do not steer.
3. Dash into a wall, corner, ramp and low ceiling. Never pass through blocking geometry.
   Check player-to-player contact. Stopping against a wall must retain cooldown.
4. Jump/fall and dash. Preserve upward/downward velocity and gravity. Shooting and
   lifesteal work while dashing; R and automatic reload progress are not restarted.
5. Take lethal damage during dash: no immunity, immediate cancellation, normal death
   countdown/respawn. Health/ammo/position/dash state reset; no old request resumes.
6. Escape, lose focus, disable the controller, or open F6 during pending/active dash.
   It stops, accepted cooldown remains, and recapture/held MB2 cannot retrigger.
7. Change hero while waiting/dashing/on cooldown, repeat death/respawn and disconnect/
   reconnect. Exactly one current kit/model/dash/HUD; old-life requests do not apply.
   Fire Wizard and Sword cannot dash or attack online.
8. Inspect owner/observer visibility and any assigned optional dash audio/effects;
   host effects play once. Recheck offline Gunslinger dash/shooting/lifesteal and
   Fire/Sword behavior in MovementTest or the lobby Offline test.


Current automated results are recorded in the latest AGENTS.md handoff. Historical milestone
counts there refer to earlier implementations; the current native fixture exercises the shared
server motor and compensated hitscan. Manual graphical/input/audio and latency/loss tests above
are still required.

## Repeating the headless wire check

This is a developer harness, separate from normal gameplay and user builds. Prepare the existing
isolated `Builds/NetworkDashValidation` project with current Assets/Scripts and its cached package
manifest; never run asset-generating batch checks in the main/open project. Copy
`Tests/NetworkWireSmoke.cs` into that copy's `Assets/Scripts` only. Run Unity with
`-batchmode -nographics -quit -projectPath <isolated-project> -executeMethod
NetworkPrototypeBatchChecks.BuildWirePlayer`. This method refuses the main project and builds a
Development Windows exe at `Builds/NetworkWireBuild/ClassFPS.exe`. Then run
`powershell -ExecutionPolicy Bypass -File Tests/RunNetworkWireChecks.ps1` from the main checkout.
The harness starts two hidden headless processes on localhost UDP 7777, so stop other sessions first.
It uses a temporary flat physics fixture and artificial commands through the actual owner RPC,
server motor, ammo/cooldown, dash snapshots, client prediction and reconciliation. It verifies
acknowledgements, bounded final position error, resource updates and disconnect cleanup. The
injected smoke MonoBehaviour exists only in the disposable copy/build, not normal project Assets.
This does not test graphical input, interpolation appearance, audio, or latency/loss behavior.
