using System;
using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [Header("마나 설정")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float regenRate = 10f;

    [SerializeField] private float currentMana;
    [SerializeField] private bool showDebugGUI = true;

    public float CurrentMana
    {
        get => currentMana;
        private set => currentMana = value;
    }
    public float MaxMana => maxMana;

    public event Action<float, float> OnManaChanged;

    private void Awake()
    {
        currentMana = maxMana;
    }

    private void Update()
    {
        if (currentMana < maxMana && regenRate > 0f)
        {
            currentMana = Mathf.Min(maxMana, currentMana + regenRate * Time.deltaTime);
            OnManaChanged?.Invoke(currentMana, maxMana);
        }
    }

    private void OnGUI()
    {
        if (!showDebugGUI) return;
        GUI.Box(new Rect(10, 10, 160, 30), $"Mana: {currentMana:F0} / {maxMana:F0}");
    }

    public bool HasEnoughMana(float amount)
    {
        return CurrentMana >= amount;
    }

    public bool UseMana(float amount)
    {
        if (CurrentMana < amount) return false;

        CurrentMana -= amount;
        OnManaChanged?.Invoke(CurrentMana, maxMana);
        return true;
    }

    public void RestoreMana(float amount)
    {
        CurrentMana = Mathf.Min(maxMana, CurrentMana + amount);
        OnManaChanged?.Invoke(CurrentMana, maxMana);
    }
}
