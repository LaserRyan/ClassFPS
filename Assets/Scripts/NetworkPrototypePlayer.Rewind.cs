using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Historical gameplay capsules only. Never rewind transforms, visual models or physics scenes.
public static class NetworkCapsuleQuery
{
    public static float RayDistance(Ray ray, Vector3 a, Vector3 b, float radius)
    {
        Vector3 ba = b - a, oa = ray.origin - a;
        float baba = Vector3.Dot(ba, ba), bard = Vector3.Dot(ba, ray.direction);
        float baoa = Vector3.Dot(ba, oa), rdoa = Vector3.Dot(ray.direction, oa);
        Vector3 closest = a + ba * (baba > .00001f ? Mathf.Clamp01(baoa / baba) : 0);
        if ((ray.origin - closest).sqrMagnitude <= radius * radius) return 0;
        float aa = baba - bard * bard, bb = baba * rdoa - baoa * bard;
        float cc = baba * Vector3.Dot(oa, oa) - baoa * baoa - radius * radius * baba;
        float best = float.PositiveInfinity;
        float h = bb * bb - aa * cc;
        if (h >= 0 && Mathf.Abs(aa) > .00001f)
        {
            float t = (-bb - Mathf.Sqrt(h)) / aa;
            float y = baoa + t * bard;
            if (t >= 0 && y >= 0 && y <= baba) best = t;
        }
        best = Mathf.Min(best, Sphere(ray, a, radius));
        return Mathf.Min(best, Sphere(ray, b, radius));
    }
    private static float Sphere(Ray ray, Vector3 center, float radius)
    {
        Vector3 offset = ray.origin - center;
        float b = Vector3.Dot(offset, ray.direction), c = offset.sqrMagnitude - radius * radius;
        float h = b * b - c;
        if (h < 0) return float.PositiveInfinity;
        float t = -b - Mathf.Sqrt(h);
        return t >= 0 ? t : float.PositiveInfinity;
    }
    public static double RewindTime(double now, double sample, double renderDelay, double rttSeconds, double maximum)
    {
        // NGO NetworkTimeSystem defaults to a 50 ms buffered client ServerTime.
        double budget = System.Math.Min(maximum, System.Math.Max(0, rttSeconds * .5 + renderDelay + .05 + .035));
        return System.Math.Max(now - budget, System.Math.Min(now, sample - renderDelay));
    }
}
public sealed partial class NetworkPrototypePlayer
{
    private readonly NetworkVariable<float> renderDelay = new NetworkVariable<float>(.1f);
    private double RenderDelay => renderDelay.Value;
    private struct CapsuleFrame { public double Time; public Vector3 A, B; public float Radius; }
    private readonly List<CapsuleFrame> hitHistory = new List<CapsuleFrame>();
    private CapsuleFrame CurrentCapsule()
    {
        CharacterController capsule = GetComponent<CharacterController>();
        Vector3 scale = transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float half = Mathf.Max(0, capsule.height * Mathf.Abs(scale.y) * .5f - radius);
        Vector3 center = transform.TransformPoint(capsule.center);
        return new CapsuleFrame { Time = ServerNow, A = center - Vector3.up * half,
            B = center + Vector3.up * half, Radius = radius };
    }
    private void RecordHistory()
    {
        CapsuleFrame frame = CurrentCapsule();
        if (hitHistory.Count > 0 && hitHistory[hitHistory.Count - 1].Time == frame.Time)
            hitHistory[hitHistory.Count - 1] = frame;
        else hitHistory.Add(frame);
        while (hitHistory.Count > 64) hitHistory.RemoveAt(0);
    }
    private CapsuleFrame HistoricalCapsule(double time)
    {
        if (hitHistory.Count == 0) return CurrentCapsule();
        CapsuleFrame a = hitHistory[0];
        for (int i = 1; i < hitHistory.Count; i++)
        {
            CapsuleFrame b = hitHistory[i];
            if (b.Time >= time)
            {
                float t = b.Time > a.Time ? Mathf.Clamp01((float)((time - a.Time) / (b.Time - a.Time))) : 0;
                return new CapsuleFrame { A = Vector3.Lerp(a.A, b.A, t), B = Vector3.Lerp(a.B, b.B, t), Radius = b.Radius };
            }
            a = b;
        }
        return a;
    }
    private NetworkPrototypePlayer ResolveCompensatedHit(Ray ray, float range, double sampleTime, out Vector3 endpoint)
    {
        float nearest = range;
        HitscanGun source = selection.SelectedHero.KitPrefab.PrimaryWeapon as HitscanGun;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, range, source.HitLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<NetworkPrototypePlayer>() != null) continue;
            nearest = Mathf.Min(nearest, hit.distance);
        }
        double rtt = NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(OwnerClientId) / 1000.0;
        double time = IsTestOpponent ? ServerNow : NetworkCapsuleQuery.RewindTime(ServerNow, sampleTime,
            RenderDelay, rtt, NetworkPrototypeSession.Active.MaximumRewind);
        NetworkPrototypePlayer target = null;
        foreach (NetworkObject obj in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            NetworkPrototypePlayer actor = obj.GetComponent<NetworkPrototypePlayer>();
            if (actor == null || actor == this || actor.combatState.Value.Dead || actor.appliedRevision == 0) continue;
            if ((source.HitLayers.value & (1 << actor.gameObject.layer)) == 0) continue;
            CapsuleFrame capsule = actor.HistoricalCapsule(time);
            float distance = NetworkCapsuleQuery.RayDistance(ray, capsule.A, capsule.B, capsule.Radius);
            if (distance >= nearest) continue;
            nearest = distance; target = actor;
        }
        endpoint = ray.GetPoint(nearest);
        return target;
    }
}
