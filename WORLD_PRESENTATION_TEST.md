# World player placeholder

Stop Play mode, open `Assets/MovementTest.unity`, allow scripts to import, then run
**Tools > ClassFPS > Set Up Prototype Hero Selection** and **Validate Prototype Hero
Assets**. Setup reuses existing hero/kit assets and adds an optional World Presentation
Prefab reference to each hero. Existing tuning, sounds and first-person art remain.
Run setup twice to check that references, materials and model counts remain stable.

Generated assets:

- `Assets/Prefabs/Heroes/GunslingerWorld.prefab`: blue capsule, forward visor, revolver.
- `Assets/Prefabs/Heroes/FireWizardWorld.prefab`: orange capsule, visor, casting hand.
- `Assets/Prefabs/Heroes/SwordPrototypeWorld.prefab`: purple capsule, visor, sword.
- `Assets/Materials/WorldHeroes`: three body colors and one equipment material.

Edit the **World Presentation Prefab** field on the corresponding hero under
`Assets/Definitions/Heroes`. Prefab child transforms/materials and WorldHeroPresentation
pivot/recoil settings are editable. Setup preserves assigned presentation prefabs and
existing generated assets/materials. Clearing the optional field uses a runtime
placeholder of the matching color/weapon; fallback material is cleaned up on destruction.
Custom art must have a WorldHeroPresentation on its root, with child Visuals, Aim Pitch
and Attack Pose references. Keep its root upright at the player origin. Missing pivot
references are tolerated, but assigned pivots are needed for aim/recoil animation.

World prefabs are visual-only. Selection/asset validation rejects colliders, rigidbodies,
weapons, health/damage receivers, projectiles, cameras, listeners and AudioSources in
them. Generated meshes have no colliders. The runtime fallback disables primitive
colliders immediately before destroying them. Player's existing CharacterController and
damage components are untouched; there is no new body hitbox. World equipment neither
fires nor damages anything, and it plays no additional sound.

## Inspect both views

1. Enter Play mode and select a hero. In **Game**, verify the usual first-person gun,
   sword or fire effects and accepted animations remain visible. Look down/up and turn:
   your own world body and equipment must stay invisible to Main Camera.
2. Press Escape. Open **Scene** alongside Game, find **Player > World Hero Presentation**
   in the runtime Hierarchy, select its Body child, and press **F** with Scene focused.
   Zoom out and orbit to inspect the capsule, forward visor and world equipment. The
   Scene view camera should show the world model and hide the camera-local kit,
   including its tracer/flash/charge/flame renderers. Check with Gizmos off to avoid
   confusing editor icons/wireframes with rendered first-person meshes.
3. Keep Scene and Game visible together. Click Game to capture again, move/look/attack
   while observing Scene. Body stays upright and turns with player yaw. Only the arm
   and equipment pitch up/down; accepted gun shots, sword starts and fire casts produce
   a small visual kick. No extra shot/projectile/damage/audio occurs. Full third-person
   swing, flame and cast animations are deferred; launched world projectiles/impacts
   remain visible to both views, while first-person flame/charge effects are owner-only.
4. Restart Play mode for each hero: blue Gunslinger/revolver, orange Fire Wizard/hand,
   purple Sword/blade. Existing hero display names and stable IDs are preserved.
5. Test repeated death/respawn: the child Visuals hides while dead, returns on full
   restoration, and exactly one world presentation and one original kit remain.
   Appearance and pitch reset with the existing spawn flow; no model is rebuilt.
6. Check movement/jump/wall collisions and damage hitboxes, dash into walls/airborne,
   shooting/reload during dash, actual-damage lifesteal, burn, health/ammo/dash HUD,
   sounds, Escape/focus loss and recapture/release protections. World visuals must
   neither intercept hits nor alter any of those systems.
7. If testing an additional normal Camera, leave Main Camera unchanged and use a
   separate target RenderTexture or non-overlapping viewport. Do not add an
   AudioListener. Check both render orders for first-person/world visibility leaks.
   The Scene view procedure above requires no saved camera or project-setting changes.

## Camera ownership seam

`PlayerViewVisibility` caches renderers from this actor's first-person kit and world
presentation (including the optional legacy avatar). In URP begin/end camera callbacks,
it temporarily sets/restores **forceRenderingOff**. It preserves Renderer.enabled,
active states and prior suppression, handles nested camera scopes, and restores pending
state on disable. No layers or culling masks are changed. Other actors' world bodies
remain visible to a player's gameplay camera. Render callbacks do not change gameplay,
effects timers, audio or physics. Custom rendering paths that bypass URP camera callbacks
must explicitly provide equivalent filtering; the normal Game/Scene cameras use them.

Selection currently calls Configure with the shared Player's Main Camera. Later ownership
code will call **SetOwnerCamera(localCamera)** on the locally owned actor's visibility
component, and **SetOwnerCamera(null)** for remote actors on that client. Each camera
then hides only its owner's body; every external/non-owner view hides that actor's
first-person kit and sees its world model. If renderer hierarchies change, call Configure
again to refresh the cache. No network or ownership framework was added.

World root yaw comes from parenting to Player. WorldHeroPresentation.Bind can receive
a null aim camera for a remote actor and **SetAimPitch(degrees)** can later consume
replicated aim. **AcceptedAttack** is a presentation-only hook driven by current accepted
weapon events; future remote animation notifications can use an equivalent presentation
path without invoking damage/weapon gameplay. No remote actor controller is implemented.

## Verification record

Completed automated checks:

- `Tests/RunPlayerViewVisibilityChecks.ps1`: **16 checks** against the actual filtering
  source with minimal camera/renderer shims. Tests owner/external and second-actor
  visibility, both nested camera orders, prior-hidden state, restoration on camera exit
  and disable, unchanged Renderer.enabled, subscription cleanup, and ownership reassignment.
- `Tests/RunCompilationChecks.ps1`: Runtime Editor/development/release and separate
  Editor compilation against installed Unity 6000.3.26f1/Input System assemblies passed.
  Inspector-assigned field and unused release debug-field warnings are expected.
- Source/document/staged whitespace checks passed.

No live Unity setup/validator, Play mode, native culling/shadows, rendering, physics,
audio or build test was run by the agent. The shim checks verify filtering policy and
restoration only; verify actual camera visibility (including shadows/effects and Scene
view), setup repeat safety and gameplay with the manual checklist above.
