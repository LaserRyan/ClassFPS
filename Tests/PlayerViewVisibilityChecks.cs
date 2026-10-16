using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

// Real filtering source with renderer/camera shims; no native rendering claims.
namespace UnityEngine
{
    public class DisallowMultipleComponent : Attribute { }
    public class MonoBehaviour { }
    public class Camera { }
    public class Renderer { public bool forceRenderingOff; public bool enabled = true; }
    public class Transform
    {
        private readonly Renderer[] renderers;
        public Transform(params Renderer[] renderers) { this.renderers = renderers; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.ConvertAll(renderers, r => (T)(object)r);
    }
}
namespace UnityEngine.Rendering
{
    public struct ScriptableRenderContext { }
    public static class RenderPipelineManager
    {
        public static event Action<ScriptableRenderContext, Camera> beginCameraRendering;
        public static event Action<ScriptableRenderContext, Camera> endCameraRendering;
        public static void Begin(Camera camera) => beginCameraRendering?.Invoke(default, camera);
        public static void End(Camera camera) => endCameraRendering?.Invoke(default, camera);
    }
}
public static class PlayerViewVisibilityChecks
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception(label);
    }
    private static void Lifecycle(PlayerViewVisibility view, string name) => typeof(PlayerViewVisibility)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    public static void Main()
    {
        var local = new Camera();
        var external = new Camera();
        var fp = new Renderer();
        var body = new Renderer();
        var avatar = new Renderer();
        var hiddenEffect = new Renderer { forceRenderingOff = true };
        var otherFP = new Renderer();
        var otherBody = new Renderer();
        var a = new PlayerViewVisibility();
        var b = new PlayerViewVisibility();
        a.Configure(local, new Transform(fp, hiddenEffect), new Transform(body), new Transform(avatar));
        b.Configure(null, new Transform(otherFP), new Transform(otherBody));
        Lifecycle(a, "OnEnable");
        Lifecycle(b, "OnEnable");
        RenderPipelineManager.Begin(local);
        Check(!fp.forceRenderingOff, "owner sees first-person renderers");
        Check(body.forceRenderingOff && avatar.forceRenderingOff, "owner hides own world model and avatar");
        Check(!otherBody.forceRenderingOff, "owner sees other actor body");
        Check(otherFP.forceRenderingOff, "owner hides other actor first-person kit");
        Check(hiddenEffect.forceRenderingOff, "preserve previously hidden renderer");
        Check(body.enabled && fp.enabled, "never toggle gameplay/presentation enabled state");
        RenderPipelineManager.Begin(external);
        Check(fp.forceRenderingOff, "nested external hides first person");
        Check(!body.forceRenderingOff && !avatar.forceRenderingOff, "nested external shows world");
        RenderPipelineManager.End(external);
        Check(!fp.forceRenderingOff && body.forceRenderingOff, "nested exit restores owner state");
        RenderPipelineManager.End(local);
        Check(!body.forceRenderingOff && !fp.forceRenderingOff, "camera exit restores initial state");
        Check(hiddenEffect.forceRenderingOff, "hidden initial state survives restoration");
        RenderPipelineManager.Begin(external);
        RenderPipelineManager.Begin(local);
        Check(!fp.forceRenderingOff && body.forceRenderingOff, "owner inside external scope is correct");
        RenderPipelineManager.End(local);
        Check(fp.forceRenderingOff && !body.forceRenderingOff, "restore external after nested owner");
        RenderPipelineManager.End(external);
        a.SetOwnerCamera(null);
        RenderPipelineManager.Begin(local);
        Check(fp.forceRenderingOff && !body.forceRenderingOff, "remote ownership policy");
        RenderPipelineManager.End(local);
        a.SetOwnerCamera(local);
        RenderPipelineManager.Begin(local);
        Lifecycle(a, "OnDisable");
        Check(!body.forceRenderingOff && !fp.forceRenderingOff, "disable restores pending suppression");
        RenderPipelineManager.End(local);
        Lifecycle(b, "OnDisable");
        RenderPipelineManager.Begin(external);
        Check(!fp.forceRenderingOff && !otherFP.forceRenderingOff, "disabled subscribers removed");
        RenderPipelineManager.End(external);
        Console.WriteLine(checks + " camera visibility checks passed.");
    }
}
