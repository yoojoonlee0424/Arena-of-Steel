using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    private float lastAttackTime = 0f;

    protected override void Attack()
    {
        if (enemyData == null || playerTransform == null) return;
        if (Time.time <= lastAttackTime + enemyData.attackCooldown) return;

        lastAttackTime = Time.time;

        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(enemyData.attackDamage);
        }
    }
}