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
    [Range(0f, 1f)]
    [Tooltip("거리 감쇠율 (0 = 일반 탄환/감쇠 없음, 0~1 = 폭발형 외곽 데미지 감쇠 비율)")]
    public float distanceFalloff = 0f;
    public float explosionForce = 3.5f;
    public bool isolateFragments = true;
    public float fragmentLifetime = 6f;

    private Rigidbody rb;
    private Collider bulletCollider;
    private GameObject shooter;
    private Vector3 flightDir;
    private bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
        flightDir = -transform.right.normalized;
    }

    void Start()
    {
        flightDir = -transform.right.normalized;
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = flightDir * speed;
        }
        Destroy(gameObject, lifeTime);
    }

    public void Init(int newDamage, GameObject newShooter = null, float newSpeed = -1f)
    {
        damage = newDamage;
        shooter = newShooter;
        flightDir = -transform.right.normalized;

        if (newSpeed > 0f)
        {
            speed = newSpeed;
            if (rb != null)
            {
                rb.linearVelocity = flightDir * speed;
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

        OnHit(collision.gameObject, contactPoint, null, collision.collider);
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

        Vector3 dir = flightDir != Vector3.zero ? flightDir : -transform.right.normalized;
        if (vox.HasActiveVoxelNear(contactPoint, vox.VoxelSize * 2f, out Vector3 nearestVoxel))
        {
            OnHit(collision.gameObject, nearestVoxel, vox, collision.collider);
            return true;
        }

        Ray ray = new Ray(transform.position - dir * 1.5f, dir);
        if (vox.RaycastVoxel(ray, 4f, out Vector3 rayHit, out _))
        {
            OnHit(collision.gameObject, rayHit, vox, collision.collider);
            return true;
        }

        OnHit(collision.gameObject, contactPoint, vox, collision.collider);
        return true;
    }

    private void OnHit(GameObject target, Vector3 point, VoxVoxelObject vox = null, Collider hitCollider = null)
    {
        if (hasHit) return;

        DamageTarget(target);
        DamageVoxel(target, point, vox);
        SpawnHitEffect(point);
        HandlePierce(hitCollider);
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
            Vector3 dir = flightDir != Vector3.zero ? flightDir : -transform.right.normalized;
            vox.DamageSphere(point, new VoxelDamageArgs
            {
                radius = explosionRadius,
                damage = damage,
                distanceFalloff = distanceFalloff,
                isolateFragments = isolateFragments,
                explosionForce = explosionForce,
                fragmentLifetime = fragmentLifetime,
                impactDirection = dir
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

    private void HandlePierce(Collider hitCollider)
    {
        if (pierceCount > 0)
        {
            pierceCount--;
            if (bulletCollider != null && hitCollider != null)
                Physics.IgnoreCollision(bulletCollider, hitCollider, true);
            if (rb != null && flightDir != Vector3.zero)
                rb.linearVelocity = flightDir * speed;
        }
        else
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}