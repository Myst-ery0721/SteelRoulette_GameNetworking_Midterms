using UnityEngine;

// One weapon = one asset. Create -> Steel Roulette -> Weapon
[CreateAssetMenu(menuName = "Steel Roulette/Weapon")]
public class WeaponData : ScriptableObject
{
    public string displayName = "Sword";
    public int damage = 20;
    public float range = 1.8f;     // how far the swing reaches
    public float cooldown = 0.6f;  // seconds between swings
}