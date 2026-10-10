using UnityEngine;

[CreateAssetMenu(fileName = "GunSO", menuName = "Scriptable Objects/GunSO")]
public class GunSO : ScriptableObject
{
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magsize = 30;

    public GameObject bullet;

    public GameObject weaponFlash;

}
