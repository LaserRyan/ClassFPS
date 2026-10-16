using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Instance-specific filtering: never changes layers, camera masks, or Renderer.enabled.
// A null owner camera represents an actor with no first-person view on this client.
[DisallowMultipleComponent]
public sealed class PlayerViewVisibility : MonoBehaviour
{
    private Camera ownerCamera;
    private Renderer[] firstPerson = new Renderer[0];
    private Renderer[] world = new Renderer[0];
    private readonly Stack<CameraState> pending = new Stack<CameraState>();
    private readonly Stack<CameraState> reusable = new Stack<CameraState>();
    private sealed class CameraState
    {
        public Camera Camera;
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public readonly List<bool> Previous = new List<bool>();
    }

    public void Configure(Camera owner, Transform firstPersonRoot, Transform worldRoot, Transform avatar = null)
    {
        RestoreAll();
        ownerCamera = owner;
        firstPerson = firstPersonRoot != null ? firstPersonRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        var renderers = new List<Renderer>();
        if (worldRoot != null) renderers.AddRange(worldRoot.GetComponentsInChildren<Renderer>(true));
        if (avatar != null) renderers.AddRange(avatar.GetComponentsInChildren<Renderer>(true));
        world = renderers.ToArray();
    }

    // Ownership integration point. Bind the local view only; remote actors receive null.
    public void SetOwnerCamera(Camera camera) { RestoreAll(); ownerCamera = camera; }
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        RestoreAll();
    }
    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        CameraState state = reusable.Count > 0 ? reusable.Pop() : new CameraState();
        state.Camera = camera;
        // Only suppress this actor's renderers; preserve prior renderer state, including
        // nested camera rendering and renderers intentionally hidden by other systems.
        Apply(state, firstPerson, camera != ownerCamera);
        Apply(state, world, camera == ownerCamera);
        pending.Push(state);
    }
    private void Apply(CameraState state, Renderer[] renderers, bool hidden)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            bool baseline = renderer.forceRenderingOff;
            foreach (CameraState outer in pending)
                for (int i = 0; i < outer.Renderers.Count; i++)
                    if (outer.Renderers[i] == renderer) baseline = outer.Previous[i];
            state.Renderers.Add(renderer);
            state.Previous.Add(renderer.forceRenderingOff);
            renderer.forceRenderingOff = baseline || hidden;
        }
    }
    private void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (pending.Count > 0 && pending.Peek().Camera == camera) Restore(pending.Pop());
    }
    private void Restore(CameraState state)
    {
        for (int i = state.Renderers.Count - 1; i >= 0; i--)
            if (state.Renderers[i] != null) state.Renderers[i].forceRenderingOff = state.Previous[i];
        state.Renderers.Clear();
        state.Previous.Clear();
        state.Camera = null;
        reusable.Push(state);
    }
    private void RestoreAll() { while (pending.Count > 0) Restore(pending.Pop()); }
}
