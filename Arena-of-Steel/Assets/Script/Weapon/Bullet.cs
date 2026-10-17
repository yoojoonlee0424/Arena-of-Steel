using UnityEngine;
using VoxelDestructionPro.Vox;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 15f;
    public float lifeTime = 3f;
    public int damage = 20;
    public int pierceCount = 0;
    public GameObject hitEffect;

    [Header("Voxel Destruction Settings")]
    public bool enableVoxelDamage = true;
    public float explosionRadius = 1.2f;
    public float explosionForce = 3.5f;
    public bool isolateFragments = true;
    public float fragmentLifetime = 6f;

    private Rigidbody rb;
    private Collider bulletCollider;
    private GameObject shooter;
    private bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
    }

    void Start()
    {
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = -transform.right * speed;
        }
        Destroy(gameObject, lifeTime);
    }

    public void Init(int newDamage, GameObject newShooter = null, float newSpeed = -1f)
    {
        damage = newDamage;
        shooter = newShooter;

        if (newSpeed > 0f)
        {
            speed = newSpeed;
            if (rb != null)
            {
                rb.linearVelocity = -transform.right * speed;
            }
        }

        if (shooter != null && bulletCollider != null)
        {
            Collider[] shooterColliders = shooter.GetComponentsInChildren<Collider>();
            for (int i = 0; i < shooterColliders.Length; i++)
            {
                Physics.IgnoreCollision(bulletCollider, shooterColliders[i], true);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit || CanIgnoreCollision(collision)) return;

        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        if (HitVoxel(collision, contactPoint)) return;

        OnHit(collision.gameObject, contactPoint);
    }

    private bool CanIgnoreCollision(Collision collision)
    {
        if (shooter == null) return false;

        if (collision.gameObject == shooter || collision.transform.IsChildOf(shooter.transform))
        {
            return true;
        }

        bool shooterIsEnemy = shooter.GetComponentInParent<EnemyBase>() != null;
        bool hitIsEnemy = collision.gameObject.GetComponentInParent<EnemyBase>() != null;
        if (shooterIsEnemy && hitIsEnemy)
        {
            if (bulletCollider != null && collision.collider != null)
            {
                Physics.IgnoreCollision(bulletCollider, collision.collider, true);
            }
            return true;
        }

        bool shooterIsPlayer = shooter.CompareTag("Player") || shooter.GetComponentInParent<PlayerHealth>() != null;
        bool hitIsPlayer = collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<PlayerHealth>() != null;
        if (shooterIsPlayer && hitIsPlayer)
        {
            if (bulletCollider != null && collision.collider != null)
            {
                Physics.IgnoreCollision(bulletCollider, collision.collider, true);
            }
            return true;
        }

        return false;
    }

    private bool HitVoxel(Collision collision, Vector3 contactPoint)
    {
        if (!enableVoxelDamage) return false;

        VoxVoxelObject vox = collision.gameObject.GetComponentInParent<VoxVoxelObject>();
        if (vox == null) return false;

        if (vox.HasActiveVoxelNear(contactPoint, vox.VoxelSize * 1.5f, out Vector3 nearestVoxel))
        {
            OnHit(collision.gameObject, nearestVoxel, vox);
            return true;
        }

        Vector3 flightDir = -transform.right.normalized;
        Ray ray = new Ray(transform.position - flightDir * 1.5f, flightDir);
        if (vox.RaycastVoxel(ray, 4f, out Vector3 rayHit, out _))
        {
            OnHit(collision.gameObject, rayHit, vox);
            return true;
        }

        if (bulletCollider != null && collision.collider != null)
        {
            Physics.IgnoreCollision(bulletCollider, collision.collider, true);
        }
        return true;
    }

    private void OnHit(GameObject target, Vector3 point, VoxVoxelObject vox = null)
    {
        if (hasHit) return;

        DamageTarget(target);
        DamageVoxel(target, point, vox);
        SpawnHitEffect(point);
        HandlePierce();
    }

    private void DamageTarget(GameObject target)
    {
        EnemyBase enemy = target.GetComponentInParent<EnemyBase>();
        if (enemy != null && (shooter == null || !shooter.GetComponentInParent<EnemyBase>()))
        {
            enemy.TakeDamage(damage);
        }

        PlayerHealth player = target.GetComponentInParent<PlayerHealth>();
        if (player != null && (shooter == null || (!shooter.CompareTag("Player") && !shooter.GetComponentInParent<PlayerHealth>())))
        {
            player.TakeDamage(damage);
        }
    }

    private void DamageVoxel(GameObject target, Vector3 point, VoxVoxelObject vox = null)
    {
        if (!enableVoxelDamage) return;

        if (vox == null) vox = target.GetComponentInParent<VoxVoxelObject>();
        if (vox != null)
        {
            vox.DamageSphere(point, new VoxelDamageArgs
            {
                radius = explosionRadius,
                isolateFragments = isolateFragments,
                explosionForce = explosionForce,
                fragmentLifetime = fragmentLifetime,
                impactDirection = -transform.right.normalized
            });
        }
    }

    private void SpawnHitEffect(Vector3 point)
    {
        if (hitEffect != null)
        {
            Instantiate(hitEffect, point, Quaternion.identity);
        }
    }

    private void HandlePierce()
    {
        if (pierceCount > 0)
        {
            pierceCount--;
        }
        else
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}