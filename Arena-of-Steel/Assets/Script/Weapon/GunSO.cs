using UnityEngine;

[CreateAssetMenu(fileName = "GunSO", menuName = "Scriptable Objects/GunSO")]
public class GunSO : ScriptableObject
{
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magsize = 30;
    public int damage = 25;
    public GameObject bullet;
    public GameObject weaponFlash;
    public float pitchKick = 1.2f;
    public float yawKick = 0.4f;
    public float yawRandomness = 1.0f;
    public float returnStrength = 20f;
    public float damping = 18f;

    [Header("Aim Settings")]
    public float aimSmoothTime = 20f;
    public float aimOffsetX = 0f;
    public float aimOffsetY = 0f;
    public float aimOffsetZ = 0f;

    [Header("Drop Settings")]
    public GameObject pickupPrefab;
}