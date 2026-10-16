using UnityEngine;

public abstract class HeroSecondary : MonoBehaviour
{
    public abstract void Bind(FPSController movement);
    public abstract void Activate(WeaponAttackContext context);
    public abstract void Cancel();
    public abstract void ResetForRespawn();
}
