using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayUIManager : MonoBehaviour
{
    [Header("Wave Info UI")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI remainingEnemyText;

    [Header("Preparation UI")]
    [SerializeField] private TextMeshProUGUI prepTimerText;
    [SerializeField] private TextMeshProUGUI interactPromptText;

    [Header("Reward Selection UI")]
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject rewardCardPrefab;

    private void Awake()
    {
        if (rewardPanel != null) rewardPanel.SetActive(false);
        if (prepTimerText != null) prepTimerText.gameObject.SetActive(false);
        if (interactPromptText != null) interactPromptText.text = string.Empty;
    }

    private void OnEnable()
    {
        StageManager.OnWaveChanged += UpdateWaveUI;
        WaveSpawner.OnRemainingEnemiesChanged += UpdateEnemyUI;
        WaveSpawner.OnWaveRewardRequested += ShowRewardUI;
        WaveSpawner.OnPrepTimerChanged += UpdatePrepTimerUI;
        WaveSpawner.OnPrepFinished += HidePrepTimerUI;
        WeaponPickup.OnPromptChanged += UpdateInteractPrompt;
    }

    private void OnDisable()
    {
        StageManager.OnWaveChanged -= UpdateWaveUI;
        WaveSpawner.OnRemainingEnemiesChanged -= UpdateEnemyUI;
        WaveSpawner.OnWaveRewardRequested -= ShowRewardUI;
        WaveSpawner.OnPrepTimerChanged += UpdatePrepTimerUI;
        WaveSpawner.OnPrepFinished -= HidePrepTimerUI;
        WeaponPickup.OnPromptChanged += UpdateInteractPrompt;
    }

    private void Start()
    {
        UpdateWaveUI();
    }

    private void UpdateWaveUI()
    {
        if (StageManager.Instance == null || waveText == null) return;

        int currentWave = StageManager.Instance.currentWave + 1;
        waveText.text = $"{currentWave} Wave";
    }

    private void UpdateEnemyUI(int remaining)
    {
        if (remainingEnemyText == null) return;
        remainingEnemyText.text = $"Enemies: {remaining}";
    }

    private void UpdateInteractPrompt(string prompt)
    {
        if (interactPromptText != null)
        {
            interactPromptText.text = prompt;
        }
    }

    private void ShowRewardUI(WaveRewardSO[] rewards)
    {
        if (rewards == null || rewards.Length == 0 || rewardPanel == null)
        {
            WaveSpawner.Instance?.StartPreparationPhase();
            return;
        }

        rewardPanel.SetActive(true);
        PopulateRewardCards(rewards);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputActive(false);

        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null) cam.SetInputActive(false);
    }

    private void PopulateRewardCards(WaveRewardSO[] rewards)
    {
        if (cardContainer == null || rewardCardPrefab == null) return;

        ClearRewardCards();

        for (int i = 0; i < rewards.Length; i++)
        {
            var reward = rewards[i];
            if (reward == null) continue;

            GameObject cardObj = Instantiate(rewardCardPrefab, cardContainer);
            RewardCardUI card = cardObj.GetComponent<RewardCardUI>();
            if (card != null)
            {
                card.Init(reward, () => OnRewardSelected(reward));
            }
        }
    }

    private void ClearRewardCards()
    {
        if (cardContainer == null) return;
        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnRewardSelected(WaveRewardSO reward)
    {
        ApplyReward(reward);
        CloseRewardUI();
        WaveSpawner.Instance?.StartPreparationPhase();
    }

    private void ApplyReward(WaveRewardSO reward)
    {
        if (reward == null) return;

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        if (reward.rewardType == RewardType.Heal)
        {
            var health = playerObj.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                int healAmount = reward.intValue > 0 ? reward.intValue : health.MaxHealth / 2;
                health.Heal(healAmount);
            }
        }
        else if (reward.rewardType == RewardType.WeaponDrop)
        {
            Vector3 spawnPos = playerObj.transform.position + playerObj.transform.forward * 2f;
            WeaponPickup.Spawn(reward.weaponData, spawnPos, Quaternion.identity);
        }
    }

    private void CloseRewardUI()
    {
        ClearRewardCards();
        if (rewardPanel != null) rewardPanel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputActive(true);

        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null) cam.SetInputActive(true);
    }

    private void UpdatePrepTimerUI(float secondsRemaining)
    {
        if (prepTimerText == null) return;

        if (!prepTimerText.gameObject.activeSelf)
        {
            prepTimerText.gameObject.SetActive(true);
        }

        prepTimerText.text = $"Next Wave: {Mathf.CeilToInt(secondsRemaining)}s";
    }

    private void HidePrepTimerUI()
    {
        if (prepTimerText != null)
        {
            prepTimerText.gameObject.SetActive(false);
        }
    }
}