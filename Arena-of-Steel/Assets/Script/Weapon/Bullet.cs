using UnityEngine;
using VoxelDestructionPro.Vox;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 15f;
    public float lifeTime = 3f;

    [Header("Voxel Destruction Settings")]
    public float explosionRadius = 1.2f;
    public float explosionForce = 3.5f;
    public bool isolateFragments = true;
    public float fragmentLifetime = 6f;

    private Rigidbody rb;
    private bool hasHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = -transform.right * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        HandleHit(collision.gameObject, hitPoint);
    }

    private void HandleHit(GameObject hitObj, Vector3 hitPoint)
    {
        if (hasHit) return;
        hasHit = true;

        VoxVoxelObject vox = hitObj.GetComponentInParent<VoxVoxelObject>();
        if (vox != null)
        {
            vox.DamageSphere(hitPoint, new VoxelDamageArgs
            {
                radius = explosionRadius,
                isolateFragments = isolateFragments,
                explosionForce = explosionForce,
                fragmentLifetime = fragmentLifetime,
                impactDirection = -transform.right.normalized
            });
        }

        Destroy(gameObject);
    }
}
