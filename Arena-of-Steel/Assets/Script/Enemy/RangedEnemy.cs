using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Ranged Weapon Settings")]
    public GameObject bulletPrefab;
    public Transform bulletSpwanPoint;
    public GameObject WeaponFlash;
    public float bloom = 0.75f;

    private float lastShotTime = 0f;

    protected override void Attack()
    {
        if (enemyData == null || bulletPrefab == null || bulletSpwanPoint == null) return;
        if (Time.time <= lastShotTime + enemyData.attackCooldown) return;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        Quaternion bulletRotation = Quaternion.LookRotation(directionToPlayer);

        float maxInaccuracy = 10f;
        float currentInaccuracy = bloom * maxInaccuracy;
        float randomYaw = Random.Range(-currentInaccuracy, currentInaccuracy);
        float randomPitch = Random.Range(-currentInaccuracy, currentInaccuracy);

        bulletRotation *= Quaternion.Euler(randomPitch, randomYaw + 90f, 0f);

        GameObject bulletObj = Instantiate(bulletPrefab, bulletSpwanPoint.position, bulletRotation);
        Bullet bulletComp = bulletObj.GetComponent<Bullet>();
        if (bulletComp != null)
        {
            bulletComp.Init(enemyData.attackDamage, gameObject);
        }

        if (WeaponFlash != null)
        {
            Instantiate(WeaponFlash, bulletSpwanPoint.position, bulletSpwanPoint.rotation);
        }

        lastShotTime = Time.time;
    }
}