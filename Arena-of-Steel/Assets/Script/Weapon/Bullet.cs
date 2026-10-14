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
    private Collider bulletCollider;
    private bool hasHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = -transform.right * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        VoxVoxelObject vox = collision.gameObject.GetComponentInParent<VoxVoxelObject>();
        if (vox != null)
        {
            if (vox.HasActiveVoxelNear(contactPoint, vox.VoxelSize * 1.5f, out Vector3 nearestVoxel))
            {
                HandleHit(collision.gameObject, nearestVoxel);
                return;
            }

            Vector3 flightDir = -transform.right.normalized;
            Ray ray = new Ray(transform.position - flightDir * 1.5f, flightDir);
            if (vox.RaycastVoxel(ray, 4f, out Vector3 rayHit, out _))
            {
                HandleHit(collision.gameObject, rayHit);
                return;
            }

            if (bulletCollider != null && collision.collider != null)
            {
                Physics.IgnoreCollision(bulletCollider, collision.collider, true);
            }
            return;
        }

        HandleHit(collision.gameObject, contactPoint);
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
