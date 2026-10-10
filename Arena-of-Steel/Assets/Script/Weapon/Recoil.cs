using UnityEngine;

public class Recoil : MonoBehaviour
{
    [SerializeField] private Transform pivotPoint;
    [Header("반동 정도")]
    public float pitchKick = 1.2f;
    public float yawKick = 0.4f;
    public float yawRandomness = 1.0f;

    [Header("반동 회복")]
    [Tooltip("반동이 다시 중앙으로 복귀 속도")]
    public float returnStrength = 20f;

    [Tooltip("얼마나 겹치는지")]
    public float damping = 18f;

    [Header("제한")]
    public float maxPitch = 20f;

    private Vector2 recoilOffset;
    private Vector2 recoilVelocity;

    private void LateUpdate()
    {
        float dt = Time.deltaTime;

        Vector2 accel = (-returnStrength * recoilOffset) - (damping * recoilVelocity);
        recoilVelocity += accel * dt;
        recoilOffset += recoilVelocity * dt;

        pivotPoint.localRotation = Quaternion.Euler(-recoilVelocity.y, recoilOffset.x, 0f);
    }


    public void AddRecoil(float recoilMultiplier = 1f)
    {
        float yaw = Random.Range(-yawKick, yawKick) * yawRandomness;

        recoilOffset.y += pitchKick * recoilMultiplier;
        recoilOffset.x += yaw * recoilMultiplier;
    }

    public void ResetRecoil()
    {
        recoilOffset = Vector2.zero;
        recoilVelocity = Vector2.zero;
        pivotPoint.localRotation = Quaternion.identity;
    }

}
