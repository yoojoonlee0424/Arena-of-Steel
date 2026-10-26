using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    private float lastAttackTime = 0f;

    public bool isAttacking = false;
    protected override void Attack()
    {
        if (enemyData == null || playerTransform == null) return;
        isAttacking = false;
        if (Time.time <= lastAttackTime + enemyData.attackCooldown) return;

        lastAttackTime = Time.time;

        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(enemyData.attackDamage);
            isAttacking=true;
        }
    }
}