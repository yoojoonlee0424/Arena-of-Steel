using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Linq;



public class Enemy : MonoBehaviour
{
    public int health = 100;
    public int damageHit;

    public GameObject bulletPrefab;
    public Transform bulletSpwanPoint;
    public GameObject WeaponFlash;
    public float bloom;
    public float fireRate;
    public float lastShotTime = 0f;

    public Material hitMat;

    public GameObject EnemyBody;

    private Rigidbody rb;
    private Renderer rend;
    private Material originalMaterial;

    [Header("Ai ¼³Á¤")]
    public int currentPointIndex = 0;
    public Vector3 currentTarget;
    public float positionThreshold;
    public float idleTime = 5f;
    public float attackDistance = 5f;
    public float maxVisionDistance = 20f;
    public float minChasingHealth = 30f;

    public Transform[] partrolPoints;
    private float idleTimeCounter;
    private Transform playerTransform;
    private bool canSeePlayer;
    private Vector3 lastknowPlayerPosition;


    private NavMeshAgent agent;


    public enum State {Idle, Partrolling, Chasing, Attacking }
    public State state = State.Idle;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();
        originalMaterial = rend.material;

        agent = GetComponent<NavMeshAgent>();
        playerTransform = GameObject.FindWithTag("Player").GetComponent<Transform>();


        GameObject patrolPointParent = GameObject.FindWithTag("PatrolPoint");
        partrolPoints = patrolPointParent.GetComponentsInChildren<Transform>().Where(t => t != patrolPointParent.transform).ToArray();

    }


    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Damage")
        {
            health -= damageHit;

            if(health <= 0)
            {
                Die();
            }
            else
            {
                StartCoroutine(Blink());
            }
        }
    }

    void Die()
    {
        if(!this.enabled)
        {
            return;
        }



        rb.freezeRotation = true;
        transform.rotation = Quaternion.Euler(transform.rotation.x,transform.rotation.y, transform.rotation.z +90f);
        EnemyBody.SetActive(false);
        this.enabled = false;
    }

    IEnumerator Blink()
    {
        rend.material = hitMat;
        yield return new WaitForSeconds(0.1f);
        rend.material = originalMaterial;
    }


    private void Update()
    {
        LookForPlayer();

        switch(state)
        {
            case State.Idle:
                Idle();
                break;
            case State.Partrolling:
                Patrolling();
                break;
            case State.Attacking:
                Attacking();
                break;
            case State.Chasing:
                Chasing();
                break;

        }


        rb.linearVelocity = Vector3.zero;

        LookAtPlayer();
        SetLastKnownPlayerPosition();
    }

    private void LookForPlayer()
    {
        Vector3 directionToPlayer = playerTransform.position - transform.position;

        if(Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, maxVisionDistance))
        {
            canSeePlayer = hit.transform == playerTransform;

            if(canSeePlayer && state != State.Attacking)
            {
                state = State.Chasing;
            }


        }
    }


    private void Idle()
    {
        agent.ResetPath();

        idleTimeCounter -= Time.deltaTime;

        if( idleTimeCounter < 0 )
        {
            state = State.Partrolling;
            idleTimeCounter = idleTime;
        }
    }

    private void Patrolling()
    {
        if(Vector3.Distance(currentTarget, transform.position) < positionThreshold)
        {
            float chance = Random.Range(0, 100);

            if(chance < 10)
            {
                state = State.Idle;
                return;
            }

            currentPointIndex++;
            currentTarget = partrolPoints[currentPointIndex %  partrolPoints.Length].position;
        }
        else
        {
            agent.SetDestination(currentTarget);
        }


    }

    private void Attacking()
    {
        idleTimeCounter = idleTime;
        agent.ResetPath();


        Shoot();


        if(Vector3.Distance(transform.position, playerTransform.position) > attackDistance || !canSeePlayer)
        {
            if(health < minChasingHealth)
            {
                state = State.Partrolling;
            }
            else
            {
                state = State.Chasing;
            }
        }
    }

    private void Chasing()
    {
        idleTimeCounter = idleTime;
        agent.SetDestination(lastknowPlayerPosition);

        if(health < minChasingHealth)
        {
            state = State.Partrolling;
        }
        else if(Vector3.Distance(transform.position, playerTransform.position) <= attackDistance && canSeePlayer)
        {
            state = State.Attacking;
        }
        else if(Vector3.Distance(transform.position, playerTransform.position) > maxVisionDistance)
        {
            state = State.Partrolling;
        }
        else if(Vector3.Distance(transform.position, playerTransform.position) < positionThreshold && !canSeePlayer)
        {
            state = State.Partrolling;
        }
    }


    private void LookAtPlayer()
    {
        if(canSeePlayer)
        {
            transform.LookAt(new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z));
        }
    }

    private void SetLastKnownPlayerPosition()
    {
        if(canSeePlayer)
        {
            lastknowPlayerPosition = playerTransform.position;
        }
    }


    private void Shoot()
    {
        if(Time.time > lastShotTime + fireRate)
        {
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.Normalize();

            Quaternion bulletRotation = Quaternion.LookRotation(directionToPlayer);

            float maxInaccuracy = 10f;
            float currentInaccuracy = bloom * maxInaccuracy;
            float randomJaw = Random.Range(-currentInaccuracy, currentInaccuracy);
            float randomPitch = Random.Range(-currentInaccuracy, currentInaccuracy);

            bulletRotation *= Quaternion.Euler(randomPitch, randomJaw + 90, 0f);

            Instantiate(bulletPrefab, bulletSpwanPoint.position, bulletRotation);
            Instantiate(WeaponFlash, bulletSpwanPoint.position, bulletSpwanPoint.rotation);
            lastShotTime = Time.time;

        }
    }

}
