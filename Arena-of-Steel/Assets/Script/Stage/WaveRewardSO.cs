using UnityEngine;

public enum RewardType
{
    Heal,
    WeaponDrop
}

[CreateAssetMenu(fileName = "NewReward", menuName = "WaveReward/Reward")]
public class WaveRewardSO : ScriptableObject
{
    [Header("UI Info")]
    public string rewardName = "Reward";
    [TextArea] public string description = "Description";
    public Sprite icon;

    [Header("Reward Settings")]
    public RewardType rewardType;
    public int intValue = 50;
    public GunSO weaponData;
}
