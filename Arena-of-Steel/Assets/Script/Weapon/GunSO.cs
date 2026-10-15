using UnityEngine;

[CreateAssetMenu(fileName = "GunSO", menuName = "Scriptable Objects/GunSO")]
public class GunSO : ScriptableObject
{
    [Header("무기 설정")]
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magsize = 30;
    [Header("총알 종류")]
    public GameObject bullet;
    [Header("총기 이펙트 종류")]
    public GameObject weaponFlash;
    [Header("반동 정도")]
    public float pitchKick = 1.2f;
    public float yawKick = 0.4f;
    public float yawRandomness = 1.0f;
    [Header("반동 회복")]
    [Tooltip("반동이 다시 중앙으로 복귀 속도")]
    public float returnStrength = 20f;

    [Tooltip("얼마나 겹치는지")]
    public float damping = 18f;


}
