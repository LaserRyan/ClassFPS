using UnityEngine;

// One resource shared by both actions of a fire kit, configured from its definition.
[DisallowMultipleComponent]
public sealed class FireEnergy : MonoBehaviour
{
    private HeatAmmoState state;
    public HeatAmmoState State
    {
        get
        {
            if (state == null)
            {
                FireSpellWeapon weapon = GetComponent<FireSpellWeapon>();
                FireSpellDefinition definition = weapon != null ? weapon.Spell : null;
                state = new HeatAmmoState(definition != null ? definition.MaximumEnergy : 100f,
                    definition != null ? definition.RegenerationDelay : 2f,
                    definition != null ? definition.RegenerationRate : 25f,
                    definition != null ? definition.OverheatDuration : 3f, Time.time);
            }
            return state;
        }
    }
    private void Update() => State.Advance(Time.time);
    public void ResetFull() => State.ResetFull(Time.time);
}
