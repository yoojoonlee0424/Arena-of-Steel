using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button selectButton;

    public void Init(WaveRewardSO reward, Action onClick)
    {
        if (titleText != null)
        {
            titleText.text = reward != null ? reward.rewardName : string.Empty;
        }

        if (descText != null)
        {
            descText.text = reward != null ? reward.description : string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.sprite = reward != null ? reward.icon : null;
            iconImage.gameObject.SetActive(iconImage.sprite != null);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}
