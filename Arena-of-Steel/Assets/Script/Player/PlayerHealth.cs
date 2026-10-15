using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    public static event Action<float, float> OnHpChanged;

    void Start()
    {
        currentHealth = maxHealth;
        OnHpChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Damage")
        {
            DecreaseHealth(10);
        }
    }

    private void DecreaseHealth(int decreaseAmount)
    {
        currentHealth -= decreaseAmount;
        OnHpChanged?.Invoke(currentHealth, maxHealth);
        //Ui_hit.instance.InstantiateHitUi();

        if(currentHealth <= 0)
        {
            Die();
        }
    }


    private void Die()
    {
        SceneManager.LoadSceneAsync(0);
    }
}
