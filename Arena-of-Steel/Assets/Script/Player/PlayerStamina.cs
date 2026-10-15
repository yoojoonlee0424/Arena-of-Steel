using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("마나 설정")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenRate = 10f;
    [SerializeField] private float currentStamina;

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
        if (currentStamina < maxStamina && regenRate > 0f)
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
        if (CurrentStamina < amount) return false;

        CurrentStamina -= amount;
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        return true;
    }

    public void RestoreStamina(float amount)
    {
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + amount);
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }
}
