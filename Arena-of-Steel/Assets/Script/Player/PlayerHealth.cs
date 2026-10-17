using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    public static event Action<float, float> OnHpChanged;
    public static event Action OnPlayerDied;

    private bool isDead;

    void Start()
    {
        currentHealth = maxHealth;
        isDead = false;
        OnHpChanged?.Invoke(currentHealth, maxHealth);
    }

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    public void Heal(int healAmount)
    {
        if (isDead || healAmount <= 0) return;
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
        OnHpChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        DecreaseHealth(damage);
    }

    private void DecreaseHealth(int decreaseAmount)
    {
        if (isDead) return;

        currentHealth -= decreaseAmount;
        OnHpChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        OnPlayerDied?.Invoke();
    }
}