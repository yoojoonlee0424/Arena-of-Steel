using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;

public class Bot_animation : MonoBehaviour
{
    [SerializeField] private float dampTime = 0.4f;
    [SerializeField] private Animator animator;
    private NavMeshAgent navMeshAgent;
    private RangedEnemy RangedEnemy;
    private MeleeEnemy MeleeEnemy;

    private float speed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();    
        RangedEnemy = GetComponent<RangedEnemy>();
        MeleeEnemy = GetComponent<MeleeEnemy>();
    }

    // Update is called once per frame
    void Update()
    {
        speed = navMeshAgent.velocity.magnitude / navMeshAgent.speed;
        animator.SetFloat("Speed", speed, dampTime, Time.deltaTime);

        if(RangedEnemy != null&&RangedEnemy.isShooting)
        {
            Shoot();
        }

        if(MeleeEnemy != null&&MeleeEnemy.isAttacking)
        {
            Melee();
        }
    }

    void Shoot()
    {
        animator.SetTrigger("Shooting");
    }

    void Melee()
    {
        animator.SetTrigger("Melee");
    }
}
