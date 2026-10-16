# ClassFPS multiplayer test report

## Result

The user tested the Editor/standalone workflow and reported that connection and
development hero changes worked. Disconnect initially failed; after the cursor fix
and rebuilding the executable, the user reported that it works.

This confirms the reported workflow, not every item in the full multiplayer checklist.
Late joining, every hero/ownership combination, release-server rejection, and complete
offline regression testing have not been individually confirmed by the user.

## Implemented

- Gunslinger: hitscan revolver, facing-direction dash, actual-damage lifesteal.
- Distinct world bodies/weapons for Gunslinger, Fire Wizard and Sword; per-player
  camera filtering preserves the owner's first-person presentation.
- Local multiplayer Host/Join/address/Disconnect, one owned actor per connection,
  replicated movement/yaw/pitch/hero identity, and ownership-controlled input/view/HUD.
- Development hero changes use server validation and a respawn-style reset. Online
  combat/abilities remain disabled. Movement is currently owner-authoritative.
- Disconnect fix prevents the gameplay controller from stealing connection-panel
  clicks to recapture the cursor. Click the world outside the panel to recapture.

## Current saved configuration

- NGO 2.13.3; Unity has resolved Unity Transport to 2.7.4 in the current lockfile.
  Initial isolated validation used Transport 2.6.0; these are distinct configurations.
- Revolver: 25 damage, 100 m range, 0.4-second cooldown, six rounds, 1.5-second reload.
- Gunslinger abilities: 5 m dash over 0.2 seconds, 5-second cooldown, 15% lifesteal.
- MultiplayerTest is enabled in the build scene list; SampleScene is disabled.
- The saved MultiplayerTest scene currently enables Allow Test Hero Changes for
  development testing. The component's code default is false; release-server gating
  still rejects requests regardless of the saved setting. This testing override is
  preserved rather than silently changing the user's saved scene.

## Verification

- Runtime compilation passed for Editor/development/release; separate Editor compilation passed.
- Disconnect follow-up: 24 isolated native host assertions passed, including panel
  capture policy, the shared Disconnect handler, cleanup and reconnect.
- Earlier checks: 51 network policy/health assertions, 33 Gunslinger, 44 heat-resource
  and 16 camera-visibility assertions passed.
- The isolated headless Editor logged an unrelated UnityEditor.Search indexing exception;
  it did not prevent the passing host assertions. Native rendered UI/audio was not
  tested by the agent. The user performed the reported interactive testing above.

## Repository audit

GitHub main was queried directly and matched code commit b747cac before this report.
The audit found uncommitted Unity-generated hero assets/materials, saved tuning,
render/build settings, multiplayer scene changes and the resolved package lockfile.
Those reviewed project files are included with this report so the saved tested project
state is retained. No scene/prefab YAML was manually edited during this audit.

The unrelated root file beginning `can shooting, target health, and obstacle colors`
is intentionally excluded and untouched. Generated Library/Temp folders and executable
builds remain ignored; rebuild the executable from source when needed. Final commit
and remote verification are reported in the conversation after the push completes.

Full setup, authority limitations and remaining manual checks: MULTIPLAYER_TEST.md.
