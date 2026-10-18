using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyBase : MonoBehaviour
{
    public static event Action<EnemyBase> OnAnyEnemyDied;
    public static event Action<EnemyBase> OnBossSpawned;
    public static event Action<EnemyBase> OnBossDied;
    public static event Action<int, int> OnBossHealthChanged;
    public event Action OnDeath;

    public bool IsBoss => enemyData != null && enemyData.isBoss;

    [Header("Enemy Stats")]
    public EnemyData enemyData;

    [Header("Visual & Feedback")]
    public Material hitMat;
    public GameObject EnemyBody;

    protected int health;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected Rigidbody rb;
    protected Renderer rend;
    protected Material originalMaterial;
    protected bool isDead;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            originalMaterial = rend.material;
        }
    }

    protected virtual void Start()
    {
        ApplyEnemyData();
        if (IsBoss)
        {
            OnBossSpawned?.Invoke(this);
            OnBossHealthChanged?.Invoke(health, enemyData.maxHealth);
        }
        if (playerTransform == null)
        {
            FindPlayer();
        }
    }

    protected virtual void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        float attackRange = enemyData != null ? enemyData.attackRange : 0f;

        if (distance <= attackRange)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            LookAtPlayer();
            Attack();
        }
        else if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    public virtual void SetTarget(Transform target)
    {
        playerTransform = target;
    }

    public virtual void ApplyEnemyData()
    {
        if (enemyData == null) return;

        health = enemyData.maxHealth;

        if (agent != null)
        {
            agent.speed = enemyData.moveSpeed;
            agent.stoppingDistance = enemyData.stoppingDistance;
        }
    }

    protected virtual void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    protected virtual void LookAtPlayer()
    {
        if (playerTransform == null) return;
        Vector3 lookTarget = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        transform.LookAt(lookTarget);
    }

    public virtual void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;
        if (IsBoss)
        {
            OnBossHealthChanged?.Invoke(Mathf.Max(0, health), enemyData.maxHealth);
        }

        if (health <= 0)
        {
            Die();
        }
        else if (hitMat != null && rend != null)
        {
            StartCoroutine(BlinkRoutine());
        }
    }

    protected virtual IEnumerator BlinkRoutine()
    {
        rend.material = hitMat;
        yield return new WaitForSeconds(0.1f);
        rend.material = originalMaterial;
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        if (IsBoss)
        {
            OnBossDied?.Invoke(this);
        }

        OnDeath?.Invoke();
        OnAnyEnemyDied?.Invoke(this);

        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (IsBoss && !isDead)
        {
            OnBossDied?.Invoke(this);
        }
    }

    protected abstract void Attack();
}