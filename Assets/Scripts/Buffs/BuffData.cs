using UnityEngine;

// One buff = one asset. Create -> Steel Roulette -> Buff
[CreateAssetMenu(menuName = "Steel Roulette/Buff")]
public class BuffData : ScriptableObject
{
    public string displayName = "Sharpness";
    public int bonusDamage = 0;
    public int bonusMaxHP = 0;
    public int bonusSpeed = 0;     // percent, 10 = +10% speed
}