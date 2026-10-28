using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : MonoBehaviour
{
    [SerializeField] private Image hpGauge;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image mpGauge;
    [SerializeField] private TextMeshProUGUI mpText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Image warningScreen;
    [SerializeField] private float alphaOffset = 0.2f;

    private void OnEnable()
    {
        PlayerHealth.OnHpChanged += HpChanged;
        PlayerStamina.OnStaminaChanged += MpChanged;
        WeaponController.OnAmmoChanged += HandleAmmoChanged;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHpChanged -= HpChanged;
        PlayerStamina.OnStaminaChanged -= MpChanged;
        WeaponController.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void HandleAmmoChanged(WeaponController weapon, int current, int max)
    {
        if (ammoText == null || weapon == null || !weapon.gameObject.activeInHierarchy) return;

        string weaponName = weapon.GunSO != null ? weapon.GunSO.name : "Weapon";
        ammoText.text = weapon.isReloadng ? $"{weaponName} | RELOADING..." : $"{weaponName} | {current} / {max}";
    }

    private void HpChanged(float currentHealth, float maxHealth)
    {
        if (hpGauge != null && maxHealth > 0) hpGauge.fillAmount = currentHealth / maxHealth;
        if (hpText != null) hpText.text = $"{Mathf.Max(0, currentHealth):F0}/{maxHealth}";
        if (warningScreen != null && maxHealth > 0)
        {
            float alpha = Mathf.Clamp01(1f - (currentHealth / maxHealth) - alphaOffset);
            warningScreen.color = new Color(warningScreen.color.r, warningScreen.color.g, warningScreen.color.b, alpha);
        }
    }

    private void MpChanged(float currentMana, float maxMana)
    {
        if (mpGauge != null && maxMana > 0) mpGauge.fillAmount = currentMana / maxMana;
        if (mpText != null) mpText.text = $"{Mathf.Max(0, currentMana):F0}/{maxMana}";
    }
}
