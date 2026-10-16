# Local multiplayer milestone

This is a movement/appearance prototype. Online combat, damage, projectiles, burn,
lifesteal, dash, reload input and other abilities are disabled. Offline play retains
the existing behavior and one fixed kit per hero.

## Networking choice and authority

The project had Multiplayer Center tooling, but no networking runtime. This milestone
adds **Netcode for GameObjects 2.13.3**, with its **Unity Transport 2.6.0** dependency.
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

Movement is owner-authoritative: each owner publishes position, yaw and pitch at up
to 30 Hz; observers smooth position/yaw and apply world-weapon pitch. Movement is
trusted, with no server collision validation, prediction, reconciliation or anti-cheat.
A server-written hero revision groups identity and spawn pose atomically; owner poses
carry that revision, so old movement cannot undo a hero-change respawn. Replicated
variables retain current identity and pose for late joiners. Before real combat, move
damage/health/resources/ability execution to server authority, validate attack requests,
and decide movement validation and lag-compensation policy. None of that is implemented.

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

Only the selected kit is instantiated. Online kits are blocked/disabled, audio sources
are stopped/disabled, and secondary/passive prefabs are not instantiated. PlayerHealth's
NetworkProtected flag rejects offline damage/healing/full-restore operations; approved
hero configuration explicitly resets maximum/current health. Training bots are paused
in the connection lobby/network session and resumed only for the offline test.

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
- MB1, MB2, R, F8/F9 cannot shoot, spend resources, damage/heal, dash, burn, respawn or
  play attack audio online. Check no projectile or damage is created on either peer.
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

## Completed checks and limits

- Standalone Runtime compilation in Editor, development and release configurations,
  plus separate Editor compilation, using actual NGO/Transport/Unity assemblies.
  `Tests/RunCompilationChecks.ps1` defaults to Library/ScriptAssemblies after import;
  `-ScriptAssemblies <path>` can use an isolated validation project's assemblies.
- Actual Unity package import/C# compilation and NGO RPC/NetworkVariable IL processing
  passed in an isolated Unity 6000.3.26f1 project using cached packages and the exact
  downloaded NGO/Transport versions. No source-project scene/settings were changed.
- `NetworkPrototypeBatchChecks.Run`: setup twice, native single-host Play mode,
  **22 assertions** covering default setting, unknown IDs, ownership/camera/listener,
  inert online input/abilities/damage, menu suppression, repeated hero replacement,
  actual stale/unknown/disabled-setting RPC rejection, cleanup and host restart.
  The isolated Editor logged an unrelated UnityEditor.Search startup indexing exception;
  host assertions passed with no project/network initialization errors after fixes.
  This optional CLI method generates/saves assets and enters Play mode: run it only
  in a disposable copy, with `-batchmode -nographics -executeMethod
  NetworkPrototypeBatchChecks.Run` and **without -quit** (it exits when finished).
- `Tests/RunNetworkPolicyChecks.ps1`: **17 Editor + 17 development + 17 release checks**
  of the actual compile-gated server policy and real health code with Unity shims.
  Release checks explicitly reject even an enabled server setting. Tests cover owner,
  catalog, concurrent/stale revision gates and protected damage/healing/restore behavior.
- Existing **33 Gunslinger**, **44 heat state**, and **16 visibility** checks passed.
- Source/document whitespace checks passed. Unity-generated empty serialized string
  fields retain the serializer's usual trailing spaces; no scene/prefab YAML was edited.

No standalone game build, second connected instance, native remote interpolation,
interactive keyboard/mouse/UI, graphical visibility, audio listening or full gameplay
regression was executed. The headless host checks do not replace the manual connection,
release-host wire rejection and offline checklist above.

### Disconnect button follow-up

The user reports connection and development hero changes working, but Disconnect did
not respond. FPSController was recapturing any left-click after Escape before OnGUI
could handle the button. The connection panel now provides a capture exclusion rect
before movement Update, for network and embedded offline play. Other Game-view clicks
still capture normally. The UI and native lifecycle checks share Session.Disconnect.
The expanded native host check has **24 assertions**, including inside/outside panel
capture policy, button-handler shutdown, cleanup and reconnect. Runtime compilation in
Editor/development/release and separate Editor compilation passed again.

Retest: stop Play mode, let scripts import, **rebuild the standalone executable**, then
host/join again. Press Escape and click Disconnect on the client; its actor should
disappear from the host and the client should show the connection menu. Rejoin, then
disconnect the host and verify both return to their menus. Also click the world outside
the panel to verify normal recapture. Actual mouse-driven button clicks in both windows
remain a manual check; native assertions test the capture predicate and handler path.
