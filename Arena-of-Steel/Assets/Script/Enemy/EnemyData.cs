using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "Enemy";
    public GameObject prefab;
    public int maxHealth = 100;
    public float moveSpeed = 3.5f;
    public float stoppingDistance = 1.5f;
    public float attackRange = 2f;
    public float attackCooldown = 1f;
    public int attackDamage = 10;
}
