using UnityEngine;

public class Recoil : MonoBehaviour
{
    [SerializeField] private WeaponSwap weaponSwap;
    [SerializeField] private Transform pivotPoint;
    public GunSO GunSO;
    public GunSO GunSO_2;


    private Vector2 recoilOffset;
    private Vector2 recoilVelocity;


    private void LateUpdate()
    {
        if(weaponSwap.isMain == true)
        {
            float dt = Time.deltaTime;

            Vector2 accel = (-GunSO.returnStrength * recoilOffset) - (GunSO.damping * recoilVelocity);
            recoilVelocity += accel * dt;
            recoilOffset += recoilVelocity * dt;

            pivotPoint.localRotation = Quaternion.Euler(-recoilVelocity.y, recoilOffset.x, 0f);
        }
        else
        {
            float dt = Time.deltaTime;

            Vector2 accel = (-GunSO_2.returnStrength * recoilOffset) - (GunSO_2.damping * recoilVelocity);
            recoilVelocity += accel * dt;
            recoilOffset += recoilVelocity * dt;

            pivotPoint.localRotation = Quaternion.Euler(-recoilVelocity.y, recoilOffset.x, 0f);
        }

    }


    public void AddRecoil(float recoilMultiplier = 1f)
    {
        if(weaponSwap.isMain == true)
        {
            float yaw = Random.Range(-GunSO.yawKick, GunSO.yawKick) * GunSO.yawRandomness;

            recoilOffset.y += GunSO.pitchKick * recoilMultiplier;
            recoilOffset.x += yaw * recoilMultiplier;
        }
        else
        {
            float yaw = Random.Range(-GunSO_2.yawKick, GunSO_2.yawKick) * GunSO_2.yawRandomness;

            recoilOffset.y += GunSO_2.pitchKick * recoilMultiplier;
            recoilOffset.x += yaw * recoilMultiplier;
        }
    }

    public void ResetRecoil()
    {
        recoilOffset = Vector2.zero;
        recoilVelocity = Vector2.zero;
        pivotPoint.localRotation = Quaternion.identity;
    }

}
