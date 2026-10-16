using UnityEngine;

public abstract class HeroPassive : MonoBehaviour
{
    public abstract void Bind(PlayerHealth owner, Weapon primary);
}
