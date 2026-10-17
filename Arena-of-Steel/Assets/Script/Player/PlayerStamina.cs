using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenRate = 10f;
    [SerializeField] private float regenDelay = 1.2f;
    [SerializeField] private float currentStamina;

    private float regenDelayTimer = 0f;

    public float CurrentStamina
    {
        get => currentStamina;
        private set => currentStamina = value;
    }
    public float MaxStamina => maxStamina;

    public static event Action<float, float> OnStaminaChanged;

    private void Start()
    {
        currentStamina = maxStamina;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    private void Update()
    {
        if (regenDelayTimer > 0f)
        {
            regenDelayTimer -= Time.deltaTime;
        }
        else if (currentStamina < maxStamina && regenRate > 0f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + regenRate * Time.deltaTime);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    public bool HasEnoughStamina(float amount)
    {
        return CurrentStamina >= amount;
    }

    public bool UseStamina(float amount)
    {
        if (CurrentStamina <= 0f) return false;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        regenDelayTimer = regenDelay;
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        return true;
    }

    public void RestoreStamina(float amount)
    {
        CurrentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }
}