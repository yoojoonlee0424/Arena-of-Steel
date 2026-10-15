using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : MonoBehaviour
{
    [SerializeField] private Image hpGauge;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image mpGauge;
    [SerializeField] private TextMeshProUGUI mpText;

    void OnEnable()
    {
        PlayerHealth.OnHpChanged += HpChanged;
        PlayerStamina.OnStaminaChanged += MpChanged;
    }

    void OnDisable()
    {
        PlayerHealth.OnHpChanged -= HpChanged;
        PlayerStamina.OnStaminaChanged -= MpChanged;
    }

    void HpChanged(float currentHealth, float maxHealth)
    {
        hpGauge.fillAmount = currentHealth/maxHealth;
        hpText.text = $"{currentHealth:F0}/{maxHealth}";
    }

    void MpChanged(float currentMana, float maxMana)
    {
        mpGauge.fillAmount = currentMana/maxMana;
        mpText.text = $"{currentMana:F0}/{maxMana}";
    }
}
